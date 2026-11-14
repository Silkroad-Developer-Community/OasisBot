using System.Collections.Generic;
using RSBot.Core;

namespace RSBot.CommandCenter.Components;

internal class PluginConfig
{
    public static bool Enabled
    {
        get
        {
            var enabled = PlayerConfig.Profile.EnableCommandCenter;
            PlayerConfig.Save();
            return enabled;
        }
    }

    public static Dictionary<string, string> GetEmoteToCommandMapping()
    {
        var result = new Dictionary<string, string>(16);

        foreach (var emoticon in Emoticons.Items)
        {
            string actionName = string.Empty;
            switch (emoticon.Type)
            {
                case EmoticonType.Yes:
                    actionName = PlayerConfig.Profile.Commands.EmoticonYes;
                    break;
                case EmoticonType.No:
                    actionName = PlayerConfig.Profile.Commands.EmoticonNo;
                    break;
                case EmoticonType.Rush:
                    actionName = PlayerConfig.Profile.Commands.EmoticonRush;
                    break;
                case EmoticonType.Greeting:
                    actionName = PlayerConfig.Profile.Commands.EmoticonGreeting;
                    break;
                case EmoticonType.Smile:
                    actionName = PlayerConfig.Profile.Commands.EmoticonSmile;
                    break;
            }

            result.Add(emoticon.Name, actionName);
        }

        return result;
    }

    public static string GetAssignedEmoteCommand(string emoteName)
    {
        var mapping = GetEmoteToCommandMapping();

        return mapping.ContainsKey(emoteName) ? mapping[emoteName] : Emoticons.GetEmoticonDefaultCommand(emoteName);
    }
}
