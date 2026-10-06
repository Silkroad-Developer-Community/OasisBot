using RSBot.Core.Client.ReferenceObjects;

namespace RSBot.Core.Network.Handler.Agent;

internal class ItemObtainedNotification : IPacketHandler
{
    public ushort Opcode => 0x366D;

    public PacketDestination Destination => PacketDestination.Client;

    public void Invoke(Packet packet)
    {
        byte mode = packet.ReadByte();
        byte subtype = 0;
        string characterName;
        ushort regionId = 0;

        switch (mode)
        {
            case 0:
                characterName = packet.ReadString();
                regionId = packet.ReadUShort();
                break;

            case 1:
                subtype = packet.ReadByte();
                characterName = packet.ReadString();
                break;

            default:
                return;
        }

        uint itemId = packet.ReadUInt();
        RefObjItem item = Game.ReferenceManager.GetRefItem(itemId);
        string itemName = item?.GetRealName() ?? itemId.ToString();

        if (mode == 0)
        {
            RefRegionCode region = Game.ReferenceManager.GetRefRegionCode(regionId);
            string regionName = region?.GetRealName() ?? regionId.ToString();
            Log.Notify($"Item [{itemName}] is picked up by [{characterName}] in [{regionName}].");
            return;
        }

        string boxCode = subtype switch
        {
            1 => "ITEM_PRE_MALL_SILKROAD_BOX",
            3 => "ITEM_MALL_RUIN_EQUIP_BOX",
            4 => "ITEM_MALL_LIMITED_EQUIP_BOX",
            _ => null,
        };

        if (boxCode != null)
        {
            RefObjItem box = Game.ReferenceManager.GetRefItem(boxCode);
            string boxName = box?.GetRealName() ?? boxCode;
            Log.Notify($"Item [{itemName}] is received by [{characterName}] from [{boxName}].");
            return;
        }

        // Other subtypes have the same payload, including subtype 2.
        Log.Notify($"Item [{itemName}] is received by [{characterName}] (notification subtype: {subtype}).");
    }
}
