using System.Collections.Generic;
using RSBot.Core.Event;
using RSBot.Core.Objects;
using RSBot.Core.Objects.Item;

namespace RSBot.Core.Network.Handler.Agent.Inventory;

internal class InventoryUpdateItemResponse : IPacketHandler
{
    /// <summary>
    ///     Gets or sets the opcode.
    /// </summary>
    /// <value>
    ///     The opcode.
    /// </value>
    public ushort Opcode => 0x3040;

    /// <summary>
    ///     Gets or sets the destination.
    /// </summary>
    /// <value>
    ///     The destination.
    /// </value>
    public PacketDestination Destination => PacketDestination.Client;

    /// <summary>
    ///     Handles the packet.
    /// </summary>
    /// <param name="packet">The packet.</param>
    public void Invoke(Packet packet)
    {
        if (Game.ClientType == GameClientType.Global || Game.ClientType == GameClientType.RuSro)
        {
            InvokeModernClients(packet);
            return;
        }

        InvokeLegacy(packet);
    }

    private static void InvokeLegacy(Packet packet)
    {
        var sourceSlot = packet.ReadByte();
        var itemUpdateFlag = (ItemUpdateFlag)packet.ReadByte();

        var item = Game.Player.Inventory.GetItemAt(sourceSlot);
        if (item == null)
            return;

        if (itemUpdateFlag.HasFlag(ItemUpdateFlag.RefObjID))
            item.ItemId = packet.ReadUInt();

        if (itemUpdateFlag.HasFlag(ItemUpdateFlag.OptLevel))
            item.OptLevel = packet.ReadByte();

        if (itemUpdateFlag.HasFlag(ItemUpdateFlag.Variance))
            item.Attributes = new ItemAttributesInfo(packet.ReadULong());

        if (itemUpdateFlag.HasFlag(ItemUpdateFlag.Quanity))
            item.Amount = packet.ReadUShort();

        if (itemUpdateFlag.HasFlag(ItemUpdateFlag.Durability))
            item.Durability = packet.ReadUInt();

        if (itemUpdateFlag.HasFlag(ItemUpdateFlag.State) || itemUpdateFlag.HasFlag(ItemUpdateFlag.State2))
            item.State = (InventoryItemState)packet.ReadByte();

        if (itemUpdateFlag.HasFlag(ItemUpdateFlag.MagParams))
        {
            item.MagicOptions = new List<MagicOptionInfo>();

            var magParamCount = packet.ReadByte();

            for (var i = 0; i < magParamCount; i++)
                item.MagicOptions.Add(MagicOptionInfo.FromPacket(packet));
        }

        if (itemUpdateFlag.HasFlag(ItemUpdateFlag.Unknown))
        {
            // When opening a pet, it comes as (ItemUpdateFlag)128, where it is used and I can't find out what the name is. Can update if anyone knows?
        }

        EventManager.FireEvent("OnUpdateInventoryItem", sourceSlot);
    }

    private static void InvokeModernClients(Packet packet)
    {
        var updateType = packet.ReadByte();

        if (updateType == 9)
        {
            var objectId = packet.ReadUInt();
            var containerSlot = packet.ReadByte();
            var updateFlags = (ModernItemUpdateFlag)packet.ReadUShort();
            var inventory = GetInventoryForObject(objectId);
            var containerItem = inventory?.GetItemAt(containerSlot);

            ApplyModernClientsContainerUpdate(packet, containerItem, updateFlags);

            if (containerItem != null && ReferenceEquals(inventory, Game.Player.Inventory))
                EventManager.FireEvent("OnUpdateInventoryItem", containerSlot);

            return;
        }

        var sourceSlot = packet.ReadByte();
        var itemUpdateFlags = (ModernItemUpdateFlag)packet.ReadUShort();
        var item = Game.Player.Inventory.GetItemAt(sourceSlot);
        if (item == null)
            return;

        ApplyModernClientsItemUpdate(packet, item, itemUpdateFlags, updateType);
        EventManager.FireEvent("OnUpdateInventoryItem", sourceSlot);
    }

    private static void ApplyModernClientsContainerUpdate(
        Packet packet,
        InventoryItem item,
        ModernItemUpdateFlag updateFlags
    )
    {
        if (updateFlags.HasFlag(ModernItemUpdateFlag.Durability))
        {
            var durability = packet.ReadUInt();
            if (item != null)
                item.Durability = durability;
        }

        if (updateFlags.HasFlag(ModernItemUpdateFlag.BindingOptions))
            ReadBindingOptions(packet, item);
    }

    private static void ApplyModernClientsItemUpdate(
        Packet packet,
        InventoryItem item,
        ModernItemUpdateFlag updateFlags,
        byte updateType
    )
    {
        if (updateFlags.HasFlag(ModernItemUpdateFlag.RefObjID))
            item.ItemId = packet.ReadUInt();

        if (updateFlags.HasFlag(ModernItemUpdateFlag.Quantity))
            item.Amount = packet.ReadUShort();

        if (updateFlags.HasFlag(ModernItemUpdateFlag.Durability))
            item.Durability = packet.ReadUInt();

        if (updateFlags.HasFlag(ModernItemUpdateFlag.OptLevel))
            item.OptLevel = packet.ReadByte();

        if (updateFlags.HasFlag(ModernItemUpdateFlag.Variance))
            item.Attributes = new ItemAttributesInfo(packet.ReadULong());

        if (updateFlags.HasFlag(ModernItemUpdateFlag.MagParams) && item.Record != null)
            ReadMagicOptions(packet, item);

        if (updateFlags.HasFlag(ModernItemUpdateFlag.BindingOptions) && (updateType == 0 || updateType == 8))
            ReadBindingOptions(packet, item);

        if (updateFlags.HasFlag(ModernItemUpdateFlag.RemainTime))
            packet.ReadUInt();

        if (updateFlags.HasFlag(ModernItemUpdateFlag.ItemState))
            packet.ReadByte();
    }

    private static void ReadMagicOptions(Packet packet, InventoryItem item)
    {
        item.MagicOptions = new List<MagicOptionInfo>();

        var magParamCount = packet.ReadByte();
        for (var i = 0; i < magParamCount; i++)
            item.MagicOptions.Add(MagicOptionInfo.FromPacket(packet));
    }

    private static void ReadBindingOptions(Packet packet, InventoryItem item)
    {
        item ??= new InventoryItem();
        item.BindingOptions = new List<BindingOption>();

        for (var bindingIndex = 0; bindingIndex < 4; bindingIndex++)
        {
            var bindingType = (BindingOptionType)packet.ReadByte();
            var bindingAmount = packet.ReadByte();

            for (var i = 0; i < bindingAmount; i++)
                item.BindingOptions.Add(BindingOption.FromPacket(packet, bindingType));
        }
    }

    private static InventoryItemCollection GetInventoryForObject(uint objectId)
    {
        if (Game.Player.UniqueId == objectId)
            return Game.Player.Inventory;

        if (Game.Player.AbilityPet?.UniqueId == objectId)
            return Game.Player.AbilityPet.Inventory;

        if (Game.Player.Fellow?.UniqueId == objectId)
            return Game.Player.Fellow.Inventory;

        if (Game.Player.Growth?.UniqueId == objectId)
            return Game.Player.Growth.Inventory;

        if (Game.Player.Vehicle?.UniqueId == objectId)
            return Game.Player.Vehicle.Inventory;

        return null;
    }
}
