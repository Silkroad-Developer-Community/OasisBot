using System;
using System.Collections.Generic;
using System.Windows.Forms;
using RSBot.Python.API.GUI;

namespace RSBot.Python.Models
{
    public class PythonGuiPage
    {
        private readonly List<GuiControlWrapper> _controls = [];

        public string PluginId { get; init; }
        public string Title { get; init; }
        public TabPage TabPage { get; init; }

        internal void Track(GuiControlWrapper wrapper)
        {
            lock (_controls)
                _controls.Add(wrapper);
        }

        public void Dispose()
        {
            GuiControlWrapper[] snapshot;
            lock (_controls)
            {
                snapshot = _controls.ToArray();
                _controls.Clear();
            }

            foreach (var control in snapshot)
                control.Dispose();

            if (TabPage != null)
            {
                if (TabPage.Parent is TabControl parent && !parent.IsDisposed)
                {
                    void RemovePage()
                    {
                        if (parent.SelectedTab == TabPage || parent.SelectedIndex < 0)
                        {
                            if (parent.TabPages.Count > 1)
                                parent.SelectedIndex = 0;
                        }
                        parent.TabPages.Remove(TabPage);
                        parent.Invalidate(true);
                        parent.Update();
                    }
                    if (parent.InvokeRequired)
                        parent.Invoke(new Action(RemovePage));
                    else
                        RemovePage();
                }

                TabPage.Dispose();
            }
        }
    }
}
