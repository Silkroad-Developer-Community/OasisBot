using System.Collections.Generic;
using RSBot.Core;
using RSBot.Core.Client.ReferenceObjects;
using RSBot.Core.Network;

namespace RSBot.Chat.Bundle.Network
{
    internal class LinkedItemResponseHandler : IPacketHandler
    {
        public ushort Opcode => 0xB504;

        public PacketDestination Destination => PacketDestination.Client;

        private const int MaxCacheSize = 100;

        private static readonly Queue<ulong> _itemHistory = new();

        public void Invoke(Packet packet)
        {
            if (packet.ReadByte() == 0)
            {
                packet.ReadUShort(); // Error code
                return;
            }

            var itemsCount = packet.ReadByte();
            for (var i = 0; i < itemsCount; i++)
            {
                var uid = packet.ReadULong();
                ReadRental(packet);
                var id = packet.ReadUInt();
                if (id == 0)
                    continue;

                var item = Game.ReferenceManager.GetRefItem(id);
                if (item == null)
                {
                    // Entries have no length prefix, so the next entry cannot be located safely.
                    Log.Debug($"[LinkedItemResponseHandler] Unknown item ID: {id}");
                    return;
                }

                if (!TryReadItem(packet, item, out var amount))
                    return;

                AddToCache(uid, item.GetRealName() ?? id.ToString(), amount);
            }
        }

        private static void ReadRental(Packet packet)
        {
            var flags = packet.ReadUInt();
            if ((flags & 3) == 0)
                return;

            packet.ReadUShort(); // CanDelete
            if ((flags & 1) != 0)
            {
                if (Game.ClientType < GameClientType.Chinese || Game.ClientType == GameClientType.Rigid)
                {
                    packet.ReadUInt(); // PeriodBeginTime
                    packet.ReadUInt(); // PeriodEndTime
                }
                else
                {
                    packet.ReadULong(); // PeriodBeginTime
                    packet.ReadULong(); // PeriodEndTime
                }
            }

            if ((flags & 2) != 0)
            {
                packet.ReadUShort(); // CanRecharge
                packet.ReadUInt(); // MeterRateTime
            }
        }

        private static bool TryReadItem(Packet packet, RefObjItem item, out ushort amount)
        {
            amount = 1;
            if (item.IsEquip || item.IsJobEquip || (item.IsFellowEquip && Game.ClientType >= GameClientType.Chinese))
            {
                if (Game.ClientType == GameClientType.Global || Game.ClientType == GameClientType.Korean)
                    packet.ReadULong(); // separate attribute mask
                packet.ReadByte(); // OptLevel
                packet.ReadULong(); // Variance
                packet.ReadUInt(); // Durability / expiration

                ReadMagicOptions(packet);

                var bindingGroups = Game.ClientType switch
                {
                    _ when Game.ClientType < GameClientType.Chinese => 2,
                    GameClientType.Korean => 3,
                    _ => 4,
                };
                for (var group = 0; group < bindingGroups; group++)
                {
                    packet.ReadByte(); // Binding type
                    var bindingCount = packet.ReadByte();
                    for (var i = 0; i < bindingCount; i++)
                    {
                        packet.ReadByte(); // Slot
                        packet.ReadULong(); // Binding option ID and value
                    }
                }
            }
            else if (item.IsStackable)
            {
                if (item.TypeID3 == 5 && item.TypeID4 != 1)
                {
                    packet.ReadUInt(); // Gold payload
                    return true;
                }

                amount = packet.ReadUShort();
                if (item.IsTrading)
                    packet.ReadString(); // Owner name
                else if (item.TypeID3 == 11 && (item.TypeID4 == 1 || item.TypeID4 == 2))
                    packet.ReadByte(); // Stone assimilation
                else if (item.TypeID3 == 14 && item.TypeID4 == 2)
                {
                    if (Game.ClientType == GameClientType.Global || Game.ClientType == GameClientType.Korean)
                    {
                        packet.ReadUInt(); // two DWORDs
                        packet.ReadUInt();
                    }
                    else
                    {
                        ReadMagicOptions(packet);
                    }
                }
            }
            else if (item.IsPet)
            {
                return TryReadPet(packet);
            }
            else if (item.IsTransmonster || (item.IsMagicCube && item.TypeID4 == 1))
            {
                packet.ReadUInt(); // RefCharID / cube data
            }
            else if (item.TypeID2 == 2)
            {
                // Other mall item subtypes have no additional payload.
                return true;
            }
            else
            {
                Log.Debug($"[LinkedItemResponseHandler] Unsupported item type: {item.ID}");
                return false;
            }

            return true;
        }

        private static void ReadMagicOptions(Packet packet)
        {
            var count = packet.ReadByte();
            for (var i = 0; i < count; i++)
                packet.ReadULong();
        }

        private static bool TryReadPet(Packet packet)
        {
            if (packet.ReadByte() == 1) // Inactive: no RefCharID, name or buffs follow.
                return true;

            var characterId = packet.ReadUInt();
            var character = Game.ReferenceManager.GetRefObjChar(characterId);
            if (character == null)
            {
                Log.Debug($"[LinkedItemResponseHandler] Unknown pet character ID: {characterId}");
                return false;
            }

            if (
                character.Bionic != 1
                || character.TypeID1 != 1
                || character.TypeID2 != 2
                || character.TypeID3 != 3
                || (
                    character.TypeID4 != 3
                    && character.TypeID4 != 4
                    && (Game.ClientType < GameClientType.Chinese || character.TypeID4 != 9)
                )
            )
                return true;

            packet.ReadString(); // Name
            if (character.TypeID4 == 4)
                packet.ReadUInt(); // Grab pet expiration (seconds)
            else if (Game.ClientType >= GameClientType.Chinese)
                packet.ReadByte(); // Growth / fellow pet level

            var buffCount = packet.ReadByte();
            for (var i = 0; i < buffCount; i++)
            {
                var buffType = packet.ReadByte();
                packet.ReadUInt(); // Skill / item ID
                packet.ReadUInt(); // Remaining time
                if (buffType == 5)
                {
                    packet.ReadUInt(); // Additional buff data
                    packet.ReadByte();
                }
            }

            return true;
        }

        private void AddToCache(ulong uid, string name, ushort amount)
        {
            if (!Chat.LinkedItems.ContainsKey(uid))
            {
                _itemHistory.Enqueue(uid);

                if (_itemHistory.Count > MaxCacheSize)
                {
                    ulong oldId = _itemHistory.Dequeue();
                    Chat.LinkedItems.Remove(oldId);
                }
            }

            Chat.LinkedItems[uid] = (name, amount);
        }
    }
}
