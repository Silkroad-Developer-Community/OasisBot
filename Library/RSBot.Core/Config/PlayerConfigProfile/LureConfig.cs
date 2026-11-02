using RSBot.Core.Objects;

namespace RSBot.Core.PlayerConfigProfile
{
    public class LureConfig
    {
        public string SelectedScriptPath { get; set; } = string.Empty;
        public string WalkbackPath { get; set; } = string.Empty;
        public TrainingAreaConfig Area { get; set; } = new();
        public bool UseScript { get; set; } = false;
        public bool WalkRandomly { get; set; } = false;
        public bool StayAtCenter { get; set; } = false;
        public bool StayAtCenterForN { get; set; } = false;
        public int StayAtCenterForSeconds { get; set; } = 5;
        public bool UseSpeedDrug { get; set; } = false;
        public bool UseHowlingShout { get; set; } = false;
        public bool UseNormalAttack { get; set; } = false;
        public bool NoHowlingAtCenter { get; set; } = false;
        public bool UseAttackingSkills { get; set; } = false;
        public bool StopIfNumPartyMemberDead { get; set; } = false;
        public int NumPartyMemberDead { get; set; } = 3;
        public bool StopIfNumPartyMember { get; set; } = false;
        public int NumPartyMember { get; set; } = 3;
        public bool StopIfNumMonsterType { get; set; } = false;
        public int NumMonsterType { get; set; } = 3;
        public MonsterRarity SelectedMonsterType { get; set; } = MonsterRarity.General;
        public bool StopIfNumPartyMembersOnSpot { get; set; } = false;
        public int NumPartyMembersOnSpot { get; set; } = 3;
    }
}
