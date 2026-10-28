#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <bcrypt.h>
#include <algorithm>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <deque>
#include <fstream>
#include <mutex>
#include <string>
#include <vector>
#include <array>
#include "ClientDiscovery.h"

static_assert(sizeof(void*) == 4, "XIGNCODE host must be built for x86");

// Pipe frames: little-endian uint32 kind, uint32 byte length, then payload.
enum Message : uint32_t { Initialize = 1, Ready = 2, Challenge = 3, Reply = 4,
    Failure = 5, Stop = 6, Diagnostic = 7, Identity = 8,
    InitializeWithDiscovery = 9, ClientLayout = 10 };
constexpr uint32_t ProtocolVersion = 1;
constexpr uint32_t ProbeSize = 4000;
constexpr uint32_t MaximumFrame = 65536;
struct Frame { uint32_t kind; std::vector<uint8_t> bytes; };
using SelectInterface = HRESULT(WINAPI*)(void**, unsigned);
using EnterSdk = BOOL(WINAPI*)(const wchar_t*, const wchar_t*, unsigned);
using CommandSdk = BOOL(WINAPI*)(unsigned, const void*);
using SuccessCallback = int(WINAPI*)(void*, unsigned, const void*, unsigned, void*);
using FailureCallback = int(WINAPI*)(void*, unsigned, unsigned, void*);

static HANDLE pipeHandle = INVALID_HANDLE_VALUE;
static HANDLE stopping = nullptr;
static HANDLE outputAvailable = nullptr;
static std::mutex outputMutex, identityMutex;
static std::deque<Frame> outputQueue;
static std::string identity;
static CommandSdk commandSdk = nullptr;
static HMODULE sdkModule = nullptr;
static uint8_t bootstrapContext[8192] = {};
static void* retainedInterfaces[31] = {};
static DWORD exceptionCode = 0;
static std::string lastPhase;
static bool sendClientLayout = false;
static std::wstring sdkApplicationCode;

static bool ReadExact(void* data, DWORD size) {
    auto cursor = static_cast<uint8_t*>(data);
    while (size) {
        DWORD count = 0;
        OVERLAPPED operation{};
        operation.hEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!operation.hEvent) return false;
        BOOL ok = ReadFile(pipeHandle, cursor, size, &count, &operation);
        if (!ok && GetLastError() == ERROR_IO_PENDING)
            ok = GetOverlappedResult(pipeHandle, &operation, &count, TRUE);
        CloseHandle(operation.hEvent);
        if (!ok || !count) return false;
        cursor += count; size -= count;
    }
    return true;
}
static bool WriteExact(const void* data, DWORD size) {
    auto cursor = static_cast<const uint8_t*>(data);
    while (size) {
        DWORD count = 0;
        OVERLAPPED operation{};
        operation.hEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!operation.hEvent) return false;
        BOOL ok = WriteFile(pipeHandle, cursor, size, &count, &operation);
        if (!ok && GetLastError() == ERROR_IO_PENDING)
            ok = GetOverlappedResult(pipeHandle, &operation, &count, TRUE);
        CloseHandle(operation.hEvent);
        if (!ok || !count) return false;
        cursor += count; size -= count;
    }
    return true;
}
static bool ReadFrame(Frame& frame) {
    uint32_t header[2];
    if (!ReadExact(header, sizeof(header)) || header[1] > MaximumFrame) return false;
    frame.kind = header[0]; frame.bytes.resize(header[1]);
    return ReadExact(frame.bytes.data(), header[1]);
}
static void Post(unsigned kind, const void* data, size_t size) {
    if (size > MaximumFrame) return;
    Frame frame{kind, std::vector<uint8_t>(size)};
    if (size) std::memcpy(frame.bytes.data(), data, size);
    {
        std::lock_guard<std::mutex> guard(outputMutex);
        if (outputQueue.size() >= 256) { SetEvent(stopping); return; }
        outputQueue.push_back(std::move(frame));
    }
    SetEvent(outputAvailable);
}
static void Phase(const char* text) {
    lastPhase = text;
    Post(Diagnostic, text, std::strlen(text));
}
static void Fail(const char* phase, DWORD error) {
    std::string text = std::string(phase) + ": " + std::to_string(error);
    Post(Failure, text.data(), text.size());
}
static DWORD WINAPI WriteWorker(void*) {
    for (;;) {
        WaitForSingleObject(outputAvailable, INFINITE);
        for (;;) {
            Frame frame;
            {
                std::lock_guard<std::mutex> guard(outputMutex);
                if (outputQueue.empty()) break;
                frame = std::move(outputQueue.front()); outputQueue.pop_front();
            }
            uint32_t header[2] = {frame.kind, static_cast<uint32_t>(frame.bytes.size())};
            if (!WriteExact(header, sizeof(header)) || !WriteExact(frame.bytes.data(), header[1])) {
                SetEvent(stopping); return 0;
            }
        }
        if (WaitForSingleObject(stopping, 0) == WAIT_OBJECT_0) return 0;
    }
}
static DWORD WINAPI ParentWorker(void* parameter) {
    HANDLE handles[2] = {static_cast<HANDLE>(parameter), stopping};
    if (WaitForMultipleObjects(2, handles, FALSE, INFINITE) == WAIT_OBJECT_0)
        TerminateProcess(GetCurrentProcess(), 3);
    return 0;
}

static std::wstring Utf8ToWide(const std::vector<uint8_t>& bytes) {
    if (bytes.empty()) return {};
    int count = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS,
        reinterpret_cast<const char*>(bytes.data()), static_cast<int>(bytes.size()), nullptr, 0);
    if (count <= 0) return {};
    std::wstring text(count, L'\0');
    MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS,
        reinterpret_cast<const char*>(bytes.data()), static_cast<int>(bytes.size()), text.data(), count);
    return text;
}
static void SetIdentity(const std::vector<uint8_t>& bytes) {
    auto wide = Utf8ToWide(bytes);
    int count = WideCharToMultiByte(CP_ACP, 0, wide.data(), static_cast<int>(wide.size()), nullptr, 0, nullptr, nullptr);
    std::string ansi(count > 0 ? count : 0, '\0');
    if (count > 0) WideCharToMultiByte(CP_ACP, 0, wide.data(), static_cast<int>(wide.size()), ansi.data(), count, nullptr, nullptr);
    std::lock_guard<std::mutex> guard(identityMutex);
    identity = std::move(ansi);
}
// Mirrors the client's command-100 callback. No password is requested or sent.
static HRESULT WINAPI ClientInfo(unsigned kind, char* output, unsigned capacity, void*) {
    if (kind != 1 || !output || !capacity) return E_INVALIDARG;
    std::lock_guard<std::mutex> guard(identityMutex);
    if (identity.empty()) { output[0] = 0; return E_PENDING; }
    size_t count = (std::min)(identity.size(), static_cast<size_t>(capacity - 1));
    std::memcpy(output, identity.data(), count); output[count] = 0;
    return S_OK;
}
static int WINAPI OnReply(void*, unsigned, const void* bytes, unsigned length, void*) {
    if ((!bytes && length) || length > ProbeSize) { Fail("invalid SDK callback", ERROR_INVALID_DATA); return 0; }
    // SDK owns this memory. Copy before returning; a separate thread writes the pipe.
    Post(Reply, bytes, length);
    return 1;
}
static int WINAPI OnFailure(void*, unsigned, unsigned error, void*) {
    Fail("SDK Probe callback", error);
    return 1;
}

static bool Sha256(const std::vector<uint8_t>& bytes, std::array<uint8_t, 32>& digest) {
    BCRYPT_ALG_HANDLE algorithm = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
    DWORD objectSize = 0, returned = 0;
    if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) < 0) return false;
    bool result = BCryptGetProperty(algorithm, BCRYPT_OBJECT_LENGTH, reinterpret_cast<PUCHAR>(&objectSize), sizeof(objectSize), &returned, 0) >= 0 && objectSize;
    std::vector<uint8_t> object(objectSize);
    result = result && BCryptCreateHash(algorithm, &hash, object.data(), objectSize, nullptr, 0, 0) >= 0
        && BCryptHashData(hash, const_cast<PUCHAR>(bytes.data()), static_cast<ULONG>(bytes.size()), 0) >= 0
        && BCryptFinishHash(hash, digest.data(), static_cast<ULONG>(digest.size()), 0) >= 0;
    if (hash) BCryptDestroyHash(hash);
    BCryptCloseAlgorithmProvider(algorithm, 0);
    return result;
}
// The bootstrap opens x3.xem with dwShareMode == 0, and concurrent
// SysEnter calls can also conflict. Serialize startup for the same SDK path;
// after Ready every process handles its own commands independently.
class SdkInitializationLock {
    HANDLE mutex = nullptr;
    bool held = false;
public:
    bool Acquire(const std::wstring& path) {
        std::wstring canonical(32768, L'\0');
        DWORD length = GetFullPathNameW(path.c_str(), static_cast<DWORD>(canonical.size()), canonical.data(), nullptr);
        if (!length || length >= canonical.size()) { SetLastError(ERROR_INVALID_NAME); return false; }
        canonical.resize(length);
        CharUpperBuffW(canonical.data(), length);
        auto begin = reinterpret_cast<const uint8_t*>(canonical.data());
        std::vector<uint8_t> bytes(begin, begin + canonical.size() * sizeof(wchar_t));
        std::array<uint8_t, 32> hash{};
        if (!Sha256(bytes, hash)) { SetLastError(ERROR_INVALID_DATA); return false; }
        std::wstring name = L"Local\\RSBot.XigncodeLoader";
        for (uint8_t value : hash) name += L"." + std::to_wstring(value);
        mutex = CreateMutexW(nullptr, FALSE, name.c_str());
        if (!mutex) return false;
        DWORD status = WaitForSingleObject(mutex, 60000);
        held = status == WAIT_OBJECT_0 || status == WAIT_ABANDONED;
        if (!held && status == WAIT_TIMEOUT) SetLastError(ERROR_TIMEOUT);
        return held;
    }
    ~SdkInitializationLock() {
        DWORD error = GetLastError();
        if (held) ReleaseMutex(mutex);
        if (mutex) CloseHandle(mutex);
        SetLastError(error);
    }
};
static HMODULE SafeLoadBootstrap(uint8_t* entry, unsigned arguments, const wchar_t* path) {
    __try {
        if (arguments == 3) {
            auto load = reinterpret_cast<HMODULE(WINAPI*)(const wchar_t*, HMODULE, void*)>(entry);
            return load(path, GetModuleHandleW(L"ntdll.dll"), bootstrapContext);
        }
        if (arguments == 2) {
            auto load = reinterpret_cast<HMODULE(WINAPI*)(const wchar_t*, HMODULE)>(entry);
            return load(path, GetModuleHandleW(L"ntdll.dll"));
        }
        return reinterpret_cast<HMODULE(WINAPI*)(const wchar_t*)>(entry)(path);
    }
    __except (exceptionCode = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER) { SetLastError(exceptionCode); return nullptr; }
}
static std::vector<uint8_t> LayoutFrame(const ClientDiscovery::Analysis& client, const std::array<uint8_t, 32>& hash) {
    std::vector<uint8_t> data;
    auto dword = [&](uint32_t value) { auto p = reinterpret_cast<const uint8_t*>(&value); data.insert(data.end(), p, p + 4); };
    dword(1); dword(client.imageSize); dword(client.sysEnter); dword(client.sendCommand); dword(client.connectCall); dword(client.connectImport);
    data.insert(data.end(), client.sysBytes.begin(), client.sysBytes.end());
    data.insert(data.end(), client.sysMask.begin(), client.sysMask.end());
    data.insert(data.end(), client.sendBytes.begin(), client.sendBytes.end());
    data.insert(data.end(), client.sendMask.begin(), client.sendMask.end());
    data.insert(data.end(), hash.begin(), hash.end());
    return data;
}
static bool DiscoverClient(const std::wstring& executable, ClientDiscovery::Analysis& client, std::array<uint8_t, 32>& hash) {
    ClientDiscovery::Image image; std::string error;
    if (!image.Read(executable) || !ClientDiscovery::Discover(image, client, error)) {
        if (!error.empty()) Phase(error.c_str());
        SetLastError(ERROR_REVISION_MISMATCH); return false;
    }
    if (!Sha256(image.Raw(), hash)) { SetLastError(ERROR_INVALID_DATA); return false; }
    return true;
}
static int InspectClient(const wchar_t* executable, const wchar_t* framePath) {
    ClientDiscovery::Analysis client; std::array<uint8_t, 32> hash{};
    if (!DiscoverClient(executable, client, hash)) { std::puts("{\"success\":false}"); return 2; }
    std::printf("{\"success\":true,\"imageSize\":%u,\"sysEnter\":%u,\"sendCommand\":%u,\"connectCall\":%u,\"connectImport\":%u,\"loader\":%u,\"bootstrapLength\":%zu,\"entryOffset\":%u,\"loaderArguments\":%u,\"sdkCandidates\":%zu,\"gatewayCandidates\":%zu,\"loaderCandidates\":%zu}\n",
        client.imageSize, client.sysEnter, client.sendCommand, client.connectCall, client.connectImport, client.loader,
        client.bootstrap.size(), client.entryOffset, client.loaderArguments, client.sdkCandidates, client.gatewayCandidates, client.loaderCandidates);
    if (framePath) {
        auto data = LayoutFrame(client, hash);
        std::ofstream output(framePath, std::ios::binary); output.write(reinterpret_cast<const char*>(data.data()), data.size());
        if (!output) return 3;
    }
    return 0;
}
static bool InitializeSdk(const std::wstring& executable, const std::wstring& sdkDirectory) {
#ifdef XIGNCODE_HOST_TESTING
    (void)executable; (void)sdkDirectory;
    Phase("fixture backend; transport tests only");
    if (sendClientLayout) {
        ClientDiscovery::Analysis client; client.imageSize = 65536;
        client.sysEnter = 4096; client.sendCommand = 8192; client.connectCall = 12288; client.connectImport = 16384;
        client.sysMask.fill(255); client.sendMask.fill(255); std::array<uint8_t, 32> hash{};
        auto data = LayoutFrame(client, hash); Post(ClientLayout, data.data(), data.size());
    }
    return true;
#else
    Phase("discovering client and SDK loader signatures");
    ClientDiscovery::Analysis client; std::array<uint8_t, 32> hash{};
    if (!DiscoverClient(executable, client, hash)) return false;
    if (sendClientLayout) { auto data = LayoutFrame(client, hash); Post(ClientLayout, data.data(), data.size()); }
    auto path = sdkDirectory + L"\\x3.xem";
    SdkInitializationLock initialization;
    Phase("waiting for SDK initialization lock");
    if (!initialization.Acquire(path)) return false;
    auto& bootstrap = client.bootstrap;
    DWORD entryOffset = client.entryOffset;
    auto memory = static_cast<uint8_t*>(VirtualAlloc(nullptr, bootstrap.size(), MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE));
    if (!memory) return false;
    std::memcpy(memory, bootstrap.data(), bootstrap.size());
    FlushInstructionCache(GetCurrentProcess(), memory, bootstrap.size());
    Phase("loading original x3.xem");
    sdkModule = SafeLoadBootstrap(memory + entryOffset, client.loaderArguments, path.c_str());
    DWORD error = GetLastError();
    VirtualFree(memory, 0, MEM_RELEASE);
    SetLastError(error);
    if (!sdkModule) return false;
    auto select = reinterpret_cast<SelectInterface>(GetProcAddress(sdkModule, MAKEINTRESOURCEA(1)));
    if (!select) return false;
    auto extra = reinterpret_cast<SelectInterface>(GetProcAddress(sdkModule, MAKEINTRESOURCEA(4)));
    if (extra) {
        using Prepare = HRESULT(WINAPI*)(HMODULE, unsigned, unsigned);
        Prepare prepare = nullptr;
        if (extra(reinterpret_cast<void**>(&prepare), 34) >= 0 && prepare) {
            HRESULT status = prepare(sdkModule, 0, 0);
            if (FAILED(status)) { SetLastError(status); return false; }
        }
    }
    EnterSdk enter = nullptr;
    HRESULT status = select(reinterpret_cast<void**>(&enter), 1);
    if (FAILED(status) || !enter) { SetLastError(FAILED(status) ? status : ERROR_PROC_NOT_FOUND); return false; }
    Phase("calling original SysEnter");
    sdkApplicationCode = client.applicationCode;
    if (!enter(sdkApplicationCode.c_str(), sdkDirectory.c_str(), client.initFlags)) return false;
    status = select(reinterpret_cast<void**>(&commandSdk), 26);
    if (FAILED(status) || !commandSdk) { SetLastError(FAILED(status) ? status : ERROR_PROC_NOT_FOUND); return false; }
    Phase("registering SDK interfaces and client callback");
    const unsigned identifiers[] = {2000,2100,2400,2500,3810,3820,3830,3840,3850,3100,3200,3300,3400,3500,3600,3700,3800,3860,3870,3880,3890,3900,3910,3920,3930,3940,3950,3960,3970,3980,3990};
    for (size_t i = 0; i < _countof(identifiers); ++i) {
        struct { unsigned identifier; void** output; } args{identifiers[i], &retainedInterfaces[i]};
        commandSdk(7100, &args); // Client also tolerates an unavailable optional interface.
    }
    struct { decltype(&ClientInfo) callback; void* context; } info{ClientInfo, nullptr};
    return commandSdk(100, &info) != FALSE;
#endif
}
static bool SafeInitialize(const std::wstring& executable, const std::wstring& directory) {
    __try { return InitializeSdk(executable, directory); }
    __except (exceptionCode = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER) { SetLastError(exceptionCode); return false; }
}
static bool Submit(const std::vector<uint8_t>& challenge) {
#ifdef XIGNCODE_HOST_TESTING
    if (challenge[0] == 0) return true; // A fragment with no reply.
    if (challenge[0] == 3) { OnReply(nullptr, 0, nullptr, 0, nullptr); return true; }
    OnReply(nullptr, 0, challenge.data(), 17, nullptr);
    if (challenge[0] == 2) OnReply(nullptr, 0, challenge.data() + 17, 17, nullptr);
    return true;
#else
    struct { const void* input; unsigned capacity; SuccessCallback success; FailureCallback failure; void* context; }
        args{challenge.data(), ProbeSize, OnReply, OnFailure, nullptr};
    return commandSdk(0x55, &args) != FALSE;
#endif
}
static bool SafeSubmit(const std::vector<uint8_t>& bytes) {
    __try { return Submit(bytes); }
    __except (exceptionCode = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER) { SetLastError(exceptionCode); return false; }
}
static DWORD WINAPI ReadWorker(void*) {
    Frame frame;
    while (ReadFrame(frame)) {
        if (frame.kind == Stop && frame.bytes.empty()) break;
        if (frame.kind == Identity && frame.bytes.size() <= 1024) { SetIdentity(frame.bytes); continue; }
        if (frame.kind != Challenge || frame.bytes.size() != ProbeSize) { Fail("invalid IPC request", ERROR_INVALID_DATA); break; }
        if (!SafeSubmit(frame.bytes)) { Fail("SendCommand Probe", GetLastError()); break; }
        const char accepted[] = "Probe submitted";
        Post(Diagnostic, accepted, sizeof(accepted)-1);
    }
    SetEvent(stopping); SetEvent(outputAvailable);
    return 0;
}
int wmain(int argc, wchar_t** argv) {
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
    if ((argc == 3 || argc == 4) && wcscmp(argv[1], L"--inspect-client") == 0)
        return InspectClient(argv[2], argc == 4 ? argv[3] : nullptr);
    if (argc != 5) return 64; // pipe name, parent PID, sro_client.exe, XIGNCODE directory
    wchar_t* end = nullptr;
    DWORD parentId = wcstoul(argv[2], &end, 10);
    if (!parentId || !end || *end || parentId == GetCurrentProcessId()) return 64;
    HANDLE parent = OpenProcess(SYNCHRONIZE, FALSE, parentId);
    if (!parent) return 65;
    stopping = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    outputAvailable = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    std::wstring path = L"\\\\.\\pipe\\" + std::wstring(argv[1]);
    DWORD pipeError = 0;
    for (unsigned i = 0; i < 100; ++i) {
        // A synchronous pipe handle serializes blocked reads and writes. Use
        // separate overlapped operations so callbacks can arrive while input is idle.
        pipeHandle = CreateFileW(path.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, nullptr);
        if (pipeHandle != INVALID_HANDLE_VALUE) break;
        pipeError = GetLastError();
        if (WaitForSingleObject(parent, 50) == WAIT_OBJECT_0) return 66;
    }
    if (pipeHandle == INVALID_HANDLE_VALUE) {
        std::fwprintf(stderr, L"Could not connect to host pipe (Win32 %lu).\n", pipeError);
        return 66;
    }
    HANDLE writer = CreateThread(nullptr, 0, WriteWorker, nullptr, 0, nullptr);
    HANDLE monitor = CreateThread(nullptr, 0, ParentWorker, parent, 0, nullptr);
    if (!writer || !monitor) return 67;
    Frame first;
    bool initialized = false;
    if (!ReadFrame(first) || (first.kind != Initialize && first.kind != InitializeWithDiscovery) || first.bytes.size() > 1024) {
        Fail("invalid initialization frame", ERROR_INVALID_DATA);
    } else {
        sendClientLayout = first.kind == InitializeWithDiscovery;
        SetIdentity(first.bytes);
        initialized = SafeInitialize(argv[3], argv[4]);
        if (!initialized) Fail(lastPhase.c_str(), GetLastError());
    }
    if (initialized) {
        Post(Ready, &ProtocolVersion, sizeof(ProtocolVersion));
        HANDLE reader = CreateThread(nullptr, 0, ReadWorker, nullptr, 0, nullptr);
        if (reader) {
            while (MsgWaitForMultipleObjects(1, &stopping, FALSE, INFINITE, QS_ALLINPUT) != WAIT_OBJECT_0) {
                MSG message;
                while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE)) {
                    if (message.message == WM_QUIT) SetEvent(stopping);
                    TranslateMessage(&message); DispatchMessageW(&message);
                }
            }
        }
    }
    SetEvent(stopping); SetEvent(outputAvailable);
    WaitForSingleObject(writer, 1000);
    CancelIoEx(pipeHandle, nullptr);
    // SDK owns process-wide workers and callbacks. End this session by exiting
    // its dedicated process; do not unload code underneath those workers.
    ExitProcess(initialized ? 0 : 2);
}
