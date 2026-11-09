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

            if (PlayerConfig.Profile.Skills.ResurrectionSkillId != 0)
                SkillManager.ResurrectionSkill = Game.Player.Skills.GetSkillInfoById(PlayerConfig.Profile.Skills.ResurrectionSkillId);

            if (PlayerConfig.Profile.Skills.ImbueSkillId != 0)
                SkillManager.ImbueSkill = Game.Player.Skills.GetSkillInfoById(PlayerConfig.Profile.Skills.ImbueSkillId);

            if (PlayerConfig.Profile.Skills.TeleportSkillId != 0)
                SkillManager.TeleportSkill = Game.Player.Skills.GetSkillInfoById(PlayerConfig.Profile.Skills.TeleportSkillId);
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
            if (Game.AcceptanceRequest != null && PlayerConfig.Profile.Skills.AcceptResurrection)
                Game.AcceptanceRequest.Accept();
        }

        /// <summary>
        ///     Will be triggered if EXP/SP were gained. Increases the selected mastery level (if available)
        /// </summary>
        private void OnSpUpdated()
        {
            var gap = PlayerConfig.Profile.Skills.MasteryGap;
            var selectedMasteryName = PlayerConfig.Profile.Skills.SelectedMastery;
            var mastery = Game.Player.Skills.Masteries.FirstOrDefault(m => m.Record.NameCode == selectedMasteryName);
            var checkLearnMastery = PlayerConfig.Profile.Skills.AutoLearnMastery;
            var checkLearnMasteryBotStopped = PlayerConfig.Profile.Skills.LearnMasteryWhenBotStopped;
            if (selectedMasteryName == null || !checkLearnMastery)
                return;
            if (!checkLearnMasteryBotStopped && !Kernel.Bot.Running)
                return;
            if (mastery == null || mastery.Level + gap == Game.Player.Level)
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
                if (!PlayerConfig.Profile.Skills.AttackSkillsByRarity.TryGetValue(i, out var skillIds))
                    continue;

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

            foreach (var buffId in PlayerConfig.Profile.Skills.BuffSkills)
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

        public static void SaveSkills(string monsterType, uint[] skills)
        {
            if (monsterType == "RSBot.Skills.Buffs" || monsterType == "Buffs")
            {
                PlayerConfig.Profile.Skills.BuffSkills.Clear();
                PlayerConfig.Profile.Skills.BuffSkills.AddRange(skills);
            }
            else
            {
                var indexStr = monsterType.Replace("RSBot.Skills.Attacks_", "");
                if (int.TryParse(indexStr, out var rarityIndex))
                {
                    if (!PlayerConfig.Profile.Skills.AttackSkillsByRarity.ContainsKey(rarityIndex))
                        PlayerConfig.Profile.Skills.AttackSkillsByRarity[rarityIndex] = new();

                    PlayerConfig.Profile.Skills.AttackSkillsByRarity[rarityIndex].Clear();
                    PlayerConfig.Profile.Skills.AttackSkillsByRarity[rarityIndex].AddRange(skills);
                }
            }

            PlayerConfig.Save();
        }

        public static void CheckSkillWithdrawnOrUpgraded(SkillInfo oldSkill, SkillInfo newSkill)
        {
            for (var i = 0; i < _numMonsterTypes; i++)
            {
                if (!PlayerConfig.Profile.Skills.AttackSkillsByRarity.TryGetValue(i, out var skills))
                    continue;

                var index = skills.IndexOf(oldSkill.Id);
                if (index == -1)
                    continue;

                if (oldSkill.Id == newSkill.Id)
                    skills.RemoveAt(index);
                else
                    skills[index] = newSkill.Id;
            }

            var buffs = PlayerConfig.Profile.Skills.BuffSkills;
            var buffIndex = buffs.IndexOf(oldSkill.Id);
            if (buffIndex != -1)
            {
                // remove skill
                if (newSkill.Id == oldSkill.Id)
                    buffs.RemoveAt(buffIndex);
                else
                    buffs[buffIndex] = newSkill.Id;
            }

            if (PlayerConfig.Profile.Skills.ResurrectionSkillId == oldSkill.Id)
            {
                if (oldSkill.Id == newSkill.Id)
                {
                    SkillManager.ResurrectionSkill = null;
                    PlayerConfig.Profile.Skills.ResurrectionSkillId = 0;
                }
                else
                {
                    PlayerConfig.Profile.Skills.ResurrectionSkillId = newSkill.Id;
                    SkillManager.ResurrectionSkill = newSkill;
                }
            }

            if (PlayerConfig.Profile.Skills.ImbueSkillId == oldSkill.Id)
            {
                if (oldSkill.Id == newSkill.Id)
                {
                    SkillManager.ImbueSkill = null;
                    PlayerConfig.Profile.Skills.ImbueSkillId = 0;
                }
                else
                {
                    PlayerConfig.Profile.Skills.ImbueSkillId = newSkill.Id;
                    SkillManager.ImbueSkill = newSkill;
                }
            }

            if (PlayerConfig.Profile.Skills.TeleportSkillId == oldSkill.Id)
            {
                if (oldSkill.Id == newSkill.Id)
                {
                    SkillManager.TeleportSkill = null;
                    PlayerConfig.Profile.Skills.TeleportSkillId = 0;
                }
                else
                {
                    PlayerConfig.Profile.Skills.TeleportSkillId = newSkill.Id;
                    SkillManager.TeleportSkill = newSkill;
                }
            }

            ApplyAttackSkills();
            ApplyBuffSkills();

            PlayerConfig.Save();
        }

        public static void SetImbueSkill(SkillInfo imbue)
        {
            SkillManager.ImbueSkill = imbue;
            PlayerConfig.Profile.Skills.ImbueSkillId = imbue == null ? 0 : imbue.Id;
            PlayerConfig.Save();
        }

        public static void SetResurrectionSkill(SkillInfo skill)
        {
            SkillManager.ResurrectionSkill = skill;
            PlayerConfig.Profile.Skills.ResurrectionSkillId = skill == null ? 0 : skill.Id;
            PlayerConfig.Save();
        }

        public static void SetMasteryToLearn(string mastery)
        {
            PlayerConfig.Profile.Skills.SelectedMastery = mastery;
            PlayerConfig.Save();
        }

        public static void SetTeleportSkill(uint skillId)
        {
            PlayerConfig.Profile.Skills.TeleportSkillId = skillId;
            PlayerConfig.Save();

            SkillManager.TeleportSkill = Game.Player.Skills.GetSkillInfoById(skillId);
        }
    }
}
