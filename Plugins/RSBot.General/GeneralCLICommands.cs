using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Components.Command;
using System;
using System.Threading.Tasks;

namespace RSBot.General;

public class StartClientCommand : ICLICommand
{
    public string Name => "start-client";
    public string Description => "Starts the Silkroad client.";

    public void Execute(string[] args)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await GeneralPlugin.Instance.Manager.StartClientAsync().ConfigureAwait(false);
                Log.Notify("Client started");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to start client: {ex.Message}");
            }
        });
    }
}

public class ShowClientCommand : ICLICommand
{
    public string Name => "show";
    public string Description => "Shows the Silkroad client.";

    public void Execute(string[] args)
    {
        ClientManager.SetVisible(true);
    }
}

public class HideClientCommand : ICLICommand
{
    public string Name => "hide";
    public string Description => "Hides the Silkroad client.";

    public void Execute(string[] args)
    {
        ClientManager.SetVisible(false);
    }
}
