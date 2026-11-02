namespace RSBot.Core.Config.PlayerConfigProfile
{
    public class StatisticsConfig
    {
        public bool EnableStatistics { get; set; } = true;
        public bool SPPerHour { get; set; } = true;
        public bool ItemsPerHour { get; set; } = true;
        public bool KillsPerHour { get; set; } = true;
        public bool GoldPerHour { get; set; } = true;
        public bool EXPPerHour { get; set; } = true;
        public bool SPGained { get; set; } = true;
        public bool ItemsPicked { get; set; } = true;
        public bool LevelUps { get; set; } = true;
        public bool Kills { get; set; } = true;
        public bool GoldGained { get; set; } = true;
        public bool EXPGained { get; set; } = true;
        public bool Deaths { get; set; } = true;
        public bool BottingTime { get; set; } = true;
    }
}
