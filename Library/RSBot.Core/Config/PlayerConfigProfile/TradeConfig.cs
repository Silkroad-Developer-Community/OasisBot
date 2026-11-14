using System.Collections.Generic;

namespace RSBot.Core.Config.PlayerConfigProfile
{
    public class TradeConfig
    {
        public string TracePlayerName { get; set; } = string.Empty;
        public bool TracePlayer { get; set; } = false;
        public bool MountTransport { get; set; } = false;
        public bool UseRouteScripts { get; set; } = true;
        public int SelectedRouteListIndex { get; set; } = 0;
        public bool RunTownScript { get; set; } = false;
        public bool WaitForHunter { get; set; } = false;
        public bool AttackThiefPlayers { get; set; } = false;
        public bool AttackThiefNpcs { get; set; } = false;
        public bool CastBuffs { get; set; } = false;
        public bool CounterAttack { get; set; } = false;
        public bool ProtectTransport { get; set; } = false;
        public bool BuyGoods { get; set; } = true;
        public bool SellGoods { get; set; } = true;
        public int BuyGoodsQuantity { get; set; } = 0;
        public int MaxTransportDistance { get; set; } = 15;
        public List<string> RouteScriptList { get; set; } = [];
        public Dictionary<string, List<string>> RouteScripts { get; set; } = [];
    }
}
