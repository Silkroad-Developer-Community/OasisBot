using System.Collections.Generic;

namespace RSBot.Core.PlayerConfigProfile
{
    public class TrainingAreaConfig
    {
        public string Name { get; set; } = string.Empty;
        public int Radius { get; set; } = 50;
        public ushort Region { get; set; } = 0;
        public float X { get; set; } = 0;
        public float Y { get; set; } = 0;
        public float Z { get; set; } = 0;
        public bool IsActive { get; set; } = false;
    }

    public class TrainingConfig
    {
        public List<TrainingAreaConfig> Areas { get; set; } = [];
        public int Index { get; set; } = 0;
        public bool AttackWeakerFirst { get; set; } = false;
        public bool DontFollowMobs { get; set; } = false;
        public bool IgnoreDimensionPillar { get; set; } = false;
        public bool WalkAround { get; set; } = true;
        public bool ReturnToCenter { get; set; } = false;

        public bool CastBuffs { get; set; } = true;
        public bool UseMount { get; set; } = true;
        public bool UseSpeedDrug { get; set; } = false;
        public bool UseReverseScroll { get; set; } = false;
        public string WalkbackFile { get; set; } = string.Empty;

        public bool BerserkWhenFull { get; set; } = false;
        public bool BerserkOnMonsterRarity { get; set; } = false;
        public bool BerserkOnMonsterAmount { get; set; } = false;
        public byte MinBerserkMonsterAmount { get; set; } = 5;
        public bool BerserkAvoidance { get; set; } = false;
    }
}
