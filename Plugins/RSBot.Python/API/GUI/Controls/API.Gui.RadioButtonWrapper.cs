using System;
using System.Windows.Forms;
using Python.Runtime;
using RSBot.Core;
using RSBot.Python.API.Handler;
using RSBot.Python.Runtime;
using SDUI.Controls;

namespace RSBot.Python.API.GUI.Controls
{
    public class PythonRadioButton : GuiControlWrapper
    {
        private PyObject _callback;
        private readonly Radio _radioButton;
        private bool _suppress;

        public PythonRadioButton(
            TabPage parent,
            PythonExecutor executor,
            string text,
            string groupName,
            int x,
            int y,
            int width,
            int height,
            PyObject callback
        )
            : base(parent, () => new Radio { Text = text ?? string.Empty }, executor, x, y, width, height)
        {
            _radioButton = (Radio)Control;
            _callback = callback;
            _radioButton.CheckedChanged += OnCheckedChanged;
        }

        private void OnCheckedChanged(object sender, EventArgs e)
        {
            if (_suppress || _callback == null)
                return;

            var isChecked = _radioButton.Checked;
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

        public bool get_checked() => Invoke(() => _radioButton.Checked);

        public void set_checked(bool value) =>
            Invoke(() =>
            {
                _suppress = true;
                _radioButton.Checked = value;
                _suppress = false;
            });

        public override void Dispose()
        {
            _radioButton.CheckedChanged -= OnCheckedChanged;
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
