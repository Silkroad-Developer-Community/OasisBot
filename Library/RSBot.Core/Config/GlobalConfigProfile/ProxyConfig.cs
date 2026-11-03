namespace RSBot.Core.GlobalConfigProfile
{
    public class ProxyConfig
    {
        public bool IsActive { get; set; } = false;
        public string Ip { get; set; } = string.Empty;
        public int Port { get; set; } = 0;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public byte Version { get; set; } = 4;
    }
}
