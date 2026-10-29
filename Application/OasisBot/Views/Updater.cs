using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using RSBot.Core;
using SDUI.Controls;

namespace RSBot.Views;

public partial class Updater : UIWindow
{
    /// <summary>
    ///     GitHub update address
    /// </summary>
    private readonly string _githubUrl = "https://api.github.com/repos/Silkroad-Developer-Community/OasisBot/releases";

    /// <summary>
    ///     Localhost update address (for testing)
    /// </summary>
    private readonly string _localhostUrl = "http://localhost:8000/update.json";

    /// <summary>
    ///     Final update address
    /// </summary>
    private string _updateUrl => GlobalConfig.Get("RSBot.DebugEnvironment", false) ? _localhostUrl : _githubUrl;

    /// <summary>
    ///     Get or sets the web client
    /// </summary>
    private WebClient _webClient;

    /// <summary>
    /// The download URL for the installer
    /// </summary>
    private string _downloadUrl;

    /// <summary>
    /// Indicates whether a download is currently in progress
    /// </summary>
    private bool _isDownloading;

    public Updater()
    {
        InitializeComponent();
        CheckForIllegalCrossThreadCalls = false;
    }

    /// <summary>
    ///     Get current version
    /// </summary>
    private Version _currentVersion => Assembly.GetExecutingAssembly().GetName().Version;

    private void Append(string text, Color color, FontStyle fontStyle = FontStyle.Regular, float emSize = 0)
    {
        rtbUpdateInfo.SuspendLayout();
        rtbUpdateInfo.Select(rtbUpdateInfo.TextLength, text.Length);
        rtbUpdateInfo.SelectionColor = color;
        rtbUpdateInfo.SelectionFont = new Font(
            Font.FontFamily,
            emSize == 0 ? rtbUpdateInfo.Font.Size : emSize,
            fontStyle
        );
        rtbUpdateInfo.Write(text);
        rtbUpdateInfo.ResumeLayout();
    }

    private void _Client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
    {
        if (e.Cancelled)
        {
            _isDownloading = false;
            btnDownload.Enabled = true;
            MessageBox.Show(
                "The update download was cancelled.",
                "Update Cancelled",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        if (e.Error != null)
        {
            _isDownloading = false;
            btnDownload.Enabled = true;
            MessageBox.Show(
                $"Failed to download update: {e.Error.Message}",
                "Update Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
            return;
        }

        var installerPath = Path.Combine(Kernel.BasePath, "RSBot-Setup-Latest.exe");
        try
        {
            Process.Start(installerPath);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _isDownloading = false;
            btnDownload.Enabled = true;
            MessageBox.Show(
                $"Failed to launch installer: {ex.Message}\n\nNote: If you are testing with the mock updater, the downloaded file is a dummy and cannot be executed.",
                "Update Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void _Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        try
        {
            downloadProgress.Value = e.ProgressPercentage;
            lblDownloadInfo.Text = string.Format(
                "{0} MB / {1} MB",
                (e.BytesReceived / 1024d / 1024d).ToString("0.00"),
                (e.TotalBytesToReceive / 1024d / 1024d).ToString("0.00")
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(0);
        }
    }

    private void btnDownload_Click(object sender, EventArgs e)
    {
        StartDownload();
    }

    public void StartAutomaticUpdate()
    {
        StartDownload();
    }

    private void StartDownload()
    {
        if (_webClient == null || string.IsNullOrWhiteSpace(_downloadUrl) || _isDownloading)
            return;

        _isDownloading = true;
        btnDownload.Enabled = false;

        try
        {
            downloadProgress.Visible = true;
            downloadProgress.Location = new Point(20, 45);
            cbChangeLog.Checked = false;
            centerPanel.Visible = false;
            lblInfo.Text = "Downloading updates ...";

            var installerPath = Path.Combine(Kernel.BasePath, "RSBot-Setup-Latest.exe");

            _webClient.DownloadProgressChanged -= _Client_DownloadProgressChanged;
            _webClient.DownloadFileCompleted -= _Client_DownloadFileCompleted;
            _webClient.DownloadProgressChanged += _Client_DownloadProgressChanged;
            _webClient.DownloadFileCompleted += _Client_DownloadFileCompleted;
            _webClient.DownloadFileAsync(new Uri(_downloadUrl), installerPath);
        }
        catch (Exception ex)
        {
            _isDownloading = false;
            btnDownload.Enabled = true;
            MessageBox.Show(ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(0);
        }
    }

    private async void cbChangeLog_CheckedChanged(object sender, EventArgs e)
    {
        await Task.Run(() =>
        {
            if (cbChangeLog.Checked)
                for (var i = Height; i <= 400; i += 4)
                    Height += 4;
            else
                for (var i = Height; i >= 110; i -= 4)
                    Height -= 4;
        });
    }

    /// <summary>
    ///     Check for Updates
    /// </summary>
    /// <returns><c>true</c> if there is otherwise, <c>false</c>.</returns>
    public async Task<bool> Check()
    {
        if (
            !File.Exists(Path.Combine(Kernel.BasePath, "unins000.exe"))
            && !GlobalConfig.Get("RSBot.DebugEnvironment", false)
        )
            return false;

        try
        {
            _webClient = new WebClient();
            _webClient.Headers.Add("User-Agent", "OasisBot-Updater");
            var json = await _webClient.DownloadStringTaskAsync(_updateUrl);
            var releases = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);

            if (releases == null || releases.Count == 0)
                return false;

            var latest = releases[0];
            var versionString = (string)latest.tag_name;
            if (versionString.StartsWith("v"))
                versionString = versionString.Substring(1);

            var version = new Version(versionString);

            if (version > _currentVersion)
            {
                _downloadUrl = (string)latest.assets[0].browser_download_url;
                var body = (string)latest.body;

                Append("Build " + version, Color.FromArgb(99, 33, 99), FontStyle.Regular, 13);
                Append("\n", Color.DarkGray);

                if (!string.IsNullOrEmpty(body))
                {
                    foreach (var line in body.Split('\n'))
                        Append(line + "\n", Color.DarkSlateGray);
                }

                rtbUpdateInfo.SelectionStart = 0;

                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to check for updates: {ex.Message}");
        }

        return false;
    }
}
