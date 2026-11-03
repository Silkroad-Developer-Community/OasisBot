using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RSBot.Core;
using RSBot.Core.Event;
using RSBot.Core.Objects.Party;

namespace RSBot.Party
{
    public class PartyManager
    {
        public PartyManager()
        {
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            EventManager.SubscribeEvent("OnEnterGame", OnEnterGame);
            EventManager.SubscribeEvent("OnDeletePartyEntry", OnDeletePartyEntry);
            EventManager.SubscribeEvent("OnPartyMemberLeave", new Action<PartyMember>(OnPartyMemberLeave));
            EventManager.SubscribeEvent("OnPartyMemberBanned", new Action<PartyMember>(OnPartyMemberBanned));
            EventManager.SubscribeEvent("OnPartyDismiss", OnPartyDismiss);
            EventManager.SubscribeEvent("OnAgentServerDisconnected", OnPartyDismiss);
            EventManager.SubscribeEvent("OnPartyLeaderChange", OnPartyData);
            EventManager.SubscribeEvent("OnPartyData", OnPartyData);
        }

        private async void OnEnterGame()
        {
            await Task.Delay(5000);

            if (
                Game.Ready
                && Bundle.Container.PartyMatching.Config.AutoReform
                && !Bundle.Container.PartyMatching.HasMatchingEntry
            )
                Bundle.Container.PartyMatching.Create();
        }

        private void OnPartyMemberLeave(PartyMember member)
        {
            if (Bundle.Container.PartyMatching.Config.AutoReform)
                if (
                    Game.Party != null
                    && !Bundle.Container.PartyMatching.HasMatchingEntry
                    && Game.Party.Members?.Count < Game.Party.Settings.MaxMember
                )
                    Bundle.Container.PartyMatching.Create();
        }

        private void OnPartyMemberBanned(PartyMember member)
        {
            if (member.Name != Game.Player.Name)
            {
                if (Bundle.Container.PartyMatching.Config.AutoReform)
                    if (
                        Game.Party != null
                        && !Bundle.Container.PartyMatching.HasMatchingEntry
                        && Game.Party.Members?.Count < Game.Party.Settings.MaxMember
                    )
                        Bundle.Container.PartyMatching.Create();
            }
            else
                OnPartyDismiss();
        }

        public static void OnPartyDismiss()
        {
            if (!Game.Ready)
                return;

            Bundle.Container.PartyMatching.HasMatchingEntry = false;
            OnDeletePartyEntry();
        }

        private static void OnDeletePartyEntry()
        {
            Bundle.Container.PartyMatching.Id = 0;

            if (Game.Ready && Bundle.Container.PartyMatching.Config.AutoReform)
                Bundle.Container.PartyMatching.Create();
        }

        public void OnPartyData()
        {
            try
            {
                if (Game.Party.Members == null)
                {
                    OnPartyDismiss();
                    return;
                }
            }
            catch { }
        }

        public static string[] GetBuffingGroups()
        {
            return PlayerConfig.Profile.Party.Buffing.Groups.ToArray();
        }

        public static void SaveBuffingGroups(IEnumerable<string> buffs)
        {
            PlayerConfig.Profile.Party.Buffing.Groups.Clear();
            PlayerConfig.Profile.Party.Buffing.Groups.AddRange(buffs);
            PlayerConfig.Save();
        }

        public static string GetBuffingMembers()
        {
            return PlayerConfig.Profile.Party.Buffing.Members;
        }

        public static void SaveBuffingPartyMembers(string members)
        {
            PlayerConfig.Profile.Party.Buffing.Members = members;
            PlayerConfig.Save();

            EventManager.FireEvent("OnPartyBuffSettingsChanged");
        }

        public static void SaveAutoPartyPlayerList(IEnumerable<string> players)
        {
            PlayerConfig.Profile.Party.AutoPartyList.Clear();
            PlayerConfig.Profile.Party.AutoPartyList.AddRange(players);
            PlayerConfig.Save();

            Bundle.Container.Refresh();
        }

        public static void SaveCommandPlayersList(IEnumerable<string> players)
        {
            PlayerConfig.Profile.Party.CommandAllowedPlayers.Clear();
            PlayerConfig.Profile.Party.CommandAllowedPlayers.AddRange(players);
            PlayerConfig.Save();

            Bundle.Container.Refresh();
        }

        public static string GetSelectedGroup()
        {
            return PlayerConfig.Profile.Party.Buffing.SelectedGroup;
        }

        public static void LeaveParty()
        {
            Game.Party?.Leave();
        }

        public static void SetPartyExpAutoShare(bool enabled)
        {
            PlayerConfig.Profile.Party.ExpAutoShare = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyItemAutoShare(bool enabled)
        {
            PlayerConfig.Profile.Party.ItemAutoShare = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyAllowInvites(bool enabled)
        {
            PlayerConfig.Profile.Party.AllowInvitations = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyAutoAcceptInvitesAll(bool enabled)
        {
            PlayerConfig.Profile.Party.AcceptAll = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyAutoAcceptInvitesFromList(bool enabled)
        {
            PlayerConfig.Profile.Party.AcceptFromListOnly = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyAutoInviteAll(bool enabled)
        {
            PlayerConfig.Profile.Party.InviteAll = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyAutoInviteList(bool enabled)
        {
            PlayerConfig.Profile.Party.InviteFromListOnly = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyAutoAcceptAtTrainingPlace(bool enabled)
        {
            PlayerConfig.Profile.Party.AcceptOnlyAtTrainingPlace = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyAutoAcceptIfBotStopped(bool enabled)
        {
            PlayerConfig.Profile.Party.AcceptIfBotStopped = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyAutoLeaveIfMasterNot(bool enabled)
        {
            PlayerConfig.Profile.Party.LeaveIfMasterNotPresent = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyAutoLeaveMaster(string masterName)
        {
            PlayerConfig.Profile.Party.MasterCharacterName = masterName;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyCommandListenFromMaster(bool enabled)
        {
            PlayerConfig.Profile.Party.ListenCommandsFromMaster = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyCommandListenFromList(bool enabled)
        {
            PlayerConfig.Profile.Party.ListenCommandsOnlyFromList = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetFollowMaster(bool enabled)
        {
            PlayerConfig.Profile.Party.AlwaysFollowPartyMaster = enabled;
            PlayerConfig.Save();

            Bundle.Container.Refresh();
        }

        public static void SetPartyAutoJoinByName(bool enabled)
        {
            PlayerConfig.Profile.Party.AutoJoinByName = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyAutoJoinByTitle(bool enabled)
        {
            PlayerConfig.Profile.Party.AutoJoinByTitle = enabled;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyAutoJoinName(string name)
        {
            PlayerConfig.Profile.Party.AutoJoinTargetName = name;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void SetPartyAutoJoinTitle(string title)
        {
            PlayerConfig.Profile.Party.AutoJoinTargetTitle = title;
            PlayerConfig.Save();
            Bundle.Container.AutoParty.Refresh();
        }

        public static void BanishMemberFromParty(string memberName)
        {
            if (Game.Party.IsLeader)
                Game.Party.GetMemberByName(memberName)?.Banish();
        }

        public static void DeletePartyMatchingEntry()
        {
            if (!Game.Ready)
                return;
            Bundle.Container.PartyMatching.Config.AutoReform = false;
            PlayerConfig.Save();
            Bundle.Container.PartyMatching.Delete();
        }
    }
}
