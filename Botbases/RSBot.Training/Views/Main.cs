using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using RSBot.Core;
using RSBot.Core.Event;
using RSBot.Core.Objects;
using RSBot.Training.Views.Dialogs;
using SDUI.Controls;
using CheckBox = SDUI.Controls.CheckBox;

namespace RSBot.Training.Views;

[ToolboxItem(false)]
public partial class Main : DoubleBufferedControl
{
    private const int ScriptRecorderOwnerId = 2000;

    #region Fields

    private bool _settingsLoaded;

    #endregion Fields

    /// <summary>
    ///     Initializes a new instance of the <see cref="Main" /> class.
    /// </summary>
    public Main()
    {
        InitializeComponent();
        InitializeCustomComponents();
        SubscribeEvents();

        lvAvoidance.Items[0].Tag = MonsterRarity.General;
        lvAvoidance.Items[1].Tag = MonsterRarity.Champion;
        lvAvoidance.Items[2].Tag = MonsterRarity.Giant;
        lvAvoidance.Items[3].Tag = MonsterRarity.GeneralParty;
        lvAvoidance.Items[4].Tag = MonsterRarity.ChampionParty;
        lvAvoidance.Items[5].Tag = MonsterRarity.GiantParty;
        lvAvoidance.Items[6].Tag = MonsterRarity.Unique | MonsterRarity.Unique2;
        lvAvoidance.Items[7].Tag = MonsterRarity.EliteStrong;
        lvAvoidance.Items[8].Tag = MonsterRarity.Elite;
        lvAvoidance.Items[9].Tag = MonsterRarity.Event;
    }

    /// <summary>
    ///     Subscribes the events.
    /// </summary>
    private void SubscribeEvents()
    {
        EventManager.SubscribeEvent("OnLoadCharacter", OnLoadCharacter);
        EventManager.SubscribeEvent("OnSetTrainingArea", OnSetTrainingArea);
        EventManager.SubscribeEvent("OnSaveScript", new Action<int, string>(OnSaveScript));
    }

    /// <summary>
    ///     Loads the settings.
    /// </summary>
    private void LoadSettings()
    {
        foreach (var checkbox in groupBoxAdvanced.Controls.OfType<CheckBox>())
        {
            switch (checkbox.Name)
            {
                case "checkBoxDontFollowMobs":
                    checkbox.Checked = PlayerConfig.Profile.Training.AttackWeakerFirst;
                    break;
                case "checkAttackWeakerFirst":
                    checkbox.Checked = PlayerConfig.Profile.Training.DontFollowMobs;
                    break;
                case "checkBoxDimensionPillar":
                    checkbox.Checked = PlayerConfig.Profile.Training.IgnoreDimensionPillar;
                    break;
            }
        }

        foreach (var checkbox in groupBoxBerserk.Controls.OfType<CheckBox>())
        {
            switch (checkbox.Name)
            {
                case "checkBerzerkWhenFull":
                    checkbox.Checked = PlayerConfig.Profile.Training.BerserkWhenFull;
                    break;
                case "checkBerserkOnMonsterRarity":
                    checkbox.Checked = PlayerConfig.Profile.Training.BerserkOnMonsterRarity;
                    break;
                case "checkBerzerkMonsterAmount":
                    checkbox.Checked = PlayerConfig.Profile.Training.BerserkOnMonsterAmount;
                    break;
                case "checkBerzerkAvoidance":
                    checkbox.Checked = PlayerConfig.Profile.Training.BerserkAvoidance;
                    break;
            }
        }

        foreach (var num in groupBoxBerserk.Controls.OfType<NumUpDown>())
        {
            switch (num.Name)
            {
                case "numBerzerkMonsterAmount":
                    num.Value = (decimal)PlayerConfig.Profile.Training.MinBerserkMonsterAmount;
                    break;
            }
        }

        foreach (var checkbox in groupBoxWalkback.Controls.OfType<CheckBox>())
        {
            switch (checkbox.Name)
            {
                case "checkCastBuffs":
                    checkbox.Checked = PlayerConfig.Profile.Training.CastBuffs;
                    break;
                case "checkUseMount":
                    checkbox.Checked = PlayerConfig.Profile.Training.UseMount;
                    break;
                case "checkUseSpeedDrug":
                    checkbox.Checked = PlayerConfig.Profile.Training.UseSpeedDrug;
                    break;
                case "checkBoxUseReverse":
                    checkbox.Checked = PlayerConfig.Profile.Training.UseReverseScroll;
                    break;
            }
        }

        radioCenter.Checked = PlayerConfig.Profile.Training.ReturnToCenter;
        radioWalkAround.Checked = PlayerConfig.Profile.Training.WalkAround;
        LoadAvoidance();
    }

    /// <summary>
    ///     Saves the settings.
    /// </summary>
    private void ApplySettings()
    {
        foreach (var checkbox in groupBoxAdvanced.Controls.OfType<CheckBox>())
        {
            switch (checkbox.Name)
            {
                case "checkBoxDontFollowMobs":
                    PlayerConfig.Profile.Training.AttackWeakerFirst = checkbox.Checked;
                    break;
                case "checkAttackWeakerFirst":
                    PlayerConfig.Profile.Training.DontFollowMobs = checkbox.Checked;
                    break;
                case "checkBoxDimensionPillar":
                    PlayerConfig.Profile.Training.IgnoreDimensionPillar = checkbox.Checked;
                    break;
            }
        }

        foreach (var checkbox in groupBoxBerserk.Controls.OfType<CheckBox>())
        {
            switch (checkbox.Name)
            {
                case "checkBerzerkWhenFull":
                    PlayerConfig.Profile.Training.BerserkWhenFull = checkbox.Checked;
                    break;
                case "checkBerserkOnMonsterRarity":
                    PlayerConfig.Profile.Training.BerserkOnMonsterRarity = checkbox.Checked;
                    break;
                case "checkBerzerkMonsterAmount":
                    PlayerConfig.Profile.Training.BerserkOnMonsterAmount = checkbox.Checked;
                    break;
                case "checkBerzerkAvoidance":
                    PlayerConfig.Profile.Training.BerserkAvoidance = checkbox.Checked;
                    break;
            }
        }

        foreach (var num in groupBoxBerserk.Controls.OfType<NumUpDown>())
        {
            switch (num.Name)
            {
                case "numBerzerkMonsterAmount":
                    PlayerConfig.Profile.Training.MinBerserkMonsterAmount = (byte)num.Value;
                    break;
            }
        }

        foreach (var checkbox in groupBoxWalkback.Controls.OfType<CheckBox>())
        {
            switch (checkbox.Name)
            {
                case "checkCastBuffs":
                    PlayerConfig.Profile.Training.CastBuffs = checkbox.Checked;
                    break;
                case "checkUseMount":
                    PlayerConfig.Profile.Training.UseMount = checkbox.Checked;
                    break;
                case "checkUseSpeedDrug":
                    PlayerConfig.Profile.Training.UseSpeedDrug = checkbox.Checked;
                    break;
                case "checkBoxUseReverse":
                    PlayerConfig.Profile.Training.UseReverseScroll = checkbox.Checked;
                    break;
            }
        }

        PlayerConfig.Profile.Training.ReturnToCenter = radioCenter.Checked;
        PlayerConfig.Profile.Training.WalkAround = radioWalkAround.Checked;

        PlayerConfig.Save();
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
    ///     Called when the script recorder saves a script.
    /// </summary>
    /// <param name="ownerId"></param>
    /// <param name="path"></param>
    private void OnSaveScript(int ownerId, string path)
    {
        if (IsDisposed || Disposing)
            return;

        if (ownerId != ScriptRecorderOwnerId)
            return;

        txtWalkscript.Text = path;
        PlayerConfig.Profile.Training.WalkbackFile = path;
        PlayerConfig.Save();
    }

    /// <summary>
    ///     Called when the training area has been set to a certain location. Will append a new script command "area" and
    ///     update the UI.
    /// </summary>
    private void OnSetTrainingArea()
    {
        if (IsDisposed || Disposing)
            return;

        var area = Kernel.Bot.Botbase.Area;

        txtXCoord.Text = area.Position.X.ToString("0.0");
        txtYCoord.Text = area.Position.Y.ToString("0.0");
        txtRadius.Text = area.Radius.ToString();
        txtRegion.Text = area.Position.Region.Id.ToString();

        EventManager.FireEvent("AppendScriptCommand", area.GetScriptLine());
    }

    /// <summary>
    ///     Saves the avoidance.
    /// </summary>
    private void SaveAvoidance()
    {
        var avoid = new List<MonsterRarity>();
        var prefer = new List<MonsterRarity>();
        var berserk = new List<MonsterRarity>();
        foreach (ListViewItem item in lvAvoidance.Items)
            if (item.Group == lvAvoidance.Groups["grpAvoid"])
                avoid.Add((MonsterRarity)item.Tag);
            else if (item.Group == lvAvoidance.Groups["grpPrefer"])
                prefer.Add((MonsterRarity)item.Tag);
            else if (item.Group == lvAvoidance.Groups["grpBerserk"])
                berserk.Add((MonsterRarity)item.Tag);

        PlayerConfig.Profile.Avoidance.AvoidMonsters = avoid;
        PlayerConfig.Profile.Avoidance.PreferMonsters = prefer;
        PlayerConfig.Profile.Avoidance.BerserkMonsters = berserk;

        PlayerConfig.Save();
    }

    /// <summary>
    ///     Loads the avoidance.
    /// </summary>
    private void LoadAvoidance()
    {
        var prefer = (PlayerConfig.Profile.Avoidance.PreferMonsters ?? new List<MonsterRarity>()).ToLookup(
            p => "Prefer",
            p => p
        );
        var avoid = (PlayerConfig.Profile.Avoidance.AvoidMonsters ?? new List<MonsterRarity>()).ToLookup(
            p => "Avoid",
            p => p
        );
        var berserk = (PlayerConfig.Profile.Avoidance.BerserkMonsters ?? new List<MonsterRarity>()).ToLookup(
            p => "Berserk",
            p => p
        );

        foreach (var group in avoid.Union(prefer).Union(berserk))
        foreach (var item in group)
        {
            var listViewItem = lvAvoidance
                .Items.Cast<ListViewItem>()
                .FirstOrDefault(p => ((MonsterRarity)p.Tag & item) == item);
            if (listViewItem == null)
                continue;

            listViewItem.Group = lvAvoidance.Groups[$"grp{group.Key}"];
        }
    }

    /// <summary>
    ///     Handles the Click event of the btnGetCurrent control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnGetCurrent_Click(object sender, EventArgs e)
    {
        var pos = Game.Player.Position;

        var currentArea = PlayerConfig.Profile.Training.Areas.Find(a => a.IsActive);
        if (currentArea == null)
        {
            currentArea = new Core.PlayerConfigProfile.TrainingAreaConfig { IsActive = true };
            PlayerConfig.Profile.Training.Areas.Add(currentArea);
        }

        currentArea.Region = pos.Region.Id;
        currentArea.X = pos.XOffset;
        currentArea.Y = pos.YOffset;
        currentArea.Z = pos.ZOffset;

        PlayerConfig.Save();

        EventManager.FireEvent("OnSetTrainingArea");
    }

    /// <summary>
    ///     Handles the TextChanged event of the txtXCoord control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void txtXCoord_TextChanged(object sender, EventArgs e)
    {
        if (!float.TryParse(txtXCoord.Text, out var result))
            return;
    }

    /// <summary>
    ///     Handles the TextChanged event of the txtYCoord control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void txtYCoord_TextChanged(object sender, EventArgs e)
    {
        if (!float.TryParse(txtYCoord.Text, out var result))
            return;
    }

    /// <summary>
    ///     Handles the TextChanged event of the txtRadius control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void txtRadius_TextChanged(object sender, EventArgs e)
    {
        if (!int.TryParse(txtRadius.Text, out var result))
            return;

        var currentArea = PlayerConfig.Profile.Training.Areas.Find(a => a.IsActive);

        if (currentArea == null)
        {
            currentArea = new Core.PlayerConfigProfile.TrainingAreaConfig { Name = "Default", IsActive = true };
            PlayerConfig.Profile.Training.Areas.Add(currentArea);
        }

        PlayerConfig.Profile.Training.Areas.Find(a => a.IsActive).Radius = result;
        PlayerConfig.Save();
    }

    /// <summary>
    ///     Handles the TextChanged event of the txtRegion control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void txtRegion_TextChanged(object sender, EventArgs e)
    {
        if (!ushort.TryParse(txtRegion.Text, out ushort result))
            return;
    }

    /// <summary>
    ///     Handles the Click event of the btnApplyArea control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnApplyArea_Click(object sender, EventArgs e)
    {
        if (
            !float.TryParse(txtYCoord.Text, out var y)
            || !float.TryParse(txtXCoord.Text, out var x)
            || !ushort.TryParse(txtRegion.Text, out ushort region)
        )
        {
            Log.Warn("[Training area] Check that the X, Y, and Region coordinates have been entered correctly.");
            return;
        }

        TrainingManager.ApplyTrainingArea(x, y, region);
    }

    /// <summary>
    ///     Handles the Click event of the btnBrowse control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnBrowse_Click(object sender, EventArgs e)
    {
        var diag = new OpenFileDialog
        {
            Filter = @"RSBot Bot script (*.rbs)|*.rbs",
            Title = @"Browse for a walkback script",
        };

        if (diag.ShowDialog() != DialogResult.OK)
            return;

        txtWalkscript.Text = diag.FileName;
        PlayerConfig.Profile.Training.WalkbackFile = txtWalkscript.Text;
        PlayerConfig.Save();
        btnRemove.Visible = true;
    }

    /// <summary>
    ///     Handles the Click event of the btnAvoid control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs" /> instance containing the event data.</param>
    private void btnAvoid_Click(object sender, EventArgs e)
    {
        if (lvAvoidance.SelectedItems.Count <= 0)
            return;

        foreach (ListViewItem item in lvAvoidance.SelectedItems)
            item.Group = lvAvoidance.Groups["grpAvoid"];

        SaveAvoidance();
    }

    /// <summary>
    ///     Handles the Click event of the btnPrefer control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnPrefer_Click(object sender, EventArgs e)
    {
        if (lvAvoidance.SelectedItems.Count <= 0)
            return;
        foreach (ListViewItem item in lvAvoidance.SelectedItems)
            item.Group = lvAvoidance.Groups["grpPrefer"];

        SaveAvoidance();
    }

    /// <summary>
    ///     Handles the Click event of the btnBerserk control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnBerserk_Click(object sender, EventArgs e)
    {
        if (lvAvoidance.SelectedItems.Count <= 0)
            return;
        foreach (ListViewItem item in lvAvoidance.SelectedItems)
            item.Group = lvAvoidance.Groups["grpBerserk"];

        SaveAvoidance();
    }

    /// <summary>
    ///     Handles the Click event of the btnNoCustomBehavior control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnNoCustomBehavior_Click(object sender, EventArgs e)
    {
        if (lvAvoidance.SelectedItems.Count <= 0)
            return;
        foreach (ListViewItem item in lvAvoidance.SelectedItems)
            item.Group = lvAvoidance.Groups["grpNone"];

        SaveAvoidance();
    }

    /// <summary>
    /// </summary>
    private void OnLoadCharacter()
    {
        if (IsDisposed || Disposing)
            return;

        var area = Kernel.Bot.Botbase.Area;
        //Training Area
        txtXCoord.Text = area.Position.X.ToString("0.0");
        txtYCoord.Text = area.Position.Y.ToString("0.0");
        txtRadius.Text = area.Radius.ToString();
        txtRegion.Text = area.Position.Region.Id.ToString();
        //Walkback
        txtWalkscript.Text = PlayerConfig.Profile.Training.WalkbackFile;
        btnRemove.Visible = !string.IsNullOrEmpty(txtWalkscript.Text);
    }

    private void buttonSelectTrainingArea_Click(object sender, EventArgs e)
    {
        var trainingArea = new TrainingAreasDialog();
        if (trainingArea.ShowDialog(this) == DialogResult.OK)
            EventManager.FireEvent("OnSetTrainingArea");
    }

    private void linkAttackWeakerMobsHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        MessageBox.Show(
            "If the player is under attack by a monster that is set to be avoided the bot will counter attack weaker mobs that are currently attacking the player first before targeting the avoided monster again. The bot will only kill weaker monsters that are attacking the player and won't start to pull new mobs to the battle.",
            "Attack weaker mobs first",
            MessageBoxButtons.OK,
            MessageBoxIcon.Question
        );
    }

    private void linkRecord_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        EventManager.FireEvent("OnShowScriptRecorder", ScriptRecorderOwnerId, true);
    }

    private void Main_Load(object sender, EventArgs e)
    {
        _settingsLoaded = false;
        LoadSettings();
        _settingsLoaded = true;
    }

    private void btnRemove_Click(object sender, EventArgs e)
    {
        txtWalkscript.Text = string.Empty;
        PlayerConfig.Profile.Training.WalkbackFile = txtWalkscript.Text;
        PlayerConfig.Save();
        btnRemove.Visible = false;
    }

    /// <summary>
    ///    Initializes custom components that are not handled by the designer.
    /// </summary>
    private void InitializeCustomComponents()
    {
        var btnRefetchNavLink = new SDUI.Controls.Button
        {
            Text = "Update NavLink",
            Size = new System.Drawing.Size(110, 23),
            Location = new System.Drawing.Point(232, 27),
            Color = System.Drawing.Color.Transparent,
            Radius = 6,
            ShadowDepth = 4F,
        };

        btnRefetchNavLink.Click += async (s, e) =>
        {
            btnRefetchNavLink.Enabled = false;
            btnRefetchNavLink.Text = "Updating...";

            await Bot.NavigationManager.FetchRemoteLinkageData();

            btnRefetchNavLink.Text = "Update NavLink";
            btnRefetchNavLink.Enabled = true;
        };

        groupBoxWalkback.Controls.Add(btnRefetchNavLink);
    }
}
