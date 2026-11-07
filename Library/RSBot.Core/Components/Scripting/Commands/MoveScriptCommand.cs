using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RSBot.Core.Objects;

namespace RSBot.Core.Components.Scripting.Commands;

internal class MoveScriptCommand : IScriptCommand
{
    /// <summary>
    ///     Poll interval while waiting for the look-ahead point of the next waypoint.
    /// </summary>
    private const int ArrivalPollInterval = 10;

    /// <summary>
    ///     Minimum time between two move requests. Prevents packet flooding on densely recorded paths.
    /// </summary>
    private const int MoveRequestThrottle = 120;

    /// <summary>
    ///     Mount, fellow and speed drug checks run at most this often instead of on every waypoint.
    /// </summary>
    private const int SupportThrottle = 5000;

    private static int _lastMoveRequestTick;
    private static int _lastSupportTick;

    #region Properties

    /// <summary>
    ///     Gets the name.
    /// </summary>
    /// <value>
    ///     The name.
    /// </value>
    public string Name => "move";

    /// <summary>
    ///     Gets a value indicating whether this instance is running.
    /// </summary>
    /// <value>
    ///     <c>true</c> if this instance is running; otherwise, <c>false</c>.
    /// </value>
    public bool IsBusy { get; private set; }

    /// <summary>
    ///     Gets or sets flag if you must dismount for teleporting.
    /// </summary>
    public static bool MustDismount { get; set; }

    /// <summary>
    ///     Gets the arguments.
    /// </summary>
    /// <value>
    ///     The arguments.
    /// </value>
    public Dictionary<string, string> Arguments =>
        new()
        {
            { "XSector", "The X sector of the region" },
            { "YSector", "The Y sector of the region" },
            { "XOffset", "The X offset inside the region" },
            { "YOffset", "The Y offset inside the region" },
            { "ZOffset", "The Z offset inside the region" },
        };

    #endregion Properties

    #region Methods

    /// <summary>
    ///     Executes this instance.
    /// </summary>
    /// <param name="arguments"></param>
    /// <returns>
    ///     A value indicating if the command has been executed successfully.
    /// </returns>
    public bool Execute(string[] arguments = null)
    {
        if (arguments == null || (arguments.Length != Arguments.Count && arguments.Length != Arguments.Count + 1))
        {
            Log.Warn("[Script] Invalid move command: Position information missing / invalid format.");

            return false;
        }

        if (IsBusy)
            return false;

        try
        {
            IsBusy = true;

            while (Game.Player.InAction)
                Thread.Sleep(100);

            const int retryAttempts = 5;
            var stepRetryCounter = 0;

            while (!ExecuteMove(arguments))
            {
                if (!IsBusy)
                    return false;

                if (stepRetryCounter++ >= retryAttempts)
                {
                    Log.Warn(
                        "[Script] The move command failed due to an unknown reason! Please check the walk script."
                    );

                    return false;
                }

                Log.Debug($"[Script] Retry this step {stepRetryCounter}/{retryAttempts}...");

                //Wait until executing the next step so the server can get its shit done so most likely the next step will not fail.
                Thread.Sleep(1000);
            }

            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    ///     Executes the movement.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    private bool ExecuteMove(IReadOnlyList<string> arguments)
    {
        if (
            !float.TryParse(arguments[0], out var xOffset)
            || !float.TryParse(arguments[1], out var yOffset)
            || !float.TryParse(arguments[2], out var zOffset)
            || !byte.TryParse(arguments[3], out var xSector)
            || !byte.TryParse(arguments[4], out var ySector)
        )
        {
            IsBusy = false;

            return false; //Invalid format
        }

        Position previousPosition = Game.Player.Position;
        Position pos = new(xSector, ySector, xOffset, yOffset, zOffset);

        var mode = arguments.Count > Arguments.Count ? arguments[Arguments.Count].ToLowerInvariant() : null;
        var forceStop = mode == "stop";
        var forceSeamless = mode == "seamless" || mode == "chain";

        // Mount / fellow / speed drug handling is expensive and spawns extra packets, so it runs at
        // most once per SupportThrottle instead of on every single waypoint.
        if (!forceStop && Kernel.TickCount - _lastSupportTick >= SupportThrottle)
        {
            _lastSupportTick = Kernel.TickCount;

            if (PlayerConfig.Get("RSBot.Training.checkUseSpeedDrug", true))
            {
                if (
                    !Game.Player.HasActiveVehicle
                    && !Game.Player.InAction
                    && Game.Player.State.ActiveBuffs.FindIndex(p => p.Record.Params.Contains(1752396901)) < 0
                )
                {
                    var item = Game.Player.Inventory.GetItem(
                        new TypeIdFilter(3, 3, 13, 1),
                        p => p.Record.Desc1.Contains("_SPEED_")
                    );
                    item?.Use();
                }
            }

            if (PlayerConfig.Get("RSBot.Training.checkUseMount", true))
            {
                if (
                    !Game.Player.HasActiveVehicle
                    && !Game.Player.InAction
                    && !(
                        ScriptManager.Running
                        && ScriptManager.File == PlayerConfig.Get("RSBot.Lure.SelectedScriptPath", string.Empty)
                    )
                )
                {
                    Game.Player.SummonFellow();

                    var fellow = Game.Player.Fellow;

                    if (fellow != null)
                    {
                        fellow.CastSkill("P2SKILL_SPECIAL_SP_GET_A"); //SP Recall

                        double distanceToFellow = previousPosition.DistanceTo(fellow.Position);
                        if (distanceToFellow <= 5.0)
                        {
                            fellow.Mount();
                        }
                        else
                            Log.Debug($"Can't mount fellow pet because it's {distanceToFellow}m away");
                    }
                }

                if (!Game.Player.HasActiveVehicle && !Game.Player.IsInDungeon && !Game.Player.InAction)
                    Game.Player.SummonVehicle();
            }
        }

        //Check if the new position is nearby a cave entrance.
        //If so dismount the vehicle
        //TODO: Find out how to get the ingame positions of ground teleporters like dw cave...

        var distance = pos.DistanceTo(previousPosition);
        if (distance > 100)
        {
            Log.Warn("[Script] Target position too far away, bot logic aborted!");

            IsBusy = false;
            return false;
        }

        // A waypoint chain must not wait for the full travel time: the next waypoint is requested while
        // the character is still walking, so it never stops between two move lines.
        var seamless =
            !forceStop
            && (
                forceSeamless
                || (
                    PlayerConfig.Get<bool>("RSBot.Script.SeamlessWalk", true)
                    && ScriptManager.HasNextMoveCommand()
                )
            );

        ThrottleMoveRequests();

        Log.Debug(
            $"[Script] Move to position {pos.Region}({pos.Region.X},{pos.Region.Y}) X={pos.X}, Y={pos.Y}"
                + (seamless ? " (chained)" : string.Empty)
        );

        var posResult = Game.Player.MoveTo(pos, !seamless);
        _lastMoveRequestTick = Kernel.TickCount;

        if (MustDismount)
        {
            MustDismount = false;
            Game.Player.Vehicle.Dismount();

            return Game.Player.MoveTo(previousPosition);
        }

        if (!posResult)
            return false;

        return !seamless || AwaitChainPoint(pos);
    }

    /// <summary>
    ///     Keeps the rate of move requests below the server friendly threshold, otherwise densely
    ///     recorded paths would flood the server and the client would reject or rubber-band the steps.
    /// </summary>
    private static void ThrottleMoveRequests()
    {
        if (_lastMoveRequestTick == 0)
            return;

        var elapsed = Kernel.TickCount - _lastMoveRequestTick;
        if (elapsed < MoveRequestThrottle)
            Thread.Sleep(MoveRequestThrottle - elapsed);
    }

    /// <summary>
    ///     Waits until the remaining walk distance is covered by the look-ahead time, so the next
    ///     waypoint can be requested before the character reaches the current one.
    /// </summary>
    /// <returns><c>true</c> when the next waypoint may be requested; otherwise <c>false</c>.</returns>
    private bool AwaitChainPoint(Position target)
    {
        var speed = Math.Max(Game.Player.ActualSpeed * 0.1f, 1f);
        var lookAhead = Math.Max(speed * 0.2f, 2f);
        var timeout = (int)(target.DistanceToPlayer() / speed * 1000f) + 3000;

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeout)
        {
            if (!IsBusy)
                return false;

            var remaining = Game.Player.Movement.Source.DistanceTo(target);
            if (remaining <= lookAhead)
                return true;

            // The server already reported the stop, i.e. we are standing on (or next to) the waypoint.
            if (!Game.Player.Movement.Moving && remaining <= Math.Max(lookAhead * 2f, 5f))
                return true;

            Thread.Sleep(ArrivalPollInterval);
        }

        Log.Debug("[Script] Look-ahead timeout reached, requesting the next waypoint anyway.");

        return true;
    }

    public void Stop()
    {
        IsBusy = false;
    }

    #endregion Methods
}
