using System;
using System.Linq;
using System.Threading;
using RSBot.Core;
using RSBot.Core.Client.ReferenceObjects;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Objects;
using RSBot.Core.Objects.Skill;
using RSBot.Skills.Components;

namespace RSBot.Skills
{
    public class SkillsManager
    {
        private bool _isUpdatingMastery = false;
        private const int _numMonsterTypes = 10;

        public SkillsManager()
        {
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            EventManager.SubscribeEvent("OnLoadCharacter", OnLoadCharacter);

            EventManager.SubscribeEvent("OnSkillUpgraded", new Action<SkillInfo, SkillInfo>(OnSkillUpgraded));
            EventManager.SubscribeEvent("OnWithdrawSkill", new Action<SkillInfo, SkillInfo>(OnWithdrawSkill));

            EventManager.SubscribeEvent("OnResurrectionRequest", OnResurrectionRequest);
            EventManager.SubscribeEvent("OnExpSpUpdate", OnSpUpdated);
        }

        #region Events
        /// <summary>
        ///     Main_s the on load character.
        /// </summary>
        private void OnLoadCharacter()
        {
            ApplyAttackSkills();
            ApplyBuffSkills();
            ApplyDotSkills();
        }

        private void OnSkillUpgraded(SkillInfo oldSkill, SkillInfo newSkill)
        {
            Log.NotifyLang("SkillUpgraded", newSkill);

            CheckSkillWithdrawnOrUpgraded(oldSkill, newSkill);
        }

        private void OnWithdrawSkill(SkillInfo oldSkill, SkillInfo newSkill)
        {
            Log.NotifyLang("SkillWithdrawn", oldSkill);

            CheckSkillWithdrawnOrUpgraded(oldSkill, newSkill);
        }

        private void OnResurrectionRequest()
        {
            const string key = "RSBot.Skills.";
            if (Game.AcceptanceRequest != null && PlayerConfig.Get<bool>(key + "checkAcceptResurrection"))
                Game.AcceptanceRequest.Accept();
        }

        /// <summary>
        ///     Will be triggered if EXP/SP were gained. Increases the selected mastery level (if available)
        /// </summary>
        private void OnSpUpdated()
        {
            var gap = PlayerConfig.Get<decimal>("RSBot.Skills.numMasteryGap");
            var selectedMasteryName = PlayerConfig.Get<string>("RSBot.Skills.selectedMastery");
            var mastery = Game.Player.Skills.Masteries.FirstOrDefault(m => m.Record.NameCode == selectedMasteryName);
            var checkLearnMastery = PlayerConfig.Get<bool>("RSBot.Skills.checkLearnMastery");
            var checkLearnMasteryBotStopped = PlayerConfig.Get<bool>("RSBot.Skills.checkLearnMasteryBotStopped");
            if (selectedMasteryName == null || !checkLearnMastery)
                return;
            if (!checkLearnMasteryBotStopped && !Kernel.Bot.Running)
                return;
            if (mastery.Level + gap == Game.Player.Level)
                return;
            UpdateMastery(mastery.Level, mastery.Record, gap);
        }
        #endregion
        public void UpdateMastery(byte level, RefSkillMastery record, decimal gap = 0)
        {
            if (_isUpdatingMastery)
                return;
            while (level + gap < Game.Player.Level)
            {
                _isUpdatingMastery = true;
                var nextMasteryLevel = Game.ReferenceManager.GetRefLevel((byte)(level + 1));

                if (nextMasteryLevel.Exp_M > Game.Player.SkillPoints)
                {
                    Log.Debug(
                        $"Auto. upping mastery cancelled due to insufficient skill points. Required: {nextMasteryLevel.Exp_M}"
                    );

                    break;
                }
                Log.Notify($"Auto. train mastery [{record.Name} to lv. {nextMasteryLevel}");
                LearnMasteryHandler.LearnMastery(record.ID);
                level += 1;
                Thread.Sleep(500);
            }
            _isUpdatingMastery = false;
        }

        /// <summary>
        ///     Applies the attack skills.
        /// </summary>
        public static void ApplyAttackSkills()
        {
            foreach (var collection in SkillManager.Skills.Values)
                collection.Clear();

            for (var i = 0; i < _numMonsterTypes; i++)
            {
                var skillIds = PlayerConfig.GetArray<uint>("RSBot.Skills.Attacks_" + i);

                foreach (var skillId in skillIds)
                {
                    var skillInfo = Game.Player.Skills.GetSkillInfoById(skillId);
                    if (skillInfo == null)
                        continue;

                    switch (i)
                    {
                        case 1:
                            SkillManager.Skills[MonsterRarity.Champion].Add(skillInfo);
                            continue;
                        case 2:
                            SkillManager.Skills[MonsterRarity.Giant].Add(skillInfo);
                            continue;
                        case 3:
                            SkillManager.Skills[MonsterRarity.GeneralParty].Add(skillInfo);
                            continue;
                        case 4:
                            SkillManager.Skills[MonsterRarity.ChampionParty].Add(skillInfo);
                            continue;
                        case 5:
                            SkillManager.Skills[MonsterRarity.GiantParty].Add(skillInfo);
                            continue;
                        case 6:
                            SkillManager.Skills[MonsterRarity.Elite].Add(skillInfo);
                            continue;
                        case 7:
                            SkillManager.Skills[MonsterRarity.EliteStrong].Add(skillInfo);
                            continue;
                        case 8:
                            SkillManager.Skills[MonsterRarity.Unique].Add(skillInfo);
                            continue;
                        case 9:
                            SkillManager.Skills[MonsterRarity.Event].Add(skillInfo);
                            continue;
                        default:
                            SkillManager.Skills[MonsterRarity.General].Add(skillInfo);
                            continue;
                    }
                }
            }
        }

        /// <summary>
        ///     Applies the buff skills.
        /// </summary>
        public static void ApplyBuffSkills()
        {
            SkillManager.Buffs.Clear();

            Game.Player.TryGetAbilitySkills(out var abilitySkills);

            foreach (var buffId in PlayerConfig.GetArray<uint>("RSBot.Skills.Buffs"))
            {
                var skillInfo = Game.Player.Skills.GetSkillInfoById(buffId);
                if (skillInfo == null)
                {
                    skillInfo = abilitySkills.FirstOrDefault(p => p.Id == buffId);
                    if (skillInfo == null)
                        continue;
                }

                SkillManager.Buffs.Add(skillInfo);
            }
        }

        /// <summary>
        ///     Applies the DoT skills of warlock mode. They are configured apart from the regular attack skills,
        ///     because a DoT has its own limits (two per monster, a limited number of monsters).
        /// </summary>
        public static void ApplyDotSkills()
        {
            SkillManager.DotSkills.Clear();

            Game.Player.TryGetAbilitySkills(out var abilitySkills);

            foreach (var skillId in PlayerConfig.GetArray<uint>("RSBot.Skills.DotSkills"))
            {
                var skillInfo = Game.Player.Skills.GetSkillInfoById(skillId);
                if (skillInfo == null)
                {
                    skillInfo = abilitySkills.FirstOrDefault(p => p.Id == skillId);
                    if (skillInfo == null)
                        continue;
                }

                SkillManager.DotSkills.Add(skillInfo);
            }
        }

        public static void SaveSkills(string monsterType, uint[] skills)
        {
            PlayerConfig.SetArray(monsterType, skills);
        }

        public static void CheckSkillWithdrawnOrUpgraded(SkillInfo oldSkill, SkillInfo newSkill)
        {
            for (var i = 0; i < _numMonsterTypes; i++)
            {
                var skills = PlayerConfig.GetArray<uint>($"RSBot.Skills.Attacks_{i}").ToList();
                var index = skills.IndexOf(oldSkill.Id);
                if (index == -1)
                    continue;

                if (oldSkill.Id == newSkill.Id)
                    skills.RemoveAt(index);
                else
                    skills[index] = newSkill.Id;

                PlayerConfig.SetArray($"RSBot.Skills.Attacks_{i}", skills);
            }

            var buffs = PlayerConfig.GetArray<uint>("RSBot.Skills.Buffs").ToList();
            var buffIndex = buffs.IndexOf(oldSkill.Id);
            if (buffIndex != -1)
            {
                // remove skill
                if (newSkill.Id == oldSkill.Id)
                    buffs.RemoveAt(buffIndex);
                else
                    buffs[buffIndex] = newSkill.Id;

                PlayerConfig.SetArray("RSBot.Skills.Buffs", buffs);
            }

            var resurrectionSkill = PlayerConfig.Get<uint>("RSBot.Skills.ResurrectionSkill");
            if (resurrectionSkill == oldSkill.Id)
            {
                if (oldSkill.Id == newSkill.Id)
                    SkillManager.ResurrectionSkill = null;
                else
                    resurrectionSkill = newSkill.Id;

                PlayerConfig.Set("RSBot.Skills.ResurrectionSkill", resurrectionSkill);
            }

            var selectedImbue = PlayerConfig.Get<uint>("RSBot.Skills.Imbue");
            if (selectedImbue == oldSkill.Id)
            {
                if (oldSkill.Id == newSkill.Id)
                    SkillManager.ImbueSkill = null;
                else
                    selectedImbue = newSkill.Id;

                PlayerConfig.Set("RSBot.Skills.Imbue", selectedImbue);
            }

            var selectedTeleportSkill = PlayerConfig.Get<uint>("RSBot.Skills.TeleportSkill");
            if (selectedTeleportSkill == oldSkill.Id)
            {
                if (oldSkill.Id == newSkill.Id)
                    SkillManager.TeleportSkill = null;
                else
                    selectedTeleportSkill = newSkill.Id;

                PlayerConfig.Set("RSBot.Skills.TeleportSkill", selectedTeleportSkill);
            }

            ApplyAttackSkills();
            ApplyBuffSkills();
            ApplyDotSkills();

            PlayerConfig.Save();
        }

        public static void SetImbueSkill(SkillInfo imbue)
        {
            SkillManager.ImbueSkill = imbue;
            PlayerConfig.Set("RSBot.Skills.Imbue", imbue == null ? 0 : imbue.Id);
        }

        public static void SetResurrectionSkill(SkillInfo skill)
        {
            SkillManager.ResurrectionSkill = skill;
            PlayerConfig.Set("RSBot.Skills.ResurrectionSkill", skill == null ? 0 : skill.Id);
        }

        public static void SetMasteryToLearn(string mastery)
        {
            PlayerConfig.Set("RSBot.Skills.selectedMastery", mastery);
        }

        public static void SetTeleportSkill(uint skillId)
        {
            PlayerConfig.Set("RSBot.Skills.TeleportSkill", skillId);

            SkillManager.TeleportSkill = Game.Player.Skills.GetSkillInfoById(skillId);
        }
    }
}
