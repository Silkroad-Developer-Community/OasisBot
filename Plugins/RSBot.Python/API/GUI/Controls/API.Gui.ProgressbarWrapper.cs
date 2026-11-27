using System.Drawing;
using System.Windows.Forms;
using Python.Runtime;
using RSBot.Python.Runtime;
using SDUI.Controls;

namespace RSBot.Python.API.GUI.Controls
{
    public class PythonProgressbar : GuiControlWrapper
    {
        private PyObject _callback;
        private readonly SDUI.Controls.ProgressBar _progressBar;
        private bool _suppress;

        public PythonProgressbar(
            TabPage parent,
            PythonExecutor executor,
            int x,
            int y,
            int width,
            int height,
            PyObject callback
        )
            : base(parent, () => new SDUI.Controls.ProgressBar(), executor, x, y, width, height)
        {
            _progressBar = (SDUI.Controls.ProgressBar)Control;
            _callback = callback;
        }

        public void set_value(long value) => Invoke(() => _progressBar.Value = value);

        public long get_value() => Invoke(() => _progressBar.Value);

        public void set_maximum(long value) => Invoke(() => _progressBar.Maximum = value);

        public long get_maximum() => Invoke(() => _progressBar.Maximum);

        public void show_percentage(bool value) => Invoke(() => _progressBar.ShowAsPercent = value);

        public void show_value(bool value) => Invoke(() => _progressBar.ShowValue = value);

        public void set_color(object color) =>
            Invoke(() =>
            {
                var c = ParseColor(color, Color.DodgerBlue);
                _progressBar.Gradient = [c, c];
            });

        public void set_gradient(object colorStart, object colorEnd) =>
            Invoke(() =>
            {
                var c1 = ParseColor(colorStart, Color.DodgerBlue);
                var c2 = ParseColor(colorEnd, Color.LightSkyBlue);
                _progressBar.Gradient = [c1, c2];
            });

        public override void Dispose()
        {
            var callback = _callback;
            _callback = null;

            if (callback != null)
            {
                _ = Executor.ExecuteAsync(() =>
                {
                    try
                    {
                        callback.Dispose();
                    }
                    catch { }
                });
            }

            base.Dispose();
        }

        private static Color ParseColor(object value, Color fallback)
        {
            if (value == null)
                return fallback;
            try
            {
                if (value is Color color)
                    return color;
                if (value is int intVal)
                    return Color.FromArgb((intVal >> 16) & 0xFF, (intVal >> 8) & 0xFF, intVal & 0xFF);
                if (value is PyInt pyInt)
                {
                    var val = pyInt.ToInt32();
                    return Color.FromArgb((val >> 16) & 0xFF, (val >> 8) & 0xFF, val & 0xFF);
                }
                if (value is string strVal && !string.IsNullOrWhiteSpace(strVal))
                    return ColorTranslator.FromHtml(strVal);
            }
            catch { }
            return fallback;
        }
    }
}
