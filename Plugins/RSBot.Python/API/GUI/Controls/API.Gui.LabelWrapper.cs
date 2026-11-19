using RSBot.Python.Runtime;
using System.Windows.Forms;

namespace RSBot.Python.API.GUI.Controls
{
    public class PythonLabel : GuiControlWrapper
    {
        public PythonLabel(TabPage parent, PythonExecutor executor, string text, int x, int y, int width, int height)
        : base(parent, () => new SDUI.Controls.Label { Text = text ?? string.Empty, AutoSize = false }, executor, x, y, width, height)
        {
        }
    }
}
