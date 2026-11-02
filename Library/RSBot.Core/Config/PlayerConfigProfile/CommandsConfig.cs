namespace RSBot.Core.Config.PlayerConfigProfile
{
    public class CommandsConfig
    {
        public bool EnableCommandCenter { get; set; } = false;
        public string EmoticonYes { get; set; } = "start";
        public string EmoticonNo { get; set; } = "stop";
        public string EmoticonRush { get; set; } = "area";
        public string EmoticonGreeting { get; set; } = "area";
        public string EmoticonSmile { get; set; } = "show";
    }
}
