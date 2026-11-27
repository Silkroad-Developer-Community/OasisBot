using System.Collections.Generic;
using System.Linq;
using Python.Runtime;
using RSBot.Core;
using RSBot.Core.Client.ReferenceObjects;
using RSBot.Core.Extensions;
using RSBot.Core.Objects;
using RSBot.Python.Runtime;

namespace RSBot.Python.API.Core
{
    public class InventoryAPI : IPythonPlugin
    {
        /// <summary>
        /// Unique module name of the plugin.
        /// </summary>
        public string ModuleName => "inventory";

        public void Init(PythonContext context) { }

        private PyList BuildItemList(IEnumerable<InventoryItem> items)
        {
            using (Py.GIL())
            {
                var list = new PyList();

                foreach (var item in items)
                {
                    if (item == null)
                        continue;
                    var pyOptions = new List<string>();
                    var pyAttributes = new List<string>();
                    if (item.MagicOptions != null)
                    {
                        foreach (var magicOption in item.MagicOptions)
                        {
                            var option = Game.ReferenceManager.GetMagicOption(magicOption.Id);

                            if (option != null)
                                pyOptions.Append(GetFusingTranslation(option, magicOption.Value));
                        }
                    }
                    if (item.Attributes != 0)
                    {
                        var availableAttributes = ItemAttributesInfo.GetAvailableAttributeGroupsForItem(item.Record);

                        if (availableAttributes != null)
                        {
                            foreach (var attribute in availableAttributes)
                            {
                                var slot = ItemAttributesInfo.GetAttributeSlotForItem(attribute, item.Record);
                                var translation = attribute.GetTranslation();
                                pyAttributes.Append($"{translation} +{item.Attributes.GetPercentage(slot)}%");
                            }
                        }
                    }

                    var pyItem = PythonData.Dict(
                        ("model", item.ItemId),
                        ("servername", item.Record.CodeName),
                        ("name", item.Record.GetRealName()),
                        ("tid", item.Record.Tid),
                        ("quantity", item.Amount),
                        ("degree", item.Record.Degree),
                        ("plus", item.OptLevel),
                        ("rarity", item.Record.GetRarityName()),
                        ("durability", item.Durability),
                        ("slot", item.Slot),
                        ("magic_options", PythonData.List(pyOptions)),
                        ("attributes", PythonData.List(pyAttributes))
                    );
                    list.Append(pyItem);
                }

                return list;
            }
        }

        public PyDict get_inventory()
        {
            using (Py.GIL())
            {
                if (Game.Player == null)
                    return PythonData.Dict(
                        ("size", 0),
                        ("gold", 0L),
                        ("equipped", new PyList()),
                        ("items", new PyList()),
                        ("avatar", new PyList())
                    );

                var inventorySize = Game.Player.Inventory.Capacity;
                var gold = Game.Player.Gold;

                var itemsEquipped = Game.Player.Inventory.GetEquippedPartItems();
                var itemsInventory = Game.Player.Inventory.GetNormalPartItems();
                var itemsAvatar = Game.Player.Avatars;

                var pyEquippedList = BuildItemList(itemsEquipped);
                var pyInventoryList = BuildItemList(itemsInventory);
                var pyAvatarList = BuildItemList(itemsAvatar);

                return PythonData.Dict(
                    ("size", inventorySize),
                    ("gold", gold),
                    ("equipped", pyEquippedList),
                    ("items", pyInventoryList),
                    ("avatar", pyAvatarList)
                );
            }
        }

        public PyList get_storage()
        {
            using (Py.GIL())
            {
                var result = new PyList();

                if (Game.Player == null || Game.Player.Storage == null)
                    return result;

                var storage = Game.Player.Storage;
                result = BuildItemList(storage);

                return result;
            }
        }

        public PyList get_guild_storage()
        {
            using (Py.GIL())
            {
                var result = new PyList();

                if (Game.Player == null || Game.Player.GuildStorage == null)
                    return result;

                var storage = Game.Player.GuildStorage;
                result = BuildItemList(storage);

                return result;
            }
        }

        public PyList get_pet_storage()
        {
            using (Py.GIL())
            {
                var result = new PyList();

                if (Game.Player == null || !Game.Player.HasActiveAbilityPet)
                    return result;

                var storage = Game.Player.AbilityPet.Inventory;
                result = BuildItemList(storage);

                return result;
            }
        }

        public PyDict get_job_pouch()
        {
            using (Py.GIL())
            {
                var result = new PyDict();

                if (Game.Player == null || Game.Player.Job2SpecialtyBag == null)
                    return result;

                var storage = Game.Player.Job2SpecialtyBag;
                var list = new PyList();

                foreach (var item in storage)
                {
                    if (item == null)
                        continue;

                    var pyItem = PythonData.Dict(
                        ("servername", item.Record.CodeName),
                        ("name", item.Record.GetRealName()),
                        ("quantity", item.Amount),
                        ("slot", item.Slot)
                    );

                    list.Append(pyItem);
                }
                return PythonData.Dict(("size", storage.Capacity), ("free_slots", storage.FreeSlots), ("items", list));
            }
        }

        public string GetFusingTranslation(RefMagicOpt magicOption, uint value)
        {
            //TODO: Use and extend GetGroupTranslation instead of hard coding this
            switch (magicOption?.Group)
            {
                case "MATTR_INT":
                case "MATTR_AVATAR_INT":
                case "MATTR_INT_AVATAR":
                    return $"Int {value} Increase";

                case "MATTR_STR":
                case "MATTR_STR_AVATAR":
                case "MATTR_AVATAR_STR":
                    return $"Str {value} Increase";

                case "MATTR_DUR":
                case "MATTR_AVATAR_DRUA":
                    return $"Durability {value}% Increase";

                case "MATTR_EVADE_BLOCK":
                    return $"Blocking rate {value}";

                case "MATTR_AVATAR_DARA":
                    return $"Damage Absorption {value}% Increase";

                case "MATTR_AVATAR_ER":
                case "MATTR_ER":
                    return $"Parry rate {value}% Increase";

                case "MATTR_HR":
                case "MATTR_AVATAR_HR":
                    return $"Attack rate {value}% Increase";

                case "MATTR_RESIST_FROSTBITE":
                    return $"Freezing,FrostbiteHour {value}% Reduce";

                case "MATTR_RESIST_ESHOCK":
                    return $"Electrict shockHour {value}% Reduce";

                case "MATTR_RESIST_BURN":
                    return $"BurnHour {value}% Reduce";

                case "MATTR_RESIST_POISON":
                    return $"PoisoningHour {value}% Reduce";

                case "MATTR_LUCK":
                    return $"Lucky({value}Time/times)";

                case "MATTR_SOLID":
                    return $"Steady({value}Time/times)";
                    ;
                case "MATTR_ASTRAL":
                    return $"Astral({value}Time/times)";

                case "MATTR_ATHANASIA":
                    return $"Immortal({value}Time/times)";

                case "MATTR_AVATAR_MDIA":
                case "MATTR_AVATAR_MDIA_2":
                case "MATTR_AVATAR_MDIA_3":
                case "MATTR_AVATAR_MDIA_4":
                    return $"Ignore Monster Defense {value}% Probability";

                case "MATTR_HP":
                case "MATTR_AVATAR_HP":
                    return $"HP {value} Increase";

                case "MATTR_MP":
                case "MATTR_AVATAR_MP":
                    return $"MP {value} Increase";

                case "MATTR_CRITICAL":
                case "MATTR_EVADE_CRITICAL":
                    return $"Critical {value}";

                case "MATTR_NOT_REPARABLE":
                    return "Not repairable";

                case "MATTR_REGENHPMP":
                    return $"HP recovery/MP recovery {value}% Increase";

                case "MATTR_RESIST_ZOMBIE":
                    return $"ZombieHour {value}% Reduce";

                case "MATTR_RESIST_CSMP":
                    return $"CombustionProbability {value}% Reduce";

                case "MATTR_RESIST_SLEEP":
                    return $"SleepProbability {value}% Reduce";

                case "MATTR_RESIST_STUN":
                    return $"StunProbability {value}% Reduce";

                case "MATTR_RESIST_FEAR":
                    return $"FearProbability {value}% Reduce";

                case "MATTR_RESIST_DISEASE":
                    return $"DiseaseProbability {value}% Reduce";

                case "MATTR_DEC_MAXDUR":
                    return $"Maximum durability {value}% Reduce";
            }

            return magicOption?.Group ?? $"Error. Mag. opt. value: {value}";
        }
    }
}
