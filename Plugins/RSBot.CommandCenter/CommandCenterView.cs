using System.Windows.Forms;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Plugins;

namespace RSBot.CommandCenter
{
    public class CommandCenterView : IPluginView
    {
        public string InternalName => "RSBot.CommandCenter";

        public string DisplayName => "Command center";

        public bool DisplayAsTab => false;

        public int Index => 100;

        public bool RequireIngame => true;

        public Control View => Views.View.Main;

        public void Translate()
        {
            LanguageManager.Translate(View, Kernel.Language);
        }
    }
}
