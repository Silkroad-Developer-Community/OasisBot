using System.IO;
using System.Threading;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Objects;
using RSBot.Training.Bot;

namespace RSBot.Training.Bundle.Loop;

internal class LoopBundle : IBundle
{
    private System.DateTime _lastAutopathAttempt = System.DateTime.MinValue;

    /// <summary>
    ///     The last time the walk script was started. A walk script that cannot reach its first waypoint
    ///     fails immediately, so starting it again on every tick would flood the log without moving the
    ///     character.
    /// </summary>
    private System.DateTime _lastWalkbackAttempt = System.DateTime.MinValue;

    /// <summary>
    ///     Minimum time between two starts of the walk script.
    /// </summary>
    private static readonly System.TimeSpan WalkbackCooldown = System.TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Gets the configuration.
    /// </summary>
    /// <value>
    ///     The configuration.
    /// </value>
    public LoopConfig Config { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether this <see cref="LoopBundle" /> is running.
    /// </summary>
    /// <value>
    ///     <c>true</c> if running; otherwise, <c>false</c>.
    /// </value>
    public bool Running { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether [townscript running].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [townscript running]; otherwise, <c>false</c>.
    /// </value>
    public bool TownscriptRunning { get; private set; }

    /// <summary>
    ///     Invokes this instance.
    /// </summary>
    public void Invoke()
    {
        if (!Running)
            return;

        if (Config.UseVehicle && !Game.Player.HasActiveVehicle && !Game.Player.IsInDungeon)
        {
            Game.Player.SummonVehicle();

            //Wait for the vehicle to spawn
            Thread.Sleep(1000);
        }

        //We don't need to use buffs in town...
        if (Config.CastBuffs && !TownscriptRunning)
            Bundles.Buff.Invoke();
    }

    /// <summary>
    ///     Refreshes this instance.
    /// </summary>
    public void Refresh()
    {
        Config = new LoopConfig
        {
            WalkScript = PlayerConfig.Get<string>("RSBot.Walkback.File"),
            UseSpeedDrug = PlayerConfig.Get<bool>("RSBot.Training.checkUseSpeedDrug", true),
            UseVehicle = PlayerConfig.Get<bool>("RSBot.Training.checkUseMount", true),
            CastBuffs = PlayerConfig.Get<bool>("RSBot.Training.checkCastBuffs", true),
            UseReverse = PlayerConfig.Get<bool>("RSBot.Training.checkBoxUseReverse", false),
        };
    }

    public void Stop()
    {
        if (ScriptManager.Running)
            ScriptManager.Stop();

        if (ShoppingManager.Running)
            ShoppingManager.Stop();

        Running = false;
        _lastAutopathAttempt = System.DateTime.MinValue;
    }

    /// <summary>
    ///     Starts this instance.
    /// </summary>
    public void Start()
    {
        Running = true;

        Refresh();
        CheckForTownScript();

        Running = false;
    }

    /// <summary>
    ///     Checks for town script.
    /// </summary>
    public void CheckForTownScript()
    {
        if (ScriptManager.Running)
            return;

        var filename = Path.Combine(
            ScriptManager.InitialDirectory,
            "Towns",
            Game.Player.Movement.Source.Region + ".rbs"
        );

        //The player is in town, therefore, we need to run the town script first.
        if (!File.Exists(filename))
        {
            // The character was just returned to town, but the client has not reported the new region yet.
            // Starting the walk script now would fail instantly and repeat the whole reaction.
            if (ScriptManager.ReturnPending)
            {
                Log.Debug(
                    "[Training] The character is on its way back; waiting for the town script before walking back."
                );

                return;
            }

            CheckForWalkbackScript();
            return;
        }

        if (PlayerConfig.Get<bool>("RSBot.Protection.checkStopBotOnReturnToTown"))
        {
            Kernel.Bot.Stop();
            return;
        }

        Log.NotifyLang("LoadingTownScript", filename);

        TownscriptRunning = true;

        ScriptManager.Load(filename);
        ScriptManager.RunScript(false);

        // The character has been processed by the town script, so the walk script may start again even
        // when the previous run was aborted as out of reach.
        ScriptManager.ClearReturnPending();

        if (Running && Config.UseReverse)
        {
            var filter = new TypeIdFilter(3, 3, 3, 3);
            var item = Game.Player.Inventory.GetItem(filter);
            if (item != null)
                /*
                    2 => go to last recall point
                    3 => go to last died position
                    7 => select position on map

                    error codes:
                        85: Cannot find the place where you selected as recall point.
                        86: Cannot find the place where you died.
                 */
                if (item.UseTo(3))
                {
                    TownscriptRunning = false;
                    return;
                }
        }

        TownscriptRunning = false;

        Invoke();

        CheckForWalkbackScript(true);
    }

    /// <summary>
    ///     Checks for walkback script.
    /// </summary>
    public void CheckForWalkbackScript(bool startFromTown = false)
    {
        if (ScriptManager.Running || !Kernel.Bot.Running)
            return;

        if (Config.WalkScript == null || !File.Exists(Config.WalkScript))
        {
            if (Container.Bot.Area.Position.DistanceToPlayer() <= Container.Bot.Area.Radius + 50)
                return;

            if (System.DateTime.UtcNow - _lastAutopathAttempt < System.TimeSpan.FromSeconds(30))
                return;

            _lastAutopathAttempt = System.DateTime.UtcNow;

            Log.Notify("No walkback script found. Attempting to generate a dynamic path...");
            if (NavigationManager.CalculatePathToTrainingArea())
            {
                var dynamicScript = NavigationManager.GenerateRBSFile();
                if (dynamicScript != null)
                {
                    Config.WalkScript = dynamicScript;
                }
            }
        }

        if (Config.WalkScript == null || !File.Exists(Config.WalkScript))
            return;

        // A walk script that cannot reach its first waypoint fails within a second, and the bundle is
        // invoked on every tick. Without the cooldown the same script would be loaded over and over.
        if (System.DateTime.UtcNow - _lastWalkbackAttempt < WalkbackCooldown)
            return;

        _lastWalkbackAttempt = System.DateTime.UtcNow;

        Invoke();
        Log.NotifyLang("LoadingWalkScript", Config.WalkScript);

        ScriptManager.Load(Config.WalkScript);
        ScriptManager.RunScript(!startFromTown);
    }
}
