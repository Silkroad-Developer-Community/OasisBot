using RSBot.Python.Runtime;

namespace RSBot.Python.API.GUI
{
    public class WFAPI : IPythonPlugin
    {
        private PythonContext _context;

        public string ModuleName => "gui";

        public PythonGui GUI(string title)
        {
            var pluginId = _context.Manager.ResolvePluginIdForGui();
            var page = _context.GuiHost.CreatePage(pluginId, title);
            return new PythonGui(page, _context.Executor);
        }

        public void Init(PythonContext context) => _context = context;

    }
}
