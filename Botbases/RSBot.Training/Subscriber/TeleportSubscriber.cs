using RSBot.Core;
using RSBot.Core.Event;
using RSBot.Training.Bundle;

namespace RSBot.Training.Subscriber;

internal class TeleportSubscriber
{
    /// <summary>
    ///     Subscribes the events.
    /// </summary>
    public static void SubscribeEvents()
    {
        EventManager.SubscribeEvent("OnTeleportStart", OnTeleportStart);
        EventManager.SubscribeEvent("OnTeleportComplete", OnTeleportComplete);
    }

    #region Event listeners

    /// <summary>
    ///     Will be triggered when an ingame teleportation was started
    /// </summary>
    private static void OnTeleportStart()
    {
        if (!Kernel.Bot.Running)
            return;

        Bundles.Loop.OnTeleportStart();

        if (Bundles.Loop.Running)
            Bundles.Loop.Stop();
    }

    /// <summary>
    ///     Will be triggered when an ingame teleportation was complete
    /// </summary>
    private static void OnTeleportComplete()
    {
        // The loop must learn about the completion even when the bot is not running: otherwise the flag
        // stays set and the next run would wait for a teleport that will never be reported again.
        Bundles.Loop.OnTeleportComplete();

        if (!Kernel.Bot.Running)
            return;
    }

    #endregion Event listeners
}
