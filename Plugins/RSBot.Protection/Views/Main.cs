using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using RSBot.Core;
using RSBot.Core.Event;
using RSBot.Core.Objects.Skill;
using RSBot.Protection.Components.Player;
using SDUI.Controls;
using CheckBox = SDUI.Controls.CheckBox;

namespace RSBot.Protection.Views;

[ToolboxItem(false)]
public partial class Main : DoubleBufferedControl
{
    public Main()
    {
        InitializeComponent();
        SubscribeEvents();
    }

    /// <summary>
    ///     Subscribes the events.
    /// </summary>
    private void SubscribeEvents()
    {
        EventManager.SubscribeEvent("OnLoadCharacter", OnLoadCharacter);

        EventManager.SubscribeEvent("OnSkillLearned", new Action<SkillInfo>(OnSkillLearned));
        EventManager.SubscribeEvent("OnSkillUpgraded", new Action<SkillInfo, SkillInfo>(OnSkillUpgraded));

        EventManager.SubscribeEvent("OnIncreaseStrength", OnIncreaseStat);
        EventManager.SubscribeEvent("OnIncreaseIntelligence", OnIncreaseStat);
    }

    /// <summary>
    ///     Loads the settings.
    /// </summary>
    private void LoadSettings()
    {
        var config = PlayerConfig.Profile.Protection;

        // groupHPMP
        checkUseHPPotionsPlayer.Checked = config.UseHpPotions;
        numPlayerHPPotionMin.Value = config.MinHpPotionPercent;

        checkUseMPPotionsPlayer.Checked = config.UseMpPotions;
        numPlayerMPPotionMin.Value = config.MinMpPotionPercent;

        checkUseVigorHP.Checked = config.UseVigorHp;
        numPlayerHPVigorPotionMin.Value = config.MinPlayerHpVigorPercent;

        checkUseVigorMP.Checked = config.UseVigorMp;
        numPlayerMPVigorPotionMin.Value = config.MinPlayerMpVigorPercent;

        checkUseSkillHP.Checked = config.UseSkillHp;
        numPlayerSkillHPMin.Value = config.MinSkillHpPercent;

        checkUseSkillMP.Checked = config.UseSkillMp;
        numPlayerSkillMPMin.Value = config.MinSkillMpPercent;

        // groupBadStatus
        checkUseUniversalPills.Checked = config.UseUniversalPills;
        checkUseBadStatusSkill.Checked = config.UseBadStatusSkill;

        // groupPet
        checkUsePetHP.Checked = config.UsePetHp;
        numPetMinHP.Value = config.MinPetHpPercent;

        checkUseHGP.Checked = config.UsePetHgp;
        numPetMinHGP.Value = config.MinPetHgpPercent;

        checkAutoSummonAttackPet.Checked = config.AutoSummonAttackPet;
        checkReviveAttackPet.Checked = config.AutoReviveAttackPet;
        checkUseAbnormalStatePotion.Checked = config.UseAbnormalStatePotion;

        // groupBackTown
        checkDead.Checked = config.CheckDead;
        numDeadTimeout.Value = config.DeadTimeoutSeconds;

        checkDurability.Checked = config.CheckDurability;
        checkInventory.Checked = config.CheckInventoryFull;
        checkNoArrows.Checked = config.CheckNoArrows;

        checkNoHPPotions.Checked = config.CheckNoHpPotions;
        numHPPotionsLeft.Value = config.MinHpPotionsLeft;

        checkNoMPPotions.Checked = config.CheckNoMpPotions;
        numMPPotionsLeft.Value = config.MinMpPotionsLeft;

        checkLevelUp.Checked = config.CheckLevelUp;
        checkFullPetInventory.Checked = config.ReturnOnFullPetInventory;
        checkStopBotOnReturnToTown.Checked = config.StopBotOnReturnToTown;

        checkShardFatigue.Checked = config.CheckShardFatigue;
        numShardFatigueMinToDC.Value = config.ShardFatigueMinToDc;

        // groupStatPoints
        checkIncBotStopped.Checked = config.IncreaseWhenBotStopped;
        checkIncStr.Checked = config.AutoIncreaseStr;
        checkIncInt.Checked = config.AutoIncreaseInt;

        numIncStr.Maximum = 3;
        numIncInt.Maximum = 3;
        numIncInt.Value = Math.Clamp(config.PointsToInt, (int)numIncInt.Minimum, 3);
        numIncStr.Value = Math.Clamp(config.PointsToStr, (int)numIncStr.Minimum, 3 - (int)numIncInt.Value);
        numIncStr.Maximum = 3 - numIncInt.Value;
        numIncInt.Maximum = 3 - numIncStr.Value;
    }

    /// <summary>
    ///     Saves the settings.
    /// </summary>
    private void ApplySettings()
    {
        var config = PlayerConfig.Profile.Protection;

        // groupHPMP
        config.UseHpPotions = checkUseHPPotionsPlayer.Checked;
        config.MinHpPotionPercent = (int)numPlayerHPPotionMin.Value;

        config.UseMpPotions = checkUseMPPotionsPlayer.Checked;
        config.MinMpPotionPercent = (int)numPlayerMPPotionMin.Value;

        config.UseVigorHp = checkUseVigorHP.Checked;
        config.MinPlayerHpVigorPercent = (int)numPlayerHPVigorPotionMin.Value;

        config.UseVigorMp = checkUseVigorMP.Checked;
        config.MinPlayerMpVigorPercent = (int)numPlayerMPVigorPotionMin.Value;

        config.UseSkillHp = checkUseSkillHP.Checked;
        config.MinSkillHpPercent = (int)numPlayerSkillHPMin.Value;

        config.UseSkillMp = checkUseSkillMP.Checked;
        config.MinSkillMpPercent = (int)numPlayerSkillMPMin.Value;

        // groupBadStatus
        config.UseUniversalPills = checkUseUniversalPills.Checked;
        config.UseBadStatusSkill = checkUseBadStatusSkill.Checked;

        // groupPet
        config.UsePetHp = checkUsePetHP.Checked;
        config.MinPetHpPercent = (int)numPetMinHP.Value;

        config.UsePetHgp = checkUseHGP.Checked;
        config.MinPetHgpPercent = (int)numPetMinHGP.Value;

        config.AutoSummonAttackPet = checkAutoSummonAttackPet.Checked;
        config.AutoReviveAttackPet = checkReviveAttackPet.Checked;
        config.UseAbnormalStatePotion = checkUseAbnormalStatePotion.Checked;

        // groupBackTown
        config.CheckDead = checkDead.Checked;
        config.DeadTimeoutSeconds = (int)numDeadTimeout.Value;

        config.CheckDurability = checkDurability.Checked;
        config.CheckInventoryFull = checkInventory.Checked;
        config.CheckNoArrows = checkNoArrows.Checked;

        config.CheckNoHpPotions = checkNoHPPotions.Checked;
        config.MinHpPotionsLeft = (int)numHPPotionsLeft.Value;

        config.CheckNoMpPotions = checkNoMPPotions.Checked;
        config.MinMpPotionsLeft = (int)numMPPotionsLeft.Value;

        config.CheckLevelUp = checkLevelUp.Checked;
        config.ReturnOnFullPetInventory = checkFullPetInventory.Checked;
        config.StopBotOnReturnToTown = checkStopBotOnReturnToTown.Checked;

        config.CheckShardFatigue = checkShardFatigue.Checked;
        config.ShardFatigueMinToDc = (int)numShardFatigueMinToDC.Value;

        // groupStatPoints
        config.IncreaseWhenBotStopped = checkIncBotStopped.Checked;
        config.AutoIncreaseStr = checkIncStr.Checked;
        config.PointsToStr = (int)numIncStr.Value;
        config.AutoIncreaseInt = checkIncInt.Checked;
        config.PointsToInt = (int)numIncInt.Value;

        if (_skillSettingsLoaded)
        {
            config.HpSkillId = (comboSkillPlayerHP.SelectedItem as SkillInfo)?.Id ?? 0;
            config.MpSkillId = (comboSkillPlayerMP.SelectedItem as SkillInfo)?.Id ?? 0;
            config.BadStatusSkillId = (comboSkillBadStatus.SelectedItem as SkillInfo)?.Id ?? 0;
        }

        PlayerConfig.Save();
    }

    /// <summary>
    ///     Refreshes the skills.
    /// </summary>
    private void RefreshSkills()
    {
        if (InvokeRequired)
        {
            Invoke(RefreshSkills);
            return;
        }

        _skillSettingsLoaded = false;

        comboSkillBadStatus.Items.Clear();
        comboSkillPlayerHP.Items.Clear();
        comboSkillPlayerMP.Items.Clear();

        if (Game.Player?.Skills?.KnownSkills == null)
        {
            _skillSettingsLoaded = true;
            return;
        }

        var config = PlayerConfig.Profile.Protection;

        foreach (var skill in Game.Player.Skills.KnownSkills)
        {
            if (!skill.Enabled)
                continue;

            if (skill.IsPassive)
                continue;

            if (skill.Record.Target_Required && !skill.Record.TargetGroup_Self)
                continue;

            // TODO: Check is the cure skill?
            var index = comboSkillBadStatus.Items.Add(skill);
            if (config.BadStatusSkillId == skill.Id)
                comboSkillBadStatus.SelectedIndex = index;

            // TODO: Check is the hp skill?
            index = comboSkillPlayerHP.Items.Add(skill);
            if (config.HpSkillId == skill.Id)
                comboSkillPlayerHP.SelectedIndex = index;

            // TODO: Check is the mp skill?
            index = comboSkillPlayerMP.Items.Add(skill);
            if (config.MpSkillId == skill.Id)
                comboSkillPlayerMP.SelectedIndex = index;
        }

        _skillSettingsLoaded = true;
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
    ///     Handles the SelectedIndexChanged event of the comboSkill control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void comboSkill_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_skillSettingsLoaded)
            ApplySettings();
    }

    /// <summary>
    ///     Call after skill learned
    /// </summary>
    /// <param name="skill">The learned skill.</param>
    private void OnSkillLearned(SkillInfo skill)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<SkillInfo>(OnSkillLearned), skill);
            return;
        }

        RefreshSkills();
    }

    /// <summary>
    ///     Call after skill upgraded
    /// </summary>
    /// <param name="oldSkill">The old skill.</param>
    /// <param name="newSkill">The new skill.</param>
    private void OnSkillUpgraded(SkillInfo oldSkill, SkillInfo newSkill)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<SkillInfo, SkillInfo>(OnSkillUpgraded), oldSkill, newSkill);
            return;
        }

        var config = PlayerConfig.Profile.Protection;
        var changed = false;

        if (config.HpSkillId == oldSkill.Id)
        {
            config.HpSkillId = newSkill.Id;
            changed = true;
        }

        if (config.MpSkillId == oldSkill.Id)
        {
            config.MpSkillId = newSkill.Id;
            changed = true;
        }

        if (config.BadStatusSkillId == oldSkill.Id)
        {
            config.BadStatusSkillId = newSkill.Id;
            changed = true;
        }

        if (changed)
            PlayerConfig.Save();

        RefreshSkills();
    }

    /// <summary>
    /// </summary>
    private void OnLoadCharacter()
    {
        if (InvokeRequired)
        {
            Invoke(OnLoadCharacter);
            return;
        }

        _settingsLoaded = false;
        LoadSettings();
        _settingsLoaded = true;

        RefreshSkills();
    }

    /// <summary>
    ///     Re-calculates the max points of the Str numeric
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void numIncInt_ValueChanged(object sender, EventArgs e)
    {
        numIncStr.Maximum = 3 - numIncInt.Value;

        if (_settingsLoaded)
            ApplySettings();
    }

    /// <summary>
    ///     Re-calculates the max points of the Int numeric
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void numIncStr_ValueChanged(object sender, EventArgs e)
    {
        numIncInt.Maximum = 3 - numIncStr.Value;

        if (_settingsLoaded)
            ApplySettings();
    }

    private void OnIncreaseStat()
    {
        if (Game.Player == null)
            return;

        if (Game.Player.StatPoints < numIncInt.Value + numIncStr.Value)
        {
            buttonRun.Text = "Run";

            _statIncreaseRunning = false;
        }
    }

    private void buttonRun_Click(object sender, EventArgs e)
    {
        if (_statIncreaseRunning)
        {
            buttonRun.Text = "Run";

            _statIncreaseRunning = false;

            StatPointsHandler.CancellationRequested = true;

            return;
        }

        StatPointsHandler.CancellationRequested = false;
        var stepSize = numIncInt.Value + numIncStr.Value;

        if (stepSize == 0)
            return;
        //Only run if at least 3 stat points can be increased
        if (Game.Player.StatPoints < stepSize)
            return;

        var availableSteps = Math.Floor(Game.Player.StatPoints / stepSize);

        if (Game.Player.StatPoints == stepSize)
            availableSteps = 1;

        if (availableSteps == 0)
            return;

        Task.Run(() => StatPointsHandler.IncreaseStatPoints((int)availableSteps));

        _statIncreaseRunning = true;

        buttonRun.Text = "Cancel";
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

    #region Fields

    private bool _settingsLoaded;
    private bool _skillSettingsLoaded;
    private bool _statIncreaseRunning;

    #endregion Fields
}
