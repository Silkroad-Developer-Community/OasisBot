using RSBot.Core;

namespace RSBot.Training.Bundle.Protection;

internal class ManaRecovery
{
    public static bool Active => PlayerConfig.Profile.Protection.UseSkillMp;
    public static int Value => PlayerConfig.Profile.Protection.MinSkillMpPercent;
    public static uint SkillId => PlayerConfig.Profile.Protection.MpSkillId;
    public static double Current => 100.0 * Game.Player.Mana / Game.Player.MaximumMana;
}
