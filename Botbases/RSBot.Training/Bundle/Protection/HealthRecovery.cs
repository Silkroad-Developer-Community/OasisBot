using RSBot.Core;

namespace RSBot.Training.Bundle.Protection;

internal class HealthRecovery
{
    public static bool Active => PlayerConfig.Profile.Protection.UseSkillHp;
    public static int Value => PlayerConfig.Profile.Protection.MinSkillHpPercent;
    public static uint SkillId => PlayerConfig.Profile.Protection.HpSkillId;
    public static double Current => 100.0 * Game.Player.Health / Game.Player.MaximumHealth;
}
