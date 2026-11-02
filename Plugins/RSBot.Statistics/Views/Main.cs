using System;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using System.Windows.Forms;
using RSBot.Core;
using RSBot.Statistics.Stats;
using RSBot.Statistics.Stats.Calculators;
using SDUI.Controls;
using CheckBox = SDUI.Controls.CheckBox;

namespace RSBot.Statistics.Views;

[ToolboxItem(false)]
public partial class Main : DoubleBufferedControl
{
    /// <summary>
    ///     The initial reset
    /// </summary>
    private bool _initialReset = true;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Main" /> class.
    /// </summary>
    public Main()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Loads the settings.
    /// </summary>
    private void LoadSettings()
    {
        foreach (var check in panelLiveFilters.Controls.OfType<CheckBox>())
        {
            switch(check.Name)
            {
                case "SPPerHour":
                    check.Checked = PlayerConfig.Profile.Statistics.SPPerHour;
                    break;
                case "ItemsPerHour":
                    check.Checked = PlayerConfig.Profile.Statistics.ItemsPerHour;
                    break;
                case "KillsPerHour":
                    check.Checked = PlayerConfig.Profile.Statistics.KillsPerHour;
                    break;
                case "GoldPerHour":
                    check.Checked = PlayerConfig.Profile.Statistics.GoldPerHour;
                    break;
                case "EXPPerHour":
                    check.Checked = PlayerConfig.Profile.Statistics.EXPPerHour;
                    break;
            }
        }

        foreach (var check in panelStaticFilters.Controls.OfType<CheckBox>())
        {
            switch(check.Name)
            {
                case "SPGained":
                    check.Checked = PlayerConfig.Profile.Statistics.SPGained;
                    break;
                case "ItemsPicked":
                    check.Checked = PlayerConfig.Profile.Statistics.ItemsPicked;
                    break;
                case "LevelUps":
                    check.Checked = PlayerConfig.Profile.Statistics.LevelUps;
                    break;
                case "Kills":
                    check.Checked = PlayerConfig.Profile.Statistics.Kills;
                    break;
                case "GoldGained":
                    check.Checked = PlayerConfig.Profile.Statistics.GoldGained;
                    break;
                case "EXPGained":
                    check.Checked = PlayerConfig.Profile.Statistics.EXPGained;
                    break;
                case "Deaths":
                    check.Checked = PlayerConfig.Profile.Statistics.Deaths;
                    break;
                case "BottingTime":
                    check.Checked = PlayerConfig.Profile.Statistics.BottingTime;
                    break;
            }
        }
    }

    /// <summary>
    ///     Saves the settings.
    /// </summary>
    private void SaveSettings()
    {
        if (_initialReset)
            return;

        foreach (CheckBox check in panelLiveFilters.Controls)
        {
            switch (check.Name)
            {
                case "SPPerHour":
                    PlayerConfig.Profile.Statistics.SPPerHour = check.Checked;
                    break;
                case "ItemsPerHour":
                    PlayerConfig.Profile.Statistics.ItemsPerHour = check.Checked;
                    break;
                case "KillsPerHour":
                    PlayerConfig.Profile.Statistics.KillsPerHour = check.Checked;
                    break;
                case "GoldPerHour":
                    PlayerConfig.Profile.Statistics.GoldPerHour = check.Checked;
                    break;
                case "EXPPerHour":
                    PlayerConfig.Profile.Statistics.EXPPerHour = check.Checked;
                    break;
            }
        }
        foreach (CheckBox check in panelStaticFilters.Controls)
        {
            switch (check.Name)
            {
                case "SPGained":
                    PlayerConfig.Profile.Statistics.SPGained = check.Checked;
                    break;
                case "ItemsPicked":
                    PlayerConfig.Profile.Statistics.ItemsPicked = check.Checked;
                    break;
                case "LevelUps":
                    PlayerConfig.Profile.Statistics.LevelUps = check.Checked;
                    break;
                case "Kills":
                    PlayerConfig.Profile.Statistics.Kills = check.Checked;
                    break;
                case "GoldGained":
                    PlayerConfig.Profile.Statistics.GoldGained = check.Checked;
                    break;
                case "EXPGained":
                    PlayerConfig.Profile.Statistics.EXPGained = check.Checked;
                    break;
                case "Deaths":
                    PlayerConfig.Profile.Statistics.Deaths = check.Checked;
                    break;
                case "BottingTime":
                    PlayerConfig.Profile.Statistics.BottingTime = check.Checked;
                    break;
            }
        }
        PlayerConfig.Save();
    }

    /// <summary>
    ///     Populates the filter list.
    /// </summary>
    private void PopulateFilterList()
    {
        var calculators = CalculatorRegistry.Calculators;
        calculators.Reverse();

        Invoke(() =>
        {
            foreach (var calculator in calculators)
            {
                var checkBox = new CheckBox
                {
                    Dock = DockStyle.Top,
                    Text = calculator.Label,
                    Name = calculator.Name,
                };

                checkBox.CheckedChanged += Filter_CheckedChanged;

                if (calculator.UpdateType == UpdateType.Live)
                    panelLiveFilters.Controls.Add(checkBox);
                else
                    panelStaticFilters.Controls.Add(checkBox);
            }
        });
    }

    /// <summary>
    ///     Populates the statistics list.
    /// </summary>
    private void PopulateStatisticsList()
    {
        lvStatistics.BeginUpdate();
        lvStatistics.Items.Clear();

        foreach (var calculator in CalculatorRegistry.Calculators)
        {
            if (!StatatisticActive(calculator.Name))
                continue;

            var lvItem = new ListViewItem(calculator.Label) { Tag = calculator };
            lvItem.SubItems.Add("0");

            switch (calculator.Group)
            {
                case StatisticsGroup.Player:
                    lvItem.Group = lvStatistics.Groups["grpPlayer"];
                    break;

                case StatisticsGroup.Loot:
                    lvItem.Group = lvStatistics.Groups["grpLoot"];
                    break;

                case StatisticsGroup.Enemy:
                    lvItem.Group = lvStatistics.Groups["grpEnemy"];
                    break;
                case StatisticsGroup.Bot:
                    lvItem.Group = lvStatistics.Groups["grpBot"];
                    break;
            }

            lvStatistics.Items.Add(lvItem);
        }

        lvStatistics.EndUpdate();
    }

    /// <summary>
    ///     Updates the statistics.
    /// </summary>
    private void UpdateStatistics()
    {
        foreach (ListViewItem item in lvStatistics.Items)
        {
            var calculator = (IStatisticCalculator)item?.Tag;

            if (calculator != null)
                item.SubItems[1].Text = string.Format(calculator.ValueFormat, calculator.GetValue());
        }
    }

    /// <summary>
    ///     Returns a value indicating if a specified statistic is active.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns></returns>
    private bool StatatisticActive(string name)
    {
        return panelLiveFilters.Controls.ContainsKey(name)
            ? ((CheckBox)panelLiveFilters.Controls[name]).Checked
            : ((CheckBox)panelStaticFilters.Controls[name]).Checked;
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the Filter control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs" /> instance containing the event data.</param>
    /// <exception cref="System.NotImplementedException"></exception>
    private void Filter_CheckedChanged(object sender, EventArgs e)
    {
        SaveSettings();
        PopulateStatisticsList();
    }

    /// <summary>
    ///     Handles the Elapsed event of the RefreshTimer control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="ElapsedEventArgs" /> instance containing the event data.</param>
    /// <exception cref="System.NotImplementedException"></exception>
    private void RefreshTimer_Elapsed(object sender, EventArgs e)
    {
        try
        {
            UpdateStatistics();
        }
        catch { }
    }

    /// <summary>
    ///     Handles the Click event of the btnReset control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnReset_Click(object sender, EventArgs e)
    {
        foreach (var calculator in CalculatorRegistry.Calculators)
            calculator.Reset();
    }

    private void resetToolStripMenuItem_Click(object sender, EventArgs e)
    {
        foreach (ListViewItem lvItem in lvStatistics.SelectedItems)
            if (lvItem.Tag is IStatisticCalculator calculator)
                calculator.Reset();
    }

    /// <summary>
    ///     Occurs before Main form is displayed.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Main_Load(object sender, EventArgs e)
    {
        //Don't reset after teleportation or something equal
        if (!_initialReset)
            return;

        PopulateFilterList();

        LoadSettings();

        foreach (var calculator in CalculatorRegistry.Calculators)
            calculator.Reset();

        PopulateStatisticsList();
        _initialReset = false;
    }
}
