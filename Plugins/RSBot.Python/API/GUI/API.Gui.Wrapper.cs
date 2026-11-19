using RSBot.Python.Runtime;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace RSBot.Python.API.GUI
{
    public abstract class GuiControlWrapper : IDisposable
    {
        protected Control Control { get; private set; }
        protected TabPage Parent { get; }
        protected PythonExecutor Executor { get; }

        protected GuiControlWrapper(TabPage parent, Func<Control> controlFactory, PythonExecutor executor, int x, int y, int width = -1, int height = -1)
        {
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Executor = executor;
            void CreateAndAdd()
            {
                Control = controlFactory();
                Control.Location = new Point(x, y);
                if (width > 0 && height > 0)
                    Control.Size = new Size(width, height);
                else if (width > 0)
                    Control.Width = width;
                else if (height > 0)
                    Control.Height = height;
                Parent.Controls.Add(Control);
            }
            if (Parent.InvokeRequired)
                Parent.Invoke(new Action(CreateAndAdd));
            else
                CreateAndAdd();
        }

        protected void Invoke(Action action)
        {
            if (Parent == null || Parent.IsDisposed)
                return;
            if (Parent.InvokeRequired)
                Parent.Invoke(action);
            else
                action();
        }
        protected T Invoke<T>(Func<T> func)
        {
            if (Parent == null || Parent.IsDisposed)
                return default;
            if (Parent.InvokeRequired)
                return (T)Parent.Invoke(func);
            return func();
        }

        public virtual void Dispose()
        {
            Invoke(() =>
            {
                if (Parent != null && !Parent.IsDisposed)
                    Parent.Controls.Remove(Control);

                Control.Dispose();
            });
        }

        #region API Functions
        public void set_visible(bool value) => Invoke(() => Control.Visible = value);
        public void set_enabled(bool value) => Invoke(() => Control.Enabled = value);

        public void move_position(int x, int y) => Invoke(() => Control.Location = new Point(x, y));
        public void move_position(double x, double y) => move_position((int)Math.Round(x), (int)Math.Round(y));

        public void set_size(int width, int height) => Invoke(() => Control.Size = new Size(width, height));
        public void set_size(double width, double height) => set_size((int)Math.Round(width), (int)Math.Round(height));

        public void remove() => Dispose();

        public virtual void set_text(string text) => Invoke(() => Control.Text = text ?? string.Empty);
        public virtual string get_text() => Invoke(() => Control.Text ?? string.Empty);

        #endregion
    }
}
