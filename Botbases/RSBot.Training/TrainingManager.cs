using System.Timers;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Objects;
using RSBot.Training.Bundle;

namespace RSBot.Training
{
    public class TrainingManager
    {
        private readonly Timer _pickPetTimer = new(200);

        public TrainingManager()
        {
            _pickPetTimer.Elapsed += OnPickPetTimerElapsed;
            _pickPetTimer.AutoReset = true;
            _pickPetTimer.Start();
        }

        public static void ApplyTrainingArea(float x, float y, ushort region)
        {
            Position pos = new(x, y, region);

            PlayerConfig.Set("RSBot.Area.Region", pos.Region.Id);
            PlayerConfig.Set("RSBot.Area.X", pos.XOffset);
            PlayerConfig.Set("RSBot.Area.Y", pos.YOffset);
            PlayerConfig.Set("RSBot.Area.Z", pos.ZOffset);

            Log.Notify(
                "[Training area] New training area coordinates set. "
                    + $"X: {pos.XOffset}, Y: {pos.YOffset}, Z: {pos.ZOffset}, Region: {pos.Region}"
            );
            EventManager.FireEvent("OnSetTrainingArea");
        }

        public static void SetWalkingScript(string script)
        {
            PlayerConfig.Set("RSBot.Walkback.File", script);
            Log.Notify($"[Training area] New walking script set: {script}");
        }

        private void OnPickPetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (Kernel.Bot.Running || !Game.Ready)
                return;
            if (
                Bundles.Loot.Config.UseAbilityPet
                && Game.Player.HasActiveAbilityPet
                && !PickupManager.RunningAbilityPetPickup
            )
                PickupManager.RunAbilityPet(Game.Player.Position);
        }
    }
}
