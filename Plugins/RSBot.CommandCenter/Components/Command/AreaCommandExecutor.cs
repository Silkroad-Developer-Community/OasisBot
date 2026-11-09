using RSBot.Core;
using RSBot.Core.Components.Command;
using RSBot.Core.Event;

namespace RSBot.CommandCenter.Components.Command;

internal class AreaCommandExecutor : ICommandExecutor
{
    public string CommandName => "area";

    public string CommandDescription => "Set the training area";

    public bool Execute(bool silent)
    {
        if (!silent)
            Game.ShowNotification(
                $"[RSBot] Setting training area to X={Game.Player.Position.X:0.00} Y={Game.Player.Position.Y:0.00} R=50"
            );

        var activeArea = PlayerConfig.Profile.Training.Areas.Find(a => a.IsActive);
        if (activeArea == null)        
            return false;

        activeArea.X = Game.Player.Position.XOffset;
        activeArea.Y = Game.Player.Position.YOffset;
        activeArea.Z = Game.Player.Position.ZOffset;
        activeArea.Region = Game.Player.Position.Region.Id;
        activeArea.Radius = 50;
        
        PlayerConfig.Save();

        EventManager.FireEvent("OnSetTrainingArea");

        return true;
    }
}
