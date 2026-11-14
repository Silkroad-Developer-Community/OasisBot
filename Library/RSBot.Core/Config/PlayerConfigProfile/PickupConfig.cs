namespace RSBot.Core.PlayerConfigProfile
{
    public class PickupConfig
    {
        public bool PickupGold { get; set; } = true;
        public bool PickupRareItems { get; set; } = true;
        public bool PickupBlueItems { get; set; } = true;
        public bool PickupQuestItems { get; set; } = true;
        public bool PickupAnyEquips { get; set; } = true;
        public bool PickupEverything{ get; set; } = false;
        public bool PickupWhileBotting { get; set; } = false;
        public bool PickupWhileAttacking { get; set; } = false;
        public bool PickupInBerzerk { get; set; } = false;
        public bool JustPickMyItems { get; set; } = false;
        public bool UseAbilityPet { get; set; } = true;
    }
}
