using RSBot.Python.Runtime;

namespace RSBot.Python.API
{
    /// <summary>
    /// Every API module (Core, Inventory, GUI, etc.) should implement this interface
    /// </summary>
    public interface IPythonPlugin
    {
        /// <summary>
        /// Unique name of the module.
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// Called once when the module is initialized.
        /// </param>
        void Init(PythonContext context);
    }
}
