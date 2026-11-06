using System;
using System.Collections.Generic;
using RSBot.Core;
using RSBot.Core.Event;
using RSBot.Core.Objects;

namespace RSBot.Protection.Components.Town;

public class DurabilityLowHandler : AbstractTownHandler
{
    /// <summary>
    ///     Durability threshold at which a repair kit is applied.
    /// </summary>
    private const uint RepairKitDurability = 6;

    /// <summary>
    ///     Durability threshold at which the base return to town is used.
    /// </summary>
    private const uint ReturnToTownDurability = 5;

    /// <summary>
    ///     Slot value that makes the repair kit work for every equipment slot.
    /// </summary>
    private const int AnySlot = 8;

    /// <summary>
    ///     Interval between the equipment scans.
    /// </summary>
    private const int ScanInterval = 1_000;

    /// <summary>
    ///     Time given to the server to apply the repair kit before the result is verified.
    /// </summary>
    private const int RepairVerificationDelay = 5_000;

    /// <summary>
    ///     Interval between the return scroll attempts.
    /// </summary>
    private const int ReturnScrollRetryDelay = 10_000;

    /// <summary>
    ///     The last scan tick count
    /// </summary>
    private static int _lastScanTick = Kernel.TickCount;

    /// <summary>
    ///     The last return scroll tick count
    /// </summary>
    private static int _lastReturnScrollTick = Kernel.TickCount - ReturnScrollRetryDelay;

    /// <summary>
    /// Indicates whether the system is currently busy processing an operation.
    /// </summary>
    /// <remarks>This field is intended for internal use to track the busy state of the system. It should not
    /// be accessed directly outside of the class.</remarks>
    private static bool _isBusy = false;

    /// <summary>
    ///     Tracks the repair kit usage, per equipment slot.
    /// </summary>
    private static readonly Dictionary<byte, RepairKitAttempt> _repairKitAttempts = new();

    /// <summary>
    ///     Initializes this instance.
    /// </summary>
    public static void Initialize()
    {
        SubscribeEvents();
    }

    /// <summary>
    ///     Subscribes the events.
    /// </summary>
    private static void SubscribeEvents()
    {
        EventManager.SubscribeEvent("OnUpdateItemDurability", new Action<byte, uint>(OnUpdateItemDurability));
        EventManager.SubscribeEvent("OnTick", OnTick);
    }

    /// <summary>
    ///     Check the equiped items durabilities
    /// </summary>
    private static void OnTick()
    {
        if (!Kernel.Bot.Running)
            return;

        if (Kernel.TickCount - _lastScanTick < ScanInterval)
            return;

        if (PlayerInTownScriptRegion())
            return;

        if (_isBusy)
            return;

        _isBusy = true;

        try
        {
            _lastScanTick = Kernel.TickCount;

            CheckEquippedItems();
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>
    ///     Applies a repair kit at 6 durability and falls back to the return scroll at 5 or below.
    /// </summary>
    private static void CheckEquippedItems()
    {
        var returnToTownEnabled = PlayerConfig.Get<bool>("RSBot.Protection.checkDurability");
        var repairKitEnabled = PlayerConfig.Get<bool>("RSBot.Protection.checkUseRepairKit");

        if (!returnToTownEnabled && !repairKitEnabled)
            return;

        var repairKitSlot = (int)PlayerConfig.Get<decimal>("RSBot.Protection.numRepairKitSlot", AnySlot);

        for (byte slot = 0; slot < 8; slot++)
        {
            var item = Game.Player.Inventory.GetItemAt(slot);
            if (item == null || !item.Record.IsEquip)
            {
                _repairKitAttempts.Remove(slot);
                continue;
            }

            if (item.Durability > RepairKitDurability)
            {
                _repairKitAttempts.Remove(slot);
                continue;
            }

            var repairKitAllowed = repairKitEnabled && (repairKitSlot >= AnySlot || repairKitSlot == slot);
            var repairKitUsed = _repairKitAttempts.TryGetValue(slot, out var attempt);

            if (repairKitUsed && !attempt.Failed)
            {
                if (Kernel.TickCount - attempt.TickCount < RepairVerificationDelay)
                    continue;

                if (item.Durability > attempt.Durability)
                {
                    _repairKitAttempts.Remove(slot);
                    continue;
                }

                attempt.Failed = true;
            }

            if (repairKitAllowed && !repairKitUsed && item.Durability > ReturnToTownDurability)
            {
                var repairKit = Game.Player.Inventory.GetItem(p => p.Record.IsRepairKit);
                if (repairKit != null)
                {
                    _repairKitAttempts[slot] = new RepairKitAttempt(item.Durability, Kernel.TickCount);

                    if (repairKit.Use())
                        Log.NotifyLang("UseRepairKitDurLow", item.Record.GetRealName(), item.Durability);

                    return;
                }
            }

            if (returnToTownEnabled && item.Durability <= ReturnToTownDurability)
            {
                ReturnToTown(item);
                return;
            }
        }
    }

    /// <summary>
    ///     Uses the return scroll, limited by <see cref="ReturnScrollRetryDelay" />.
    /// </summary>
    /// <param name="item">The worn out item.</param>
    private static void ReturnToTown(InventoryItem item)
    {
        if (Kernel.TickCount - _lastReturnScrollTick < ReturnScrollRetryDelay)
            return;

        _lastReturnScrollTick = Kernel.TickCount;

        if (Game.Player.UseReturnScroll())
            Log.WarnLang("ReturnToTownDurLow", item.Record.GetRealName());
    }

    /// <summary>
    ///     Cores the on update item.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <param name="durability">The durability.</param>
    private static void OnUpdateItemDurability(byte slot, uint durability)
    {
        var item = Game.Player.Inventory.GetItemAt(slot);
        if (item == null)
            return;

        item.Durability = durability;
    }

    /// <summary>
    ///     State of the repair kit usage for a single equipment slot.
    /// </summary>
    private sealed class RepairKitAttempt
    {
        public RepairKitAttempt(uint durability, int tickCount)
        {
            Durability = durability;
            TickCount = tickCount;
        }

        /// <summary>
        ///     Gets the durability the item had when the repair kit was used.
        /// </summary>
        public uint Durability { get; }

        /// <summary>
        ///     Gets the tick count when the repair kit was used.
        /// </summary>
        public int TickCount { get; }

        /// <summary>
        ///     Gets or sets a value indicating whether the repair kit did not restore the durability.
        /// </summary>
        public bool Failed { get; set; }
    }
}
