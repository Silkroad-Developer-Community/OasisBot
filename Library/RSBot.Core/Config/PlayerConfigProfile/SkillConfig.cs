using System.Collections.Generic;

namespace RSBot.Core.PlayerConfigProfile
{
    public class SkillConfig
    {
        public uint ImbueSkillId { get; set; } = 0;
        public uint TeleportSkillId { get; set; } = 0;
        public uint ResurrectionSkillId { get; set; } = 0;

        public Dictionary<int, List<uint>> AttackSkillsByRarity { get; set; } = new()
        {
            [0] = new(),
            [1] = new(),
            [2] = new(),
            [3] = new(),
            [4] = new(),
            [5] = new(),
            [6] = new(),
            [7] = new(),
            [8] = new(),
            [9] = new()
        };

        public List<uint> BuffSkills { get; set; } = new();

        public bool UseSkillsInOrder { get; set; } = false;
        public bool UseDefaultAttack { get; set; } = true;
        public bool CastBuffsBetweenAttacks { get; set; } = true;
        public bool CastBuffsDuringWalkBack { get; set; } = false;
        public bool CastBuffsInTowns { get; set; } = false;
        public bool UseTeleportSkill { get; set; } = false;
        public bool WarlockMode { get; set; } = false;
        public bool DoNotAttack { get; set; } = false;

        public bool AcceptResurrection { get; set; } = true;
        public bool ResurrectPartyMembers { get; set; } = false;
        public ushort ResDelaySeconds { get; set; } = 120;
        public ushort ResRadius { get; set; } = 100;

        public bool AutoLearnMastery { get; set; } = false;
        public bool LearnMasteryWhenBotStopped { get; set; } = false;
        public int MasteryGap { get; set; } = 0;
        public string SelectedMastery { get; set; } = string.Empty;

        public bool ShowAttacks { get; set; } = true;
        public bool ShowBuffs { get; set; } = true;
        public bool HideLowerLevelSkills { get; set; } = false;
    }
}
