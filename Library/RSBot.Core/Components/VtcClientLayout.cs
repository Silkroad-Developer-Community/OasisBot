using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace RSBot.Core.Components;

/// <summary>RVA-based targets discovered from this executable by the native session host.</summary>
internal sealed class VtcClientLayout
{
    internal uint ImageSize { get; }
    internal uint SysEnterRva { get; }
    internal uint SendCommandRva { get; }
    internal uint ConnectCallRva { get; }
    internal uint ConnectImportRva { get; }
    internal byte[] SysEnterBytes { get; }
    internal byte[] SysEnterMask { get; }
    internal byte[] SendCommandBytes { get; }
    internal byte[] SendCommandMask { get; }
    private readonly byte[] _fileHash;

    internal bool HasGateway => ConnectCallRva != 0 && ConnectImportRva != 0;

    private VtcClientLayout(byte[] data)
    {
        ImageSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4));
        SysEnterRva = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8));
        SendCommandRva = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(12));
        ConnectCallRva = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(16));
        ConnectImportRva = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(20));
        SysEnterBytes = data.AsSpan(24, 8).ToArray();
        SysEnterMask = data.AsSpan(32, 8).ToArray();
        SendCommandBytes = data.AsSpan(40, 6).ToArray();
        SendCommandMask = data.AsSpan(46, 6).ToArray();
        _fileHash = data.AsSpan(52, 32).ToArray();
    }

    internal static VtcClientLayout Parse(byte[] data)
    {
        if (data.Length != 84 || BinaryPrimitives.ReadUInt32LittleEndian(data) != 1)
            throw new InvalidDataException("Unsupported XIGNCODE client discovery data.");
        var layout = new VtcClientLayout(data);
        if (
            layout.ImageSize == 0
            || layout.ImageSize > 268435456
            || layout.SysEnterRva == layout.SendCommandRva
            || !layout.Contains(layout.SysEnterRva, 8)
            || !layout.Contains(layout.SendCommandRva, 6)
            || (layout.ConnectCallRva != 0 || layout.ConnectImportRva != 0)
                && (!layout.Contains(layout.ConnectCallRva, 6) || !layout.Contains(layout.ConnectImportRva, 4))
            || !ValidMask(layout.SysEnterMask)
            || !ValidMask(layout.SendCommandMask)
        )
            throw new InvalidDataException("Invalid XIGNCODE client discovery targets.");
        return layout;
    }

    private bool Contains(uint rva, uint length) => rva != 0 && (ulong)rva + length <= ImageSize;

    private static bool ValidMask(byte[] mask) =>
        mask.Any(value => value != 0) && mask.All(value => value == 0 || value == 255);

    internal void ValidateForGame(string executable)
    {
        if (!HasGateway)
            throw new InvalidDataException("VTC: the gateway connect signature is missing or ambiguous.");
        using var file = File.OpenRead(executable);
        if (!CryptographicOperations.FixedTimeEquals(_fileHash, SHA256.HashData(file)))
            throw new InvalidDataException(
                "VTC: the executable changed after XIGNCODE target discovery. Restart this session."
            );
    }

    internal static uint Address(IntPtr moduleBase, uint rva)
    {
        long value = moduleBase.ToInt64();
        if (value < 0 && IntPtr.Size == 4)
            value = unchecked((uint)value);
        long address = value + rva;
        if (address < 0 || address > uint.MaxValue)
            throw new InvalidDataException("The VTC client must be a 32-bit process.");
        return (uint)address;
    }

    internal static bool Matches(byte[] actual, byte[] expected, byte[] mask)
    {
        if (actual.Length != expected.Length || mask.Length != expected.Length)
            return false;
        for (int i = 0; i < actual.Length; ++i)
            if (mask[i] != 0 && actual[i] != expected[i])
                return false;
        return true;
    }
}
