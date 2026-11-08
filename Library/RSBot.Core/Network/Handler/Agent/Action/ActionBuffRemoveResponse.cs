using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Objects.Spawn;

namespace RSBot.Core.Network.Handler.Agent.Action;

internal class ActionBuffRemoveResponse : IPacketHandler
{
    /// <summary>
    ///     Invokes the specified packet.
    /// </summary>
    /// <param name="packet">The packet.</param>
    public void Invoke(Packet packet)
    {
        var buffTokensCount = packet.ReadByte();

        for (var i = 0; i < buffTokensCount; i++)
        {
            var token = packet.ReadUInt();
            if (token == 0)
                continue;

            if (Game.Player.State.TryRemoveActiveBuff(token, out var buff))
            {
                Log.Notify($"The buff [{buff.Record?.GetRealName()}] expired");

                EventManager.FireEvent("OnRemoveBuff", buff);

                var playerSkill = Game.Player.Skills.GetSkillInfoById(buff.Id);
                playerSkill?.Reset();

                continue;
            }

            if (!SpawnManager.TryGetEntity<SpawnedBionic>(p => p.State.TryGetActiveBuff(token, out _), out var bionic))
            {
                // The buff is already gone (for example a DoT applied to a monster that died and was
                // removed from the spawns). This is normal during combat and must not fill the log with
                // warnings, otherwise real problems are hard to spot.
                Log.Debug($"{token} not found while trying remove buff with token!");
                continue;
            }

            bionic.State.TryRemoveActiveBuff(token, out _);
        }
    }

    #region Properites

    /// <summary>
    ///     Gets or sets the opcode.
    /// </summary>
    /// <value>
    ///     The opcode.
    /// </value>
    public ushort Opcode => 0xB072;

    /// <summary>
    ///     Gets or sets the destination.
    /// </summary>
    /// <value>
    ///     The destination.
    /// </value>
    public PacketDestination Destination => PacketDestination.Client;

    #endregion Properites
}
