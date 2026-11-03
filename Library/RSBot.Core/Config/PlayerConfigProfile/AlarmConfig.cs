using RSBot.Core.Objects;
using System.Collections.Generic;

namespace RSBot.Core.PlayerConfigProfile
{
    public class AlarmConfig
    {
        public bool PlayAlarmUniqueInRange { get; set; } = false;
        public string AlarmPathUniqueInRange { get; set; } = string.Empty;

        public bool PlayTigerGirl { get; set; } = false;
        public string AlarmPathTigerGirl { get; set; } = string.Empty;

        public bool PlayCerberus { get; set; } = false;
        public string AlarmPathCerberus { get; set; } = string.Empty;

        public bool PlayCaptainIvy { get; set; } = false;
        public string AlarmPathCaptainIvy { get; set; } = string.Empty;

        public bool PlayUruchi { get; set; } = false;
        public string AlarmPathUruchi { get; set; } = string.Empty;

        public bool PlayIsyutaru { get; set; } = false;
        public string AlarmPathIsyutaru { get; set; } = string.Empty;

        public bool PlayLordYarkan { get; set; } = false;
        public string AlarmPathLordYarkan { get; set; } = string.Empty;

        public bool PlayDemonShaitan { get; set; } = false;
        public string AlarmPathDemonShaitan { get; set; } = string.Empty;

        public bool PlayGeneral { get; set; } = false;
        public string AlarmPathGeneral { get; set; } = string.Empty;
        public string RegexGeneral { get; set; } = string.Empty;
    }

    public class AvoidanceConfig
    {
        public List<MonsterRarity> AvoidMonsters { get; set; } = new();
        public List<MonsterRarity> PreferMonsters { get; set; } = new();
        public List<MonsterRarity> BerserkMonsters { get; set; } = new();
    }
}
