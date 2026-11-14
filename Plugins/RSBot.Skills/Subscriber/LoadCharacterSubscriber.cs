using RSBot.Core.Event;

namespace RSBot.Skills.Subscriber;

internal static class LoadCharacterSubscriber
{
    public static void SubscribeEvents()
    {
        EventManager.SubscribeEvent("OnLoadCharacter", LoadSkillSettings);
    }

    private static void LoadSkillSettings()
    {
        SkillsManager.ApplyAttackSkills();
        SkillsManager.ApplyBuffSkills();
    }
}
