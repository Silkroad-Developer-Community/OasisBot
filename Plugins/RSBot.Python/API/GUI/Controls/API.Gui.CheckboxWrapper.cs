using System;
using System.Windows.Forms;
using Python.Runtime;
using RSBot.Core;
using RSBot.Python.API.Handler;
using RSBot.Python.Runtime;

namespace RSBot.Python.API.GUI.Controls
{
    public class PythonCheckBox : GuiControlWrapper
    {
        private PyObject _callback;
        private readonly SDUI.Controls.CheckBox _checkBox;
        private bool _suppress;

        public PythonCheckBox(
            TabPage parent,
            PythonExecutor executor,
            string text,
            int x,
            int y,
            int width,
            int height,
            PyObject callback
        )
            : base(
                parent,
                () => new SDUI.Controls.CheckBox { Text = text ?? string.Empty },
                executor,
                x,
                y,
                width,
                height
            )
        {
            _checkBox = (SDUI.Controls.CheckBox)Control;
            _callback = callback;
            _checkBox.CheckedChanged += OnCheckedChanged;
        }

        private void OnCheckedChanged(object sender, EventArgs e)
        {
            if (_suppress || _callback == null)
                return;

            var isChecked = _checkBox.Checked;
            var callback = _callback;

            _ = Executor.ExecuteAsync(() =>
            {
                try
                {
                    using var arg = isChecked.ToPython();
                    using var result = callback.Invoke(arg);
                }
                catch (Exception ex)
                {
                    Log.Notify(PythonErrorHandler.FormatException(ex));
                }
            });
        }

        public bool get_checked() => Invoke(() => _checkBox.Checked);

        public void set_checked(bool value) =>
            Invoke(() =>
            {
                _suppress = true;
                _checkBox.Checked = value;
                _suppress = false;
            });

        public override void Dispose()
        {
            _checkBox.CheckedChanged -= OnCheckedChanged;
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
    }
}
