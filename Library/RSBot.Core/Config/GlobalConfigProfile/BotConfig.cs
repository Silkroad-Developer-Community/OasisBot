using System.Collections.Generic;

namespace RSBot.Core.GlobalConfigProfile
{
    public class BotConfig
    {
        public string BotbaseName { get; set; } = "RSBot.Training";
        public GameClientType ClientType { get; set; } = GameClientType.Vietnam;
        public string BindIp { get; set; } = "0.0.0.0";
        public ProxyConfig Proxy { get; set; } = new ProxyConfig();
        public bool EnableLog { get; set; } = true;
        public bool StartBot { get; set; } = false;
        public bool UseReturnScroll { get; set; } = false;
        public bool EnableAutomatedLogin { get; set; } = false;
        public string AutoLoginAccountUsername { get; set; } = string.Empty;
        public string StaticCaptcha { get; set; } = string.Empty;
        public bool EnableStaticCaptcha { get; set; } = false;
        public bool StayConnected { get; set; } = false;
        public bool TrayWhenMinimize { get; set; } = false;
        public bool EnableLoginDelay { get; set; } = false;
        public decimal LoginDelay { get; set; } = 3;
        public bool EnableWaitAfterDC { get; set; } = false;
        public decimal WaitAfterDC { get; set; } = 3;
        public bool HideOnStartClient { get; set; } = false;
        public bool CharacterAutoSelect { get; set; } = false;
        public bool CharacterAutoSelectFirst { get; set; } = true;
        public bool CharacterAutoSelectHigher { get; set; } = false;
        public bool AutoHidePendingWindow { get; set; } = false;
        public bool PendingEnableQueueLogs { get; set; } = false;
        public bool EnableQueueNotification { get; set; } = false;
        public decimal QueueLeft { get; set; } = 30;
    }
}
