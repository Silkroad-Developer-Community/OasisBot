using System.Collections.Generic;

namespace RSBot.Core.PlayerConfigProfile
{
    public class TownConfig
    {
        public bool Enabled { get; set; } = true;
        public bool RepairGear { get; set; } = true;

        public bool SellPetItems { get; set; } = true;
        public bool StorePetItems { get; set; } = true;
        public List<string> SellList { get; set; } = new();
        public List<string> StoreList { get; set; } = new();
        public List<string> PickupList { get; set; } = new();

        public List<string> AccessoryGroup { get; set; } = new();
        public List<string> PotionGroup { get; set; } = new();
        public List<string> ProtectorGroup { get; set; } = new();
        public List<string> StableGroup { get; set; } = new();
        public List<string> WeaponGroup { get; set; } = new();
    }
}
