using RSBot.Python.API.GUI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

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
                if (TabPage.Parent is TabControl parent)
                {
                    if (parent.InvokeRequired)
                        parent.Invoke(new Action(() => parent.TabPages.Remove(TabPage)));
                    else
                        parent.TabPages.Remove(TabPage);
                }

                TabPage.Dispose();
            }
        }

    }
}
