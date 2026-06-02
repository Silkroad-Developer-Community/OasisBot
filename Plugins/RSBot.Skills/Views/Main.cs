using RSBot.Core;
using RSBot.Core.Client.ReferenceObjects;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Extensions;
using RSBot.Core.Objects;
using RSBot.Core.Objects.Skill;
using SDUI.Controls;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using CheckBox = SDUI.Controls.CheckBox;
using ListViewExtensions = RSBot.Core.Extensions.ListViewExtensions;

namespace RSBot.Skills.Views;

[ToolboxItem(false)]
public partial class Main : DoubleBufferedControl
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Main" /> class.
    /// </summary>
    public Main()
    {
        InitializeComponent();
        SubscribeEvents();

        listAttackingSkills.SmallImageList = ListViewExtensions.StaticImageList;
        listBuffs.SmallImageList = ListViewExtensions.StaticImageList;
        listSkills.SmallImageList = ListViewExtensions.StaticImageList;
        listActiveBuffs.SmallImageList = ListViewExtensions.StaticImageList;

        _lock = new object();
    }

    /// <summary>
    ///     Subscribes the events.
    /// </summary>
    private void SubscribeEvents()
    {
        EventManager.SubscribeEvent("OnLoadCharacter", OnLoadCharacter);

        EventManager.SubscribeEvent("OnSkillLearned", new Action<SkillInfo>(OnSkillLearned));
        EventManager.SubscribeEvent("OnSkillUpgraded", new Action<SkillInfo, SkillInfo>(OnSkillUpgraded));
        EventManager.SubscribeEvent("OnWithdrawSkill", new Action<SkillInfo, SkillInfo>(OnWithdrawSkill));
        EventManager.SubscribeEvent("OnLearnSkillMastery", new Action<MasteryInfo>(OnLearnSkillMastery));

        EventManager.SubscribeEvent("OnAddBuff", new Action<SkillInfo>(OnAddBuff));
        EventManager.SubscribeEvent("OnRemoveBuff", new Action<SkillInfo>(OnRemoveBuff));
        EventManager.SubscribeEvent("OnAddItemPerk", new Action<uint, uint>(OnAddItemPerk));
        EventManager.SubscribeEvent("OnRemoveItemPerk", new Action<uint, ItemPerk>(OnRemoveItemPerk));
    }

    /// <summary>
    ///     Called when [remove item perk].
    /// </summary>
    /// <param name="targetId">The target identifier.</param>
    /// <param name="removedPerk">The removed perk.</param>
    private void OnRemoveItemPerk(uint targetId, ItemPerk removedPerk)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<uint, ItemPerk>(OnRemoveItemPerk), targetId, removedPerk);
            return;
        }
        if (targetId != Game.Player.UniqueId || removedPerk == null)
            return;

        for (var i = 0; i < listActiveBuffs.Items.Count; i++)
        {
            var listItem = listActiveBuffs.Items[i];

            if (listItem?.Tag is not ItemPerk perkInfo || perkInfo.Token != removedPerk.Token)
                continue;

            listItem.Remove();
            return;
        }
    }

    /// <summary>
    ///     Called when [add item perk].
    /// </summary>
    /// <param name="targetId">The target identifier.</param>
    /// <param name="token">The token.</param>
    private void OnAddItemPerk(uint targetId, uint token)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<uint, uint>(OnAddItemPerk), targetId, token);
            return;
        }
        if (targetId != Game.Player.UniqueId)
            return;

        var perk = Game.Player.State.ActiveItemPerks[token];
        var item = new ListViewItem { Text = perk.Item?.GetRealName(), Tag = perk };

        listActiveBuffs.Items.Add(item);

        item.LoadSkillImage();
    }

    

    /// <summary>
    ///     Loads the settings.
    /// </summary>
    private void LoadSettings()
    {
        const string key = "RSBot.Skills.";

        foreach (var checkbox in panelPlayerSkills.Controls.OfType<CheckBox>())
            checkbox.Checked = PlayerConfig.Get(key + checkbox.Name, checkbox.Checked);

        foreach (var checkbox in groupBoxAttackingSkills.Controls.OfType<CheckBox>())
            checkbox.Checked = PlayerConfig.Get(key + checkbox.Name, checkbox.Checked);

        foreach (var checkbox in groupBoxAutomatedResurrection.Controls.OfType<CheckBox>())
            checkbox.Checked = PlayerConfig.Get(key + checkbox.Name, checkbox.Checked);

        foreach (var checkbox in groupBoxAdvancedBuff.Controls.OfType<CheckBox>())
            checkbox.Checked = PlayerConfig.Get(key + checkbox.Name, checkbox.Checked);

        foreach (var checkbox in grpMasteryUpdate.Controls.OfType<CheckBox>())
            checkbox.Checked = PlayerConfig.Get(key + checkbox.Name, checkbox.Checked);

        foreach (var num in grpMasteryUpdate.Controls.OfType<NumUpDown>())
            num.Value = PlayerConfig.Get(key + num.Name, num.Value);

        foreach (var num in groupBoxAutomatedResurrection.Controls.OfType<NumUpDown>())
            num.Value = PlayerConfig.Get(key + num.Name, num.Value);

        foreach (var checkbox in groupAdvancedSetup.Controls.OfType<CheckBox>())
            checkbox.Checked = PlayerConfig.Get(key + checkbox.Name, checkbox.Checked);
    }

    /// <summary>
    ///     Saves the settings.
    /// </summary>
    private void ApplySettings()
    {
        const string key = "RSBot.Skills.";
        foreach (var checkbox in panelPlayerSkills.Controls.OfType<CheckBox>())
            PlayerConfig.Set(key + checkbox.Name, checkbox.Checked);

        foreach (var checkbox in groupBoxAttackingSkills.Controls.OfType<CheckBox>())
            PlayerConfig.Set(key + checkbox.Name, checkbox.Checked);

        foreach (var checkbox in groupBoxAutomatedResurrection.Controls.OfType<CheckBox>())
            PlayerConfig.Set(key + checkbox.Name, checkbox.Checked);

        foreach (var checkbox in groupBoxAdvancedBuff.Controls.OfType<CheckBox>())
            PlayerConfig.Set(key + checkbox.Name, checkbox.Checked);

        foreach (var checkbox in grpMasteryUpdate.Controls.OfType<CheckBox>())
            PlayerConfig.Set(key + checkbox.Name, checkbox.Checked);

        foreach (var num in grpMasteryUpdate.Controls.OfType<NumUpDown>())
            PlayerConfig.Set(key + num.Name, num.Value);

        foreach (var num in groupBoxAutomatedResurrection.Controls.OfType<NumUpDown>())
            PlayerConfig.Set(key + num.Name, num.Value);

        foreach (var checkbox in groupAdvancedSetup.Controls.OfType<CheckBox>())
            PlayerConfig.Set(key + checkbox.Name, checkbox.Checked);
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the settings control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void settings_CheckedChanged(object sender, EventArgs e)
    {
        if (_settingsLoaded)
            ApplySettings();
    }

    /// <summary>
    ///     Handles the ValueChanged event of the numSettings control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void numSettings_ValueChanged(object sender, EventArgs e)
    {
        if (_settingsLoaded)
            ApplySettings();
    }

    /// <summary>
    ///     Loads the masteries.
    /// </summary>
    private void LoadMasteries()
    {
        var selectedMastery = PlayerConfig.Get<string>("RSBot.Skills.selectedMastery");
        comboLearnMastery.BeginUpdate();
        comboLearnMastery.Items.Clear();

        foreach (var mastery in Game.Player.Skills.Masteries)
            comboLearnMastery.Items.Add(new MasteryComboBoxItem { Level = mastery.Level, Record = mastery.Record });

        foreach (MasteryComboBoxItem item in comboLearnMastery.Items)
            if (item.Record.NameCode == selectedMastery)
                comboLearnMastery.SelectedItem = item;

        comboLearnMastery.EndUpdate();

        comboLearnMastery.Update();
    }

    /// <summary>
    ///     Loads the available teleport skills into the combo box
    /// </summary>
    private void LoadTeleportSkills()
    {
        comboTeleportSkill.BeginUpdate();
        comboTeleportSkill.Items.Clear();

        var selectedTeleportSkill = PlayerConfig.Get<uint>("RSBot.Skills.TeleportSkill");
        foreach (
            var skill in Game.Player.Skills.KnownSkills.Where(s =>
                s.CanBeCasted && s.Record.Action_ActionDuration == 0 && s.Record.Params[2] == 500
            )
        )
        {
            var index = comboTeleportSkill.Items.Add(
                new TeleportSkillComboBoxItem { Level = skill.Record.Basic_Level, Record = skill.Record }
            );

            if (selectedTeleportSkill == skill.Record.ID)
            {
                comboTeleportSkill.SelectedIndex = index;
                SkillManager.TeleportSkill = skill;
            }
        }

        comboLearnMastery.EndUpdate();
    }

    /// <summary>
    ///     Loads the attacks.
    /// </summary>
    /// <param name="index">The index.</param>
    private void LoadAttacks(int index = 0)
    {
        lock (_lock)
        {
            listAttackingSkills.BeginUpdate();
            listAttackingSkills.Items.Clear();

            var skillArray = PlayerConfig.GetArray<uint>("RSBot.Skills.Attacks_" + index);
            foreach (var skillId in skillArray)
            {
                var skillInfo = Game.Player.Skills.GetSkillInfoById(skillId);
                if (skillInfo == null)
                    continue;

                var item = new ListViewItem(skillInfo.Record.GetRealName()) { Tag = skillInfo };
                item.SubItems.Add("lv. " + skillInfo.Record.Basic_Level);
                listAttackingSkills.Items.Add(item);
                item.LoadSkillImageAsync();
            }

            listAttackingSkills.EndUpdate();
        }
    }

    /// <summary>
    ///     Loads the buffs.
    /// </summary>
    private void LoadBuffs()
    {
        lock (_lock)
        {
            listBuffs.BeginUpdate();
            listBuffs.Items.Clear();

            Game.Player.TryGetAbilitySkills(out var abilitySkills);

            var buffs = PlayerConfig.GetArray<uint>("RSBot.Skills.Buffs");
            foreach (var buffId in buffs)
            {
                var buffInfo = Game.Player.Skills.GetSkillInfoById(buffId);
                if (buffInfo == null)
                {
                    buffInfo = abilitySkills.FirstOrDefault(p => p.Id == buffId);
                    if (buffInfo == null)
                        continue;
                }

                var item = new ListViewItem(buffInfo.Record.GetRealName()) { Tag = buffInfo };

                item.SubItems.Add("lv. " + buffInfo.Record.Basic_Level);
                listBuffs.Items.Add(item);
                item.LoadSkillImageAsync();
            }

            listBuffs.EndUpdate();
        }
    }

    /// <summary>
    ///     Loads the imbues.
    /// </summary>
    private void LoadImbues()
    {
        lock (_lock)
        {
            comboImbue.Items.Clear();

            var selectedImbue = PlayerConfig.Get<int>("RSBot.Skills.Imbue");

            comboImbue.SelectedIndex = comboImbue.Items.Add("None");

            foreach (var skill in Game.Player.Skills.KnownSkills.Where(s => s.IsImbue && s.Enabled))
            {
                /*
                if (skill.IsLowLevel())
                    continue;
                */
                var index = comboImbue.Items.Add(skill);

                if (selectedImbue == 0)
                    continue;

                if (selectedImbue == skill.Id)
                    comboImbue.SelectedIndex = index;
            }
        }
    }

    /// <summary>
    ///     Loads the resurrection skills.
    /// </summary>
    private void LoadResurrectionSkills()
    {
        lock (_lock)
        {
            comboResurrectionSkill.Items.Clear();
            comboResurrectionSkill.Items.Add("None");

            foreach (
                var skill in Game.Player.Skills.KnownSkills.Where(s =>
                    s.Record != null
                    && ((s.Record.TargetEtc_SelectDeadBody && !s.Record.TargetGroup_Enemy_M) || s.Record.GroupID == 659)
                )
            ) //group res
            {
                if (skill.IsLowLevel())
                    continue;

                var index = comboResurrectionSkill.Items.Add(skill);
                var resurrectionSkillId = PlayerConfig.Get<int>("RSBot.Skills.ResurrectionSkill");
                if (skill.Id == resurrectionSkillId)
                    comboResurrectionSkill.SelectedIndex = index;
            }

            if (comboResurrectionSkill.SelectedIndex <= 0)
                comboResurrectionSkill.SelectedIndex = 0;
        }
    }

    /// <summary>
    ///     Loads the skills.
    /// </summary>
    private void LoadSkills()
    {
        lock (_lock)
        {
            var player = Game.Player;
            if (player == null)
                return;

            LoadResurrectionSkills();
            LoadTeleportSkills();
            LoadImbues();
            LoadBuffs();
            LoadMasteries();
            LoadAttacks(comboMonsterType.SelectedIndex);

            listSkills.BeginUpdate();
            listSkills.Items.Clear();
            listSkills.Groups.Clear();

            if (Game.Player.TryGetAbilitySkills(out var abilitySkills))
            {
                var group = new ListViewGroup("Ability") { Tag = 0 };
                listSkills.Groups.Add(group);

                foreach (var skill in abilitySkills)
                {
                    if (skill.IsPassive)
                        continue;

                    var listViewItem = new ListViewItem(skill.Record.GetRealName()) { Tag = skill };
                    listViewItem.SubItems.Add("lv. " + skill.Record.Basic_Level);
                    listViewItem.Group = group;
                    listSkills.Items.Add(listViewItem);

                    listViewItem.LoadSkillImage();
                }
            }

            foreach (var mastery in player.Skills.Masteries)
            {
                var group = new ListViewGroup(
                    Game.ReferenceManager.GetTranslation(mastery.Record.NameCode) + " (lv. " + mastery.Level + ")"
                );
                group.Tag = mastery.Id;
                listSkills.Groups.Add(group);
            }

            foreach (
                var skill in player.Skills.KnownSkills.Where(s => s.Enabled && s.Record.ReqCommon_Mastery1 != 1000)
            )
            {
                if (skill.IsPassive)
                    continue;

                if (skill.IsImbue)
                    continue;

                if (checkHideLowerLevelSkills.Checked && skill.IsLowLevel())
                    continue;

                //if (!skill.IsAttack && skill.Record.Target_Required && !skill.Record.TargetGroup_Self)
                //continue;

                var name = skill.Record.GetRealName();

                var item = new ListViewItem(name) { Tag = skill };
                item.SubItems.Add("lv. " + skill.Record.Basic_Level);

                foreach (
                    var group in listSkills
                        .Groups.Cast<ListViewGroup>()
                        .Where(group => Convert.ToInt32(group.Tag) == skill.Record.ReqCommon_Mastery1)
                )
                    item.Group = group;

                if (skill.IsAttack && checkShowAttacks.Checked)
                    listSkills.Items.Add(item);
                else if (!skill.IsAttack && !skill.IsImbue && checkShowBuffs.Checked)
                    listSkills.Items.Add(item);

                item.LoadSkillImage();
            }

            listSkills.EndUpdate();
        }
    }

    /// <summary>
    ///     Saves the attacks.
    /// </summary>
    private void SaveAttacks()
    {
        var savedSkills = listAttackingSkills.Items.Cast<ListViewItem>().Select(p => ((SkillInfo)p.Tag).Id).ToArray();

        SkillsManager.SaveSkills("RSBot.Skills.Attacks_" + comboMonsterType.SelectedIndex, savedSkills);
        SkillsManager.ApplyAttackSkills();
    }

    /// <summary>
    ///     Saves the buffs.
    /// </summary>
    private void SaveBuffs()
    {
        var savedBuffs = listBuffs.Items.Cast<ListViewItem>().Select(p => ((SkillInfo)p.Tag).Id).ToArray();

        SkillsManager.SaveSkills("RSBot.Skills.Buffs", savedBuffs);
        SkillsManager.ApplyBuffSkills();
    }

    /// <summary>
    ///     Run the event after added the buff from the character
    /// </summary>
    /// <param name="buffInfo">The added <see cref="BuffInfo" /></param>
    private void OnAddBuff(SkillInfo buffInfo)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<SkillInfo>(OnAddBuff), buffInfo);
            return;
        }
        try
        {
            var item = new ListViewItem { Text = buffInfo.Record.GetRealName(), Tag = buffInfo };

            item.SubItems.Add("lv. " + buffInfo.Record.Basic_Level);

            listActiveBuffs.Items.Add(item);
            item.LoadSkillImageAsync();
        }
        catch { }
    }

    /// <summary>
    ///     Run the event after removed the buff from the character
    /// </summary>
    /// <param name="buffInfo">The removed <see cref="BuffInfo" /></param>
    private void OnRemoveBuff(SkillInfo removingBuff)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<SkillInfo>(OnRemoveBuff), removingBuff);
            return;
        }
        try
        {
            for (var i = 0; i < listActiveBuffs.Items.Count; i++)
            {
                var listItem = listActiveBuffs.Items[i];
                if (listItem == null)
                    continue;

                var itemBuffInfo = listItem.Tag as SkillInfo;
                if (
                    itemBuffInfo != null
                    /*&& itemBuffInfo.Id == removingBuff.Id*/
                    && itemBuffInfo.Token == removingBuff.Token
                )
                {
                    listItem?.Remove();
                    return;
                }
            }
        }
        catch { }
    }

    /// <summary>
    ///     Call after skill learned
    /// </summary>
    /// <param name="learnedSkill"></param>
    private void OnSkillLearned(SkillInfo learnedSkill)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<SkillInfo>(OnSkillLearned), learnedSkill);
            return;
        }
        Log.NotifyLang("SkillLearned", learnedSkill.Record.GetRealName());
        LoadSkills();
    }

    /// <summary>
    ///     Call after learned skill upgraded
    /// </summary>
    /// <param name="skill">The old skill.</param>
    /// <param name="newSkill">The new skill.</param>
    private void OnSkillUpgraded(SkillInfo oldSkill, SkillInfo newSkill)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<SkillInfo,SkillInfo>(OnSkillUpgraded), oldSkill, newSkill);
            return;
        }
        LoadSkills();
    }

    /// <summary>
    ///     Core_s the on withdraw skill.
    /// </summary>
    /// <param name="oldSkill">The old skill.</param>
    /// <param name="newSkill">The new skill.</param>
    private void OnWithdrawSkill(SkillInfo oldSkill, SkillInfo newSkill)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<SkillInfo, SkillInfo>(OnWithdrawSkill), oldSkill, newSkill);
            return;
        }
        LoadSkills();
    }

    /// <summary>
    ///     Core_s the on learn skill mastery.
    /// </summary>
    /// <param name="info">The information.</param>
    private void OnLearnSkillMastery(MasteryInfo info)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<MasteryInfo>(OnLearnSkillMastery), info);
            return;
        }
        Log.NotifyLang("MasteryUpgraded", info.Record.Name);

        LoadSkills();
    }

    /// <summary>
    ///     Main_s the on load character.
    /// </summary>
    private void OnLoadCharacter()
    {
        if (this.InvokeRequired)
        {
            this.Invoke(OnLoadCharacter);
            return;
        }
        comboMonsterType.SelectedIndex = 0;

        LoadSkills();

        listActiveBuffs.Items.Clear();
    }

    /// <summary>
    ///     Handles the Click event of the btnMoveAttackSkillDown control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnMoveAttackSkillDown_Click(object sender, EventArgs e)
    {
        listAttackingSkills.MoveSelectedItems(MoveDirection.Down);
        SaveAttacks();
    }

    /// <summary>
    ///     Handles the Click event of the btnMoveAttackSkillUp control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnMoveAttackSkillUp_Click(object sender, EventArgs e)
    {
        listAttackingSkills.MoveSelectedItems(MoveDirection.Up);
        SaveAttacks();
    }

    /// <summary>
    ///     Handles the Click event of the btnMoveBuffSkillDown control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnMoveBuffSkillDown_Click(object sender, EventArgs e)
    {
        listBuffs.MoveSelectedItems(MoveDirection.Down);
        SaveBuffs();
    }

    /// <summary>
    ///     Handles the Click event of the btnMoveBuffSkillUp control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnMoveBuffSkillUp_Click(object sender, EventArgs e)
    {
        listBuffs.MoveSelectedItems(MoveDirection.Up);
        SaveBuffs();
    }

    /// <summary>
    ///     Handles the Click event of the btnRemoveAttackSkill control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnRemoveAttackSkill_Click(object sender, EventArgs e)
    {
        foreach (ListViewItem item in listAttackingSkills.SelectedItems)
            item.Remove();

        SaveAttacks();
    }

    /// <summary>
    ///     Handles the Click event of the btnRemoveBuffSkill control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnRemoveBuffSkill_Click(object sender, EventArgs e)
    {
        foreach (ListViewItem item in listBuffs.SelectedItems)
            item.Remove();

        SaveBuffs();
    }

    /// <summary>
    ///     Handles the SelectedIndexChanged event of the comboImue control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void comboImbue_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (comboImbue.SelectedIndex < 0)
            return;

        SkillInfo imbue = null;

        if (comboImbue.SelectedIndex > 0)
            imbue = comboImbue.SelectedItem as SkillInfo;

        SkillsManager.SetImbueSkill(imbue);
    }

    /// <summary>
    ///     Handles the SelectedIndexChanged event of the comboMonsterType control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void comboMonsterType_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadAttacks(comboMonsterType.SelectedIndex);
    }

    /// <summary>
    ///     Handles the SelectedIndexChanged event of the comboResurrectionSkill control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void comboResurrectionSkill_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (comboResurrectionSkill.SelectedIndex < 0)
            return;

        SkillInfo skill = null;

        if (comboResurrectionSkill.SelectedIndex > 0)
            skill = comboResurrectionSkill.SelectedItem as SkillInfo;
        SkillsManager.SetResurrectionSkill(skill);
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the filters control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void Filter_CheckedChanged(object sender, EventArgs e)
    {
        if (_settingsLoaded)
            ApplySettings();

        LoadSkills();
    }

    /// <summary>
    ///     Handles the Click event of the menuAddAttack control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void menuAddAttack_Click(object sender, EventArgs e)
    {
        foreach (ListViewItem item in listSkills.SelectedItems)
        {
            var selectedRefSkill = item.Tag as SkillInfo;

            if (
                listAttackingSkills
                    .Items.Cast<ListViewItem>()
                    .Any(p =>
                        ((SkillInfo)p.Tag).Record.Action_Overlap != 0
                        && ((SkillInfo)p.Tag).Record.Action_Overlap == selectedRefSkill.Record.Action_Overlap
                    )
            )
                continue;

            //if (selectedRefSkill != null && selectedRefSkill.IsAttack)
            if (selectedRefSkill != null && (selectedRefSkill.Record.TargetGroup_Enemy_M || selectedRefSkill.IsAttack))
                listAttackingSkills.Items.Add((ListViewItem)item.Clone());
        }

        SaveAttacks();
    }

    /// <summary>
    ///     Handles the Click event of the menuAddBuff control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void menuAddBuff_Click(object sender, EventArgs e)
    {
        foreach (ListViewItem item in listSkills.SelectedItems)
        {
            var selectedRefSkill = item.Tag as SkillInfo;
            if (
                listBuffs
                    .Items.Cast<ListViewItem>()
                    .Any(p =>
                        ((SkillInfo)p.Tag).Record.Action_Overlap != 0
                        && ((SkillInfo)p.Tag).Record.Action_Overlap == selectedRefSkill.Record.Action_Overlap
                    )
            )
                continue;

            if (selectedRefSkill != null && !selectedRefSkill.IsAttack && !selectedRefSkill.Record.TargetGroup_Enemy_M)
                listBuffs.Items.Add((ListViewItem)item.Clone());
        }

        SaveBuffs();
    }

    private void comboLearnMastery_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (comboLearnMastery.SelectedIndex < 0)
            return;

        var selectedItem = (MasteryComboBoxItem)comboLearnMastery.SelectedItem;
        _selectedMastery = selectedItem;

        SkillsManager.SetMasteryToLearn(selectedItem.Record.NameCode);
    }

    private void listSkills_MouseDoubleClick(object sender, MouseEventArgs e)
    {
        if (listSkills.SelectedItems.Count <= 0)
            return;

        if (!Kernel.Debug)
            return;

        if (listSkills.SelectedItems[0].Tag is not SkillInfo skillInfo)
            return;

        var itemForm = new SkillProperties(skillInfo.Record);
        itemForm.Show();
    }

    private void useToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (listSkills.SelectedItems.Count <= 0)
            return;

        if (listSkills.SelectedItems[0].Tag is not SkillInfo skillInfo)
            return;

        if (skillInfo.IsAttack)
            return;

        skillInfo.Cast(buff: true);
    }

    private void skillContextMenu_Opening(object sender, CancelEventArgs e)
    {
        useToPartyMemberToolStripMenuItem.DropDownItems.Clear();

        if (!Game.Party.IsInParty)
            return;

        foreach (var member in Game.Party.Members)
        {
            if (member == null)
                return;

            useToPartyMemberToolStripMenuItem.DropDown.Items.Add(
                member.Name,
                null,
                (menuItemSender, _2) =>
                {
                    try
                    {
                        if (listSkills.SelectedItems.Count <= 0)
                            return;

                        if (listSkills.SelectedItems[0].Tag is not SkillInfo skillInfo)
                            return;

                        if (skillInfo.IsAttack)
                            return;

                        var menuItem = menuItemSender as ToolStripMenuItem;
                        if (menuItem == null)
                            return;

                        var member = Game.Party.GetMemberByName(menuItem.Text);
                        if (member == null)
                            return;

                        skillInfo.Cast(member.Player.UniqueId, true);
                    }
                    catch { }
                }
            );
        }
    }

    private void listActiveBuffs_MouseDoubleClick(object sender, MouseEventArgs e)
    {
        if (!Kernel.Debug)
            return;

        var propertiesWindow = listActiveBuffs.SelectedItems[0].Tag switch
        {
            SkillInfo skillInfo => new BuffProperties(skillInfo),
            ItemPerk itemPerk => new BuffProperties(itemPerk),
            _ => null,
        };

        propertiesWindow?.Show();
    }

    private void comboTeleportSkill_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (comboTeleportSkill.SelectedItem is not TeleportSkillComboBoxItem comboItem)
            return;

        SkillsManager.SetTeleportSkill(comboItem.Record.ID);
    }

    /// <summary>
    ///     Occurs before Main form is displayed.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Main_Load(object sender, EventArgs e)
    {
        _settingsLoaded = false;
        LoadSettings();
        _settingsLoaded = true;
    }

    private class MasteryComboBoxItem
    {
        public byte Level;
        public RefSkillMastery Record;

        public override string ToString()
        {
            return Record.Name + $" lv.{Level}";
        }
    }

    private class TeleportSkillComboBoxItem
    {
        public byte Level;
        public RefSkill Record;

        public override string ToString()
        {
            return Record.GetRealName() + $" lv.{Level}";
        }
    }

    #region Fields

    private readonly object _lock;
    private MasteryComboBoxItem _selectedMastery;
    private bool _settingsLoaded;

    #endregion Fields
}
