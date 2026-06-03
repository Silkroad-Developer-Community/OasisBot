using System.Threading;
using System.Threading.Tasks;
using System;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Plugins;

namespace RSBot.Core;

public class Bot
{
    private readonly object _lock = new();
    private Task _workerTask;

    /// <summary>
    ///     Gets or sets a value indicating whether this <see cref="Bot" /> is running.
    /// </summary>
    /// <value>
    ///     <c>true</c> if running; otherwise, <c>false</c>.
    /// </value>
    public volatile bool Running;

    /// <summary>
    ///     Gets or sets to the <see cref="CancellationToken" />
    /// </summary>
    public CancellationTokenSource TokenSource;

    /// <summary>
    ///     Gets the base.
    /// </summary>
    /// <value>
    ///     The base.
    /// </value>
    public IBotbase Botbase { get; private set; }
    public IBotbaseView BotbaseView { get; private set; }

    /// <summary>
    ///     Sets the botbase.
    /// </summary>
    /// <param name="botBase">The bot base.</param>
    public void SetBotbase(IBotbase botBase)
    {
        Botbase = botBase;

        EventManager.FireEvent("OnSetBotbase", botBase);
    }
    public void SetBotbaseView(IBotbaseView botBaseView)
    {
        BotbaseView = botBaseView;
        EventManager.FireEvent("OnSetBotbaseView", botBaseView);
    }
    /// <summary>
    ///     Starts this instance.
    /// </summary>
    public void Start()
    {
        CancellationTokenSource tokenSource;

        lock (_lock)
        {
            if (Running || Botbase == null || (_workerTask != null && !_workerTask.IsCompleted))
                return;

            tokenSource = new CancellationTokenSource();
            TokenSource = tokenSource;
            Running = true;
            _workerTask = Task.Run(() => RunAsync(tokenSource), tokenSource.Token);
        }
    }

    private async Task RunAsync(CancellationTokenSource tokenSource)
    {
        var token = tokenSource.Token;

        try
        {
            EventManager.FireEvent("OnStartBot");
            Botbase.Start();

            while (!token.IsCancellationRequested)
            {
                if (Game.Ready)
                    Botbase.Tick();

                await Task.Delay(100, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Log.Fatal(ex);
        }
        finally
        {
            if (ReferenceEquals(TokenSource, tokenSource))
                Running = false;
        }
    }

    /// <summary>
    ///     Stops this instance.
    /// </summary>
    public void Stop()
    {
        CancellationTokenSource tokenSource;

        lock (_lock)
        {
            if (Botbase == null || !Running)
                return;

            Running = false;
            tokenSource = TokenSource;
        }

        if (tokenSource != null && !tokenSource.IsCancellationRequested)
            tokenSource.Cancel();

        EventManager.FireEvent("OnStopBot");
        Log.Notify($"Stopping bot {Botbase.Name}");

        CancelActionOnStop();

        Game.SelectedEntity = null;

        ScriptManager.Stop();
        ShoppingManager.Stop();
        PickupManager.Stop();
        Botbase.Stop();

        Log.Notify($"Stopped bot {Botbase.Name}");
        Log.Status("Bot stopped");
    }

    private void CancelActionOnStop()
    {
        var player = Game.Player;
        if (player == null)
            return;

        SkillManager.CancelAction(0);

        _ = Task.Run(async () =>
        {
            for (var i = 1; i < 5; i++)
            {
                await Task.Delay(100);

                if (Running || !Game.Ready || !ReferenceEquals(Game.Player, player) || !player.InAction)
                    return;

                SkillManager.CancelAction(0);
            }
        });
    }
}
