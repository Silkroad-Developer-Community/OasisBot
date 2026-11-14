using RSBot.Core;
using RSBot.Core.Event;
using RSBot.Core.Objects;

namespace RSBot.Chat
{
    public class ChatManager
    {
        string lastWhisper;

        public ChatManager()
        {
            SubscribeEvents();
        }

        public static void Send(ChatType type, string message, string receiver = null)
        {
            if (type == ChatType.Global)
                Bundle.Chat.SendGlobalChatPacket(message);
            else
                Bundle.Chat.SendChatPacket(type, message, receiver);

            if (type == ChatType.Private)
            {
                PlayerConfig.Profile.LastWhisper = receiver;
                PlayerConfig.Save();
            }                
        }

        private void SubscribeEvents()
        {
            EventManager.SubscribeEvent("OnEnterGame", OnEnterGame);
        }

        private void OnEnterGame()
        {
            lastWhisper = PlayerConfig.Profile.LastWhisper;
        }
    }
}
