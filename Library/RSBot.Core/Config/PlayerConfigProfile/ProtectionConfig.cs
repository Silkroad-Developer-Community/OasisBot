namespace RSBot.Core.PlayerConfigProfile
{
    public class ProtectionConfig
    {
        public bool UseHpPotions { get; set; } = true;
        public int MinHpPotionPercent { get; set; } = 75;
        public int MinHpPotionsLeft { get; set; } = 15;

        public bool UseMpPotions { get; set; } = true;
        public int MinMpPotionPercent { get; set; } = 75;
        public int MinMpPotionsLeft { get; set; } = 15;

        public bool UseVigorHp { get; set; } = false;
        public int MinPlayerHpVigorPercent { get; set; } = 50;

        public bool UseVigorMp { get; set; } = false;
        public int MinPlayerMpVigorPercent { get; set; } = 50;

        public bool UseUniversalPills { get; set; } = true;
        public bool UseAbnormalStatePotion { get; set; } = true;

        public bool UseSkillHp { get; set; } = false;
        public int MinSkillHpPercent { get; set; } = 50;
        public uint HpSkillId { get; set; } = 0;

        public bool UseSkillMp { get; set; } = false;
        public int MinSkillMpPercent { get; set; } = 50;
        public uint MpSkillId { get; set; } = 0;

        public bool UseBadStatusSkill { get; set; } = false;
        public uint BadStatusSkillId { get; set; } = 0;

        public bool AutoIncreaseStr { get; set; } = false;
        public int PointsToStr { get; set; } = 0;
        public bool AutoIncreaseInt { get; set; } = false;
        public int PointsToInt { get; set; } = 0;

        public bool UsePetHp { get; set; } = false;
        public int MinPetHpPercent { get; set; } = 80;
        public bool UsePetHgp { get; set; } = false;
        public int MinPetHgpPercent { get; set; } = 90;
        public bool AutoSummonAttackPet { get; set; } = false;
        public bool AutoReviveAttackPet { get; set; } = false;
        public bool ReturnOnFullPetInventory { get; set; } = false;

        public bool CheckDead { get; set; } = false;
        public int DeadTimeoutSeconds { get; set; } = 30;
        public bool CheckDurability { get; set; } = false;
        public bool CheckInventoryFull { get; set; } = false;
        public bool CheckNoArrows { get; set; } = false;
        public bool CheckNoHpPotions { get; set; } = false;
        public bool CheckNoMpPotions { get; set; } = false;
        public bool CheckLevelUp { get; set; } = false;
        public bool StopBotOnReturnToTown { get; set; } = false;
        public bool IncreaseWhenBotStopped { get; set; } = false;

        public bool CheckShardFatigue { get; set; } = false;
        public int ShardFatigueMinToDc { get; set; } = 15;
    }
}
