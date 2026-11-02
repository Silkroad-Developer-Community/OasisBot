using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using RSBot.Core;
using RSBot.Core.Client;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.General.Components;
using RSBot.General.Models;
using SDUI.Controls;

namespace RSBot.General.Views;

[ToolboxItem(false)]
internal partial class Main : DoubleBufferedControl
{
    private bool _clientVisible;
    private bool _clientlessActionPending;
    private static int _reloginSeq;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Main" /> class.
    /// </summary>
    public Main()
    {
        //CheckForIllegalCrossThreadCalls = false;

        InitializeComponent();
        SubscribeEvents();
    }

    /// <summary>
    ///     Subscribes the events.
    /// </summary>
    private void SubscribeEvents()
    {
        EventManager.SubscribeEvent("OnLoadVersionInfo", new Action<VersionInfo>(OnLoadVersionInfo));
        EventManager.SubscribeEvent("OnClientConnected", OnClientConnected);
        EventManager.SubscribeEvent("OnStartClient", OnStartClient);
        EventManager.SubscribeEvent("OnCharacterListReceived", OnCharacterListReceived);
        EventManager.SubscribeEvent("OnInitialized", OnInitialized);
        EventManager.SubscribeEvent("OnAutoLoginAborted", OnAutoLoginAborted);
        EventManager.SubscribeEvent("OnSwitchToClientless", OnSwitchToClientless);
        EventManager.SubscribeEvent("OnAutoReloginStarted", OnAutoReloginStarted);
        EventManager.SubscribeEvent("OnClientDisconnected", OnClientDisconnected);
        EventManager.SubscribeEvent("OnAutoReloginOngoing", OnAutoReloginOngoing);
        EventManager.SubscribeEvent("OnEnterGame", OnEnterGame);
        EventManager.SubscribeEvent("OnExitClient", OnExitClient);
        EventManager.SubscribeEvent("OnClientProcessStarted", OnClientProcessStarted);
        EventManager.SubscribeEvent("OnClientlessProcessStarted", OnClientlessProcessStarted);
    }

    /// <summary>
    ///     Called when main window loaded.
    /// </summary>
    private void OnInitialized()
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(OnInitialized));
            return;
        }
        comboBoxClientType.Items.AddRange(Enum.GetNames(typeof(GameClientType)));
        comboCharacter.SelectedIndex = 0;

        Accounts.Load();
        LoadAccounts();

        //Load and display config

        txtSilkroadPath.Text = Path.Combine(
            GlobalConfig.Profile.SilkroadDirectory,
            GlobalConfig.Profile.SilkroadExecutable
        );
        checkEnableStaticCaptcha.Checked = GlobalConfig.Profile.Bot.EnableStaticCaptcha;
        checkEnableAutoLogin.Checked = GlobalConfig.Profile.Bot.EnableAutomatedLogin;
        checkStartBot.Checked = GlobalConfig.Profile.Bot.StartBot;
        checkUseReturnScroll.Checked = GlobalConfig.Profile.Bot.UseReturnScroll;
        checkStayConnected.Checked = GlobalConfig.Profile.Bot.StayConnected;
        checkBoxBotTrayMinimized.Checked = GlobalConfig.Profile.Bot.TrayWhenMinimize;
        txtStaticCaptcha.Text = GlobalConfig.Profile.Bot.StaticCaptcha;
        checkEnableLoginDelay.Checked = GlobalConfig.Profile.Bot.EnableLoginDelay;
        numLoginDelay.Value = GlobalConfig.Profile.Bot.LoginDelay;
        checkWaitAfterDC.Checked = GlobalConfig.Profile.Bot.EnableWaitAfterDC;
        numWaitAfterDC.Value = GlobalConfig.Profile.Bot.WaitAfterDC;
        checkHideClient.Checked = GlobalConfig.Profile.Bot.HideOnStartClient;
        checkCharAutoSelect.Checked = GlobalConfig.Profile.Bot.CharacterAutoSelect;
        radioAutoSelectFirst.Checked = GlobalConfig.Profile.Bot.CharacterAutoSelectFirst;
        radioAutoSelectHigher.Checked = GlobalConfig.Profile.Bot.CharacterAutoSelectHigher;
        checkAutoHidePendingWindow.Checked = GlobalConfig.Profile.Bot.AutoHidePendingWindow;
        checkEnableQueueLogs.Checked = GlobalConfig.Profile.Bot.PendingEnableQueueLogs;
        checkEnableQueueNotification.Checked = GlobalConfig.Profile.Bot.EnableQueueNotification;
        numQueueLeft.Value = GlobalConfig.Profile.Bot.QueueLeft;

        if (GlobalConfig.Profile.Bot.CharacterAutoSelect)
        {
            radioAutoSelectFirst.Enabled = true;
            radioAutoSelectHigher.Enabled = true;
        }

        comboBoxClientType.SelectedIndex = (int)Game.ClientType;

        if (!File.Exists(GlobalConfig.Profile.SilkroadDirectory + "\\media.pk2"))
            txtSilkroadPath.BackColor = Color.Red;

        if (!string.IsNullOrEmpty(Kernel.LaunchMode))
        {
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                if (Kernel.LaunchMode == "client")
                {
                    BeginInvoke(
                        new Action(() =>
                        {
                            btnStartClient_Click(btnStartClient, EventArgs.Empty);
                        })
                    );
                }
                else if (Kernel.LaunchMode == "clientless")
                {
                    BeginInvoke(
                        new Action(() =>
                        {
                            btnStartClientless_Click(btnStartClientless, EventArgs.Empty);
                        })
                    );
                }
            });
        }
    }

    /// <summary>
    ///     Called when account character list updated
    /// </summary>
    private void OnCharacterListReceived()
    {
        LoadAccounts();
    }

    /// <summary>
    ///     Loads the accounts.
    /// </summary>
    private void LoadAccounts()
    {
        comboAccounts.Items.Clear();
        comboAccounts.Items.Add(LanguageManager.GetLang("NotSelected"));

        var autoLoginUserName = GlobalConfig.Profile.Bot.AutoLoginAccountUsername;
        foreach (var account in Accounts.SavedAccounts)
        {
            var index = comboAccounts.Items.Add(account);
            if (account.Username == autoLoginUserName)
                comboAccounts.SelectedIndex = index;
        }

        if (comboAccounts.SelectedIndex == -1)
            comboAccounts.SelectedIndex = 0;
    }

    /// <summary>
    ///     Fill the combobox on the form
    /// </summary>
    private void LoadCharacters()
    {
        comboCharacter.Items.Clear();
        comboCharacter.Items.Add(LanguageManager.GetLang("NotSelected"));

        var selectedAccount = comboAccounts.SelectedItem as Account;
        if (selectedAccount?.Characters == null)
        {
            comboCharacter.SelectedIndex = 0;
            return;
        }

        foreach (var character in selectedAccount.Characters.Where(n => n != null))
        {
            var index = comboCharacter.Items.Add(character);
            if (character == selectedAccount.SelectedCharacter)
                comboCharacter.SelectedIndex = index;
        }

        if (comboCharacter.SelectedIndex == -1 || string.IsNullOrWhiteSpace(selectedAccount.SelectedCharacter))
            comboCharacter.SelectedIndex = 0;
    }

    /// <summary>
    ///     Called when [start client].
    /// </summary>
    private void OnStartClient()
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(OnStartClient));
            return;
        }
        btnStartClient.Enabled = false;
        btnStartClientless.Enabled = false;
        _clientVisible = true;
        btnClientHideShow.Enabled = true;

        if (GlobalConfig.Profile.Bot.HideOnStartClient)
            ClientManager.SetVisible(false);
    }

    /// <summary>
    ///     Called when [exit client].
    /// </summary>
    private void OnExitClient()
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(OnExitClient));
            return;
        }
        btnStartClient.Text = LanguageManager.GetLang("Start") + " Client";

        if (GeneralManager.IsClientless)
            return;

        btnStartClient.Enabled = true;
        btnStartClientless.Enabled = true;
        btnClientHideShow.Enabled = false;
    }

    /// <summary>
    ///     The on load version information.
    /// </summary>
    /// <param name="info">The information.</param>
    private void OnLoadVersionInfo(VersionInfo info)
    {
        lblVersion.Text = "v" + ((1000f + info.Version) / 1000f).ToString("0.000", CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Called when [client connected].
    /// </summary>
    private void OnClientConnected()
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(OnClientConnected));
            return;
        }
        btnStartClientless.Enabled = false;
    }

    private void OnSwitchToClientless()
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(OnSwitchToClientless));
            return;
        }
        btnStartClientless.Text = LanguageManager.GetLang("Disconnect");
        btnGoClientless.Enabled = false;
        btnStartClient.Enabled = true;
        btnStartClientless.Enabled = true;
        btnClientHideShow.Enabled = false;
    }

    #region LogicEvents
    private void OnAutoLoginAborted()
    {
        // End() invalidates queued starts synchronously and dispatches its own UI cleanup.
        View.PendingWindow?.End();
    }

    private void OnAutoReloginStarted()
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(OnAutoReloginStarted));
            return;
        }
        btnStartClient.Enabled = false;
        btnStartClientless.Enabled = false;
    }

    private void OnClientDisconnected()
    {
        if (IsDisposed || Disposing)
            return;
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(OnClientDisconnected));
            return;
        }

        btnGoClientless.Enabled = false;
        btnStartClient.Enabled = true;
        btnStartClientless.Enabled = true;

        btnStartClient.Text = LanguageManager.GetLang("Start") + " Client";
        btnStartClientless.Text = LanguageManager.GetLang("Start") + " Clientless";
    }

    private void OnAutoReloginOngoing()
    {
        if (IsDisposed || Disposing)
            return;
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(OnAutoReloginOngoing));
            return;
        }

        btnStartClient.Enabled = true;
        btnStartClientless.Enabled = true;
        btnStartClientless.Text = LanguageManager.GetLang("Start") + " Clientless";
    }

    private void OnClientProcessStarted()
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(OnClientProcessStarted));
            return;
        }

        btnStartClient.Enabled = false;
    }

    private void OnClientlessProcessStarted()
    {
        if (IsDisposed || Disposing)
            return;
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(OnClientlessProcessStarted));
            return;
        }

        btnStartClientless.Text = LanguageManager.GetLang("Disconnect");
    }

    private void OnEnterGame()
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(OnEnterGame));
            return;
        }
        if (!Game.Clientless)
        {
            btnClientHideShow.Enabled = true;
            btnClientHideShow.Text = LanguageManager.GetLang("Hide") + " Client";
            btnStartClient.Enabled = true;
            btnStartClient.Text = LanguageManager.GetLang("Kill") + " Client";
            btnGoClientless.Enabled = true;
        }
    }
    #endregion

    /// <summary>
    ///     Handles the Click event of the btnBrowseSilkroadPath control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnBrowseSilkroadPath_Click(object sender, EventArgs e)
    {
        using (var dialog = new OpenFileDialog())
        {
            var title = LanguageManager.GetLang("BrowseSilkroadPathDialogTitle");

            var msgBoxTitle = LanguageManager.GetLang("BrowseSilkroadPathMsgBoxTitle");
            var msgBoxContent = LanguageManager.GetLang("BrowseSilkroadPathMsgBoxContent");

            dialog.Title = title;
            dialog.Filter = "App (*.exe)|*.exe";
            dialog.FileName = "sro_client.exe";

            var result = dialog.ShowDialog();
            if (result != DialogResult.OK)
                return;

            GeneralManager.ChangeSilkroadPath(dialog.FileName);

            txtSilkroadPath.Text = dialog.FileName;

            result = MessageBox.Show(msgBoxContent, msgBoxTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            GlobalConfig.Save();

            if (result == DialogResult.Yes)
            {
                Application.Restart();
            }
        }
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkStartBot control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void checkAutoStartBot_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.StartBot = checkStartBot.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkUseReturnScroll control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void checkUseReturnScroll_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.UseReturnScroll = checkUseReturnScroll.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the Click event of the btnAutoLoginSettings control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnAutoLoginSettings_Click(object sender, EventArgs e)
    {
        if (View.AccountsWindow.ShowDialog() == DialogResult.OK)
            LoadAccounts();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkEnableAutoLogin control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void checkEnableAutoLogin_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.EnableAutomatedLogin = checkEnableAutoLogin.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the SelectedIndexChanged event of the comboAccounts control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void comboAccounts_SelectedIndexChanged(object sender, EventArgs e)
    {
        var selectedAccount = comboAccounts.SelectedIndex == 0 ? string.Empty : comboAccounts.SelectedItem.ToString();

        GlobalConfig.Profile.Bot.AutoLoginAccountUsername = selectedAccount;
        GlobalConfig.Save();

        LoadCharacters();
    }

    /// <summary>
    ///     Handles the TextChanged event of the txtStaticCaptcha control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void txtStaticCaptcha_TextChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.StaticCaptcha = txtStaticCaptcha.Text;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkEnableStaticCaptcha control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void checkEnableStaticCaptcha_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.EnableStaticCaptcha = checkEnableStaticCaptcha.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the SelectedIndexChanged event of the comboCharacter control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void comboCharacter_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (comboAccounts.SelectedIndex == 0)
            return;

        checkCharAutoSelect.Enabled = comboCharacter.SelectedIndex == 0;

        var selectedAccount = comboAccounts.SelectedItem as Account;
        if (selectedAccount == null)
            return;

        selectedAccount.SelectedCharacter =
            comboCharacter.SelectedIndex == 0 ? string.Empty : comboCharacter.SelectedItem.ToString();

        Accounts.Save();
    }

    /// <summary>
    ///     Handles the Click event of the btnGoClientless control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void btnGoClientless_Click(object sender, EventArgs e)
    {
        if (Game.Clientless)
            return;
        var msgBoxTitle = LanguageManager.GetLang("GoClientlessMsgBoxTitle");
        var msgBoxContent = LanguageManager.GetLang("GoClientlessMsgBoxContent");

        if (
            MessageBox.Show(msgBoxContent, msgBoxTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            != DialogResult.Yes
        )
            return;
        GeneralManager.GoClientless();
    }

    /// <summary>
    ///     Handles the Click event of the btnStartClientless control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private async void btnStartClientless_Click(object sender, EventArgs e)
    {
        if (_clientlessActionPending)
            return;

        if (!Game.Clientless)
        {
            if (!checkEnableAutoLogin.Checked || comboAccounts.SelectedIndex <= 0)
            {
                MessageBox.Show(
                    this,
                    LanguageManager.GetLang("StartClientlessMsgBoxContent"),
                    LanguageManager.GetLang("StartClientlessMsgBoxTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }
            _clientlessActionPending = true;
            btnStartClient.Enabled = false;
            btnClientHideShow.Enabled = false;
            try
            {
                await GeneralManager.StartClientlessAsync();
            }
            catch (Exception ex)
            {
                Log.Error($"Could not start clientless: {ex}");
                if (!IsDisposed && !Disposing)
                    OnClientDisconnected();
            }
            finally
            {
                _clientlessActionPending = false;
                if (!IsDisposed && !Disposing)
                {
                    if (!Game.Clientless)
                        btnClientHideShow.Enabled = ClientManager.IsRunning;
                }
            }
            return;
        }

        if (
            MessageBox.Show(
                this,
                LanguageManager.GetLang("MsgBoxDisconnectDialogContent"),
                LanguageManager.GetLang("MsgBoxDisconnectDialogTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            ) != DialogResult.Yes
        )
            return;

        _clientlessActionPending = true;
        btnStartClientless.Enabled = false;
        btnStartClient.Enabled = false;
        try
        {
            await GeneralManager.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Log.Error($"Disconnect failed: {ex}");
            if (!IsDisposed && !Disposing)
                OnClientDisconnected();
        }
        finally
        {
            _clientlessActionPending = false;
        }
    }

    /// <summary>
    ///     Handles the Click event of the btnStartClient control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private async void btnStartClient_Click(object sender, EventArgs e)
    {
        if (_clientlessActionPending)
            return;

        if (!GeneralManager.IsClientless && GeneralManager.IsConnected)
        {
            var extraStr = LanguageManager.GetLang("KillClientWarnMsgBoxSplit1");
            if (!GlobalConfig.Profile.Bot.StayConnected)
                extraStr = LanguageManager.GetLang("KillClientWarnMsgBoxSplit2");

            var title = LanguageManager.GetLang("Warning");
            var content = LanguageManager.GetLang("KillClientWarnMsgBoxContent", extraStr);

            if (MessageBox.Show(content, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                GeneralManager.KillClient();
            return;
        }
        if (GeneralManager.IsConnected)
            return;
        await GeneralPlugin.Instance.Manager.StartClientAsync();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkStayConnected control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void checkStayConnected_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.StayConnected = checkStayConnected.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkBoxBotTrayMinimized control
    /// </summary>
    private void checkBoxBotTrayMinimized_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.TrayWhenMinimize = checkBoxBotTrayMinimized.Checked;
        GlobalConfig.Save();
    }

    private void btnClientHideShow_Click(object sender, EventArgs e)
    {
        if (!ClientManager.IsRunning)
            return;

        if (!_clientVisible)
        {
            _clientVisible = true;
            ClientManager.SetVisible(true);
            btnClientHideShow.Text = LanguageManager.GetLang("Hide") + " Client";
        }
        else
        {
            _clientVisible = false;
            ClientManager.SetVisible(false);
            btnClientHideShow.Text = LanguageManager.GetLang("Show") + " Client";
        }
    }

    /// <summary>
    ///     Handles the SelectedIndexChanged event of the comboBoxClientType control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void comboBoxClientType_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Created from Activator.CreateInstance easy fix ^^
        if (comboBoxClientType.Parent.Parent == null)
            return;

        if (Game.Player != null)
        {
            MessageBox.Show(LanguageManager.GetLang("MsgBoxClientTypeWarn"));
            return;
        }

        var clientType = (GameClientType)comboBoxClientType.SelectedIndex;

        GlobalConfig.Profile.Bot.ClientType = clientType;
        Game.ClientType = clientType;
        GlobalConfig.Save();

        if (clientType.ToString().StartsWith("Vietnam"))
            captchaPanel.Visible = true;
        else
            captchaPanel.Visible = false;
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkEnableLoginDelay control.
    /// </summary>
    /// <param name="sender">
    ///     The source of the event.
    /// </param>
    /// <param name="e">
    ///     The <see cref="EventArgs" /> instance containing the event data.
    /// </param>
    private void checkEnableLoginDelay_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.EnableLoginDelay = checkEnableLoginDelay.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the ValueChanged event of the numLoginDelay control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void numLoginDelay_ValueChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.LoginDelay = numLoginDelay.Value;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkWaitAfterDC control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void checkWaitAfterDC_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.EnableWaitAfterDC = checkWaitAfterDC.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the ValueChanged event of the numWaitAfterDC control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    private void numWaitAfterDC_ValueChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.WaitAfterDC = numWaitAfterDC.Value;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkHideClient control.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void checkHideClient_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.HideOnStartClient = checkHideClient.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkCharAutoSelect control.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void checkCharAutoSelect_CheckedChanged(object sender, EventArgs e)
    {
        if (!GlobalConfig.Profile.Bot.CharacterAutoSelect)
        {
            radioAutoSelectFirst.Enabled = true;
            radioAutoSelectHigher.Enabled = true;
        }
        else
        {
            radioAutoSelectFirst.Enabled = false;
            radioAutoSelectHigher.Enabled = false;
        }

        GlobalConfig.Profile.Bot.CharacterAutoSelect = checkCharAutoSelect.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the radioAutoSelectFirst control.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void radioAutoSelectFirst_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.CharacterAutoSelectFirst = radioAutoSelectFirst.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the radioAutoSelectHigher control.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void radioAutoSelectHigher_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.CharacterAutoSelectHigher = radioAutoSelectHigher.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkDontShowPendingOnStartClient control.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void checkDontShowPendingOnStartClient_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.AutoHidePendingWindow = checkAutoHidePendingWindow.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkEnableQuequeLogs control.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void checkEnableQueueLogs_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.PendingEnableQueueLogs = checkEnableQueueLogs.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the Click event of the btnShowPending control.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void btnShowPending_Click(object sender, EventArgs e)
    {
        if (!AutoLogin.Pending)
            return;

        if (View.PendingWindow?.Visible == false)
            View.PendingWindow.ShowAtTop(View.Instance);
        else
            View.PendingWindow.Hide();
    }

    /// <summary>
    ///     Handles the CheckedChanged event of the checkEnableQueueNotification control.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void checkEnableQueueNotification_CheckedChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.EnableQueueNotification = checkEnableQueueNotification.Checked;
        GlobalConfig.Save();
    }

    /// <summary>
    ///     Handles the ValueChanged event of the numQuequeLeft control.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void numQueueLeft_ValueChanged(object sender, EventArgs e)
    {
        GlobalConfig.Profile.Bot.QueueLeft = numQueueLeft.Value;
        GlobalConfig.Save();
    }

    /// <summary>
    /// Handes the SoundSetting event for open dialog
    /// </summary>
    private void btnSoundSettingSetup_Click(object sender, EventArgs e)
    {
        if (View.SoundNotificationWindow.ShowDialog() == DialogResult.OK)
        {
            // nothing
        }
    }
}
