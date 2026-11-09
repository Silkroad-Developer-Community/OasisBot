using RSBot.Core;
using RSBot.Core.Objects;

namespace RSBot.Training.Bundle.Protection;

internal class BadStateRecovery
{
    public static bool Active => PlayerConfig.Profile.Protection.UseBadStatusSkill;
    public static uint SkillId => PlayerConfig.Profile.Protection.BadStatusSkillId;
    public static uint SkillIdForUniversall => PlayerConfig.Profile.Protection.BadStatusSkillId;
    public static uint SkillIdForPurification => PlayerConfig.Profile.Protection.BadStatusSkillId;
    public static bool IsInBadStatus => IsUniversall || IsPurification;

    public static bool IsUniversall => (Game.Player.BadEffect & BadEffectAll.UniversallPillEffects) != 0;
    public static bool IsPurification => (Game.Player.BadEffect & BadEffectAll.PurificationPillEffects) != 0;
}
