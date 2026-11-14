using System;
using System.Linq;
using RSBot.Core;
using RSBot.Core.Objects;

namespace RSBot.Training.Bundle.Avoidance;

internal class AvoidanceBundle : IBundle
{
    /// <summary>
    ///     Gets the avoidance list.
    /// </summary>
    /// <value>
    ///     The avoidance list.
    /// </value>
    public MonsterRarity[] AvoidanceList => PlayerConfig.Profile.Avoidance.AvoidMonsters?.ToArray() ?? Array.Empty<MonsterRarity>();

    /// <summary>
    ///     Gets the preferance list.
    /// </summary>
    /// <value>
    ///     The preferance list.
    /// </value>
    public MonsterRarity[] PreferanceList => PlayerConfig.Profile.Avoidance.PreferMonsters?.ToArray() ?? Array.Empty<MonsterRarity>();

    /// <summary>
    ///     Gets the berserk list.
    /// </summary>
    /// <value>
    ///     The berserk list.
    /// </value>
    public MonsterRarity[] BerserkerList => PlayerConfig.Profile.Avoidance.BerserkMonsters?.ToArray() ?? Array.Empty<MonsterRarity>();

    /// <summary>
    ///     Invokes this instance.
    /// </summary>
    public void Invoke() { }

    /// <summary>
    ///     Refreshes this instance.
    /// </summary>
    public void Refresh() { }

    public void Stop()
    {
        //Nothing to do
    }

    /// <summary>
    ///     Avoids the monster.
    /// </summary>
    /// <param name="rarity">The rarity.</param>
    /// <returns></returns>
    public bool AvoidMonster(MonsterRarity rarity) => AvoidanceList.Any(r => r == rarity || (rarity != 0 && (r & rarity) == rarity));

    /// <summary>
    ///     Prefers the monster.
    /// </summary>
    /// <param name="rarity">The rarity.</param>
    /// <returns></returns>
    public bool PreferMonster(MonsterRarity rarity) => PreferanceList.Any(r => r == rarity || (rarity != 0 && (r & rarity) == rarity));

    /// <summary>
    ///     Use Berserk on monster.
    /// </summary>
    /// <param name="rarity">The rarity.</param>
    /// <returns></returns>
    public bool UseBerserkOnMonster(MonsterRarity rarity) => BerserkerList.Any(r => r == rarity || (rarity != 0 && (r & rarity) == rarity));
}
