using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RSBot.Core.Client.ReferenceObjects;
using RSBot.Core.Event;
using RSBot.Core.Network;
using RSBot.Core.Objects;
using RSBot.Core.Objects.Skill;
using RSBot.Core.Objects.Spawn;

namespace RSBot.Core.Components;

public static class SkillManager
{
    /// <summary>
    ///     Get the skill using index
    /// </summary>
    private static int _lastIndex;

    /// <summary>
    ///     The last casted skill id
    /// </summary>
    public static uint LastCastedSkillId;

    /// <summary>
    ///     The id of the skill that was sent to the server last. The server answers a rejected cast with a
    ///     cooldown error that does not name the skill, so the request is remembered here to quiet it.
    /// </summary>
    public static uint LastRequestedSkillId;

    /// <summary>
    ///     Basic skills
    /// </summary>
    private static IEnumerable<uint> _baseSkills;

    /// <summary>
    ///     Gets or sets the skills organized by their mob priority.
    /// </summary>
    /// <value>
    ///     The skills.
    /// </value>
    public static Dictionary<MonsterRarity, List<SkillInfo>> Skills { get; set; }

    /// <summary>
    ///     Gets or sets the resurrection skill.
    /// </summary>
    /// <value>
    ///     The resurrection skill.
    /// </value>
    public static SkillInfo ResurrectionSkill { get; set; }

    /// <summary>
    ///     Gets or sets the imbue skill.
    /// </summary>
    /// <value>
    ///     The imbue skill.
    /// </value>
    public static SkillInfo ImbueSkill { get; set; }

    /// <summary>
    ///     Gets or sets the buffs.
    /// </summary>
    /// <value>
    ///     The buffs.
    /// </value>
    public static List<SkillInfo> Buffs { get; set; }

    /// <summary>
    ///     Gets or sets the teleport skill.
    /// </summary>
    public static SkillInfo TeleportSkill { get; set; }

    /// <summary>
    ///     Gets the config to always use skills in order.
    /// </summary>
    public static bool UseSkillsInOrder => PlayerConfig.Get("RSBot.Skills.checkUseSkillsInOrder", false);

    /// <summary>
    ///     Is the last action basic skill (Auto attack) <c>true</c>; otherwise <c>false</c>
    /// </summary>
    public static bool IsLastCastedBasic => _baseSkills.Contains(LastCastedSkillId);

    /// <summary>
    ///     Initializes this instance.
    /// </summary>
    internal static void Initialize()
    {
        Skills = Enum.GetValues(typeof(MonsterRarity))
            .Cast<MonsterRarity>()
            .ToDictionary(v => v, v => new List<SkillInfo>());
        Buffs = new List<SkillInfo>();

        EventManager.SubscribeEvent("OnLoadGameData", OnLoadGamedData);
        EventManager.SubscribeEvent("OnCastSkill", new Action<uint>(OnCastSkill));

        Log.Debug($"Initialized [SkillManager] for [{Skills.Count}] different mob rarities!");
    }

    private static void OnLoadGamedData()
    {
        _baseSkills = Game.ReferenceManager.GetBaseSkills();
    }

    /// <summary>
    ///     Call after casted skill
    /// </summary>
    /// <param name="skillId">The casted skill id</param>
    private static void OnCastSkill(uint skillId)
    {
        LastCastedSkillId = skillId;
    }

    /// <summary>
    ///     Sets the skills.
    /// </summary>
    /// <param name="monsterRarity">The monster rarity.</param>
    /// <param name="skills">The skills.</param>
    public static void SetSkills(MonsterRarity monsterRarity, List<SkillInfo> skills)
    {
        if (Skills == null || !Skills.ContainsKey(monsterRarity))
            return;

        Skills[monsterRarity] = skills;
    }

    /// <summary>
    ///     Gets the next skill.
    /// </summary>
    /// <returns></returns>
    public static SkillInfo GetNextSkill()
    {
        var entity = Game.SelectedEntity;
        if (entity == null)
            return null;

        var rarity = MonsterRarity.General;

        if (entity is SpawnedMonster monster)
            if (Skills[monster.Rarity].Count > 0)
                rarity = monster.Rarity;

        var distance = Game.Player.Movement.Source.DistanceTo(entity.Movement.Source);

        var minDifference = int.MaxValue;
        //var weaponRange = 0;
        var closestSkill = default(SkillInfo);

        // Warlock mode: apply the DoT skills first. A skill that is already on the target is skipped by
        // FindDotSkillForTarget, and when no DoT can be applied the normal attack skills are used, so the
        // bot does not keep casting a skill the server would reject.
        if (
            entity.State.HitState != ActionHitStateFlag.KnockDown
            && PlayerConfig.Get("RSBot.Skills.checkWarlockMode", false)
        )
        {
            var dotSkill = FindDotSkillForTarget(entity, rarity, true);
            if (dotSkill != null)
            {
                Log.Debug(
                    $"[Skills] DoT '{dotSkill.Record?.GetRealName()}' ({dotSkill.Id}) on target {entity.UniqueId}, "
                        + $"dots on target: {entity.State.ActiveBuffs.Count(b => b.IsDot)}, "
                        + $"DoT targets alive: {CountDotTargets()}"
                );

                return dotSkill;
            }
        }

        if (entity.State.HitState == ActionHitStateFlag.KnockDown)
        {
            // try to get attack skill for only knockdown states
            closestSkill = Skills[rarity].Find(p => p.Record.Params.Contains(25697));
        }
        else if (UseSkillsInOrder || distance < 10)
        {
            var counter = -1;
            var skillCount = Skills[rarity].Count;
            while (skillCount > 0 && counter < skillCount)
            {
                counter++;
                _lastIndex++;

                if (_lastIndex > Skills[rarity].Count - 1)
                    _lastIndex = 0;

                if (!Skills.ContainsKey(rarity) || Skills[rarity].Count < _lastIndex)
                    continue;

                var selectedSkill = Skills[rarity][_lastIndex];
                if (
                    !selectedSkill.CanBeCasted
                    || !CanUseSkillOnTarget(selectedSkill, entity)
                    || (selectedSkill.IsDot && HasDotOnTarget(entity, selectedSkill))
                )
                    continue;

                closestSkill = selectedSkill;
                break;
            }

            //Debug.WriteLine($"while loop: {stopwatch.ElapsedMilliseconds}");
        }
        else
        {
            /*var weapon = Game.Player.Inventory.GetItemAt(6);
            if (weapon != null)
                weaponRange = weapon.Record.Range / 10;
            */

            for (var i = 0; i < Skills[rarity].Count; i++)
            {
                var s = Skills[rarity][i];
                if (
                    !s.CanBeCasted
                    || !CanUseSkillOnTarget(s, entity)
                    || (s.IsDot && HasDotOnTarget(entity, s))
                )
                    continue;

                var difference = Math.Abs(
                    s.Record.Action_Range / 10 - distance /* + weaponRange*/
                );
                if (minDifference > difference)
                {
                    minDifference = (short)difference;
                    closestSkill = s;
                }
            }
            //Debug.WriteLine($"for loop: {stopwatch.ElapsedMilliseconds}");
        }

        return closestSkill;
    }

    /// <summary>
    ///     Checks whether the skill may be used on the selected target. A DoT skill can only be applied to a
    ///     limited number of monsters at the same time, so a monster without a DoT is skipped while the
    ///     limit is reached. The limit is configured by <c>RSBot.Skills.DotTargetLimit</c> (0 disables it).
    /// </summary>
    /// <param name="skill">The skill that is about to be used.</param>
    /// <param name="entity">The selected entity.</param>
    /// <returns><c>true</c> when the skill may be used; otherwise <c>false</c>.</returns>
    private static bool CanUseSkillOnTarget(SkillInfo skill, SpawnedEntity entity)
    {
        if (!skill.IsDot || entity is not SpawnedBionic bionic)
            return true;

        var limit = PlayerConfig.Get("RSBot.Skills.numDotTargetLimit", 2);
        if (limit <= 0)
            return true;

        // A DoT that is already on the target does not add another affected monster.
        if (bionic.State.ActiveBuffs.Any(b => b.IsDot))
            return true;

        return CountDotTargets() < limit;
    }

    /// <summary>
    ///     Counts the monsters that currently have an active DoT. A DoT stays on a corpse until it
    ///     disappears, so dead monsters count as well.
    /// </summary>
    /// <returns>The number of monsters with an active DoT.</returns>
    private static int CountDotTargets()
    {
        // Only living monsters are counted. A DoT stays on a corpse until it disappears, and counting
        // corpses made the limit look reached while another living monster could still be DoTed.
        if (
            !SpawnManager.TryGetEntities<SpawnedMonster>(
                m => m.State.LifeState == LifeState.Alive && m.State.ActiveBuffs.Any(b => b.IsDot),
                out var entities
            )
        )
            return 0;

        return entities.Count();
    }

    /// <summary>
    ///     Checks whether the given DoT skill is already applied to the target.
    /// </summary>
    /// <param name="entity">The selected entity.</param>
    /// <param name="skill">The DoT skill.</param>
    /// <returns><c>true</c> when the DoT is already on the target; otherwise <c>false</c>.</returns>
    private static bool HasDotOnTarget(SpawnedEntity entity, SkillInfo skill)
    {
        return entity is SpawnedBionic bionic && bionic.State.ActiveBuffs.Any(b => b.Id == skill.Id);
    }

    /// <summary>
    ///     Finds a DoT skill that is not applied to the target yet. When <paramref name="requireReady" /> is
    ///     set, only skills that can be casted right now are taken into account.
    /// </summary>
    /// <param name="entity">The selected entity.</param>
    /// <param name="rarity">The rarity whose skill list is used.</param>
    /// <param name="requireReady">Whether a skill on cooldown may be returned.</param>
    /// <returns>The skill to apply, or <c>null</c> when no DoT is left for the target.</returns>
    private static SkillInfo FindDotSkillForTarget(SpawnedEntity entity, MonsterRarity rarity, bool requireReady)
    {
        if (!Skills.TryGetValue(rarity, out var skills))
            return null;

        return skills.FirstOrDefault(s =>
            s.IsDot
            && (!requireReady || s.CanBeCasted)
            && CanUseSkillOnTarget(s, entity)
            && !HasDotOnTarget(entity, s)
        );
    }

    /// <summary>
    ///     Gets a value indicating whether at least one DoT can still be applied to the entity, ignoring
    ///     cooldowns. Used to decide when a target is finished and the next one may be picked.
    /// </summary>
    /// <param name="entity">The selected entity.</param>
    /// <returns><c>true</c> when another DoT may be applied; otherwise <c>false</c>.</returns>
    public static bool HasUsableDotFor(SpawnedEntity entity)
    {
        if (entity is not SpawnedMonster monster)
            return false;

        var rarity = MonsterRarity.General;
        if (Skills.TryGetValue(monster.Rarity, out var monsterSkills) && monsterSkills.Count > 0)
            rarity = monster.Rarity;

        return FindDotSkillForTarget(entity, rarity, false) != null;
    }

    /// <summary>
    ///     Gets a value indicating whether the bot should wait for a DoT instead of using the basic attack:
    ///     warlock mode is on, the target carries no DoT yet, and the DoT skill exists but is on cooldown.
    ///     Without this the bot hits the monster with the basic attack while the DoT is about to be ready.
    /// </summary>
    /// <param name="entity">The selected entity.</param>
    /// <returns><c>true</c> when the basic attack should be skipped for now; otherwise <c>false</c>.</returns>
    public static bool ShouldWaitForDot(SpawnedEntity entity)
    {
        if (!PlayerConfig.Get("RSBot.Skills.checkWarlockMode", false))
            return false;

        if (entity is not SpawnedMonster monster)
            return false;

        var rarity = MonsterRarity.General;
        if (Skills.TryGetValue(monster.Rarity, out var monsterSkills) && monsterSkills.Count > 0)
            rarity = monster.Rarity;

        if (!Skills.TryGetValue(rarity, out var skills))
            return false;

        // A DoT is ready right now - there is no reason to wait.
        if (FindDotSkillForTarget(entity, rarity, true) != null)
            return false;

        // Wait only for a DoT that is missing on the target, allowed for it and will be castable again
        // soon; a skill without enough mana would keep the bot waiting for nothing.
        return skills.Any(s =>
            s.IsDot
            && !HasDotOnTarget(entity, s)
            && CanUseSkillOnTarget(s, entity)
            && s.HasCooldown
            && Game.Player.Mana >= s.Record.Consume_MP
        );
    }

    /// <summary>
    ///     Check required of the using skill
    /// </summary>
    /// <param name="skill">The using skill</param>
    public static bool CheckSkillRequired(RefSkill skill)
    {
        if (skill.ReqCommon_Mastery1 == 1)
            return true;

        InventoryItem requiredItem = null;
        TypeIdFilter filter = null;

        var currentWeapon = Game.Player.Inventory.GetItemAt(6);
        if (skill.ReqCast_Weapon1 == WeaponType.Any)
        {
            var list = new List<TypeIdFilter>(8);

            for (var i = 0; i < skill.Params.Count; i++)
            {
                var param = skill.Params[i];
                if (param != 1919250793)
                    continue;

                var paramTypeId3 = (byte)skill.Params[++i];
                var paramTypeId4 = (byte)skill.Params[++i];
                list.Add(new TypeIdFilter(3, 1, paramTypeId3, paramTypeId4));
            }

            if (list.Count == 0)
                return true;

            filter = list.FirstOrDefault(p =>
                p.TypeID3 == currentWeapon?.Record.TypeID3 && p.TypeID4 == currentWeapon?.Record.TypeID4
            );
            if (filter != null)
                return true;

            filter = list.FirstOrDefault();
        }
        else
        {
            filter = new TypeIdFilter(p =>
                p.TypeID2 == 1
                && p.TypeID3 == 6
                && (
                    p.TypeID4 == (byte)skill.ReqCast_Weapon1
                    || ((byte)skill.ReqCast_Weapon2 != 0xFF && p.TypeID4 == (byte)skill.ReqCast_Weapon2)
                )
            );
        }

        requiredItem = Game.Player.Inventory.GetItemBest(filter);
        if (requiredItem == null)
            return false;

        var movingSlot = (byte)(requiredItem.Record.TypeID3 == 6 ? 6 : 7);
        if (requiredItem.Slot == movingSlot)
            return true;

        var result = requiredItem.Equip(movingSlot);

        if (movingSlot == 6 && requiredItem.Record.TwoHanded == 0)
        {
            // find and equip the shield item automatically
            filter = new TypeIdFilter(3, 1, 4, (byte)(Game.Player.Race == ObjectCountry.Chinese ? 1 : 2));
            var shieldItem = Game.Player.Inventory.GetItemBest(filter);
            if (shieldItem != null && shieldItem.Slot != 7)
                shieldItem.Equip(7);
        }

        return result;
    }

    public static bool CastSkill(SkillInfo skill, uint targetId = 0)
    {
        if (!Game.Player.Skills.HasSkill(skill.Id))
            return false;

        if (!SpawnManager.TryGetEntity<SpawnedBionic>(targetId, out var entity))
            return false;

        if (entity.State.LifeState == LifeState.Dead)
            return false;

        if (!CheckSkillRequired(skill.Record))
            return false;

        var packet = new Packet(0x7074);
        packet.WriteByte(ActionCommandType.Execute); //Execute
        packet.WriteByte(ActionType.Cast); //Use Skill
        packet.WriteUInt(skill.Id);
        packet.WriteByte(ActionTarget.Entity);

        // unknown byte
        if (Game.ClientType < GameClientType.Thailand)
            packet.WriteByte(1);

        packet.WriteUInt(targetId);

        Log.Debug(
            $"Skill Attacking to: {targetId} State: {entity.State.LifeState} Health: {entity.Health} HasHealth: {entity.HasHealth} Dst: {Math.Round(entity.DistanceToPlayer, 1)}"
        );

        LastRequestedSkillId = skill.Id;

        PacketManager.SendPacket(packet, PacketDestination.Server);

        return true;
    }

    /// <summary>
    ///     Cast player skill
    /// </summary>
    /// <param name="skillId">The skill identifier.</param>
    /// <param name="targetId">The target unique identifier.</param>
    /// <returns> <c>true</c> if this successfully used the selected skill; otherwise, <c>false</c>.</returns>
    public static bool CastSkillOld(SkillInfo skill, uint targetId = 0)
    {
        if (!Game.Player.Skills.HasSkill(skill.Id))
            return false;

        if (!SpawnManager.TryGetEntity<SpawnedBionic>(targetId, out var entity))
            return false;

        if (entity.State.LifeState == LifeState.Dead)
            return false;

        var weapon = Game.Player.Inventory.GetItemAt(6);

        if (!CheckSkillRequired(skill.Record))
            return false;

        var distance = entity.DistanceToPlayer;
        var speed = Game.Player.ActualSpeed;
        var movingSleep = 0d;

        // tel3 warrior sprint teleport
        var tel3Index = skill.Record.Params.FindIndex(p => p == 1952803891);
        if (tel3Index != -1)
        {
            var tel3speed = skill.Record.Params[++tel3Index];
            var tel3meter = skill.Record.Params[++tel3Index] / 10;

            if (distance < tel3meter)
                movingSleep = distance / tel3speed;
            else
                movingSleep = (distance - tel3meter) / speed + tel3meter / tel3speed;
        }
        else
        {
            var range = skill.Record.Action_Range / 10;
            if (distance - 3 > range)
                movingSleep = (distance - range) / speed;
        }

        if (movingSleep < 0)
            movingSleep = 0;
        else
            movingSleep *= 10000.0;

        var duration = (int)movingSleep; /* +
                     skill.Record.Action_CastingTime; +
                     skill.Record.Action_ActionDuration +
                     skill.Record.Action_PreparingTime;*/

        var packet = new Packet(0x7074);
        packet.WriteByte(1); //Execute
        packet.WriteByte(4); //Use Skill
        packet.WriteUInt(skill.Id);
        packet.WriteByte(ActionTarget.Entity);
        packet.WriteUInt(targetId);

        var callback = new AwaitCallback(
            response =>
                response.ReadByte() == 0x02 && response.ReadByte() == 0x00
                    ? AwaitCallbackResult.Success
                    : AwaitCallbackResult.ConditionFailed,
            0xB074
        );

        var altSkill = skill.Record;
        while (altSkill != null)
        {
            duration += altSkill.Action_CastingTime + altSkill.Action_ActionDuration + altSkill.Action_PreparingTime;

            if (altSkill.Basic_ChainCode != 0)
                altSkill = Game.ReferenceManager.GetRefSkill(altSkill.Basic_ChainCode);
            else
                break;
        }

        if (duration < 100)
            duration = 1000;

        Log.Debug(
            $"Skill Attacking to: {targetId} State: {entity.State.LifeState} Health: {entity.Health} HasHealth: {entity.HasHealth} Dst: {Math.Round(entity.DistanceToPlayer, 1)}"
        );

        PacketManager.SendPacket(packet, PacketDestination.Server, callback);
        Thread.Sleep(duration);

        if (skill.Record.Basic_Activity != 1)
        {
            callback.AwaitResponse(duration);
            return callback.IsCompleted;
        }

        return true;
    }

    /// <summary>
    ///     Casts the buff skill.
    /// </summary>
    /// <param name="skillId">The skill identifier.</param>
    public static void CastBuff(SkillInfo skill, uint target = 0, bool awaitBuffResponse = true)
    {
        if (skill.Id == 0)
            return;

        /*
        if (!Game.Player.Skills.HasSkill(skill.Id))
            return;
        */
        if (!CheckSkillRequired(skill.Record))
            return;

        Log.Notify($"Casting skill (self-buff) [{skill.Record.GetRealName()}]");

        var packet = new Packet(0x7074);
        packet.WriteByte(1); //Execute
        packet.WriteByte(4); //Use Skill
        packet.WriteUInt(skill.Id);

        if (skill.Record.TargetGroup_Self || skill.Record.TargetGroup_Party)
        {
            packet.WriteByte(ActionTarget.Entity);
            packet.WriteUInt(target == 0 ? Game.Player.UniqueId : target);
        }
        else
        {
            packet.WriteByte(ActionTarget.None);
        }

        var castCallback = new AwaitCallback(
            response =>
            {
                var result = response.ReadByte();

                if (result != 0x01)
                    return AwaitCallbackResult.Fail;

                var action = Objects.Action.DeserializeBegin(response);

                return action.PlayerIsExecutor && action.SkillId == skill.Id
                    ? AwaitCallbackResult.Success
                    : AwaitCallbackResult.ConditionFailed;
            },
            0xB070
        );

        var actionStateCallback = new AwaitCallback(
            response =>
            {
                var state = response.ReadByte();
                var recurring = response.ReadByte();

                if (state == 0x02 && recurring == 0x00)
                    return AwaitCallbackResult.Success;

                if (state == 0x03)
                    return AwaitCallbackResult.Fail;

                return AwaitCallbackResult.ConditionFailed;
            },
            0xB074
        );

        if (!awaitBuffResponse)
        {
            PacketManager.SendPacket(packet, PacketDestination.Server);
            return;
        }

        if (skill.Record.Basic_Activity != 1)
            PacketManager.SendPacket(packet, PacketDestination.Server, castCallback, actionStateCallback);
        else
            PacketManager.SendPacket(packet, PacketDestination.Server, castCallback);

        var timeout =
            skill.Record.Action_CastingTime
            + skill.Record.Action_ActionDuration
            + skill.Record.Action_PreparingTime
            + 1500;

        castCallback.AwaitResponse(timeout);

        if (!castCallback.IsCompleted)
            return;

        if (skill.Record.Basic_Activity != 1)
            actionStateCallback.AwaitResponse(timeout);
    }

    /// <summary>
    ///     Casts a skill to the given target position
    /// </summary>
    /// <param name="skill"></param>
    /// <param name="target"></param>
    public static void CastSkillAt(SkillInfo skill, Position target)
    {
        if (target.Region == 0 || target.DistanceToPlayer() > 100)
            return;

        if (skill.Id == 0)
            return;

        if (!CheckSkillRequired(skill.Record))
            return;

        var packet = new Packet(0x7074);
        packet.WriteByte(ActionCommandType.Execute); //Execute
        packet.WriteByte(ActionType.Cast); //Use Skill
        packet.WriteUInt(skill.Id);
        packet.WriteByte(ActionTarget.Area);
        packet.WriteUShort(target.Region);

        if (target.Region.IsDungeon)
        {
            packet.WriteShort(target.XOffset);
            packet.WriteShort(target.YOffset);
            packet.WriteShort(target.ZOffset);
        }
        else
        {
            packet.WriteInt(target.XOffset);
            packet.WriteInt(target.ZOffset);
            packet.WriteInt(target.YOffset);
        }

        var callback = new AwaitCallback(
            response =>
            {
                return response.ReadByte() == 0x02 && response.ReadByte() == 0x00
                    ? AwaitCallbackResult.Success
                    : AwaitCallbackResult.ConditionFailed;
            },
            0xB074
        );

        PacketManager.SendPacket(packet, PacketDestination.Server, callback);

        if (skill.Record.Basic_Activity != 1)
            callback.AwaitResponse(1000);
    }

    /// <summary>
    ///     Casts the skill. Does not check any weapon requirement.
    /// </summary>
    /// <param name="skill"></param>
    /// <param name="targetId"></param>
    /// <returns></returns>
    public static bool CastAutoAttack()
    {
        var entity = Game.SelectedEntity;
        if (entity == null)
            return false;

        if (entity.State.LifeState == LifeState.Dead)
            return false;

        var packet = new Packet(0x7074);
        packet.WriteByte(ActionCommandType.Execute); //Execute
        packet.WriteByte(ActionType.Attack); //Use Skill
        packet.WriteByte(ActionTarget.Entity);

        // unknown byte
        if (Game.ClientType < GameClientType.Thailand)
            packet.WriteByte(1);

        packet.WriteUInt(entity.UniqueId);

        Log.Debug(
            $"Normal Attacking to: {entity.UniqueId} State: {entity.State.LifeState} Health: {entity.Health} HasHealth: {entity.HasHealth} Dst: {Math.Round(entity.DistanceToPlayer, 1)}"
        );

        PacketManager.SendPacket(packet, PacketDestination.Server);

        return true;
    }

    /// <summary>
    ///     Casts the skill at.
    /// </summary>
    /// <param name="skillId">The skill identifier.</param>
    /// <param name="position">The position.</param>
    public static void CastSkillAt(uint skillId, Position position)
    {
        if (!Game.Player.Skills.HasSkill(skillId))
            return;

        var packet = new Packet(0x7074);
        packet.WriteByte(ActionCommandType.Execute); //Execute
        packet.WriteByte(ActionType.Cast); //Use Skill
        packet.WriteUInt(skillId);
        packet.WriteByte(ActionTarget.Area);
        position.Region.Serialize(packet);
        packet.WriteFloat(position.XOffset);
        packet.WriteFloat(position.ZOffset);
        packet.WriteFloat(position.YOffset);

        PacketManager.SendPacket(packet, PacketDestination.Server);
    }

    /// <summary>
    ///     Cancels the buff.
    /// </summary>
    /// <param name="skillId">The skill identifier.</param>
    public static void CancelBuff(uint skillId)
    {
        if (!Game.Player.Skills.HasSkill(skillId))
            return;

        var packet = new Packet(0x7074);
        packet.WriteByte(ActionCommandType.Execute); //Execute
        packet.WriteByte(ActionType.Dispel); //Cancel Buff
        packet.WriteUInt(skillId);
        packet.WriteByte(ActionTarget.None);

        PacketManager.SendPacket(packet, PacketDestination.Server);
    }

    /// <summary>
    ///     Cancels the action.
    /// </summary>
    /// <returns></returns>
    public static bool CancelAction(int timeout = 500)
    {
        if (timeout <= 0)
        {
            PacketManager.SendPacket(CreateCancelActionPacket(), PacketDestination.Server);
            return true;
        }

        var callback = new AwaitCallback(
            response =>
            {
                var state = response.ReadByte();
                var recurring = response.ReadByte();

                if (state == 0x02 && recurring == 0x00)
                    return AwaitCallbackResult.Success;

                if (state == 0x02)
                    return AwaitCallbackResult.Fail;

                return AwaitCallbackResult.ConditionFailed;
            },
            0xB074
        );

        PacketManager.SendPacket(CreateCancelActionPacket(), PacketDestination.Server, callback);
        callback.AwaitResponse(timeout);

        return callback.IsCompleted;
    }

    private static Packet CreateCancelActionPacket()
    {
        var packet = new Packet(0x7074);
        packet.WriteByte(0x02); //Cancel

        return packet;
    }
}
