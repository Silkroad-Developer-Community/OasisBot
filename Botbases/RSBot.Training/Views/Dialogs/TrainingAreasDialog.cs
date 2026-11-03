using System;
using System.Linq;
using System.Windows.Forms;
using RSBot.Core;
using RSBot.Core.Objects;
using RSBot.Core.PlayerConfigProfile;
using SDUI.Controls;

namespace RSBot.Training.Views.Dialogs;

public partial class TrainingAreasDialog : UIWindowBase
{
    private const string DIALOG_AREA_NAME = "Enter area name";
    private const string DIALOG_AREA_DESC = "Example: For my custom party at jangan";

    public TrainingAreasDialog()
    {
        InitializeComponent();
    }

    private void buttonAccept_Click(object sender, EventArgs e)
    {
        if (listView.SelectedIndices.Count <= 0)
        {
            MessageBox.Show("Please select a training area!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.Retry;
            return;
        }

        var selectedItem = listView.SelectedItems[0];

        if (selectedItem.Tag is not Area trainingArea)
        {
            DialogResult = DialogResult.Retry;
            return;
        }
        var trainingConfig = PlayerConfig.Profile.Training;
        var index = selectedItem.Index;

        if (index >= 0 && index < trainingConfig.Areas.Count)
        {
            trainingConfig.Index = index;
            var targetArea = trainingConfig.Areas[index];

            targetArea.Name = trainingArea.Name;
            targetArea.Region = trainingArea.Position.Region;
            targetArea.X = trainingArea.Position.XOffset;
            targetArea.Y = trainingArea.Position.YOffset;
            targetArea.Z = trainingArea.Position.ZOffset;
            targetArea.Radius = trainingArea.Radius;
        }
        else
        {
            var newArea = new TrainingAreaConfig
            {
                Name = trainingArea.Name,
                Region = trainingArea.Position.Region,
                X = trainingArea.Position.XOffset,
                Y = trainingArea.Position.YOffset,
                Z = trainingArea.Position.ZOffset,
                Radius = trainingArea.Radius,
            };
            trainingConfig.Areas.Add(newArea);
            trainingConfig.Index = index;
        }

        PlayerConfig.Save();
    }

    private void TrainingAreas_Load(object sender, EventArgs e)
    {
        var trainingConfig = PlayerConfig.Profile.Training;

        var selectedIndex = trainingConfig.Index;

        listView.BeginUpdate();
        listView.Items.Clear();

        var areas = trainingConfig.Areas;
        foreach (var area in areas)
        {
            var trainingArea = new Area
            {
                Name = area.Name,
                Position = new Position
                {
                    Region = area.Region,
                    XOffset = area.X,
                    YOffset = area.Y,
                    ZOffset = area.Z,
                },
                Radius = area.Radius,
            };

            var regionName = Game.ReferenceManager.GetTranslation(trainingArea.Position.Region.ToString());

            var listViewItem = listView.Items.Add(new ListViewItem { Tag = trainingArea });
            listViewItem.Text = (listViewItem.Index + 1).ToString();

            listViewItem.SubItems.AddRange(
                new[]
                {
                    trainingArea.Name,
                    regionName,
                    trainingArea.Position.X.ToString("0.0"),
                    trainingArea.Position.Y.ToString("0.0"),
                    trainingArea.Radius.ToString(),
                    listViewItem.Index == selectedIndex ? "Yes" : "No",
                }
            );

            if (listViewItem.Index == selectedIndex)
                listView.SetItemState(listViewItem.Index, 2, 2);
        }

        listView.EndUpdate();
    }

    private void TrainingAreas_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.Retry)
            e.Cancel = true;
    }

    private void createToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var dialog = new CreateTrainingAreaDialog();

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var position = Game.Player.Position;

            var trainingArea = new Area
            {
                Name = dialog.TrainingName.Text,
                Position = position,
                Radius = (int)dialog.Radius.Value,
            };

            var regionName = Game.ReferenceManager.GetTranslation(trainingArea.Position.Region.ToString());

            var listViewItem = listView.Items.Add(new ListViewItem { Tag = trainingArea });
            listViewItem.Text = (listViewItem.Index + 1).ToString();
            listViewItem.SubItems.AddRange(
                new[]
                {
                    trainingArea.Name,
                    regionName,
                    trainingArea.Position.X.ToString("0.0"),
                    trainingArea.Position.Y.ToString("0.0"),
                    trainingArea.Radius.ToString(),
                    "No",
                }
            );

            var areas = PlayerConfig.Profile.Training.Areas;
            areas.Add(new TrainingAreaConfig
            {
                Name = trainingArea.Name,
                Region = trainingArea.Position.Region,
                X = trainingArea.Position.XOffset,
                Y = trainingArea.Position.YOffset,
                Z = trainingArea.Position.ZOffset,
                Radius = trainingArea.Radius
            });
            PlayerConfig.Save();
        }
    }

    private void removeSelectedAreaToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (listView.SelectedItems.Count <= 0)
            return;

        var selectedIndex = listView.SelectedIndices[0];
        var areas = PlayerConfig.Profile.Training.Areas;
        areas.RemoveAt(selectedIndex);

        listView.Items.RemoveAt(selectedIndex);

        PlayerConfig.Save();
    }
}
