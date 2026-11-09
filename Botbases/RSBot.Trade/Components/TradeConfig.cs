using System.Collections.Generic;
using System.IO;
using System.Linq;
using RSBot.Core;

namespace RSBot.Trade.Components;

internal static class TradeConfig
{
    public static string TracePlayerName
    {
        get => PlayerConfig.Profile.Trade.TracePlayerName;
        set => PlayerConfig.Profile.Trade.TracePlayerName = value;
    }

    public static bool TracePlayer
    {
        get => PlayerConfig.Profile.Trade.TracePlayer;
        set => PlayerConfig.Profile.Trade.TracePlayer = value;
    }

    public static bool MountTransport
    {
        get => PlayerConfig.Profile.Trade.MountTransport;
        set => PlayerConfig.Profile.Trade.MountTransport = value;
    }

    public static bool UseRouteScripts
    {
        get => PlayerConfig.Profile.Trade.UseRouteScripts;
        set => PlayerConfig.Profile.Trade.UseRouteScripts = value;
    }

    public static int SelectedRouteListIndex
    {
        get => PlayerConfig.Profile.Trade.SelectedRouteListIndex;
        set => PlayerConfig.Profile.Trade.SelectedRouteListIndex = value;
    }

    public static bool RunTownScript
    {
        get => PlayerConfig.Profile.Trade.RunTownScript;
        set => PlayerConfig.Profile.Trade.RunTownScript = value;
    }

    public static bool WaitForHunter
    {
        get => PlayerConfig.Profile.Trade.WaitForHunter;
        set => PlayerConfig.Profile.Trade.WaitForHunter = value;
    }

    public static bool AttackThiefPlayers
    {
        get => PlayerConfig.Profile.Trade.AttackThiefPlayers;
        set => PlayerConfig.Profile.Trade.AttackThiefPlayers = value;
    }

    public static bool AttackThiefNpcs
    {
        get => PlayerConfig.Profile.Trade.AttackThiefNpcs;
        set => PlayerConfig.Profile.Trade.AttackThiefNpcs = value;
    }

    public static bool CastBuffs
    {
        get => PlayerConfig.Profile.Trade.CastBuffs;
        set => PlayerConfig.Profile.Trade.CastBuffs = value;
    }

    public static bool CounterAttack
    {
        get => PlayerConfig.Profile.Trade.CounterAttack;
        set => PlayerConfig.Profile.Trade.CounterAttack = value;
    }

    public static bool ProtectTransport
    {
        get => PlayerConfig.Profile.Trade.ProtectTransport;
        set => PlayerConfig.Profile.Trade.ProtectTransport = value;
    }

    public static bool BuyGoods
    {
        get => PlayerConfig.Profile.Trade.BuyGoods;
        set => PlayerConfig.Profile.Trade.BuyGoods = value;
    }

    public static bool SellGoods
    {
        get => PlayerConfig.Profile.Trade.SellGoods;
        set => PlayerConfig.Profile.Trade.SellGoods = value;
    }

    public static int BuyGoodsQuantity
    {
        get => PlayerConfig.Profile.Trade.BuyGoodsQuantity;
        set => PlayerConfig.Profile.Trade.BuyGoodsQuantity = value;
    }

    public static int MaxTransportDistance
    {
        get => PlayerConfig.Profile.Trade.MaxTransportDistance;
        set => PlayerConfig.Profile.Trade.MaxTransportDistance = value;
    }

    public static List<string> RouteScriptList
    {
        get
        {
            var result = PlayerConfig.Profile.Trade.RouteScriptList ?? new List<string>();

            if (!result.Contains("Default"))
                result.Add("Default");

            return result;
        }
        set => PlayerConfig.Profile.Trade.RouteScriptList = value;
    }

    public static Dictionary<string, List<string>> RouteScripts
    {
        get
        {
            var result = PlayerConfig.Profile.Trade.RouteScripts ?? new Dictionary<string, List<string>>(16);

            return result;
        }
        set
        {
            foreach (var scriptList in value)
                PlayerConfig.Profile.Trade.RouteScripts[scriptList.Key] = scriptList.Value;

            RouteScriptList = value.Keys.ToList();
        }
    }
}
