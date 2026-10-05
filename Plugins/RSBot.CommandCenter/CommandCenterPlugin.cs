using RSBot.Core;
using RSBot.Core.Plugins;

namespace RSBot.CommandCenter;

public class CommandCenterPlugin : IPlugin
{
    public string InternalName => "RSBot.CommandCenter";
    public static CommandCenterPlugin Instance { get; private set; }
    public CommandCenterManager Manager { get; private set; }

    public void Initialize()
    {
        Instance = this;
        Manager = new CommandCenterManager();
        Log.Notify("[Command Center] Plugin initialized!");
    }

    public void OnLoadCharacter()
    {
    }
}
