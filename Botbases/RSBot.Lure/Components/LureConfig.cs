using RSBot.Core;
using RSBot.Core.Client.ReferenceObjects;
using RSBot.Core.Objects;

namespace RSBot.Lure.Components;

internal static class LureConfig
{
    public static bool UseSpeedDrug
    {
        get => PlayerConfig.Profile.Lure.UseSpeedDrug;
        set => PlayerConfig.Profile.Lure.UseSpeedDrug = value;
    }

    public static bool UseHowlingShout
    {
        get => PlayerConfig.Profile.Lure.UseHowlingShout && Game.Player.Race == ObjectCountry.Europe;
        set => PlayerConfig.Profile.Lure.UseHowlingShout = value;
    }

    public static bool UseNormalAttack
    {
        get => PlayerConfig.Profile.Lure.UseNormalAttack;
        set => PlayerConfig.Profile.Lure.UseNormalAttack = value;
    }

    public static bool StopIfNumPartyMemberDead
    {
        get => PlayerConfig.Profile.Lure.StopIfNumPartyMemberDead;
        set => PlayerConfig.Profile.Lure.StopIfNumPartyMemberDead = value;
    }

    public static int NumPartyMemberDead
    {
        get => PlayerConfig.Profile.Lure.NumPartyMemberDead;
        set => PlayerConfig.Profile.Lure.NumPartyMemberDead = value;
    }

    public static bool StopIfNumPartyMember
    {
        get => PlayerConfig.Profile.Lure.StopIfNumPartyMember;
        set => PlayerConfig.Profile.Lure.StopIfNumPartyMember = value;
    }

    public static int NumPartyMember
    {
        get => PlayerConfig.Profile.Lure.NumPartyMember;
        set => PlayerConfig.Profile.Lure.NumPartyMember = value;
    }

    public static bool StopIfNumMonsterType
    {
        get => PlayerConfig.Profile.Lure.StopIfNumMonsterType;
        set => PlayerConfig.Profile.Lure.StopIfNumMonsterType = value;
    }

    public static MonsterRarity SelectedMonsterType
    {
        get => PlayerConfig.Profile.Lure.SelectedMonsterType;
        set => PlayerConfig.Profile.Lure.SelectedMonsterType = value;
    }

    public static int NumMonsterType
    {
        get => PlayerConfig.Profile.Lure.NumMonsterType;
        set => PlayerConfig.Profile.Lure.NumMonsterType = value;
    }

    public static bool StopIfNumPartyMembersOnSpot
    {
        get => PlayerConfig.Profile.Lure.StopIfNumPartyMembersOnSpot;
        set => PlayerConfig.Profile.Lure.StopIfNumPartyMembersOnSpot = value;
    }

    public static int NumPartyMembersOnSpot
    {
        get => PlayerConfig.Profile.Lure.NumPartyMembersOnSpot;
        set => PlayerConfig.Profile.Lure.NumPartyMembersOnSpot = value;
    }

    public static bool UseScript
    {
        get => PlayerConfig.Profile.Lure.UseScript;
        set => PlayerConfig.Profile.Lure.UseScript = value;
    }

    public static string SelectedScriptPath
    {
        get => PlayerConfig.Profile.Lure.SelectedScriptPath;
        set => PlayerConfig.Profile.Lure.SelectedScriptPath = value;
    }

    public static bool WalkRandomly
    {
        get => PlayerConfig.Profile.Lure.WalkRandomly;
        set => PlayerConfig.Profile.Lure.WalkRandomly = value;
    }

    public static bool StayAtCenter
    {
        get => PlayerConfig.Profile.Lure.StayAtCenter;
        set => PlayerConfig.Profile.Lure.StayAtCenter = value;
    }

    public static bool StayAtCenterFor
    {
        get => PlayerConfig.Profile.Lure.StayAtCenterForN;
        set => PlayerConfig.Profile.Lure.StayAtCenterForN = value;
    }

    public static int StayAtCenterForSeconds
    {
        get => PlayerConfig.Profile.Lure.StayAtCenterForSeconds;
        set => PlayerConfig.Profile.Lure.StayAtCenterForSeconds = value;
    }

    public static bool NoHowlingAtCenter
    {
        get => PlayerConfig.Profile.Lure.NoHowlingAtCenter;
        set => PlayerConfig.Profile.Lure.NoHowlingAtCenter = value;
    }

    public static bool UseAttackingSkills
    {
        get => PlayerConfig.Profile.Lure.UseAttackingSkills;
        set => PlayerConfig.Profile.Lure.UseAttackingSkills = value;
    }

    public static string WalkscriptPath
    {
        get => PlayerConfig.Profile.Lure.WalkbackPath;
        set => PlayerConfig.Profile.Lure.WalkbackPath = value;
    }

    public static Area Area
    {
        get
        {
            var region = PlayerConfig.Profile.Lure.Area.Region;
            var x = PlayerConfig.Profile.Lure.Area.X;
            var y = PlayerConfig.Profile.Lure.Area.Y;
            var z = PlayerConfig.Profile.Lure.Area.Z;
            var r = PlayerConfig.Profile.Lure.Area.Radius;

            return new Area
            {
                Name = "Lure",
                Position = new Position(region, x, y, z),
                Radius = r,
            };
        }
        set
        {
            PlayerConfig.Profile.Lure.Area.Region = value.Position.Region;
            PlayerConfig.Profile.Lure.Area.X = value.Position.XOffset;
            PlayerConfig.Profile.Lure.Area.Y = value.Position.YOffset;
            PlayerConfig.Profile.Lure.Area.Z = value.Position.ZOffset;
            PlayerConfig.Profile.Lure.Area.Radius = value.Radius;
        }
    }
}
