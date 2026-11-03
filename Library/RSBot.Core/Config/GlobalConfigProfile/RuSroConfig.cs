namespace RSBot.Core.GlobalConfigProfile
{
    public class RuSroConfig
    {
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
        public string LauncherId { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string TokenOwner { get; set; } = string.Empty;
        public bool EmailCodePending { get; set; } = false;
        public string EmailCodeAddress { get; set; } = string.Empty;
        public string EmailSignInToken { get; set; } = string.Empty;
        public string EmailRequestGroupId { get; set; } = string.Empty;
        public string EmailCodeRequestedAt { get; set; } = string.Empty;
    }
}
