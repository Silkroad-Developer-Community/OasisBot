using System;
using System.Text;
using System.Windows.Forms;
using RSBot.Core;
using RSBot.Core.Components;
using SDUI.Controls;

namespace RSBot.Views;

public partial class ConfigDialog : UIWindowBase
{
    public ConfigDialog()
    {
        InitializeComponent();
        Text = "Config";
    }

    private void ExitDialog_Load(object sender, EventArgs e)
    {
        LanguageManager.Translate(this, Kernel.Language);

        var proxy = GlobalConfig.Profile.Bot.Proxy;

        try
        {
            var isActive = proxy.IsActive;
            checkBoxOnOf.Checked = isActive;
            textBoxProxyIp.Text = proxy.Ip;

            var proxyPort = proxy.Port;
            if (proxyPort > 0)
                textBoxPort.Text = proxyPort.ToString();
            textBoxId.Text = proxy.Username;
            textBoxPw.Text = proxy.Password;

            comboBoxProxyVersion.SelectedIndex = proxy.Version == 4 ? 0 : 1;
        }
        catch { }
    }

    private void ConfigDialog_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.Retry)
            e.Cancel = true;
    }

    private void btnConfirm_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(textBoxProxyIp.Text))
        {
            DialogResult = DialogResult.Retry;
            return;
        }

        if (textBoxPort.Text == "0")
        {
            DialogResult = DialogResult.Retry;
            return;
        }

        StringBuilder builder = new();
        GlobalConfig.Profile.Bot.Proxy.IsActive = checkBoxOnOf.Checked;
        GlobalConfig.Profile.Bot.Proxy.Ip = textBoxProxyIp.Text;
        GlobalConfig.Profile.Bot.Proxy.Port = int.Parse(textBoxPort.Text);
        GlobalConfig.Profile.Bot.Proxy.Username = textBoxId.Text;
        GlobalConfig.Profile.Bot.Proxy.Password = textBoxPw.Text;
        GlobalConfig.Profile.Bot.Proxy.Version = (byte)(comboBoxProxyVersion.SelectedIndex == 0 ? 4 : 5);

        GlobalConfig.Save();
    }
}
