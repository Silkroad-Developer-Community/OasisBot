using RSBot.Core;
using RSBot.Python.Runtime;
using SDUI.Controls;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace RSBot.Python.Views;

[ToolboxItem(false)]
public partial class Main : DoubleBufferedControl
{
    private PythonManager _manager;
    private bool _updatingGrid;

    public Main()
    {
        InitializeComponent();
        WireEvents();
    }

    public SDUI.Controls.TabControl PluginTabControl => tcPlugin;

    public void AppendLog(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AppendLog(text)));
            return;
        }

        if (tbPluginLog.IsDisposed)
            return;

        tbPluginLog.AppendText(Environment.NewLine + text);
        tbPluginLog.ScrollToCaret();
    }

    public void AttachManager(PythonManager manager)
    {
        if (_manager != null)
        {
            _manager.LogAdded -= AppendLog;
            _manager.PluginsChanged -= UpdateGrid;
        }

        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _manager.GuiHost.AttachTabControl(tcPlugin);
        _manager.LogAdded += AppendLog;
        _manager.PluginsChanged += UpdateGrid;

        UpdateGrid();
    }

    private void WireEvents()
    {
        dgvPlugin.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (dgvPlugin.IsCurrentCellDirty)
                dgvPlugin.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        dgvPlugin.CellValueChanged += (s, e) =>
        {
            if (_updatingGrid || _manager == null || e.RowIndex < 0 || e.ColumnIndex != 0)
                return;

            var row = dgvPlugin.Rows[e.RowIndex];
            bool isEnabled = Convert.ToBoolean(row.Cells[0].Value);
            string fileName = row.Cells[5].Value?.ToString();

            var plugin = _manager.FoundPlugins.FirstOrDefault(p =>
                string.Equals(p.FileName, fileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, row.Cells[1].Value?.ToString(), StringComparison.OrdinalIgnoreCase));

            if (plugin != null)
            {
                _ = _manager.SetEnabledAsync(plugin, isEnabled);
            }
        };
    }

    private void UpdateGrid()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(UpdateGrid));
            return;
        }

        if (_manager == null)
            return;

        _updatingGrid = true;
        try
        {
            dgvPlugin.Rows.Clear();
            foreach (var info in _manager.FoundPlugins)
            {
                dgvPlugin.Rows.Add(info.IsEnabled, info.Name, info.Description, info.Author, info.Version, info.FileName);
            }
        }
        finally
        {
            _updatingGrid = false;
        }
    }
    private void btnReload_Click(object sender, EventArgs e)
    {
        if (_manager == null)
            return;

        btnReload.Enabled = false;
        _ = _manager.ReloadAsync().ContinueWith(_ =>
        {
            if (InvokeRequired)
                BeginInvoke(new Action(() => btnReload.Enabled = true));
            else
                btnReload.Enabled = true;
        });
    }

    private void btnOpenFolder_Click(object sender, EventArgs e)
    {
        try
        {
            PythonPaths.EnsureDirectories();
            Process.Start(new ProcessStartInfo
            {
                FileName = PythonPaths.PluginsRoot,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to open plugins folder: {ex.Message}");
        }
    }
}
