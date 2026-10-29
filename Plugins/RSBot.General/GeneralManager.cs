using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.General.Components;

namespace RSBot.General
{
    public class GeneralManager
    {
        private static int _reloginSeq;
        public static bool IsClientless =>
            Game.Clientless && Kernel.Proxy != null && Kernel.Proxy.IsConnectedToAgentserver;
        public static bool IsConnected => Kernel.Proxy != null && Kernel.Proxy.IsConnectedToAgentserver;

        public GeneralManager()
        {
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            ClientlessManager.RegionalAuthHandler = HandleRegionalAuth;

            EventManager.SubscribeEvent("OnAgentServerConnected", OnAgentServerConnected);
            EventManager.SubscribeEvent("OnAgentServerDisconnected", OnAgentServerDisconnected);
            EventManager.SubscribeEvent("OnGatewayServerDisconnected", OnGatewayServerDisconnected);
            EventManager.SubscribeEvent("OnEnterGame", OnEnterGame);
            EventManager.SubscribeEvent("OnExitClient", OnExitClient);
            EventManager.SubscribeEvent("OnProfileChanged", OnProfileChanged);
        }

        private static async Task<bool> HandleRegionalAuth()
        {
            if (Game.ClientType == GameClientType.RuSro)
                return await RuSroAuthService.Auth();
            else if (Game.ClientType == GameClientType.Japanese)
                return await JSROAuthService.GetTokenAsync();
            return true;
        }

        private void OnAgentServerConnected()
        {
            Interlocked.Increment(ref _reloginSeq);
            AutoLogin.Pending = false;
            EventManager.FireEvent("OnAutoLoginAborted");
        }

        private async void OnAgentServerDisconnected()
        {
            Kernel.Bot.Stop();

            // Skiped: Cuz managing from ClientlessManager
            if (Game.Clientless)
                return;

            ClientManager.Kill();

            if (GlobalConfig.Get<bool>("RSBot.General.EnableAutomatedLogin"))
            {
                var reloginSeq = Interlocked.Increment(ref _reloginSeq);

                EventManager.FireEvent("OnAutoReloginStarted");

                int delay = 10000;
                if (GlobalConfig.Get("RSBot.General.EnableWaitAfterDC", false))
                    delay = GlobalConfig.Get<int>("RSBot.General.WaitAfterDC") * 60 * 1000;

                Log.Warn($"Attempting relogin in {delay / 1000} seconds...");
                await Task.Delay(delay);

                if (reloginSeq != Volatile.Read(ref _reloginSeq))
                    return;

                var userAuthenticated = await HandleRegionalAuth();
                if (!userAuthenticated)
                {
                    Log.Warn("Regional auth failed!");
                    return;
                }

                await StartClientProcess();
                return;
            }
            EventManager.FireEvent("OnClientDisconnected");
        }

        private async void OnGatewayServerDisconnected()
        {
            AutoLogin.Pending = false;
            EventManager.FireEvent("OnAutoLoginAborted");

            var wasClientless = Game.Clientless;

            if (!Kernel.Proxy.IsConnectedToAgentserver && !Kernel.Proxy.IsSwitchingToAgentserver)
            {
                if (GlobalConfig.Get<bool>("RSBot.General.EnableAutomatedLogin"))
                {
                    var reloginSeq = Interlocked.Increment(ref _reloginSeq);

                    EventManager.FireEvent("OnAutoReloginStarted");

                    // Gateway disconnect can happen briefly during Gateway -> Agent switch (or the switch can start late
                    // due to thread scheduling). Give it a short grace period before we decide it's a real DC.
                    await Task.Delay(2000);

                    if (reloginSeq != Volatile.Read(ref _reloginSeq))
                        return;

                    if (Kernel.Proxy.IsConnectedToAgentserver || Kernel.Proxy.IsSwitchingToAgentserver)
                        return;

                    EventManager.FireEvent("OnAutoReloginOngoing");

                    Log.StatusLang("Ready");
                    Kernel.Proxy.Shutdown();

                    int delay = 10000;
                    if (GlobalConfig.Get("RSBot.General.EnableWaitAfterDC", false))
                        delay = GlobalConfig.Get<int>("RSBot.General.WaitAfterDC") * 60 * 1000;

                    Log.Warn($"Attempting relogin in {delay / 1000} seconds...");
                    await Task.Delay(delay);

                    // Prevent double-start if the disconnect event fires multiple times.
                    if (reloginSeq != Volatile.Read(ref _reloginSeq))
                        return;

                    var userAuthenticated = await HandleRegionalAuth();
                    if (!userAuthenticated)
                    {
                        Log.Warn("Regional auth failed!");
                        return;
                    }

                    if (wasClientless)
                    {
                        // Clientless relogin: restart proxy flow only.
                        Game.Clientless = true;
                        Game.Start();
                    }
                    else
                    {
                        // Client relogin: restart client process.
                        ClientManager.Kill();
                        await StartClientProcess();
                    }

                    return;
                }

                EventManager.FireEvent("OnClientDisconnected");

                Log.StatusLang("Ready");
                Kernel.Proxy.Shutdown();

                Game.Clientless = false;
            }
        }

        private async void OnEnterGame()
        {
            while (!Game.Ready)
                await Task.Delay(100);

            var startBot = GlobalConfig.Get<bool>("RSBot.General.StartBot");
            var useReturnScroll = GlobalConfig.Get<bool>("RSBot.General.UseReturnScroll");

            if (useReturnScroll)
                Game.Player.UseReturnScroll();

            if (startBot)
                Kernel.Bot.Start();
        }

        private void OnExitClient()
        {
            Log.StatusLang("Ready");

            if (Game.Clientless)
                return;

            if (!GlobalConfig.Get<bool>("RSBot.General.StayConnected"))
            {
                Kernel.Proxy.Shutdown();
            }
            else
            {
                if (!Kernel.Proxy.IsConnectedToAgentserver)
                    return;

                if (!ClientlessManager.TryGoClientless())
                    return;

                EventManager.FireEvent("OnSwitchToClientless");

                Log.NotifyLang("ClientlessModeActivated");
            }
        }

        private void OnProfileChanged()
        {
            Accounts.Load();
        }

        private async Task StartClientProcess()
        {
            EventManager.FireEvent("OnClientProcessStarted");
            Game.Start();

            await Task.Run(async () =>
            {
                var startedResult = await ClientManager.Start();
                if (!startedResult)
                {
                    OnExitClient();
                    Log.WarnLang("ClientStartingError");
                }
            });
        }

        public static void ChangeSilkroadPath(string path)
        {
            GlobalConfig.Set("RSBot.SilkroadDirectory", Path.GetDirectoryName(path));
            GlobalConfig.Set("RSBot.SilkroadExecutable", Path.GetFileName(path));
        }

        public static void GoClientless()
        {
            if (Game.Clientless)
                return;

            if (!ClientlessManager.TryGoClientless())
                return;
            ClientManager.Kill();

            EventManager.FireEvent("OnSwitchToClientless");
        }

        public static async Task StartClientlessAsync()
        {
            await Task.Run(async () =>
            {
                if (Game.Clientless)
                    return;

                var started = false;
                Game.Clientless = true;
                try
                {
                    Log.StatusLang("StartingClientless");
                    EventManager.FireEvent("OnClientlessProcessStarted");

                    var userAuthenticated = await HandleRegionalAuth();
                    if (!userAuthenticated)
                    {
                        Log.Warn("Regional auth failed!");
                        return;
                    }

                    Game.Start();
                    started = true;
                }
                finally
                {
                    if (!started)
                    {
                        Game.Clientless = false;
                        EventManager.FireEvent("OnClientDisconnected");
                    }
                }
            });
        }

        public static async Task DisconnectAsync()
        {
            var proxy = Kernel.Proxy;
            await Task.Run(() =>
            {
                Game.Clientless = false;
                AutoLogin.Pending = false;
                EventManager.FireEvent("OnAutoLoginAborted");
                EventManager.FireEvent("OnAutoReloginOngoing");
                proxy?.Shutdown();
            });
        }

        public async Task StartClientAsync()
        {
            var userAuthenticated = await HandleRegionalAuth();

            if (userAuthenticated)
            {
                await StartClientProcess();
            }
        }

        public static void KillClient()
        {
            if (!IsClientless)
            {
                ClientManager.Kill();
            }
        }
    }
}
