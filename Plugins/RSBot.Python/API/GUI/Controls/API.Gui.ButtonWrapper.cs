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
    public class PythonButton : GuiControlWrapper
    {
        private PyObject _callback;
        private readonly SDUI.Controls.Button _button;

        public PythonButton(
            TabPage parent,
            PythonExecutor executor,
            string text,
            int x,
            int y,
            int width,
            int height,
            PyObject callback
        )
            : base(parent,() => new SDUI.Controls.Button { Text = text ?? string.Empty }, executor, x, y, width, height)
        {
            _button = (SDUI.Controls.Button)Control;
            _callback = callback;
            _button.Click += OnClick;
        }

        private void OnClick(object sender, EventArgs e)
        {
            var callback = _callback;
            if (callback == null)
                return;

            _ = Executor.ExecuteAsync(() =>
            {
                try
                {
                    using var result = callback.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Notify(PythonErrorHandler.FormatException(ex));
                }
            });
        }

        public override void Dispose()
        {
            _button.Click -= OnClick;
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
