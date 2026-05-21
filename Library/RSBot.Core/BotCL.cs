using System.Linq;
using RSBot.Core.Components;

namespace RSBot.Core
{
    public class BotCL
    {
        public static void Initialize(string profile)
        {
            ProfileManager.SetSelectedProfile(profile);
            GlobalConfig.Load();
            Kernel.Initialize();
            Game.Initialize();
            Game.InitializeArchiveFiles();
            Game.ReferenceManager.Load();
            Kernel.PluginManager.LoadAssemblies(true);
            Kernel.BotbaseManager.LoadAssemblies(true);

            var botName = GlobalConfig.Get("RSBot.BotName", "RSBot.Training");
            var selectedBotbase = Kernel.BotbaseManager.Bots.FirstOrDefault(bot => bot.Value.Name == botName);
            if (selectedBotbase.Value != null)
            {
                Kernel.Bot.SetBotbase(selectedBotbase.Value);
            }
            else
            {
                var fallback = Kernel.BotbaseManager.Bots.FirstOrDefault();
                if (fallback.Value != null)
                    Kernel.Bot.SetBotbase(fallback.Value);
            }

            LoadExtensions();
        }
        public static void LoadExtensions()
        {
            foreach (var plugin in Kernel.PluginManager.Extensions.Values)
                plugin.Initialize();
        }
    }
}
