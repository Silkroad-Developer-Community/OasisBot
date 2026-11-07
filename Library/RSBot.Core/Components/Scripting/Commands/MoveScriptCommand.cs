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
    private static bool _farReported;

    /// <summary>
    ///     True once the "too far" reaction has been applied. A returned character starts the same walk script
    ///     again from town, so without this the reaction would run on every restart and turn into a loop of
    ///     scroll uses or bot stops. It is cleared as soon as a waypoint is reached again.
    /// </summary>
    private static bool _returnAttempted;

    /// <summary>
    ///     Outcome of a single move attempt.
    /// </summary>
    private enum MoveResult
    {
        /// <summary>The waypoint was reached, or the next one may be requested.</summary>
        Ok,

        /// <summary>The attempt failed, but retrying makes sense.</summary>
        Retry,

        /// <summary>The attempt failed and retrying cannot help (unreachable target, invalid line).</summary>
        Abort,
    }

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
    ///     A failed move means the character cannot reach the waypoint, so the rest of the walk script
    ///     must not be executed line by line.
    /// </summary>
    public bool AbortsScriptOnFailure => true;

    /// <summary>
    ///     An unreachable waypoint only counts as an error when the bot had to be stopped. When the character
    ///     was returned to town by a scroll the run is finished cleanly, so the botbase keeps working and
    ///     continues from the town.
    /// </summary>
    public bool FailureIsError { get; private set; } = true;

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
            FailureIsError = true;

            while (Game.Player.InAction)
                Thread.Sleep(100);

            const int retryAttempts = 5;
            var stepRetryCounter = 0;

            while (true)
            {
                var result = ExecuteMove(arguments);

                if (result == MoveResult.Ok)
                {
                    _farReported = false;
                    _returnAttempted = false;

                    return true;
                }

                // An abort also covers an external Stop(): the command is no longer busy in that case.
                if (result == MoveResult.Abort || !IsBusy)
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
    private MoveResult ExecuteMove(IReadOnlyList<string> arguments)
    {
        if (
            !float.TryParse(arguments[0], out var xOffset)
            || !float.TryParse(arguments[1], out var yOffset)
            || !float.TryParse(arguments[2], out var zOffset)
            || !byte.TryParse(arguments[3], out var xSector)
            || !byte.TryParse(arguments[4], out var ySector)
        )
        {
            return MoveResult.Abort; //Invalid format
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
            // Retrying the same unreachable waypoint cannot succeed, and continuing with the next line
            // would only cascade the same failure through the whole script.
            // A successful return by scroll keeps the bot running and only ends the walk script, so the
            // botbase can pick the character up in town; otherwise the bot is stopped.
            FailureIsError = !HandleTooFar(distance, pos);

            return MoveResult.Abort;
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

            return Game.Player.MoveTo(previousPosition) ? MoveResult.Ok : MoveResult.Retry;
        }

        if (!posResult)
            return MoveResult.Retry;

        return !seamless || AwaitChainPoint(pos) ? MoveResult.Ok : MoveResult.Abort;
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

    /// <summary>
    ///     Reports an unreachable waypoint once and applies the configured reaction: use a return scroll or
    ///     stop the bot. Repeating the report on every script restart would only spam the log with the same
    ///     condition, so the warning is emitted once per "too far" episode.
    /// </summary>
    /// <returns><c>true</c> when the character was returned to town by a scroll; otherwise <c>false</c>.</returns>
    private static bool HandleTooFar(double distance, Position target)
    {
        // The character was already returned to town (or the bot was stopped) for this episode. The walk
        // script keeps starting from its first waypoint, which is still out of reach, so reacting again
        // would only repeat the same scroll use. The script is finished quietly instead.
        if (_returnAttempted)
        {
            Log.Debug(
                "[Script] The walk script is still out of reach and the reaction has already been applied; "
                    + "waiting for the character to get back in range."
            );

            return true;
        }

        var useScroll = PlayerConfig.Get<bool>("RSBot.Training.checkBoxReturnToTownWhenFar", false);

        if (!_farReported)
        {
            _farReported = true;

            Log.Warn(
                string.Format(
                    Lang(
                        "ScriptWaypointTooFar",
                        "The character is {0} units away from the waypoint {1}({2},{3}); the limit is 100."
                    ),
                    Math.Round(distance, 1),
                    target.Region,
                    target.Region.X,
                    target.Region.Y
                )
            );
        }
        else
        {
            Log.Debug(
                $"[Script] The character is still too far from the waypoint ({Math.Round(distance, 1)} units)."
            );
        }

        if (!useScroll)
        {
            Log.Notify(Lang("ScriptStoppingBotOutOfReach", "Stopping the bot: the walk script is out of reach."));

            Kernel.Bot.Stop();

            return false;
        }

        var scroll = FindReturnScroll();

        if (scroll == null)
        {
            LogScrollCandidates();

            _returnAttempted = true;

            Log.Warn(Lang("ScriptNoScrollStopping", "No usable return scroll found; stopping the bot."));

            Kernel.Bot.Stop();

            return false;
        }

        _returnAttempted = true;

        var positionBefore = Game.Player.Position;

        // A plain return scroll is used without a target; UseTo is only a fallback for the clients where
        // the scroll opens a destination menu. The reply of the client is not reliable: some clients cast
        // the scroll and answer with a standard action packet, so the real check is that the character moved.
        if (scroll.Use() || scroll.UseTo(3) || WaitForScrollReturn(positionBefore))
        {
            Log.Notify(
                Lang("ScriptReturningByScroll", "Returning to town by scroll: the walk script is out of reach.")
            );

            return true;
        }

        Log.Warn(Lang("ScriptScrollFailed", "The return scroll could not be used."));

        Kernel.Bot.Stop();

        return false;
    }

    /// <summary>
    ///     Waits until the character has actually been moved by a return scroll. The client does not always
    ///     answer the use request, but the position changes as soon as the scroll is applied.
    /// </summary>
    /// <param name="before">The position taken right before the scroll was used.</param>
    /// <returns><c>true</c> when the character moved; otherwise <c>false</c>.</returns>
    private static bool WaitForScrollReturn(Position before)
    {
        const int timeout = 5000;

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeout)
        {
            if (!Kernel.Bot.Running)
                return false;

            var now = Game.Player.Position;

            if (now.Region.Id != before.Region.Id || now.DistanceTo(before) > 5f)
                return true;

            Thread.Sleep(ArrivalPollInterval);
        }

        return false;
    }

    /// <summary>
    ///     Resolves a user facing string from the language pack of the training botbase, which hosts the
    ///     settings of this command. The fallback keeps the log readable when the pack is not loaded.
    /// </summary>
    private static string Lang(string key, string fallback)
    {
        return LanguageManager.GetLangBySpecificKey("RSBot.Training", key, fallback);
    }

    /// <summary>
    ///     Finds a return scroll in the player inventory. The exact 3,3,3,3 filter comes first, then the
    ///     items are matched by their code name, description and real name, because item type ids differ
    ///     between clients and regions.
    /// </summary>
    private static InventoryItem FindReturnScroll()
    {
        var scroll = Game.Player.Inventory.GetItem(new TypeIdFilter(3, 3, 3, 3));
        if (scroll != null)
            return scroll;

        return Game.Player.Inventory.GetItem(item =>
        {
            var record = item.Record;
            if (record == null)
                return false;

            var text = string.Concat(
                record.CodeName,
                " ",
                record.Desc1,
                " ",
                record.Desc2,
                " ",
                record.GetRealName()
            );

            return text.IndexOf("RETURN", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("ВОЗВРАТ", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("ВОЗВРАЩ", StringComparison.OrdinalIgnoreCase) >= 0;
        });
    }

    /// <summary>
    ///     Logs the scroll-like items of the inventory, so a missing return scroll can be diagnosed from
    ///     the log without digging into the item database.
    /// </summary>
    private static void LogScrollCandidates()
    {
        foreach (var item in Game.Player.Inventory)
        {
            var record = item.Record;
            if (record == null)
                continue;

            var text = string.Concat(record.CodeName, " ", record.Desc1, " ", record.GetRealName());
            if (
                text.IndexOf("SCROLL", StringComparison.OrdinalIgnoreCase) < 0
                && text.IndexOf("СВИТОК", StringComparison.OrdinalIgnoreCase) < 0
            )
                continue;

            Log.Debug(
                $"[Script] Scroll candidate: {record.CodeName} ({record.GetRealName()}) "
                    + $"type={record.TypeID1},{record.TypeID2},{record.TypeID3},{record.TypeID4}"
            );
        }
    }

    public void Stop()
    {
        IsBusy = false;
    }

    #endregion Methods
}
