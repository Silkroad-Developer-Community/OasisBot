#pragma once
#include <windows.h>
#include <algorithm>
#include <array>
#include <cstdint>
#include <cstring>
#include <fstream>
#include <initializer_list>
#include <set>
#include <string>
#include <tuple>
#include <vector>

// Signatures describe SDK/API use, not a particular executable's addresses.
// The VTC table-indexed loader and the TR/TS count-global loaders were
// checked against disassembly. Ambiguous or incomplete matches are rejected.
namespace ClientDiscovery {
using Bytes = std::vector<uint8_t>;
inline uint32_t U32(const uint8_t* p) { uint32_t v; std::memcpy(&v, p, 4); return v; }
inline bool Match(const uint8_t* p, size_t size, std::initializer_list<int> pattern) {
    if (size < pattern.size()) return false;
    for (int value : pattern) { if (value >= 0 && *p != value) return false; ++p; }
    return true;
}
inline std::string Lower(std::string s) {
    for (char& c : s) if (c >= 'A' && c <= 'Z') c = static_cast<char>(c + ('a' - 'A'));
    if (s.size() > 4 && s.substr(s.size() - 4) == ".dll") s.resize(s.size() - 4);
    return s;
}
struct Section { uint32_t rva, size, offset, flags, virtualSize; };
class Image {
    Bytes raw;
    std::vector<Section> sections;
    std::array<IMAGE_DATA_DIRECTORY, IMAGE_NUMBEROF_DIRECTORY_ENTRIES> directories{};
    uint32_t headers = 0;
public:
    uint32_t base = 0, size = 0;
    const Bytes& Raw() const { return raw; }
    const std::vector<Section>& Sections() const { return sections; }
    bool Read(const std::wstring& path) {
        std::ifstream input(path, std::ios::binary | std::ios::ate);
        if (!input) return false;
        auto length = input.tellg();
        if (length < 4096 || length > 268435456) return false;
        raw.resize(static_cast<size_t>(length)); input.seekg(0); input.read(reinterpret_cast<char*>(raw.data()), length);
        if (!input) return false;
        IMAGE_DOS_HEADER dos{}; std::memcpy(&dos, raw.data(), sizeof(dos));
        if (dos.e_magic != IMAGE_DOS_SIGNATURE || dos.e_lfanew < 0
            || static_cast<uint64_t>(dos.e_lfanew) + sizeof(IMAGE_NT_HEADERS32) > raw.size()) return false;
        IMAGE_NT_HEADERS32 nt{}; std::memcpy(&nt, raw.data() + dos.e_lfanew, sizeof(nt));
        if (nt.Signature != IMAGE_NT_SIGNATURE || nt.FileHeader.Machine != IMAGE_FILE_MACHINE_I386
            || nt.OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR32_MAGIC
            || nt.FileHeader.SizeOfOptionalHeader < sizeof(IMAGE_OPTIONAL_HEADER32)
            || !nt.FileHeader.NumberOfSections || nt.FileHeader.NumberOfSections > 96
            || nt.OptionalHeader.SizeOfImage > 268435456 || !nt.OptionalHeader.SizeOfImage) return false;
        base = nt.OptionalHeader.ImageBase; size = nt.OptionalHeader.SizeOfImage; headers = nt.OptionalHeader.SizeOfHeaders;
        if (static_cast<uint64_t>(base) + size > (uint64_t{1} << 32)) return false;
        std::copy(std::begin(nt.OptionalHeader.DataDirectory), std::end(nt.OptionalHeader.DataDirectory), directories.begin());
        size_t table = static_cast<size_t>(dos.e_lfanew) + 24 + nt.FileHeader.SizeOfOptionalHeader;
        if (table + nt.FileHeader.NumberOfSections * sizeof(IMAGE_SECTION_HEADER) > raw.size()) return false;
        for (unsigned i = 0; i < nt.FileHeader.NumberOfSections; ++i) {
            IMAGE_SECTION_HEADER s{}; std::memcpy(&s, raw.data() + table + i * sizeof(s), sizeof(s));
            if (static_cast<uint64_t>(s.PointerToRawData) + s.SizeOfRawData > raw.size()
                || static_cast<uint64_t>(s.VirtualAddress) + (std::max)(s.Misc.VirtualSize, s.SizeOfRawData) > size) return false;
            sections.push_back({s.VirtualAddress, s.SizeOfRawData, s.PointerToRawData, s.Characteristics, (std::max)(s.Misc.VirtualSize, s.SizeOfRawData)});
        }
        return true;
    }
    const uint8_t* At(uint32_t rva, size_t length) const {
        if (static_cast<uint64_t>(rva) + length > size) return nullptr;
        if (rva < headers && static_cast<uint64_t>(rva) + length <= std::min<uint64_t>(headers, raw.size())) return raw.data() + rva;
        for (const auto& s : sections)
            if (rva >= s.rva && static_cast<uint64_t>(rva - s.rva) + length <= s.size) return raw.data() + s.offset + rva - s.rva;
        return nullptr;
    }
    bool Dword(uint32_t rva, uint32_t& value) const { auto p = At(rva, 4); if (!p) return false; value = U32(p); return true; }
    bool Rva(uint32_t va, uint32_t& rva) const { if (va < base || va - base >= size) return false; rva = va - base; return true; }
    bool Code(uint32_t rva, size_t length = 1) const {
        for (const auto& s : sections) if ((s.flags & IMAGE_SCN_MEM_EXECUTE) && rva >= s.rva
            && static_cast<uint64_t>(rva - s.rva) + length <= s.size) return true;
        return false;
    }
    bool Writable(uint32_t rva) const {
        for (const auto& s : sections) if ((s.flags & IMAGE_SCN_MEM_WRITE) && rva >= s.rva && rva - s.rva < s.virtualSize) return true;
        return false;
    }
    bool String(uint32_t rva, std::string& text) const {
        text.clear();
        for (unsigned i = 0; i < 512; ++i) { auto p = At(rva + i, 1); if (!p) return false; if (!*p) return true; text.push_back(static_cast<char>(*p)); }
        return false;
    }
    bool Wide(uint32_t va, std::wstring& text) const {
        uint32_t rva; if (!Rva(va, rva)) return false; text.clear();
        for (unsigned i = 0; i < 128; ++i) {
            auto p = At(rva + 2 * i, 2); if (!p) return false;
            wchar_t c = static_cast<wchar_t>(p[0] | (p[1] << 8)); if (!c) return !text.empty(); text.push_back(c);
        }
        return false;
    }
    bool Relative(uint32_t call, uint32_t& target) const {
        auto p = At(call, 5); if (!p || p[0] != 0xE8) return false;
        int32_t rel; std::memcpy(&rel, p + 1, 4); int64_t rva = static_cast<int64_t>(call) + 5 + rel;
        if (rva < 0 || rva >= size || !Code(static_cast<uint32_t>(rva))) return false;
        target = static_cast<uint32_t>(rva); return true;
    }
    template<class F> void Scan(F visit) const {
        for (const auto& s : sections) if (s.flags & IMAGE_SCN_MEM_EXECUTE)
            for (uint32_t i = 0; i < s.size; ++i) visit(s.rva + i, raw.data() + s.offset + i, static_cast<size_t>(s.size - i));
    }
    std::set<uint32_t> Imports(const std::string& wantedModule, const std::string& wantedName, uint16_t ordinal = 0) const {
        std::set<uint32_t> found; auto directory = directories[IMAGE_DIRECTORY_ENTRY_IMPORT];
        if (!directory.VirtualAddress || directory.Size > size || static_cast<uint64_t>(directory.VirtualAddress) + directory.Size > size) return found;
        for (uint32_t offset = 0; offset + sizeof(IMAGE_IMPORT_DESCRIPTOR) <= directory.Size; offset += sizeof(IMAGE_IMPORT_DESCRIPTOR)) {
            auto p = At(directory.VirtualAddress + offset, sizeof(IMAGE_IMPORT_DESCRIPTOR)); if (!p) break;
            IMAGE_IMPORT_DESCRIPTOR d{}; std::memcpy(&d, p, sizeof(d)); if (!d.Name && !d.FirstThunk) break;
            std::string module; if (!String(d.Name, module) || Lower(module) != Lower(wantedModule)) continue;
            for (uint32_t i = 0; i < size / 4; ++i) {
                uint32_t value; if (!Dword((d.OriginalFirstThunk ? d.OriginalFirstThunk : d.FirstThunk) + i * 4, value) || !value) break;
                bool match = false;
                if (IMAGE_SNAP_BY_ORDINAL32(value)) match = ordinal && IMAGE_ORDINAL32(value) == ordinal;
                else { std::string name; match = String(value + 2, name) && name == wantedName; }
                if (match && At(d.FirstThunk + i * 4, 4)) found.insert(d.FirstThunk + i * 4);
            }
        }
        return found;
    }
    bool Imported(uint32_t absoluteSlot, const std::string& module, const std::string& name, uint16_t ordinal = 0) const {
        uint32_t slot; return Rva(absoluteSlot, slot) && Imports(module, name, ordinal).count(slot) != 0;
    }
    template<size_t N> void Head(uint32_t rva, std::array<uint8_t, N>& bytes, std::array<uint8_t, N>& mask) const {
        std::copy_n(At(rva, N), N, bytes.begin()); mask.fill(255);
        auto directory = directories[IMAGE_DIRECTORY_ENTRY_BASERELOC];
        if (!directory.VirtualAddress || directory.Size > size || static_cast<uint64_t>(directory.VirtualAddress) + directory.Size > size) return;
        for (uint32_t offset = 0; offset + sizeof(IMAGE_BASE_RELOCATION) <= directory.Size;) {
            auto p = At(directory.VirtualAddress + offset, sizeof(IMAGE_BASE_RELOCATION)); if (!p) break;
            uint32_t page = U32(p), length = U32(p + 4);
            if (length < 8 || length > directory.Size - offset) break;
            auto entries = At(directory.VirtualAddress + offset + 8, length - 8); if (!entries) break;
            for (uint32_t i = 0; i + 2 <= length - 8; i += 2) {
                uint16_t entry; std::memcpy(&entry, entries + i, 2); if ((entry >> 12) != IMAGE_REL_BASED_HIGHLOW) continue;
                uint32_t target = page + (entry & 4095);
                for (size_t j = 0; j < N; ++j) if (rva + j >= target && rva + j - target < 4) mask[j] = 0;
            }
            offset += length;
        }
    }
};
struct Fragment { uint32_t rva, size; };
struct Analysis {
    uint32_t imageSize = 0, sysEnter = 0, sendCommand = 0, connectCall = 0, connectImport = 0;
    uint32_t initFlags = 0, loader = 0, entryOffset = 0, loaderArguments = 0, firstRegistration = 0;
    std::wstring applicationCode;
    std::array<uint8_t, 8> sysBytes{}, sysMask{};
    std::array<uint8_t, 6> sendBytes{}, sendMask{};
    Bytes bootstrap;
    std::vector<Fragment> fragments;
    size_t sdkCandidates = 0, gatewayCandidates = 0, loaderCandidates = 0;
};
struct Registration { uint32_t site, identifier, target; };
inline bool FindSdk(const Image& image, Analysis& result, std::string& error) {
    std::vector<Registration> registrations;
    image.Scan([&](uint32_t rva, const uint8_t* p, size_t n) {
        if (!Match(p, n, {0x68,-1,-1,-1,-1,0x68,-1,-1,-1,-1,0x68,0xBC,0x1B,0,0,0xE8,-1,-1,-1,-1,0x83,0xC4,0x0C})) return;
        uint32_t output, target;
        if (image.Rva(U32(p + 1), output) && image.Writable(output) && image.Relative(rva + 15, target))
            registrations.push_back({rva, U32(p + 6), target});
    });
    using Candidate = std::tuple<uint32_t, uint32_t, uint32_t, std::wstring>;
    std::set<Candidate> candidates;
    image.Scan([&](uint32_t rva, const uint8_t* p, size_t n) {
        size_t pushed = 0; uint32_t flags = 0;
        if (n >= 2 && p[0] == 0x6A) { pushed = 2; flags = static_cast<uint32_t>(static_cast<int32_t>(static_cast<int8_t>(p[1]))); }
        else if (n >= 5 && p[0] == 0x68) { pushed = 5; flags = U32(p + 1); }
        if (!pushed || !Match(p + pushed, n - pushed, {0x68,-1,-1,-1,-1,0x68,-1,-1,-1,-1,0xE8,-1,-1,-1,-1})) return;
        std::wstring directory, app;
        if (!image.Wide(U32(p + pushed + 1), directory) || _wcsicmp(directory.c_str(), L"XIGNCODE") != 0
            || !image.Wide(U32(p + pushed + 6), app)) return;
        uint32_t enter; if (!image.Relative(rva + static_cast<uint32_t>(pushed) + 10, enter) || !image.Code(enter, 8)) return;
        std::set<uint32_t> first, second;
        for (const auto& reg : registrations) if (reg.site >= enter && reg.site - enter < 16384) {
            if (reg.identifier == 2000) first.insert(reg.target);
            if (reg.identifier == 2100) second.insert(reg.target);
        }
        for (uint32_t send : first) if (second.count(send) && send != enter && image.Code(send, 6)) candidates.emplace(enter, send, flags, app);
    });
    result.sdkCandidates = candidates.size();
    if (candidates.size() != 1) { error = candidates.empty() ? "XIGNCODE initialization/registration signatures not found" : "ambiguous XIGNCODE initialization signatures"; return false; }
    const auto& candidate = *candidates.begin();
    result.sysEnter = std::get<0>(candidate); result.sendCommand = std::get<1>(candidate);
    result.initFlags = std::get<2>(candidate); result.applicationCode = std::get<3>(candidate);
    result.firstRegistration = image.size;
    for (const auto& reg : registrations) if (reg.target == result.sendCommand && reg.site >= result.sysEnter)
        result.firstRegistration = (std::min)(result.firstRegistration, reg.site);
    image.Head(result.sysEnter, result.sysBytes, result.sysMask); image.Head(result.sendCommand, result.sendBytes, result.sendMask);
    return true;
}
inline void FindGateway(const Image& image, Analysis& result) {
    auto slots = image.Imports("ws2_32", "connect", 4); auto old = image.Imports("wsock32", "connect", 4); slots.insert(old.begin(), old.end());
    std::set<std::pair<uint32_t, uint32_t>> found;
    image.Scan([&](uint32_t rva, const uint8_t* p, size_t n) {
        if (!Match(p, n, {0xFF,0x15,-1,-1,-1,-1,0x83,0xF8,0xFF})) return;
        uint32_t slot; if (!image.Rva(U32(p + 2), slot) || !slots.count(slot)) return;
        // Three-argument IPv4 connect call: sizeof(sockaddr_in) is pushed nearby.
        bool length = false;
        for (uint32_t back = 2; back <= 16 && rva >= back; ++back) { auto q = image.At(rva - back, 2); if (q && q[0] == 0x6A && q[1] == 16) length = true; }
        if (length) found.emplace(rva, slot);
    });
    result.gatewayCandidates = found.size();
    if (found.size() == 1) { result.connectCall = found.begin()->first; result.connectImport = found.begin()->second; }
}
inline bool Indexed(const uint8_t* p, size_t n, uint8_t opcode) {
    return n >= 7 && p[0] == opcode && (p[1] & 0xC7) == 4 && (p[2] & 0xC7) == 0x85 && ((p[2] >> 3) & 7) != 4;
}
inline bool StackMov(const uint8_t* p, size_t n, uint8_t opcode) { return n >= 3 && p[0] == opcode && (p[1] & 0xC7) == 0x45; }
inline bool LinkedLoader(const Image& image, const Analysis& sdk, uint32_t loader) {
    auto end = std::min<uint32_t>(sdk.firstRegistration, sdk.sysEnter + 16384);
    for (uint32_t rva = sdk.sysEnter; rva + 5 <= end; ++rva) {
        uint32_t target; if (!image.Relative(rva, target)) continue;
        if (target == loader) return true;
        for (uint32_t offset = 0; offset < 256 && image.Code(target + offset, 5); ++offset) {
            uint32_t nested; if (image.Relative(target + offset, nested) && nested == loader) return true;
        }
    }
    return false;
}
inline bool LoaderAt(const Image& image, const Analysis& sdk, uint32_t prefix, Analysis& candidate) {
    auto p = image.At(prefix, 21); if (!p || !Match(p, 21, {0x6A,0x40,0x68,0,0x30,0,0,0xA1,-1,-1,-1,-1,0x50,0x6A,0,0xFF,0x15,-1,-1,-1,-1})) return false;
    if (!image.Imported(U32(p + 17), "kernel32", "VirtualAlloc")) return false;
    uint32_t totalRva, total;
    if (!image.Rva(U32(p + 8), totalRva) || !image.Dword(totalRva, total) || total < 36 || total > 4194304) return false;
    uint32_t function = 0;
    if (prefix >= 6) { auto q = image.At(prefix - 6, 6); if (q && Match(q, 6, {0x55,0x8B,0xEC,0x83,0xEC,-1})) function = prefix - 6; }
    if (!function && prefix >= 9) { auto q = image.At(prefix - 9, 9); if (q && Match(q, 9, {0x55,0x8B,0xEC,0x81,0xEC,-1,-1,-1,-1})) function = prefix - 9; }
    if (!function || !LinkedLoader(image, sdk, function)) return false;
    std::vector<uint32_t> copies;
    for (uint32_t offset = 21; offset < 384; ++offset) {
        auto q = image.At(prefix + offset, 32); if (!q) break;
        if (!Indexed(q, 32, 0x8B) || q[7] != 0x50 + ((q[1] >> 3) & 7)
            || !StackMov(q + 8, 24, 0x8B) || !Indexed(q + 11, 21, 0x8B)
            || q[18] != 0x50 + ((q[12] >> 3) & 7) || !StackMov(q + 19, 13, 0x8B)
            || q[22] != 0x50 + ((q[20] >> 3) & 7) || q[23] != 0xE8
            || !Match(q + 28, 4, {0x83,0xC4,0x0C})) continue;
        copies.push_back(prefix + offset);
    }
    if (copies.size() != 1) return false;
    uint32_t copy = copies.front(); auto q = image.At(copy, 32); uint32_t sizes, sources;
    if (!image.Rva(U32(q + 3), sizes) || !image.Rva(U32(q + 14), sources)) return false;
    uint32_t start = 0, count = 0;
    std::vector<uint32_t> indexes;
    for (uint32_t rva = prefix + 21; rva + 17 <= copy; ++rva) {
        auto m = image.At(rva, 17);
        if (m && Match(m, 17, {0xBA,4,0,0,0,0x6B,0xC2,-1,0x8B,0x88,-1,-1,-1,-1,0x89,0x4D,-1}) && U32(m + 10) == image.base + sizes) indexes.push_back(m[7]);
    }
    if (indexes.size() == 2) {
        if (!image.Dword(sizes + 4 * indexes[0], start) || !image.Dword(sizes + 4 * indexes[1], count)) return false;
    } else if (indexes.empty()) {
        auto indexLoad = image.At(copy - 3, 3); if (!indexLoad || !StackMov(indexLoad, 3, 0x8B)) return false;
        uint8_t indexSlot = indexLoad[2]; std::vector<uint32_t> counts, starts;
        for (uint32_t rva = prefix + 21; rva + 7 <= copy; ++rva) {
            auto m = image.At(rva, 7); if (!m) continue;
            if (m[0] == 0xC7 && m[1] == 0x45 && m[2] == indexSlot) starts.push_back(U32(m + 3));
            if (m[0] == 0x3B && (m[1] & 0xC7) == 5) { uint32_t valueRva, value; if (image.Rva(U32(m + 2), valueRva) && image.Dword(valueRva, value)) counts.push_back(value); }
        }
        if (counts.size() != 1 || starts.size() != 1) return false;
        start = starts[0]; count = counts[0];
    } else return false;
    if (!count || count > 256 || start > 4096 || start + count > 4096) return false;
    Bytes blob; std::vector<Fragment> fragments;
    for (uint32_t i = start; i < start + count; ++i) {
        uint32_t length, va, rva;
        if (!image.Dword(sizes + i * 4, length) || !image.Dword(sources + i * 4, va) || !image.Rva(va, rva)
            || !length || blob.size() + length > total) return false;
        auto bytes = image.At(rva, length); if (!bytes) return false;
        blob.insert(blob.end(), bytes, bytes + length); fragments.push_back({rva, length});
    }
    if (blob.size() != total) return false;
    // Decode the entry-field access and verify the observed loader call shape.
    // VTC/TR pass ntdll (and optionally context); TS passes only the filename.
    std::vector<std::pair<uint32_t, uint32_t>> entries;
    for (uint32_t rva = copy + 31; rva < prefix + 384; ++rva) {
        auto m = image.At(rva, 28); if (!m) break;
        if (m[0] != 0x03 || (m[1] & 0xC0) != 0x40 || (m[1] & 7) == 4 || !StackMov(m + 3, 14, 0x89)) continue;
        uint32_t field = m[2]; if (field + 4 > blob.size()) continue;
        uint32_t offset = U32(blob.data() + field); if (offset < field + 4 || offset >= blob.size()) continue;
        if (StackMov(m + 6, 22, 0x8B) && StackMov(m + 9, 19, 0x89)
            && StackMov(m + 12, 16, 0x8B) && m[14] == 8
            && ((m[1] >> 3) & 7) == ((m[4] >> 3) & 7)
            && m[5] == m[8] && ((m[7] >> 3) & 7) == ((m[10] >> 3) & 7)
            && m[15] == 0x50 + ((m[13] >> 3) & 7)
            && m[16] == 0xFF && m[17] == 0x55 && m[18] == m[11]
            && m[19] == 0x89 && m[20] == 0x45 && m[22] == 0xFF && m[23] == 0x15
            && image.Imported(U32(m + 24), "kernel32", "GetLastError")) {
            entries.emplace_back(offset, 1);
            continue;
        }
        if (m[6] != 0x68 || m[11] != 0xFF || m[12] != 0x15) continue;
        std::wstring module;
        if (!image.Wide(U32(m + 7), module) || _wcsicmp(module.c_str(), L"ntdll.dll") != 0
            || !image.Imported(U32(m + 13), "kernel32", "GetModuleHandleW")) continue;
        uint32_t args = 0, cursor = rva + 17; bool called = false;
        for (unsigned steps = 0; steps < 24; ++steps) {
            auto instruction = image.At(cursor, 3); if (!instruction) break;
            if (StackMov(instruction, 3, 0x8B) || StackMov(instruction, 3, 0x89)) cursor += 3;
            else if (instruction[0] >= 0x50 && instruction[0] <= 0x57) { ++args; ++cursor; }
            else if (instruction[0] == 0xFF && instruction[1] == 0x55) { called = true; break; }
            else break;
        }
        if (called && (args == 2 || args == 3)) entries.emplace_back(offset, args);
    }
    if (entries.size() != 1) return false;
    candidate.loader = function; candidate.bootstrap = std::move(blob); candidate.fragments = std::move(fragments);
    candidate.entryOffset = entries[0].first; candidate.loaderArguments = entries[0].second; return true;
}
inline bool Discover(const Image& image, Analysis& result, std::string& error) {
    result = Analysis{}; result.imageSize = image.size;
    if (!FindSdk(image, result, error)) return false;
    FindGateway(image, result);
    std::vector<Analysis> loaders;
    image.Scan([&](uint32_t rva, const uint8_t* p, size_t n) {
        if (!Match(p, n, {0x6A,0x40,0x68,0,0x30,0,0,0xA1,-1,-1,-1,-1,0x50,0x6A,0,0xFF,0x15,-1,-1,-1,-1})) return;
        Analysis loader; if (LoaderAt(image, result, rva, loader)) loaders.push_back(std::move(loader));
    });
    result.loaderCandidates = loaders.size();
    if (loaders.size() != 1) { error = loaders.empty() ? "supported linked SDK bootstrap loader not found" : "ambiguous SDK bootstrap loaders"; return false; }
    result.loader = loaders[0].loader; result.bootstrap = std::move(loaders[0].bootstrap); result.fragments = std::move(loaders[0].fragments);
    result.entryOffset = loaders[0].entryOffset; result.loaderArguments = loaders[0].loaderArguments;
    return true;
}
} // namespace ClientDiscovery
