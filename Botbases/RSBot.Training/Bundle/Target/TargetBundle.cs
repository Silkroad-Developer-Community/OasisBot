using System.Collections.Generic;
using System.Linq;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Extensions;
using RSBot.Core.Objects;
using RSBot.Core.Objects.Spawn;

namespace RSBot.Training.Bundle.Target;

internal class TargetBundle : IBundle
{
    private const int BLACKLIST_TIMEOUT = 5_000;

    #region Fields

    private Dictionary<uint, int> _blacklist;

    /// <summary>
    ///     The monster the bot was fighting before it switched to a weaker attacker. It is preferred again
    ///     once that attacker is dead, so the bot returns to the original target.
    /// </summary>
    private uint _returnTargetId;

    private int _returnTargetTick;

    private const int RETURN_TARGET_TIMEOUT = 20_000;

    #endregion Fields

    #region Constructor

    public TargetBundle()
    {
        SubscribeEvents();
    }

    #endregion Constructor

    #region Events

    private void OnTargetBehindObstacle()
    {
        if (Game.SelectedEntity == null)
            return;

        var selectedEntityUniqueId = Game.SelectedEntity.UniqueId;
        Game.SelectedEntity?.TryDeselect();
        Game.SelectedEntity = null;

        Bundles.Movement.LastEntityWasBehindObstacle = true;

        if (_blacklist?.TryAdd(selectedEntityUniqueId, Kernel.TickCount) == true)
            Log.Debug($"Add mob [{selectedEntityUniqueId} to blacklist for {BLACKLIST_TIMEOUT}ms");
    }

    #endregion Events

    #region Methods

    private void SubscribeEvents()
    {
        EventManager.SubscribeEvent("OnTargetBehindObstacle", OnTargetBehindObstacle);
    }

    /// <summary>
    ///     Invokes this instance.
    /// </summary>
    public void Invoke()
    {
        _blacklist?.RemoveAll(
            (uniqueId, tick) =>
            {
                var flag = Kernel.TickCount - tick > BLACKLIST_TIMEOUT;
                if (flag)
                    Log.Debug($"Removed mob [{uniqueId} from blacklist!");

                return flag;
            }
        );

        var attacker = GetWeakestAttacker();
        if (attacker != null && Container.Bot.Area.IsInSight(attacker))
        {
            if (Game.SelectedEntity == null)
            {
                if (IsEmergencySituation())
                {
                    Log.Debug("[TargetBundle] Emergency situation: Attacking the weaker mob first!");

                    if (attacker.TrySelect())
                        Bundles.Movement.LastEntityWasBehindObstacle = false;

                    return;
                }
            }
            else if (
                SpawnManager.TryGetEntity<SpawnedMonster>(Game.SelectedEntity.UniqueId, out var selectedMonster)
                && attacker.UniqueId != selectedMonster.UniqueId
                && (byte)attacker.Rarity < (byte)selectedMonster.Rarity
            )
            {
                Log.Debug(
                    "[TargetBundle] A weaker mob is attacking the player: switching from "
                        + $"{selectedMonster.Rarity} to {attacker.Rarity}; the previous target is resumed afterwards."
                );

                // Remember what the bot was fighting, so it returns to that monster once the weaker attacker
                // is dead instead of walking to whichever monster happens to be nearest at that moment.
                _returnTargetId = selectedMonster.UniqueId;
                _returnTargetTick = Kernel.TickCount;

                if (attacker.TrySelect())
                    Bundles.Movement.LastEntityWasBehindObstacle = false;

                return;
            }
        }

        var warlockModeEnabled = PlayerConfig.Get("RSBot.Skills.checkWarlockMode", false);
        if (warlockModeEnabled && Game.SelectedEntity?.State.HasTwoDots() == true)
            return;

        if (Game.SelectedEntity != null && Game.SelectedEntity is not SpawnedMonster)
            Game.SelectedEntity = null;

        if (Game.SelectedEntity?.State.LifeState == LifeState.Alive)
            return;

        var monster = GetNearestEnemy();
        if (monster == null)
            return;

        if (!Container.Bot.Area.IsInSight(monster))
            return;

        if (monster.TrySelect())
        {
            Bundles.Movement.LastEntityWasBehindObstacle = false;

            // The original target is back in combat, no need to remember it any longer.
            if (monster.UniqueId == _returnTargetId)
                _returnTargetId = 0;
        }
    }

    /// <summary>
    ///     Gets the weakest monster that is currently attacking the player. The Avoid list is not required:
    ///     the caller compares the rarity with the current target and switches only when the attacker is
    ///     weaker, so a stronger monster is left only for a weaker one that already fights back.
    /// </summary>
    /// <returns>The weakest attacker, or <c>null</c> when the option is off or nobody attacks the player.</returns>
    private SpawnedMonster GetWeakestAttacker()
    {
        var attackWeakerFirst = PlayerConfig.Get<bool>("RSBot.Training.checkAttackWeakerFirst");
        if (!attackWeakerFirst)
            return null;

        if (
            !SpawnManager.TryGetEntities<SpawnedMonster>(
                e =>
                    e.AttackingPlayer
                    && e.State.LifeState == LifeState.Alive
                    && (_blacklist == null || !_blacklist.ContainsKey(e.UniqueId)),
                out var entities
            )
        )
            return null;

        return entities
            .OrderBy(e => (byte)e.Rarity)
            .ThenBy(e => e.Record.Level)
            .ThenBy(e => e.Position.DistanceToPlayer())
            .FirstOrDefault();
    }

    private bool IsEmergencySituation()
    {
        return SpawnManager.Any<SpawnedMonster>(e =>
            e.AttackingPlayer && e.State.LifeState == LifeState.Alive && Bundles.Avoidance.AvoidMonster(e.Rarity)
        );
    }

    /// <summary>
    ///     Gets the nearest enemy.
    /// </summary>
    /// <returns></returns>
    private SpawnedMonster GetNearestEnemy()
    {
        var warlockModeEnabled = PlayerConfig.Get<bool>("RSBot.Skills.checkWarlockMode");
        var ignorePillar = PlayerConfig.Get<bool>("RSBot.Training.checkBoxDimensionPillar");

        // The monster the bot left for a weaker attacker is preferred again while it is still around; after
        // the timeout it is dropped so a stale id does not keep the bot attached to a monster far away.
        if (_returnTargetId != 0 && Kernel.TickCount - _returnTargetTick > RETURN_TARGET_TIMEOUT)
            _returnTargetId = 0;

        if (
            !SpawnManager.TryGetEntities<SpawnedMonster>(
                m =>
                    m.State.LifeState == LifeState.Alive
                    && //Only alive
                    !(warlockModeEnabled && m.State.HasTwoDots())
                    && //Has two Dots?
                    m.IsBehindObstacle == false
                    && //Is not behind obstacle
                    (_blacklist == null || !_blacklist.ContainsKey(m.UniqueId))
                    && //Is not blacklisted
                    (m.AttackingPlayer || !Bundles.Avoidance.AvoidMonster(m.Rarity))
                    && //Is attacking player or shouldn't be avoided
                    Container.Bot.Area.IsInSight(m)
                    && //Is in training area
                    !m.Record.IsPandora
                    && //Isn't pandora box
                    !(m.Record.IsDimensionPillar && ignorePillar)
                    && //Isn't dimension pillar
                    !m.Record.IsSummonFlower,
                out var entities
            )
        )
            return default;

        return entities
            .OrderByDescending(m => m.UniqueId == _returnTargetId)
            .ThenByDescending(m => m.AttackingPlayer)
            .ThenByDescending(m => Bundles.Avoidance.PreferMonster(m.Rarity))
            .ThenBy(m => m.Movement.Source.DistanceToPlayer())
            .FirstOrDefault();
    }

    /// <summary>
    ///     Refreshes this instance.
    /// </summary>
    public void Refresh()
    {
        _blacklist = new Dictionary<uint, int>(8);
    }

    public void Stop()
    {
        _blacklist = null;
    }

    #endregion Methods
}
