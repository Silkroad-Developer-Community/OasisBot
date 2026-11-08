using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RSBot.Core.Objects;
using RSBot.Core.Objects.Spawn;

namespace RSBot.Core.Components;

public class PickupManager
{
    /// <summary>
    ///     The distance at which the client accepts a pickup command. Items farther away have to be walked to
    ///     first, otherwise the server ignores the request and the item stays on the ground.
    /// </summary>
    private const int PickupRange = 6;

    /// <summary>
    ///     How many items one pass may handle. The whole pass runs on the bot thread and every pickup waits
    ///     for the server answer, so an unbounded pass kept the bot from attacking for seconds.
    /// </summary>
    private const int MaxPickupsPerPass = 6;

    /// <summary>
    ///     How long one pickup pass may take in total. The pass runs on the bot thread, so an unbounded walk
    ///     to a distant item stopped the attack loop; after this budget the rest is left for the next pass.
    /// </summary>
    private const int MaxPickupPassDuration = 2500;

    /// <summary>
    ///     Gets or sets a value indicating whether this <see cref="PickupManager" /> is running.
    /// </summary>
    /// <value>
    ///     <c>true</c> if running; otherwise, <c>false</c>.
    /// </value>
    public static bool RunningPlayerPickup { get; private set; }

    /// <summary>
    ///     Gets or sets a value indicating whether this <see cref="PickupManager" /> is running for AbilityPet.
    /// </summary>
    /// <value>
    ///     <c>true</c> if running; otherwise, <c>false</c>.
    /// </value>
    public static bool RunningAbilityPetPickup { get; private set; }

    /// <summary>
    ///     Gets or sets the pickup items.
    /// </summary>
    /// <value>
    ///     The pickup items.
    /// </value>
    public static List<(string CodeName, bool PickOnlyChar)> PickupFilter { get; } = new();

    /// <summary>
    ///     Gets or sets a value indicating whether [pickup gold].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [pickup gold]; otherwise, <c>false</c>.
    /// </value>
    public static bool PickupGold => PlayerConfig.Get("RSBot.Items.Pickup.Gold", true);

    /// <summary>
    ///     Gets or sets a value indicating whether [pickup rare items].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [pickup rare items]; otherwise, <c>false</c>.
    /// </value>
    public static bool PickupRareItems => PlayerConfig.Get("RSBot.Items.Pickup.Rare", true);

    /// <summary>
    ///     Gets or sets a value indicating whether [pickup rare items].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [pickup rare items]; otherwise, <c>false</c>.
    /// </value>
    public static bool PickupBlueItems => PlayerConfig.Get("RSBot.Items.Pickup.Blue", true);

    /// <summary>
    ///     Gets or sets a value indicating whether [pickup quest items].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [pickup quest items]; otherwise, <c>false</c>.
    /// </value>
    public static bool PickupQuestItems => PlayerConfig.Get("RSBot.Items.Pickup.Quest", true);

    /// <summary>
    ///     Gets or sets a value indicating whether [pickup clean equips].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [pickup clean equips]; otherwise, <c>false</c>.
    /// </value>
    public static bool PickupAnyEquips => PlayerConfig.Get("RSBot.Items.Pickup.AnyEquips", true);

    /// <summary>
    ///     Gets or sets a value indicating whether [pickup everything].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [pickup everything]; otherwise, <c>false</c>.
    /// </value>
    public static bool PickupEverything => PlayerConfig.Get("RSBot.Items.Pickup.Everything", true);

    /// <summary>
    ///     Gets or sets a value indicating whether [use ability pet].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [use ability pet]; otherwise, <c>false</c>.
    /// </value>
    public static bool UseAbilityPet => PlayerConfig.Get("RSBot.Items.Pickup.EnableAbilityPet", true);

    /// <summary>
    ///     Gets or sets a value indicating whether [just pick my items].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [use ability pet]; otherwise, <c>false</c>.
    /// </value>
    public static bool JustPickMyItems => PlayerConfig.Get("RSBot.Items.Pickup.JustPickMyItems", false);

    /// <summary>
    ///     Gets the radius in which the ability pet is sent to collect items. It is independent of the training
    ///     area: the pet follows the character and does not pull the character out of the area, so a larger
    ///     radius only means that more loot is ordered for the pet.
    /// </summary>
    public static int AbilityPetRadius => Math.Clamp(PlayerConfig.Get("RSBot.Items.Pickup.AbilityPetRadius", 100), 5, 250);

    /// <summary>
    ///     Gets a value indicating whether the character picks up items while it is fighting a monster. When
    ///     disabled, pickup waits for the moment between two targets.
    /// </summary>
    public static bool PickupDuringAttack => PlayerConfig.Get("RSBot.Items.Pickup.DuringAttack", false);

    /// <summary>
    ///     Runs the specified center position.
    /// </summary>
    /// <param name="playerPosition">The player position.</param>
    /// <param name="centerPosition">The center position.</param>
    /// <param name="radius">The radius.</param>
    public static void RunPlayer(Position playerPosition, Position centerPosition, int radius = 50)
    {
        if (RunningPlayerPickup)
            return;

        RunningPlayerPickup = true;
        try
        {
            var flag = UseAbilityPet && Game.Player.HasActiveAbilityPet;
            if (
                !SpawnManager.TryGetEntities<SpawnedItem>(
                    i => Condition(i, centerPosition, radius, flag, flag),
                    out var entities
                )
            )
            {
                RunningPlayerPickup = false;
                return;
            }

            var attempts = 0;
            var passDeadline = Kernel.TickCount + MaxPickupPassDuration;

            foreach (
                var item in entities.OrderBy(item =>
                    item.Movement.Source.DistanceTo(playerPosition) /*.Take(5)*/
                )
            )
            {
                if (!RunningPlayerPickup)
                    return;

                if (++attempts > MaxPickupsPerPass)
                    return;

                // The budget is spent: give the bot back to combat and continue on the next pass.
                if (Kernel.TickCount > passDeadline)
                    return;

                while (Game.Player.InAction)
                    Thread.Sleep(50);

                if (item.Record.IsSpecialtyGoodBox && Game.Player.Job2SpecialtyBag.Full)
                    continue;

                // The client only accepts a pickup when the character stands at the item. Walking to it is
                // done here, but never while a monster is being fought: that would pull the character away
                // from the target and the item would be left behind anyway.
                if (item.Movement.Source.DistanceTo(Game.Player.Position) > PickupRange)
                {
                    if (Game.SelectedEntity is SpawnedMonster)
                        continue;

                    // A step without sleeping: Player.MoveTo(..., sleep: true) blocks the bot thread for the
                    // whole walk, which stopped the attack loop for several seconds. The wait below is bounded
                    // by the pass budget instead.
                    if (!Game.Player.MoveTo(item.Movement.Source, false))
                        continue;

                    var deadline = Math.Min(passDeadline, Kernel.TickCount + 3000);
                    while (
                        item.Movement.Source.DistanceTo(Game.Player.Position) > PickupRange
                        && Kernel.TickCount < deadline
                    )
                        Thread.Sleep(50);
                }

                item.Pickup();
            }
        }
        catch (Exception e)
        {
            Log.Fatal(e);
        }
        finally
        {
            RunningPlayerPickup = false;
        }
    }

    public static async void RunAbilityPet(Position centerPosition, int radius = 50)
    {
        if (RunningAbilityPetPickup)
            return;

        RunningAbilityPetPickup = true;

        try
        {
            if (
                !SpawnManager.TryGetEntities<SpawnedItem>(
                    i => Condition(i, centerPosition, radius, true),
                    out var entities
                )
            )
            {
                RunningAbilityPetPickup = false;
                return;
            }

            foreach (
                var item in entities.OrderBy(item => item.Movement.Source.DistanceTo(Game.Player.AbilityPet.Position))
            )
            {
                if (!RunningAbilityPetPickup)
                    return;

                if (item.Record.IsSpecialtyGoodBox && Game.Player.Job2SpecialtyBag.Full)
                    continue;

                await Game.Player.AbilityPet.PickupAsync(item.UniqueId);
                await Task.Yield();
            }
        }
        catch (Exception e)
        {
            Log.Fatal(e);
        }
        finally
        {
            RunningAbilityPetPickup = false;
        }
    }

    private static bool Condition(
        SpawnedItem e,
        Position centerPosition,
        int radius,
        bool applyPickOnlyChar = false,
        bool pickOnlyChar = false
    )
    {
        var playerJid = Game.Player.JID;

        if (JustPickMyItems && e.OwnerJID != playerJid)
            return false;

        // Check if Item is within the training area + tolerance
        const int tolerance = 15;
        if (e.Movement.Source.DistanceTo(centerPosition) > radius + tolerance)
            return false;

        if (applyPickOnlyChar && e.IsBehindObstacle)
            return false;

        bool isItemAutoShareParty = Game.Party.IsInParty && Game.Party.Settings.GetPartyType() is 2 or 3 or 6 or 7;

        if (isItemAutoShareParty && PickupGold && e.Record.IsGold)
        {
            if (!(applyPickOnlyChar && pickOnlyChar))
                return true;
        }

        if (e.HasOwner && e.OwnerJID != playerJid)
        {
            if (!isItemAutoShareParty)
                return false;

            if (e.Record.IsQuest && Game.Party.Members.Any(m => m.MemberId == e.OwnerJID))
                return false;
        }

        if (PickupGold && e.Record.IsGold && !(applyPickOnlyChar && pickOnlyChar))
            return true;

        if (
            (PickupRareItems && (byte)e.Rarity >= 2)
            || (PickupBlueItems && (byte)e.Rarity >= 1)
            || (PickupAnyEquips && e.Record.IsEquip)
            || (PickupQuestItems && e.Record.IsQuest)
            || PickupEverything
        )
            return true;

        return applyPickOnlyChar
            ? PickupFilter.Any(p => p.CodeName == e.Record.CodeName && p.PickOnlyChar == pickOnlyChar)
            : PickupFilter.Any(p => p.CodeName == e.Record.CodeName);
    }

    public static void AddFilter(string codeName, bool pickOnlyChar = false)
    {
        PickupFilter.RemoveAll(p => p.CodeName == codeName);
        PickupFilter.Add((codeName, pickOnlyChar));

        SaveFilter();
    }

    public static void RemoveFilter(string codeName)
    {
        PickupFilter.RemoveAll(p => p.CodeName == codeName);
        SaveFilter();
    }

    public static void LoadFilter()
    {
        var config = PlayerConfig.GetArray<string>("RSBot.Shopping.Pickup");

        foreach (var item in config)
        {
            var split = item.Split('|');
            if (split.Length < 2)
                continue;

            PickupFilter.Add((split[0], Convert.ToBoolean(split[1])));
        }
    }

    public static void SaveFilter()
    {
        var array = PickupFilter.Select(p => $"{p.CodeName}|{p.PickOnlyChar}").ToArray();
        if (array.Length == 0)
            return;

        PlayerConfig.SetArray("RSBot.Shopping.Pickup", array);
    }

    /// <summary>
    ///     Stops this instance.
    /// </summary>
    public static void Stop()
    {
        RunningPlayerPickup = false;
        RunningAbilityPetPickup = false;
    }
}
