using System.Timers;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Objects;
using RSBot.Core.PlayerConfigProfile;
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

            var currentArea = PlayerConfig.Profile.Training.Areas.Find(a => a.IsActive);

            if (currentArea == null)
            {
                currentArea = new TrainingAreaConfig
                {
                    Name = "Custom Area",
                    Region = pos.Region.Id,
                    X = pos.XOffset,
                    Y = pos.YOffset,
                    Z = pos.ZOffset,
                    IsActive = true
                };

                PlayerConfig.Profile.Training.Areas.Add(currentArea);
            }

            currentArea.Region = pos.Region.Id;
            currentArea.X = pos.XOffset;
            currentArea.Y = pos.YOffset;
            currentArea.Z = pos.ZOffset;
            currentArea.IsActive = true;            

            Log.Notify(
                "[Training area] New training area coordinates set. "
                    + $"X: {pos.XOffset}, Y: {pos.YOffset}, Z: {pos.ZOffset}, Region: {pos.Region}"
            );
            EventManager.FireEvent("OnSetTrainingArea");
        }

        public static void SetWalkingScript(string script)
        {
            PlayerConfig.Profile.Training.WalkbackFile = script;
            PlayerConfig.Save();
            Log.Notify($"[Training area] New walking script set: {script}");
        }

        private void OnPickPetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (Kernel.Bot.Running || !Game.Ready)
                return;
            if (Bundles.Loop.Config == null || Game.Player == null)
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
