using System;
using Python.Runtime;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Extensions;
using RSBot.Core.Objects;
using RSBot.Core.Objects.Skill;
using RSBot.Core.Objects.Spawn;
using RSBot.General.Components;
using RSBot.Python.Runtime;

namespace RSBot.Python.API.Core
{
    public class EntityAPI : IPythonPlugin
    {
        /// <summary>
        /// Unique module name of the plugin.
        /// </summary>
        public string ModuleName => "entity";

        public void Init(PythonContext context) { }

        public PyList get_monsters()
        {
            using (Py.GIL())
            {
                var result = new PyList();
                if (Game.Player == null)
                    return result;
                if (!SpawnManager.TryGetEntities<SpawnedMonster>(out var monsters))
                    return new PyList();

                foreach (var monster in monsters)
                {
                    if (monster == null)
                        continue;
                    var pyItem = PythonData.Dict(
                        ("uid", monster.UniqueId),
                        ("model", monster.Id),
                        ("servername", monster.Record.CodeName),
                        ("name", monster.Record.GetRealName()),
                        ("type", monster.Rarity.GetName()),
                        ("x", monster.Position.X.ToString()),
                        ("y", monster.Position.Y.ToString()),
                        ("z", monster.Position.ZOffset.ToString()),
                        ("region", monster.Position.Region.Id.ToString()),
                        ("region_name", Game.ReferenceManager.GetTranslation(monster.Position.Region.ToString())),
                        ("distance", monster.DistanceToPlayer.ToString()),
                        ("hp", monster.Health.ToString()),
                        ("max_hp", monster.MaxHealth.ToString())
                    );
                    result.Append(pyItem);
                }
                return result;
            }
        }

        public PyList get_party()
        {
            using (Py.GIL())
            {
                var result = new PyList();
                if (Game.Player == null)
                    return result;
                if (!Game.Party.IsInParty)
                    return result;

                var members = Game.Party.Members;

                foreach (var member in members)
                {
                    if (member == null)
                        continue;
                    var pyItem = PythonData.Dict(
                        ("uid", member.MemberId),
                        ("player_id", member.Player?.UniqueId ?? 0),
                        ("name", member.Name),
                        ("level", member.Level),
                        ("guild", member.Guild),
                        ("mastery1_id", member.MasteryId1),
                        ("mastery2_id", member.MasteryId2),
                        ("hp_percentage", Convert.ToByte(member.HealthMana.ToString("X2")[0].ToString(), 16) * 10),
                        ("mp_percentage", Convert.ToByte(member.HealthMana.ToString("X2")[1].ToString(), 16) * 10),
                        ("x", member.Player?.Position.X.ToString() ?? ""),
                        ("y", member.Player?.Position.Y.ToString() ?? ""),
                        ("z", member.Player?.Position.ZOffset.ToString() ?? ""),
                        ("region", member.Player?.Position.Region.Id.ToString() ?? ""),
                        (
                            "region_name",
                            Game.ReferenceManager.GetTranslation(member.Player?.Position.Region.ToString() ?? "")
                        ),
                        ("distance", member.Player?.DistanceToPlayer.ToString() ?? "")
                    );
                    result.Append(pyItem);
                }

                return result;
            }
        }

        public PyList get_players()
        {
            using (Py.GIL())
            {
                var result = new PyList();
                if (Game.Player == null)
                {
                    return result;
                }
                if (!SpawnManager.TryGetEntities<SpawnedPlayer>(out var players))
                    return new PyList();

                foreach (var player in players)
                {
                    if (player == null)
                        continue;
                    var pyItem = PythonData.Dict(
                        ("uid", player.UniqueId),
                        ("name", player.Name),
                        ("guild", player.Guild.Name),
                        ("x", player.Position.X.ToString()),
                        ("y", player.Position.Y.ToString()),
                        ("z", player.Position.ZOffset.ToString()),
                        ("region", player.Position.Region.Id.ToString()),
                        ("region_name", Game.ReferenceManager.GetTranslation(player.Position.Region.ToString())),
                        ("distance", player.DistanceToPlayer.ToString())
                    );
                    result.Append(pyItem);
                }
                return result;
            }
        }

        public PyList get_npcs()
        {
            using (Py.GIL())
            {
                var result = new PyList();
                if (Game.Player == null)
                {
                    return result;
                }
                if (!SpawnManager.TryGetEntities<SpawnedNpcNpc>(out var entities))
                    return new PyList();
                foreach (var entity in entities)
                {
                    if (entity == null)
                        continue;
                    var pyItem = PythonData.Dict(
                        ("uid", entity.UniqueId),
                        ("model", entity.Id),
                        ("servername", entity.Record.CodeName),
                        ("name", entity.Record.GetRealName()),
                        ("x", entity.Position.X.ToString()),
                        ("y", entity.Position.Y.ToString()),
                        ("z", entity.Position.ZOffset.ToString()),
                        ("region", entity.Position.Region.Id.ToString()),
                        ("region_name", Game.ReferenceManager.GetTranslation(entity.Position.Region.ToString())),
                        ("distance", entity.DistanceToPlayer.ToString())
                    );
                    result.Append(pyItem);
                }
                return result;
            }
        }

        public PyDict get_character()
        {
            using (Py.GIL())
            {
                var player = Game.Player;
                if (player == null)
                    return new PyDict();
                var position = player.Position;
                var serverName = Game.JoinedServer ?? Serverlist.Joining?.Name ?? string.Empty;

                return PythonData.Dict(
                    ("name", player.Name ?? string.Empty),
                    ("server", serverName),
                    ("guild", player.GuildName ?? string.Empty),
                    ("uid", player.UniqueId),
                    ("model", player.Id),
                    ("x", player.Position.X),
                    ("y", player.Position.Y),
                    ("z", player.Position.ZOffset),
                    ("region", player.Position.Region.Id),
                    ("region_name", Game.ReferenceManager.GetTranslation(player.Position.Region.ToString())),
                    ("hp", player.Health),
                    ("mp", player.Mana),
                    ("max_hp", player.MaximumHealth),
                    ("max_mp", player.MaximumMana),
                    ("exp", player.Experience),
                    ("max_exp", Game.ReferenceManager.GetRefLevel(player.Level).Exp_C),
                    ("sp", player.SkillPoints),
                    ("level", player.Level),
                    ("berserker", player.BerzerkPoints)
                );
            }
        }

        public PyDict get_position()
        {
            using (Py.GIL())
            {
                if (Game.Player == null)
                    return new PyDict();

                Position pos = Game.Player.Position;

                return PythonData.Dict(("x", pos.X), ("y", pos.Y), ("z", pos.ZOffset), ("region", pos.Region.Id));
            }
        }

        public PyList get_active_skills()
        {
            using (Py.GIL())
            {
                var result = new PyList();
                if (Game.Player == null)
                    return result;

                foreach (var skill in Game.Player.State.ActiveBuffs)
                {
                    var pySkill = PythonData.Dict(
                        ("id", skill.Id),
                        ("name", skill.Record.GetRealName()),
                        ("servername", skill.Record.UI_SkillName),
                        ("can_cast", skill.CanBeCasted),
                        ("cooldown", skill.HasCooldown)
                    );
                    result.Append(pySkill);
                }
                return result;
            }
        }

        public PyList get_skills()
        {
            using (Py.GIL())
            {
                var result = new PyList();
                if (Game.Player == null)
                    return result;

                foreach (SkillInfo skill in Game.Player.Skills.KnownSkills)
                {
                    if (skill == null)
                        continue;

                    var pySkill = PythonData.Dict(
                        ("name", skill.Record.GetRealName()),
                        ("id", skill.Id),
                        ("servername", skill.Record.UI_SkillName),
                        ("can_cast", skill.CanBeCasted),
                        ("on_cooldown", skill.HasCooldown),
                        ("reuse_time", skill.Record.Action_ReuseDelay),
                        ("duration", skill.Record.Action_ActionDuration.ToString()),
                        (
                            "mastery",
                            Game.ReferenceManager.GetRefSkillMastery(
                                Convert.ToUInt32(skill.Record.ReqCommon_Mastery1)
                            ).Name
                        ),
                        ("mastery_id", skill.Record.ReqCommon_Mastery1),
                        ("group", skill.Record.Basic_Group),
                        ("level", skill.Record.Basic_Level.ToString()),
                        (
                            "type",
                            skill.IsAttack ? "Attack"
                            : skill.IsDot ? "Dot"
                            : skill.IsImbue ? "Imbue"
                            : skill.IsPassive ? "Passive"
                            : ""
                        )
                    );
                    result.Append(pySkill);
                }
                return result;
            }
        }

        public PyList get_mastery()
        {
            using (Py.GIL())
            {
                var result = new PyList();
                if (Game.Player == null)
                    return result;

                foreach (MasteryInfo mastery in Game.Player.Skills.Masteries)
                {
                    var pySkill = PythonData.Dict(
                        ("name", Game.ReferenceManager.GetRefSkillMastery(Convert.ToUInt32(mastery.Id)).Name),
                        ("id", mastery.Id),
                        ("level", mastery.Level)
                    );
                    result.Append(pySkill);
                }
                return result;
            }
        }
    }
}
