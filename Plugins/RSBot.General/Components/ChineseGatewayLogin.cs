using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using RSBot.Core;
using RSBot.Core.Network;

namespace RSBot.General.Components;

internal static class ChineseGatewayLogin
{
    private const int MachineTokenKeySeed = 0x0118860C;
    private const string TicketKey = "UGCKernel";
    private const int MaxSilpsetLength = 4096;

    private static readonly byte[] TicketMarker = { 0x11, 0x00, 0x25, 0x56, 0x00, 0x33 };
    private static readonly Lazy<uint> TicketSeed = new(CreateTicketSeed);

    public static bool TryWriteVerificationAndTicket(Packet packet, string username)
    {
        try
        {
            var ticket = BuildTicket();

            packet.WriteUInt(ComputeVerifyResult(username));
            packet.WriteUInt((uint)ticket.Length);
            packet.WriteBytes(ticket);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Chinese gateway ticket could not be created: {ex.Message}");
            return false;
        }
    }

    private static byte[] BuildTicket()
    {
        var machineToken = BuildMachineToken();
        var silpsetToken = ReadSilpsetToken();

        if (machineToken.Length > ushort.MaxValue || silpsetToken.Length > ushort.MaxValue)
            throw new InvalidOperationException("Chinese gateway ticket token is too large.");

        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        writer.Write((long)(int)TicketSeed.Value);
        writer.Write(TicketMarker);
        writer.Write((ushort)machineToken.Length);
        writer.Write(machineToken);
        writer.Write((ushort)silpsetToken.Length);
        writer.Write(silpsetToken);

        var ticket = stream.ToArray();
        Rc4Xor(ticket, Encoding.ASCII.GetBytes(TicketKey));

        return ticket;
    }

    private static byte[] BuildMachineToken()
    {
        string hostName;
        try
        {
            hostName = Dns.GetHostName();
        }
        catch
        {
            hostName = Environment.MachineName;
            if (hostName.Length > 16)
                hostName = hostName.Substring(0, 16);
        }

        if (string.IsNullOrEmpty(hostName))
            throw new InvalidOperationException("Computer name is empty.");

        if (hostName.Length > byte.MaxValue)
            hostName = hostName.Substring(0, byte.MaxValue);

        var token = Encoding.ASCII.GetBytes(hostName);
        var key = Encoding.ASCII.GetBytes(MachineTokenKeySeed.ToString(CultureInfo.InvariantCulture));
        Rc4Xor(token, key);

        return token;
    }

    private static byte[] ReadSilpsetToken()
    {
        var silkroadDirectory = GlobalConfig.Get<string>("RSBot.SilkroadDirectory");
        if (string.IsNullOrWhiteSpace(silkroadDirectory))
            throw new InvalidOperationException("Silkroad directory is not configured.");

        var path = Path.Combine(silkroadDirectory, "setting", "SRsilpset.dat");
        if (!File.Exists(path))
            throw new FileNotFoundException("SRsilpset.dat was not found.", path);

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > MaxSilpsetLength)
            throw new InvalidOperationException("SRsilpset.dat is larger than the client buffer.");

        return bytes;
    }

    private static uint ComputeVerifyResult(string username)
    {
        if (string.IsNullOrEmpty(username))
            return 0;

        uint crc = 0xFFFFFFFF;
        var bytes = Encoding.GetEncoding(950).GetBytes(username);

        foreach (var value in bytes)
        {
            crc ^= value;

            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xEDB88320;
        }

        return ~crc;
    }

    private static uint CreateTicketSeed()
    {
        if (CoCreateGuid(out var guid) != 0)
            guid = Guid.NewGuid();

        var bytes = guid.ToByteArray();
        return BitConverter.ToUInt32(bytes, 0)
            + BitConverter.ToUInt16(bytes, 4)
            + BitConverter.ToUInt16(bytes, 6);
    }

    private static void Rc4Xor(byte[] buffer, byte[] key)
    {
        var state = new byte[256];
        for (var i = 0; i < state.Length; i++)
            state[i] = (byte)i;

        var j = 0;
        for (var i = 0; i < state.Length; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & 0xFF;
            (state[i], state[j]) = (state[j], state[i]);
        }

        var x = 0;
        j = 0;
        for (var index = 0; index < buffer.Length; index++)
        {
            x = (x + 1) & 0xFF;
            j = (j + state[x]) & 0xFF;
            (state[x], state[j]) = (state[j], state[x]);

            buffer[index] ^= state[(state[x] + state[j]) & 0xFF];
        }
    }

    [DllImport("ole32.dll")]
    private static extern int CoCreateGuid(out Guid guid);
}
