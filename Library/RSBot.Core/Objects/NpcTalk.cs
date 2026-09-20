using RSBot.Core.Network;

namespace RSBot.Core.Objects;

public class NpcTalk
{
    /// <summary>
    ///     Gets or sets the talk flag.
    /// </summary>
    /// <value>
    ///     The talk flag.
    /// </value>
    public byte Flag { get; set; }

    /// <summary>
    ///     Gets or sets the talk options.
    /// </summary>
    /// <value>
    ///     The talk options.
    /// </value>
    public byte[] Options { get; set; }

    /// <summary>
    ///     Gets or sets the custom talk name.
    /// </summary>
    public string CustomName { get; set; }

    /// <summary>
    ///     Gets or sets the talk option flags used by supported client formats.
    /// </summary>
    public ulong OptionFlags { get; set; }

    /// <summary>
    ///     Gets or sets the custom talk data type.
    /// </summary>
    public byte CustomType { get; set; }

    /// <summary>
    ///     Gets or sets the custom talk data identifier.
    /// </summary>
    public uint CustomId { get; set; }

    /// <summary>
    ///     Deserialize from the packet
    /// </summary>
    /// <param name="packet">The packet</param>
    public void Deserialize(Packet packet)
    {
        Flag = packet.ReadByte();

        if (Game.ClientType <= GameClientType.Thailand)
        {
            if ((Flag & 1) != 0)
                CustomName = packet.ReadString();

            if ((Flag & 2) != 0)
                Options = packet.ReadBytes(4);

            return;
        }

        if (Game.ClientType >= GameClientType.Vietnam && Game.ClientType <= GameClientType.Chinese)
        {
            if ((Flag & 1) != 0)
                CustomName = packet.ReadString();

            if ((Flag & 2) != 0)
            {
                var count = packet.ReadByte();
                Options = packet.ReadBytes(count);
            }

            if ((Flag & 4) != 0)
            {
                CustomType = packet.ReadByte();
                CustomId = packet.ReadUInt();
            }

            return;
        }

        if (
            Game.ClientType == GameClientType.Turkey
            || Game.ClientType == GameClientType.VTC_Game
            || Game.ClientType == GameClientType.Taiwan
            || Game.ClientType == GameClientType.Japanese
            || Game.ClientType == GameClientType.RuSro
            || Game.ClientType == GameClientType.Rigid
        )
        {
            if ((Flag & 1) != 0)
                CustomName = packet.ReadString();

            if ((Flag & 2) != 0)
                OptionFlags = packet.ReadULong();

            if ((Flag & 4) != 0)
            {
                CustomType = packet.ReadByte();
                CustomId = packet.ReadUInt();
            }

            return;
        }

        if (Game.ClientType == GameClientType.Global || Game.ClientType == GameClientType.Korean)
        {
            if ((Flag & 1) != 0)
                OptionFlags = packet.ReadULong();

            if ((Flag & 2) != 0)
            {
                CustomType = packet.ReadByte();
                CustomId = packet.ReadUInt();
            }

            return;
        }
    }
}
