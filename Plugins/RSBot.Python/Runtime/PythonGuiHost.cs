using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using RSBot.Core;
using RSBot.Python.Models;

namespace RSBot.Python.Runtime
{
    public class PythonGuiHost
    {
        private readonly List<PythonGuiPage> _pages = [];
        private TabControl _tabControl;

        public void AttachTabControl(TabControl tabControl)
        {
            _tabControl = tabControl;
        }

        public PythonGuiPage CreatePage(string pluginId, string title)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                pluginId = "interactive";
            if (string.IsNullOrWhiteSpace(title))
                title = pluginId;

            if (_tabControl == null)
            {
                Log.Error("TabControl is not attached to PythonGuiHost.");
                return null;
            }

            RemovePluginPage(pluginId);

            PythonGuiPage Create()
            {
                var tabPage = new TabPage
                {
                    Name = pluginId,
                    Text = title.Trim(),
                    AutoScroll = true,
                };

                _tabControl?.TabPages?.Add(tabPage);

                var page = new PythonGuiPage
                {
                    PluginId = pluginId,
                    Title = title.Trim(),
                    TabPage = tabPage,
                };

                lock (_pages)
                    _pages.Add(page);

                return page;
            }

            if (_tabControl.InvokeRequired)
                return (PythonGuiPage)_tabControl.Invoke(Create);
            return Create();
        }

        public void RemovePluginPage(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return;
            PythonGuiPage[] pages;
            lock (_pages)
            {
                pages = _pages
                    .Where(x => string.Equals(x.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                foreach (var page in pages)
                    _pages.Remove(page);
            }
            if (pages.Length == 0)
                return;
            void RemoveAction()
            {
                foreach (var page in pages)
                    page.Dispose();
                if (_tabControl != null && !_tabControl.IsDisposed)
                {
                    if (
                        _tabControl.TabPages.Count > 0
                        && (_tabControl.SelectedIndex < 0 || _tabControl.SelectedTab == null)
                    )
                        _tabControl.SelectedIndex = 0;
                    _tabControl.Invalidate(true);
                    _tabControl.Update();
                }
            }
            if (_tabControl != null && !_tabControl.IsDisposed)
            {
                if (_tabControl.InvokeRequired)
                    _tabControl.Invoke(new Action(RemoveAction));
                else
                    RemoveAction();
            }
        }

        public void Clear()
        {
            PythonGuiPage[] pages;
            lock (_pages)
            {
                pages = _pages.ToArray();
                _pages.Clear();
            }
            if (pages.Length == 0)
                return;
            void ClearAction()
            {
                foreach (var page in pages)
                    page.Dispose();
                if (_tabControl != null && !_tabControl.IsDisposed)
                {
                    if (
                        _tabControl.TabPages.Count > 0
                        && (_tabControl.SelectedIndex < 0 || _tabControl.SelectedTab == null)
                    )
                        _tabControl.SelectedIndex = 0;
                    _tabControl.Invalidate(true);
                    _tabControl.Update();
                }
            }
            if (_tabControl != null && !_tabControl.IsDisposed)
            {
                if (_tabControl.InvokeRequired)
                    _tabControl.Invoke(new Action(ClearAction));
                else
                    ClearAction();
            }
        }
    }
}
