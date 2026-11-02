using System.Collections.Generic;

namespace RSBot.Core.PlayerConfigProfile
{
    public class ItemConfig
    {
        public bool PickEverything { get; set; } = false;
        public bool PickGold { get; set; } = true;
        public bool PickEquipments { get; set; } = true;
        public bool PickRare { get; set; } = true;
        public bool PickBlue { get; set; } = true;
        public bool PickQuestItems { get; set; } = true;
        public bool JustPickMyItems { get; set; } = false;

        public bool DontPickupWhileAttacking { get; set; } = false;
        public bool DontPickupWhileBotting { get; set; } = false;
        public bool DontPickupInBerzerk { get; set; } = false;
        public bool EnableAbilityPet { get; set; } = true;
    }

    public class InventoryConfig
    {
        public bool AutoSort { get; set; } = true;
        public List<string> ItemsAtTrainplace { get; set; } = new();
        public List<string> AutoUseAccordingToPurpose { get; set; } = new();
        public ItemConfig PickupFilter { get; set; } = new();
    }
}
