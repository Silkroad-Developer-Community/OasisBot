using System;
using Python.Runtime;
using RSBot.Python.API.GUI.Controls;
using RSBot.Python.Models;
using RSBot.Python.Runtime;

namespace RSBot.Python.API.GUI
{
    public class PythonGui
    {
        private readonly PythonGuiPage _page;
        private readonly PythonExecutor _executor;

        internal PythonGui(PythonGuiPage page, PythonExecutor executor)
        {
            _page = page;
            _executor = executor;
        }

        public string title => _page.Title;

        public PythonLabel Label(string text, double x, double y) => Label(text, x, y, 180, 26);

        public PythonLabel Label(string text, double x, double y, double width, double height) =>
            Track(
                new PythonLabel(
                    _page.TabPage,
                    _executor,
                    text,
                    (int)Math.Round(x),
                    (int)Math.Round(y),
                    (int)Math.Round(width),
                    (int)Math.Round(height)
                )
            );

        public PythonButton Button(string text, double x, double y, PyObject handler = null) =>
            Button(text, x, y, 110, 32, handler);

        public PythonButton Button(
            string text,
            double x,
            double y,
            double width,
            double height,
            PyObject handler = null
        ) =>
            Track(
                new PythonButton(
                    _page.TabPage,
                    _executor,
                    text,
                    (int)Math.Round(x),
                    (int)Math.Round(y),
                    (int)Math.Round(width),
                    (int)Math.Round(height),
                    handler
                )
            );

        public PythonCheckBox CheckBox(string text, double x, double y, PyObject handler = null) =>
            CheckBox(text, x, y, 220, 28, handler);

        public PythonCheckBox CheckBox(
            string text,
            double x,
            double y,
            double width,
            double height,
            PyObject handler = null
        ) =>
            Track(
                new PythonCheckBox(
                    _page.TabPage,
                    _executor,
                    text,
                    (int)Math.Round(x),
                    (int)Math.Round(y),
                    (int)Math.Round(width),
                    (int)Math.Round(height),
                    handler
                )
            );

        public PythonRadioButton RadioButton(string text, double x, double y, PyObject handler = null) =>
            RadioButton(text, "python", x, y, 100, 28, handler);

        public PythonRadioButton RadioButton(
            string text,
            string groupName,
            double x,
            double y,
            double width,
            double height,
            PyObject handler = null
        ) =>
            Track(
                new PythonRadioButton(
                    _page.TabPage,
                    _executor,
                    text,
                    groupName,
                    (int)Math.Round(x),
                    (int)Math.Round(y),
                    (int)Math.Round(width),
                    (int)Math.Round(height),
                    handler
                )
            );

        public PythonTextBox TextBox(string text, double x, double y) => TextBox(text, x, y, 180, 32, null);

        public PythonTextBox TextBox(double x, double y) => TextBox("", x, y, 180, 32, null);

        public PythonTextBox TextBox(
            string text,
            double x,
            double y,
            double width,
            double height,
            PyObject handler = null
        ) =>
            Track(
                new PythonTextBox(
                    _page.TabPage,
                    _executor,
                    text,
                    (int)Math.Round(x),
                    (int)Math.Round(y),
                    (int)Math.Round(width),
                    (int)Math.Round(height),
                    handler
                )
            );

        public PythonComboBox ComboBox(double x, double y) => ComboBox(x, y, 180, 32, null);

        public PythonComboBox ComboBox(double x, double y, double width, double height, PyObject handler = null) =>
            Track(
                new PythonComboBox(
                    _page.TabPage,
                    _executor,
                    (int)Math.Round(x),
                    (int)Math.Round(y),
                    (int)Math.Round(width),
                    (int)Math.Round(height),
                    handler
                )
            );

        public PythonListBox ListBox(double x, double y) => ListBox(x, y, 220, 160, null);

        public PythonListBox ListBox(double x, double y, double width, double height, PyObject handler = null) =>
            Track(
                new PythonListBox(
                    _page.TabPage,
                    _executor,
                    (int)Math.Round(x),
                    (int)Math.Round(y),
                    (int)Math.Round(width),
                    (int)Math.Round(height),
                    handler
                )
            );

        public PythonProgressbar ProgressBar(double x, double y) => ProgressBar(x, y, 180, 32, null);

        public PythonProgressbar ProgressBar(double x, double y, PyObject handler = null) =>
            ProgressBar(x, y, 180, 32, handler);

        public PythonProgressbar ProgressBar(
            double x,
            double y,
            double width,
            double height,
            PyObject handler = null
        ) =>
            Track(
                new PythonProgressbar(
                    _page.TabPage,
                    _executor,
                    (int)Math.Round(x),
                    (int)Math.Round(y),
                    (int)Math.Round(width),
                    (int)Math.Round(height),
                    handler
                )
            );

        private T Track<T>(T wrapper)
            where T : GuiControlWrapper
        {
            _page.Track(wrapper);
            return wrapper;
        }
    }
}
