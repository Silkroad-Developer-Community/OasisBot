using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Objects.Spawn;

namespace RSBot.Core.Network.Handler.Agent.Action;

internal class ActionSkillCastResponse : IPacketHandler
{
    /// <summary>
    ///     Gets or sets the destination.
    /// </summary>
    /// <value>
    ///     The destination.
    /// </value>
    public PacketDestination Destination => PacketDestination.Client;

    /// <summary>
    ///     Gets or sets the opcode.
    /// </summary>
    /// <value>
    ///     The opcode.
    /// </value>
    public ushort Opcode => 0xB070;

    /// <summary>
    ///     Invokes the specified packet.
    /// </summary>
    /// <param name="packet">The packet.</param>
    public void Invoke(Packet packet)
    {
        var result = packet.ReadByte();
        if (result != 0x01)
        {
            var errorCode = packet.ReadByte();

            switch (errorCode)
            {
                case 0x0C:
                    // Same other skills are already running
                    break;

                case 0x0E:
                    Game.Player.EquipAmmunition();
                    break;

                case 0x05:
                    // The skill is still on its cooldown. Quiet it for a moment: otherwise the skill
                    // selection returns the same skill on the next tick and the cast is rejected again.
                    MarkRequestedSkillCooldown();

                    Log.Debug("Skill cooldown error. Still have time!");
                    break;

                case 0x06: // invalid target
                    break;

                case 0x10: // obstacle
                    EventManager.FireEvent("OnTargetBehindObstacle");
                    break;

                default:
                    Log.Error($"Invalid skill error code: 0x{errorCode:X2}");
                    break;
            }

            return;
        }

        var action = Objects.Action.DeserializeBegin(packet);

        if (action.PlayerIsExecutor)
        {
            //Game.Player.StopMoving();

            var skillInfo = Game.Player.Skills.GetSkillInfoById(action.SkillId);
            if (skillInfo == null)
                skillInfo = SkillManager.Buffs.Find(p => p.Id == action.SkillId);

            skillInfo?.Update();

            // A DoT is applied by the server a moment after the cast. Without a short block the skill
            // selection returns the same DoT until its buff shows up, which looks like a cast spam.
            if (skillInfo?.IsDot == true)
                skillInfo.MarkCooldown(2000);

            EventManager.FireEvent("OnCastSkill", action.SkillId);

            return;
        }

        if (!action.TryGetExecutor<SpawnedBionic>(out var executor))
            return;

        executor.TargetId = action.TargetId;
        //executor.StopMoving();

        if (!action.PlayerIsTarget)
            return;

        EventManager.FireEvent("OnEnemySkillOnPlayer");

        executor.StartAttackingTimer();
    }

    /// <summary>
    ///     Marks the skill that was sent to the server as unusable for a short time. The cooldown error does
    ///     not name the skill, so the last request is used.
    /// </summary>
    private static void MarkRequestedSkillCooldown()
    {
        var skillId = SkillManager.LastRequestedSkillId;
        if (skillId == 0)
            return;

        var skill = Game.Player.Skills.GetSkillInfoById(skillId) ?? SkillManager.Buffs.Find(p => p.Id == skillId);

        skill?.MarkCooldown();
    }
}
