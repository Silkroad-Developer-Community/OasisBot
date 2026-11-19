using System;
using System.Windows.Forms;
using Python.Runtime;
using RSBot.Core;
using RSBot.Python.API.Handler;
using RSBot.Python.Runtime;
using RSBot.Python.Views;
using SDUI.Controls;

namespace RSBot.Python.API.GUI.Controls
{
    public class PythonTextBox : GuiControlWrapper
    {
        private PyObject _callback;
        private readonly SDUI.Controls.TextBox _textBox;
        private bool _suppress;

        public PythonTextBox(
            TabPage parent,
            PythonExecutor executor,
            string text,
            int x,
            int y,
            int width,
            int height,
            PyObject callback
        )
            : base(parent, () => new SDUI.Controls.TextBox { Text = text ?? string.Empty }, executor, x, y, width, height)
        {
            _textBox = (SDUI.Controls.TextBox)Control;
            _callback = callback;
            _textBox.TextChanged += OnTextChanged;
        }

        private void OnTextChanged(object sender, EventArgs e)
        {
            if (_suppress || _callback == null)
                return;

            var text = _textBox.Text ?? string.Empty;
            var callback = _callback;

            _ = Executor.ExecuteAsync(() =>
            {
                try
                {
                    using var arg = text.ToPython();
                    using var result = callback.Invoke(arg);
                }
                catch (Exception ex)
                {
                    Log.Notify(PythonErrorHandler.FormatException(ex));
                }
            });
        }

        public void set_text_value(string value) => set_text(value);

        public void set_readonly(bool readOnly) => Invoke(() => Control.Enabled = !readOnly);

        public override void set_text(string text) =>
            Invoke(() =>
            {
                _suppress = true;
                _textBox.Text = text ?? string.Empty;
                _suppress = false;
            });

        public override void Dispose()
        {
            _textBox.TextChanged -= OnTextChanged;
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
