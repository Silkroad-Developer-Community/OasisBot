using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using RSBot.Core.Extensions;
using RSBot.General.Components;
using SDUI;
using SDUI.Controls;
using UIButton = SDUI.Controls.Button;
using UIFlowLayoutPanel = SDUI.Controls.FlowLayoutPanel;
using UILabel = SDUI.Controls.Label;

namespace RSBot.General.Views;

internal sealed class CaptchaWindow : UIWindowBase
{
    private static readonly object WebView2LoaderLock = new();
    private static bool _webView2LoaderConfigured;

    private readonly Uri _captchaUri;
    private readonly Uri _returnUri;
    private readonly CookieContainer _cookieContainer;
    private readonly WebView2 _webView;
    private readonly UILabel _statusLabel;
    private readonly UIButton _continueButton;
    private Exception _initializationException;
    private int _isCompleting;

    private CaptchaWindow(Uri captchaUri, Uri returnUri, CookieContainer cookieContainer)
    {
        _captchaUri = captchaUri ?? throw new ArgumentNullException(nameof(captchaUri));
        _returnUri = returnUri ?? throw new ArgumentNullException(nameof(returnUri));
        _cookieContainer = cookieContainer ?? throw new ArgumentNullException(nameof(cookieContainer));

        Text = "4game verification";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(760, 560);
        ClientSize = new Size(900, 700);
        BackColor = ColorScheme.BackColor;
        ForeColor = ColorScheme.ForeColor;
        Padding = new Padding(12);

        var titleLabel = new UILabel
        {
            ApplyGradient = false,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 11.25F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = ColorScheme.ForeColor,
            Text = "Complete the 4game verification in the window below.",
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _statusLabel = new UILabel
        {
            ApplyGradient = false,
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            ForeColor = ColorScheme.ForeColor,
            Text = "Opening the verification page...",
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _webView = new WebView2
        {
            AllowExternalDrop = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 8),
        };

        var cancelButton = new UIButton
        {
            AutoSize = false,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(8, 0, 0, 0),
            Size = new Size(110, 32),
            Text = "Cancel",
        };

        _continueButton = new UIButton
        {
            AutoSize = false,
            Enabled = false,
            Margin = new Padding(8, 0, 0, 0),
            Size = new Size(110, 32),
            Text = "Continue",
        };
        _continueButton.Click += ContinueButton_Click;

        var buttonPanel = new UIFlowLayoutPanel
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = Padding.Empty,
            WrapContents = false,
        };
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(_continueButton);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            RowCount = 4,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(_webView, 0, 1);
        layout.Controls.Add(_statusLabel, 0, 2);
        layout.Controls.Add(buttonPanel, 0, 3);
        Controls.Add(layout);

        CancelButton = cancelButton;
        Shown += CaptchaWindow_Shown;
    }

    internal static Task<bool> ShowAsync(Uri captchaUri, Uri returnUri, CookieContainer cookieContainer)
    {
        Control uiControl = View.Instance;
        if (uiControl.IsDisposed || !uiControl.IsHandleCreated)
        {
            return Task.FromException<bool>(
                new InvalidOperationException("The main application window is not available for 4game verification.")
            );
        }

        if (!uiControl.InvokeRequired)
            return ShowOnUiThreadAsync(uiControl, captchaUri, returnUri, cookieContainer);

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            uiControl.BeginInvoke(
                new Action(async () =>
                {
                    try
                    {
                        completion.SetResult(
                            await ShowOnUiThreadAsync(uiControl, captchaUri, returnUri, cookieContainer)
                        );
                    }
                    catch (Exception ex)
                    {
                        completion.SetException(ex);
                    }
                })
            );
        }
        catch (Exception ex)
        {
            completion.SetException(ex);
        }

        return completion.Task;
    }

    private static Task<bool> ShowOnUiThreadAsync(
        Control uiControl,
        Uri captchaUri,
        Uri returnUri,
        CookieContainer cookieContainer
    )
    {
        using var window = new CaptchaWindow(captchaUri, returnUri, cookieContainer);
        Form owner = uiControl.FindForm();
        DialogResult result = owner == null ? window.ShowDialog() : window.ShowDialog(owner);

        if (window._initializationException != null)
            return Task.FromException<bool>(window._initializationException);

        return Task.FromResult(result == DialogResult.OK);
    }

    private async void CaptchaWindow_Shown(object sender, EventArgs e)
    {
        try
        {
            ConfigureWebView2Loader();

            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OasisBot",
                "WebView2",
                "4game-captcha"
            );
            CoreWebView2EnvironmentOptions environmentOptions = null;
            if (ProxyConfig.TryGetProxy(out ProxyConfig proxyConfig))
            {
                userDataFolder += "-proxy";
                string proxyAddress = ConfiguredProxyBridge
                    .Instance.Configure(proxyConfig)
                    .GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
                environmentOptions = new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments =
                        $"--proxy-server={proxyAddress} --host-resolver-rules=\"MAP * 0.0.0.0, EXCLUDE 127.0.0.1\" --disable-quic",
                };
            }

            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: userDataFolder,
                options: environmentOptions
            );

            await _webView.EnsureCoreWebView2Async(environment);
            ConfigureBrowser();
            ImportCookies();

            _continueButton.Enabled = true;
            _statusLabel.Text = "Complete the verification. This window will close automatically when it succeeds.";
            _webView.CoreWebView2.Navigate(_captchaUri.AbsoluteUri);
        }
        catch (Exception ex)
        {
            _initializationException =
                ex is WebView2RuntimeNotFoundException
                    ? new InvalidOperationException(
                        "Microsoft Edge WebView2 Runtime is required to display the 4game verification page.",
                        ex
                    )
                    : new InvalidOperationException("Could not open the 4game verification page.", ex);

            DialogResult = DialogResult.Abort;
            Close();
        }
    }

    private static void ConfigureWebView2Loader()
    {
        lock (WebView2LoaderLock)
        {
            if (_webView2LoaderConfigured)
                return;

            string runtimeDirectory = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X86 => "win-x86",
                Architecture.X64 => "win-x64",
                Architecture.Arm64 => "win-arm64",
                _ => throw new PlatformNotSupportedException(
                    $"WebView2 does not support the current process architecture: {RuntimeInformation.ProcessArchitecture}."
                ),
            };
            string assemblyDirectory = Path.GetDirectoryName(typeof(CoreWebView2Environment).Assembly.Location);
            string loaderDirectory = Path.Combine(assemblyDirectory, "runtimes", runtimeDirectory, "native");
            string loaderPath = Path.Combine(loaderDirectory, "WebView2Loader.dll");
            if (!File.Exists(loaderPath))
            {
                throw new FileNotFoundException(
                    $"The {RuntimeInformation.ProcessArchitecture} WebView2 loader was not found.",
                    loaderPath
                );
            }

            CoreWebView2Environment.SetLoaderDllFolderPath(loaderDirectory);
            _webView2LoaderConfigured = true;
        }
    }

    private void ConfigureBrowser()
    {
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.CookieManager.DeleteAllCookies();
        _webView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;
        _webView.CoreWebView2.NewWindowRequested += (_, eventArgs) =>
        {
            eventArgs.Handled = true;
            _webView.CoreWebView2.Navigate(eventArgs.Uri);
        };
    }

    private void ImportCookies()
    {
        var importedCookies = new HashSet<string>(StringComparer.Ordinal);
        ImportCookies(_captchaUri, importedCookies);
        ImportCookies(_returnUri, importedCookies);
    }

    private void ImportCookies(Uri uri, HashSet<string> importedCookies)
    {
        CoreWebView2CookieManager cookieManager = _webView.CoreWebView2.CookieManager;
        foreach (Cookie cookie in _cookieContainer.GetCookies(uri))
        {
            string key = $"{cookie.Name}\n{cookie.Domain}\n{cookie.Path}";
            if (!importedCookies.Add(key))
                continue;

            CoreWebView2Cookie webViewCookie = cookieManager.CreateCookieWithSystemNetCookie(cookie);
            cookieManager.AddOrUpdateCookie(webViewCookie);
        }
    }

    private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            _statusLabel.Text = $"The verification page failed to load ({e.WebErrorStatus}).";
            return;
        }

        if (IsReturnUri(_webView.Source))
            await CompleteAsync();
    }

    private async void ContinueButton_Click(object sender, EventArgs e)
    {
        await CompleteAsync();
    }

    private async Task CompleteAsync()
    {
        if (Interlocked.Exchange(ref _isCompleting, 1) != 0)
            return;

        try
        {
            _continueButton.Enabled = false;
            _statusLabel.Text = "Saving the verification result...";

            IReadOnlyList<CoreWebView2Cookie> cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync(null);
            foreach (CoreWebView2Cookie cookie in cookies)
            {
                if (!Is4GameDomain(cookie.Domain))
                    continue;

                _cookieContainer.Add(cookie.ToSystemNetCookie());
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Could not save the verification result: " + ex.Message;
            _continueButton.Enabled = true;
            Interlocked.Exchange(ref _isCompleting, 0);
        }
    }

    private bool IsReturnUri(Uri uri)
    {
        if (uri == null)
            return false;

        return string.Equals(uri.Scheme, _returnUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, _returnUri.Host, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                uri.AbsolutePath.TrimEnd('/'),
                _returnUri.AbsolutePath.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase
            );
    }

    private static bool Is4GameDomain(string domain)
    {
        string normalizedDomain = domain?.TrimStart('.');
        return string.Equals(normalizedDomain, "4game.ru", StringComparison.OrdinalIgnoreCase)
            || normalizedDomain?.EndsWith(".4game.ru", StringComparison.OrdinalIgnoreCase) == true;
    }
}
