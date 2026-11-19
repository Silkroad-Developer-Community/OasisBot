using System;
using System.Windows.Forms;
using Python.Runtime;
using RSBot.Core;
using RSBot.Python.API.Handler;
using RSBot.Python.Runtime;

namespace RSBot.Python.API.GUI.Controls
{
    public class PythonComboBox : GuiControlWrapper
    {
        private PyObject _callback;
        private readonly SDUI.Controls.ComboBox _combo;
        private bool _suppress;

        public PythonComboBox(
            TabPage parent,
            PythonExecutor executor,
            int x,
            int y,
            int width,
            int height,
            PyObject callback
        )
            : base(parent, () => new SDUI.Controls.ComboBox(), executor, x, y, width, height)
        {
            _combo = (SDUI.Controls.ComboBox)Control;
            _callback = callback;
            _combo.SelectedIndexChanged += OnSelectedIndexChanged;
        }

        private void OnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppress || _callback == null)
                return;

            var index = _combo.SelectedIndex;
            var callback = _callback;

            _ = Executor.ExecuteAsync(() =>
            {
                try
                {
                    using var arg = index.ToPython();
                    using var result = callback.Invoke(arg);
                }
                catch (Exception ex)
                {
                    Log.Notify(PythonErrorHandler.FormatException(ex));
                }
            });
        }

        public void add_item(object value) => Invoke(() => _combo.Items.Add(value?.ToString() ?? string.Empty));

        public void remove_item(int index) =>
            Invoke(() =>
            {
                if (index >= 0 && index < _combo.Items.Count)
                    _combo.Items.RemoveAt(index);
            });

        public void clear() => Invoke(() => _combo.Items.Clear());

        public int selected_index() => Invoke(() => _combo.SelectedIndex);

        public void set_index(int index) =>
            Invoke(() =>
            {
                _suppress = true;
                if (index >= -1 && index < _combo.Items.Count)
                    _combo.SelectedIndex = index;
                _suppress = false;
            });

        public int item_count() => Invoke(() => _combo.Items.Count);

        public string get_item(int index) =>
            Invoke(() =>
                index >= 0 && index < _combo.Items.Count
                    ? _combo.Items[index]?.ToString() ?? string.Empty
                    : string.Empty
            );

        public string get_selected_item() => Invoke(() => _combo.SelectedItem?.ToString() ?? string.Empty);

        public override void Dispose()
        {
            _combo.SelectedIndexChanged -= OnSelectedIndexChanged;
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
