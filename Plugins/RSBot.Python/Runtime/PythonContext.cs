using System;

namespace RSBot.Python.Runtime
{
    public class PythonContext
    {
        public PythonContext(PythonManager manager, PythonGuiHost guiHost, PythonExecutor executor, Action<string> log)
        {
            Manager = manager;
            GuiHost = guiHost;
            Executor = executor;
            Log = log;
        }

        public PythonManager Manager { get; }
        public PythonGuiHost GuiHost { get; }
        public PythonExecutor Executor { get; }
        public Action<string> Log { get; }
    }
}
