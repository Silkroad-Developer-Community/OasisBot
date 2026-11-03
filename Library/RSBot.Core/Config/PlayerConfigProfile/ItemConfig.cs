using System.Collections.Generic;

namespace RSBot.Core.PlayerConfigProfile
{
    public class InventoryConfig
    {
        public bool AutoSort { get; set; } = true;
        public List<string> ItemsAtTrainplace { get; set; } = new();
        public List<string> AutoUseAccordingToPurpose { get; set; } = new();
    }
}
