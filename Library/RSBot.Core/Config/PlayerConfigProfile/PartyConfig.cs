using RSBot.Core.Objects.Party;
using System.Collections.Generic;

namespace RSBot.Core.PlayerConfigProfile
{
    public class PartyConfig
    {
        public bool AllowInvitations { get; set; } = true;
        public bool AcceptAll { get; set; } = false;
        public bool AcceptFromListOnly { get; set; } = false;
        public bool AcceptIfBotStopped { get; set; } = false;
        public bool AcceptOnlyAtTrainingPlace { get; set; } = false;
        public bool InviteAll { get; set; } = false;
        public bool InviteFromListOnly { get; set; } = false;
        public List<string> AutoPartyList { get; set; } = new();

        public PartyMatchingConfig Matching { get; set; } = new();
        public bool ExpAutoShare { get; set; } = true;
        public bool ItemAutoShare { get; set; } = true;

        public bool AutoJoinByName { get; set; } = false;
        public string AutoJoinTargetName { get; set; } = string.Empty;
        public bool AutoJoinByTitle { get; set; } = false;
        public string AutoJoinTargetTitle { get; set; } = string.Empty;

        public bool AlwaysFollowPartyMaster { get; set; } = false;
        public bool LeaveIfMasterNotPresent { get; set; } = false;
        public string MasterCharacterName { get; set; } = string.Empty;
        public bool ListenCommandsFromMaster { get; set; } = false;
        public bool ListenCommandsOnlyFromList { get; set; } = false;
        public List<string> CommandAllowedPlayers { get; set; } = new();
        public PartyBuffingConfig Buffing { get; set; } = new();
    }

    public class PartyMatchingConfig
    {
        public PartyPurpose Purpose { get; set; } = PartyPurpose.Hunting;
        public string Title { get; set; } = "For opening hunting on the silkroad!";
        public bool AutoReform { get; set; } = false;
        public bool AutoAccept { get; set; } = true;
        public byte LevelFrom { get; set; } = 1;
        public byte LevelTo { get; set; } = 140;
    }

    public class PartyBuffingConfig
    {
        public string SelectedGroup { get; set; } = "Default";
        public List<string> Groups { get; set; } = new() { "Default" };
        public bool HideLowerSkills { get; set; } = true;
        public string Members { get; set; } = string.Empty;
    }
}
