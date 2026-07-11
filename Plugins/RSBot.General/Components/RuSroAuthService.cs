using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RSBot.Core;
using RSBot.Core.Components;
using SDUI.Controls;

namespace RSBot.General.Components;

internal static class RuSroAuthService
{
    private const string ClientId = "api.4game.com";
    private const string Scope = "offline_access inn.auth";
    private const string RedirectUri = "https://launcher.ru.4game.com/silent-auth";
    private const string BackUri = "https://launcher.ru.4game.com";
    private const string AuthPageEndpoint = "https://id.4game.ru/";
    private const string AuthApiEndpoint = "https://webbff.ru.4game.ru/api/auth";
    private const string AuthorizeEndpoint = "https://webbff.ru.4game.ru/oauth/authorize";
    private const string TokenEndpoint = "https://launcherbff.ru.4game.com/connect/token";

    private const string HardwareIdConfigKey = "RSBot.RuSro.hwid";
    private const string LauncherIdConfigKey = "RSBot.RuSro.launcherid";
    private const string AccessTokenConfigKey = "RSBot.RuSro.accessToken";
    private const string RefreshTokenConfigKey = "RSBot.RuSro.refreshToken";
    private const string TokenOwnerConfigKey = "RSBot.RuSro.tokenOwner";
    private const string EmailCodePendingConfigKey = "RSBot.RuSro.emailCodePending";
    private const string EmailCodeAddressConfigKey = "RSBot.RuSro.emailCodeAddress";
    private const string EmailSignInTokenConfigKey = "RSBot.RuSro.emailSignInToken";
    private const string EmailRequestGroupIdConfigKey = "RSBot.RuSro.emailRequestGroupId";
    private const string EmailCodeRequestedAtConfigKey = "RSBot.RuSro.emailCodeRequestedAt";
    private const int EmailCodeResendDelaySeconds = 30;

    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static async Task<bool> Auth()
    {
        var selectedAccount = Accounts.SavedAccounts?.Find(p =>
            p.Username == GlobalConfig.Get<string>("RSBot.General.AutoLoginAccountUsername")
        );

        if (selectedAccount == null)
        {
            MessageBox.Show("No account selected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        string email = selectedAccount.Username?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            MessageBox.Show(
                "The selected account has no email address",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
            return false;
        }

        try
        {
            GetOrCreateDeviceIdentity();
            string accessToken = await GetAccessTokenAsync(email);
            if (!string.IsNullOrEmpty(accessToken))
            {
                await ConnectToWSAndSend();
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error("[RuSroAuthService]: " + ex.Message);
            MessageBox.Show(ex.Message, "4game authorization error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return false;
    }

    private static async Task<string> GetAccessTokenAsync(string email)
    {
        string savedAccessToken = GlobalConfig.Get<string>(AccessTokenConfigKey, "");
        if (TokenBelongsToAccount(email, savedAccessToken) && IsAccessTokenValid(savedAccessToken))
            return savedAccessToken;

        OAuthTokenResponse tokenResponse = await TryRefreshTokenAsync(email);
        if (tokenResponse == null)
            tokenResponse = await AuthorizeWithEmailCodeAsync(email);

        if (tokenResponse == null)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            throw new InvalidOperationException("4game returned an empty access token.");

        SaveTokens(email, tokenResponse);
        return tokenResponse.AccessToken;
    }

    private static async Task<OAuthTokenResponse> TryRefreshTokenAsync(string email)
    {
        string refreshToken = GlobalConfig.Get<string>(RefreshTokenConfigKey, "");
        string accessToken = GlobalConfig.Get<string>(AccessTokenConfigKey, "");

        if (string.IsNullOrWhiteSpace(refreshToken) || !TokenBelongsToAccount(email, accessToken))
            return null;

        try
        {
            using HttpClient authClient = CreateAuthClient();
            var parameters = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "refresh_token"),
                new("refresh_token", refreshToken),
            };

            OAuthTokenResponse response = await SendTokenRequestAsync(authClient, parameters);
            if (string.IsNullOrWhiteSpace(response.RefreshToken))
                response.RefreshToken = refreshToken;

            return response;
        }
        catch (Exception ex)
        {
            Log.Debug(
                "[RuSroAuthService]: Refresh token rejected, starting email authorization. " + ex.Message
            );
            return null;
        }
    }

    private static async Task<OAuthTokenResponse> AuthorizeWithEmailCodeAsync(string email)
    {
        bool resumePendingCode = TryGetPendingEmailCode(
            email,
            out string signInToken,
            out string requestGroupId,
            out DateTimeOffset codeRequestedAt
        );

        RotateDeviceIdentityAfterPendingEmailCode();

        string state = Guid.NewGuid().ToString();
        if (!resumePendingCode)
            requestGroupId = Guid.NewGuid().ToString();

        using HttpClient authClient = CreateAuthClient(requestGroupId);
        await InitializeAuthPageAsync(authClient, state);

        if (resumePendingCode)
        {
            await WaitForEmailCodeResendAsync(codeRequestedAt);
            signInToken = await ResendEmailCodeAsync(authClient, signInToken, requestGroupId);
            codeRequestedAt = DateTimeOffset.UtcNow;
            SavePendingEmailCode(email, signInToken, requestGroupId, codeRequestedAt);
        }
        else
        {
            string signInResponse = await SendAuthJsonRequestAsync(
                authClient,
                AuthApiEndpoint + "/signin",
                new { contact = email, contactType = "email" },
                requestGroupId
            );

            ApiResponse<SignInData> signIn = JsonConvert.DeserializeObject<ApiResponse<SignInData>>(signInResponse);
            signInToken = signIn?.Data?.SignInToken;
            if (string.IsNullOrWhiteSpace(signInToken))
                throw new InvalidOperationException("4game did not return a signin token.");

            codeRequestedAt = DateTimeOffset.UtcNow;
            SavePendingEmailCode(email, signInToken, requestGroupId, codeRequestedAt);
        }

        DateTimeOffset resendAvailableAt = codeRequestedAt.AddSeconds(EmailCodeResendDelaySeconds);

        string emailCode;
        while (true)
        {
            emailCode = PromptForEmailCode(email);
            if (emailCode != null)
                break;

            DialogResult resendResult = MessageBox.Show(
                "The email code did not arrive? Request a new code?",
                "4game authorization",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            if (resendResult != DialogResult.Yes)
            {
                ResetAuthorizationState();
                return null;
            }

            TimeSpan resendDelay = resendAvailableAt - DateTimeOffset.UtcNow;
            if (resendDelay > TimeSpan.Zero)
            {
                int seconds = (int)Math.Ceiling(resendDelay.TotalSeconds);
                Log.Notify($"A new 4game email code will be requested in {seconds} seconds.");
                await Task.Delay(resendDelay);
            }

            signInToken = await ResendEmailCodeAsync(authClient, signInToken, requestGroupId);
            codeRequestedAt = DateTimeOffset.UtcNow;
            resendAvailableAt = codeRequestedAt.AddSeconds(EmailCodeResendDelaySeconds);
            SavePendingEmailCode(email, signInToken, requestGroupId, codeRequestedAt);
        }

        await SendAuthJsonRequestAsync(
            authClient,
            AuthApiEndpoint + "/signin/confirm",
            new
            {
                signinToken = signInToken,
                code = emailCode,
                clientId = ClientId,
            },
            requestGroupId
        );

        string authorizationCode = await RequestAuthorizationCodeAsync(authClient, state);
        var tokenParameters = new List<KeyValuePair<string, string>>
        {
            new("code", authorizationCode),
            new("grant_type", "authorization_code"),
        };

        return await SendTokenRequestAsync(authClient, tokenParameters);
    }

    private static HttpClient CreateAuthClient(string trackingId = null)
    {
        var cookies = new CookieContainer();
        if (!string.IsNullOrWhiteSpace(trackingId))
            cookies.Add(new Cookie("trk", trackingId, "/", ".4game.ru"));

        var handler = new HttpClientHandler
        {
            CookieContainer = cookies,
            UseCookies = true,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
        };

        var authClient = new HttpClient(handler);
        authClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        authClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ru");
        return authClient;
    }

    private static async Task InitializeAuthPageAsync(HttpClient authClient, string state)
    {
        string url =
            $"{AuthPageEndpoint}?scope={Uri.EscapeDataString(Scope)}"
            + $"&client_id={Uri.EscapeDataString(ClientId)}"
            + "&response_type=code"
            + $"&state={Uri.EscapeDataString(state)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + $"&back={Uri.EscapeDataString(BackUri)}";

        using HttpResponseMessage response = await authClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            string content = await response.Content.ReadAsStringAsync();
            throw CreateRequestException("Opening the 4game authorization page", response.StatusCode, content);
        }
    }

    private static async Task<string> SendAuthJsonRequestAsync(
        HttpClient authClient,
        string endpoint,
        object body,
        string requestGroupId
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.TryAddWithoutValidation("X-Client-Id", ClientId);
        request.Headers.TryAddWithoutValidation("X-Requests-Group-Id", requestGroupId);
        request.Headers.TryAddWithoutValidation("X-Sign-In-App", "launcher");
        request.Headers.TryAddWithoutValidation("X-Request-Id", Guid.NewGuid().ToString());
        request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await authClient.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw CreateRequestException("4game sign-in request", response.StatusCode, content);

        return content;
    }

    private static string PromptForEmailCode(string email)
    {
        string dialogFormTitle = LanguageManager.GetLangBySpecificKey(
            "RSBot.General",
            "RuSroConfirmationCodeFormTitle",
            "Confirmation code"
        );
        string dialogTitle = LanguageManager.GetLangBySpecificKey(
            "RSBot.General",
            "RuSroConfirmationCodeTitle",
            "You have got an email with PIN"
        );
        string dialogContent = LanguageManager.GetLangBySpecificKey(
            "RSBot.General",
            "RuSroConfirmationCodeContent",
            "Enter it and press OK"
        );

        var inputDialog = new InputDialog(dialogFormTitle, dialogTitle, $"{dialogContent}\n{email}");
        if (inputDialog.ShowDialog() != DialogResult.OK)
            return null;

        string code = inputDialog.Value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("The email confirmation code is empty.");

        return code;
    }

    private static async Task<string> ResendEmailCodeAsync(
        HttpClient authClient,
        string signInToken,
        string requestGroupId
    )
    {
        string resendResponse = await SendAuthJsonRequestAsync(
            authClient,
            AuthApiEndpoint + "/signin/code/resended",
            new { signinToken = signInToken },
            requestGroupId
        );

        ApiResponse<SignInData> resend = JsonConvert.DeserializeObject<ApiResponse<SignInData>>(resendResponse);
        string newSignInToken = resend?.Data?.SignInToken;
        if (string.IsNullOrWhiteSpace(newSignInToken))
            throw new InvalidOperationException("4game did not return a signin token after resending the code.");

        return newSignInToken;
    }

    private static async Task WaitForEmailCodeResendAsync(DateTimeOffset codeRequestedAt)
    {
        DateTimeOffset resendAvailableAt = codeRequestedAt.AddSeconds(EmailCodeResendDelaySeconds);
        TimeSpan resendDelay = resendAvailableAt - DateTimeOffset.UtcNow;
        if (resendDelay <= TimeSpan.Zero)
            return;

        int seconds = (int)Math.Ceiling(resendDelay.TotalSeconds);
        Log.Notify($"A new 4game email code will be requested in {seconds} seconds.");
        await Task.Delay(resendDelay);
    }

    private static async Task<string> RequestAuthorizationCodeAsync(HttpClient authClient, string state)
    {
        string url =
            $"{AuthorizeEndpoint}?redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + $"&scope={Uri.EscapeDataString(Scope)}"
            + $"&client_id={Uri.EscapeDataString(ClientId)}"
            + "&response_type=code"
            + $"&state={Uri.EscapeDataString(state)}"
            + "&notMyComputer=false";

        using HttpResponseMessage response = await authClient.GetAsync(url);
        if (!IsRedirect(response.StatusCode) || response.Headers.Location == null)
        {
            string content = await response.Content.ReadAsStringAsync();
            throw CreateRequestException("Getting the 4game authorization code", response.StatusCode, content);
        }

        Uri location = response.Headers.Location.IsAbsoluteUri
            ? response.Headers.Location
            : new Uri(new Uri(url), response.Headers.Location);

        string returnedState = GetQueryParameter(location, "state");
        if (!string.Equals(state, returnedState, StringComparison.Ordinal))
            throw new InvalidOperationException("4game returned an OAuth state that does not match the request.");

        string authorizationCode = GetQueryParameter(location, "code");
        if (string.IsNullOrWhiteSpace(authorizationCode))
            throw new InvalidOperationException("4game redirect does not contain an authorization code.");

        return authorizationCode;
    }

    private static async Task<OAuthTokenResponse> SendTokenRequestAsync(
        HttpClient authClient,
        IEnumerable<KeyValuePair<string, string>> parameters
    )
    {
        var identity = GetOrCreateDeviceIdentity();

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
        request.Headers.TryAddWithoutValidation("X-User-Origin", "Forgame");
        request.Headers.TryAddWithoutValidation("Computer-Name", identity.HardwareId);
        request.Headers.TryAddWithoutValidation("Hardware-Id", identity.HardwareId);
        request.Headers.TryAddWithoutValidation("X-Request-Id", Guid.NewGuid().ToString());
        request.Content = new FormUrlEncodedContent(parameters);

        using HttpResponseMessage response = await authClient.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw CreateRequestException("Exchanging the 4game token", response.StatusCode, content);

        OAuthTokenResponse tokenResponse = JsonConvert.DeserializeObject<OAuthTokenResponse>(content);
        if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
            throw new InvalidOperationException("4game returned an invalid token response.");

        return tokenResponse;
    }

    private static void SaveTokens(string email, OAuthTokenResponse tokenResponse)
    {
        string refreshToken = tokenResponse.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
            refreshToken = GlobalConfig.Get<string>(RefreshTokenConfigKey, "");

        GlobalConfig.Set(AccessTokenConfigKey, tokenResponse.AccessToken);
        GlobalConfig.Set(RefreshTokenConfigKey, refreshToken);
        GlobalConfig.Set(TokenOwnerConfigKey, email);
        GlobalConfig.Set(EmailCodePendingConfigKey, false);
        GlobalConfig.Set(EmailCodeAddressConfigKey, "");
        GlobalConfig.Set(EmailSignInTokenConfigKey, "");
        GlobalConfig.Set(EmailRequestGroupIdConfigKey, "");
        GlobalConfig.Set(EmailCodeRequestedAtConfigKey, "");
        GlobalConfig.Save();
    }

    private static void ResetAuthorizationState()
    {
        GlobalConfig.Set(AccessTokenConfigKey, "");
        GlobalConfig.Set(RefreshTokenConfigKey, "");
        GlobalConfig.Set(TokenOwnerConfigKey, "");
        GlobalConfig.Set(EmailCodePendingConfigKey, false);
        GlobalConfig.Set(EmailCodeAddressConfigKey, "");
        GlobalConfig.Set(EmailSignInTokenConfigKey, "");
        GlobalConfig.Set(EmailRequestGroupIdConfigKey, "");
        GlobalConfig.Set(EmailCodeRequestedAtConfigKey, "");
        GlobalConfig.Set(HardwareIdConfigKey, "");
        GlobalConfig.Set(LauncherIdConfigKey, "");
        GlobalConfig.Set("RSBot.RuSro.sessionId", "");
        GlobalConfig.Save();

        Log.Debug(
            "[RuSroAuthService]: Authorization state reset. "
                + "The next login will use new cookies and identifiers."
        );
    }

    private static bool TryGetPendingEmailCode(
        string email,
        out string signInToken,
        out string requestGroupId,
        out DateTimeOffset codeRequestedAt
    )
    {
        signInToken = GlobalConfig.Get<string>(EmailSignInTokenConfigKey, "");
        requestGroupId = GlobalConfig.Get<string>(EmailRequestGroupIdConfigKey, "");
        string pendingEmail = GlobalConfig.Get<string>(EmailCodeAddressConfigKey, "");
        string requestedAt = GlobalConfig.Get<string>(EmailCodeRequestedAtConfigKey, "");

        bool hasPendingCode =
            GlobalConfig.Get<bool>(EmailCodePendingConfigKey)
            && string.Equals(pendingEmail, email, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(signInToken)
            && !string.IsNullOrWhiteSpace(requestGroupId);

        if (!DateTimeOffset.TryParse(requestedAt, out codeRequestedAt))
            codeRequestedAt = DateTimeOffset.MinValue;

        return hasPendingCode;
    }

    private static void SavePendingEmailCode(
        string email,
        string signInToken,
        string requestGroupId,
        DateTimeOffset codeRequestedAt
    )
    {
        GlobalConfig.Set(EmailCodePendingConfigKey, true);
        GlobalConfig.Set(EmailCodeAddressConfigKey, email);
        GlobalConfig.Set(EmailSignInTokenConfigKey, signInToken);
        GlobalConfig.Set(EmailRequestGroupIdConfigKey, requestGroupId);
        GlobalConfig.Set(EmailCodeRequestedAtConfigKey, codeRequestedAt.ToString("O"));
        GlobalConfig.Save();
    }

    private static bool TokenBelongsToAccount(string email, string accessToken)
    {
        string tokenOwner = GlobalConfig.Get<string>(TokenOwnerConfigKey, "");
        if (!string.IsNullOrWhiteSpace(tokenOwner))
            return string.Equals(tokenOwner, email, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(accessToken))
            return false;

        try
        {
            var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            return jwtToken.Claims
                .Where(claim => claim.Type == "username" || claim.Type == "email")
                .Any(claim => string.Equals(claim.Value, email, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAccessTokenValid(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return false;

        try
        {
            var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            return jwtToken.ValidTo > DateTime.UtcNow.AddMinutes(1);
        }
        catch
        {
            return false;
        }
    }

    private static (string HardwareId, string LauncherId) GetOrCreateDeviceIdentity()
    {
        bool changed = false;
        string hardwareId = GlobalConfig.Get<string>(HardwareIdConfigKey, "");
        string launcherId = GlobalConfig.Get<string>(LauncherIdConfigKey, "");

        if (hardwareId?.Length != 12 || hardwareId.Any(character => !Chars.Contains(character)))
        {
            hardwareId = GenerateRandomString(12);
            GlobalConfig.Set(HardwareIdConfigKey, hardwareId);
            changed = true;
        }

        if (!Guid.TryParse(launcherId, out _))
        {
            launcherId = GenerateLauncherId();
            GlobalConfig.Set(LauncherIdConfigKey, launcherId);
            changed = true;
        }

        if (changed)
            GlobalConfig.Save();

        return (hardwareId, launcherId);
    }

    private static void RotateDeviceIdentityAfterPendingEmailCode()
    {
        if (!GlobalConfig.Get<bool>(EmailCodePendingConfigKey))
            return;

        string hardwareId = GenerateRandomString(12);
        string launcherId = GenerateLauncherId();

        GlobalConfig.Set(HardwareIdConfigKey, hardwareId);
        GlobalConfig.Set(LauncherIdConfigKey, launcherId);
        GlobalConfig.Save();

        Log.Debug(
            "[RuSroAuthService]: The previous email code flow was not completed; generated new hardware and launcher IDs."
        );
    }

    private static string GenerateRandomString(int length = 64)
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(length);
        var result = new StringBuilder(length);

        for (int i = 0; i < length; i++)
            result.Append(Chars[randomBytes[i] % Chars.Length]);

        return result.ToString();
    }

    private static string GenerateLauncherId()
    {
        return Guid.NewGuid().ToString();
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.MovedPermanently
            || statusCode == HttpStatusCode.Found
            || statusCode == HttpStatusCode.SeeOther
            || statusCode == HttpStatusCode.TemporaryRedirect
            || statusCode == HttpStatusCode.PermanentRedirect;
    }

    private static string GetQueryParameter(Uri uri, string name)
    {
        foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separatorIndex = pair.IndexOf('=');
            string key = WebUtility.UrlDecode(separatorIndex >= 0 ? pair[..separatorIndex] : pair);
            if (!string.Equals(key, name, StringComparison.Ordinal))
                continue;

            string value = separatorIndex >= 0 ? pair[(separatorIndex + 1)..] : string.Empty;
            return WebUtility.UrlDecode(value);
        }

        return null;
    }

    private static Exception CreateRequestException(string operation, HttpStatusCode statusCode, string content)
    {
        string detail = ExtractErrorDescription(content);
        string status = $"{(int)statusCode} {statusCode}";
        return new InvalidOperationException(
            string.IsNullOrWhiteSpace(detail)
                ? $"{operation} failed ({status})."
                : $"{operation} failed ({status}): {detail}"
        );
    }

    private static string ExtractErrorDescription(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            JObject json = JObject.Parse(content);
            JObject error = json["error"] as JObject;
            string detail =
                json.Value<string>("error_description")
                ?? error?.Value<string>("description")
                ?? error?.Value<string>("code");

            if (string.IsNullOrWhiteSpace(detail) && json["error"]?.Type == JTokenType.String)
                detail = json["error"].Value<string>();

            if (!string.IsNullOrWhiteSpace(detail))
                return detail;
        }
        catch (JsonReaderException)
        {
            // The response is not JSON; return a short plain-text excerpt below.
        }

        string trimmedContent = content.Trim();
        return trimmedContent.Length <= 300 ? trimmedContent : trimmedContent[..300] + "...";
    }

    private sealed class ApiResponse<T>
    {
        [JsonProperty("data")]
        public T Data { get; set; }
    }

    private sealed class SignInData
    {
        [JsonProperty("signinToken")]
        public string SignInToken { get; set; }
    }

    private sealed class OAuthTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }
    }

    private static async Task ConnectToWSAndSend()
    {
        string launcherId = GlobalConfig.Get<string>("RSBot.RuSro.launcherid");
        string hwid = GlobalConfig.Get<string>("RSBot.RuSro.hwid");
        string accessToken = GlobalConfig.Get<string>("RSBot.RuSro.accessToken");
        string sub = ExtractSubFromToken(accessToken);

        Log.Debug("Sub: " + sub);

        string wsUrl = BuildWebSocketUrl(accessToken, hwid, launcherId);
        var serverUri = new Uri(wsUrl);

        using (var clientWebSocket = new ClientWebSocket())
        {
            try
            {
                await ConnectToWebSocket(clientWebSocket, serverUri);

                string login = await SendGetGameAccountRequest(clientWebSocket, sub);
                (string extractedLogin, string password) = await SendCreateGameTokenCodeRequest(
                    clientWebSocket,
                    accessToken,
                    login,
                    sub
                );

                SaveCredentials(extractedLogin, password);

                await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                Log.Debug("Connection closed.");
            }
            catch (WebSocketException ex)
            {
                Log.Debug($"WebSocket error: {ex.Message}");
            }
        }
    }

    private static string ExtractSubFromToken(string accessToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(accessToken);
        return jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
    }

    private static string BuildWebSocketUrl(string accessToken, string hwid, string launcherId)
    {
        return string.Format(
            "wss://launcherbff.ru.4game.com/?token={0}&hardware-id={1}&launcher-id={2}&computer-name={3}",
            accessToken,
            hwid,
            launcherId,
            GenerateRandomString(8)
        );
    }

    private static async Task ConnectToWebSocket(ClientWebSocket clientWebSocket, Uri serverUri)
    {
        Log.Debug("Connecting to WebSocket server...");
        await clientWebSocket.ConnectAsync(serverUri, CancellationToken.None);
        Log.Debug("Connected!");
    }

    private static async Task<string> SendGetGameAccountRequest(ClientWebSocket clientWebSocket, string sub)
    {
        string payload = string.Format(
            "{{\"jsonrpc\":\"2.0\",\"method\":\"getGameAccount\",\"params\":{{\"masterId\":\"{0}\",\"toPartnerId\":\"silk-ru\",\"lang\":\"ru\"}},\"id\":\"{1}\"}}",
            sub,
            Guid.NewGuid().ToString()
        );

        Log.Debug($"Sending payload: {payload}");
        await SendMessage(clientWebSocket, payload);

        int attempts = 0;
        const int maxAttempts = 10;
        while (attempts < maxAttempts)
        {
            attempts++;
            string response = await ReceiveMessage(clientWebSocket);
            Log.Debug($"Received: {response}");

            using (JsonDocument document = JsonDocument.Parse(response))
            {
                if (
                    document.RootElement.TryGetProperty("notification", out var notification)
                    && notification.GetString() == "invalidate"
                    && document.RootElement.TryGetProperty("params", out var paramsElement)
                    && paramsElement
                        .EnumerateArray()
                        .Any(p => p.TryGetProperty("type", out var type) && type.GetString() == "webshopOwnPromoCodes")
                )
                {
                    //string getWebshopOwnPromoCodesPayload = $"{{\"jsonrpc\":\"2.0\",\"method\":\"getWebshopOwnPromoCodes\",\"params\":{{ \"userId\":{sub},\"from\":0,\"count\":20,\"lang\":\"ru\"}},\"id\":\"{Guid.NewGuid()}\"}}";
                    //Log.Debug($"Some promocodes has experied. Sending promocodes update request: {getWebshopOwnPromoCodesPayload}");
                    await SendMessage(clientWebSocket, payload);

                    //string getWebshopOwnPromoCodesResponse = await ReceiveMessage(clientWebSocket);
                    //Log.Debug($"getWebshopOwnPromoCodesResponse: {getWebshopOwnPromoCodesResponse}");
                    continue;
                }

                if (
                    document.RootElement.TryGetProperty("notification", out notification)
                    && notification.GetString() == "invalidate"
                    && document.RootElement.TryGetProperty("params", out paramsElement)
                    && paramsElement
                        .EnumerateArray()
                        .Any(p => p.TryGetProperty("type", out var type) && type.GetString() == "pushNotification")
                )
                {
                    Log.Notify($"4game pushed notification: {response}");
                    continue;
                }

                if (
                    document.RootElement.TryGetProperty("notification", out notification)
                    && notification.GetString() == "invalidate"
                    && document.RootElement.TryGetProperty("params", out paramsElement)
                    && paramsElement
                        .EnumerateArray()
                        .Any(p => p.TryGetProperty("type", out var type) && type.GetString() == "webFeed")
                )
                {
                    Log.Notify($"4game pushed webFeed: {response}");
                    continue;
                }

                if (!document.RootElement.TryGetProperty("result", out JsonElement resultElement))
                {
                    Log.Error($"Response does not contain 'result': {response}");
                    throw new Exception("Unexpected response format: 'result' key is missing");
                }

                return resultElement[0].GetProperty("login").GetString();
            }
        }
        throw new Exception("Max attempts reached, exiting getGameAccount loop.");
    }

    private static async Task<(string, string)> SendCreateGameTokenCodeRequest(
        ClientWebSocket clientWebSocket,
        string accessToken,
        string login,
        string sub
    )
    {
        string payload =
            $"{{\"jsonrpc\":\"2.0\",\"method\":\"createGameTokenCode\",\"params\":{{\"accessToken\":\"{accessToken}\",\"ignoreLicenseAcceptance\":false,\"login\":\"{login}\",\"masterId\":\"{sub}\",\"toPartnerId\":\"silk-ru\",\"lang\":\"ru\"}},\"id\":\"{Guid.NewGuid()}\"}}";

        Log.Debug("Sending createGameTokenCode request.");
        await SendMessage(clientWebSocket, payload);

        int attempts = 0;
        const int maxAttempts = 10;
        while (attempts < maxAttempts)
        {
            attempts++;
            string response = await ReceiveMessage(clientWebSocket);
            Log.Debug($"Received response: {response}");

            using (JsonDocument document = JsonDocument.Parse(response))
            {
                if (
                    document.RootElement.TryGetProperty("notification", out var notification)
                    && notification.GetString() == "invalidate"
                    && document.RootElement.TryGetProperty("params", out var paramsElement)
                    && paramsElement
                        .EnumerateArray()
                        .Any(p => p.TryGetProperty("type", out var type) && type.GetString() == "webshopOwnPromoCodes")
                )
                {
                    //string getWebshopOwnPromoCodesPayload = $"{{\"jsonrpc\":\"2.0\",\"method\":\"getWebshopOwnPromoCodes\",\"params\":{{ \"userId\":{sub},\"from\":0,\"count\":20,\"lang\":\"ru\"}},\"id\":\"{Guid.NewGuid()}\"}}";
                    //Log.Debug($"Some promocodes has experied. Sending promocodes update request: {getWebshopOwnPromoCodesPayload}");
                    await SendMessage(clientWebSocket, payload);

                    //string getWebshopOwnPromoCodesResponse = await ReceiveMessage(clientWebSocket);
                    //Log.Debug($"getWebshopOwnPromoCodesResponse: {getWebshopOwnPromoCodesResponse}");
                    continue;
                }

                if (
                    document.RootElement.TryGetProperty("notification", out notification)
                    && notification.GetString() == "invalidate"
                    && document.RootElement.TryGetProperty("params", out paramsElement)
                    && paramsElement
                        .EnumerateArray()
                        .Any(p => p.TryGetProperty("type", out var type) && type.GetString() == "serviceStatusChanged")
                )
                {
                    Log.Notify($"4game service status changed: {response}");
                    continue;
                }

                if (
                    document.RootElement.TryGetProperty("notification", out notification)
                    && notification.GetString() == "invalidate"
                    && document.RootElement.TryGetProperty("params", out paramsElement)
                    && paramsElement
                        .EnumerateArray()
                        .Any(p => p.TryGetProperty("type", out var type) && type.GetString() == "pushNotification")
                )
                {
                    Log.Notify($"4game pushed notification: {response}");
                    continue;
                }

                if (
                    document.RootElement.TryGetProperty("error", out JsonElement error)
                    && error.TryGetProperty("code", out JsonElement errorCode)
                    && errorCode.GetString() == "license.agreement.not.accepted"
                )
                {
                    Log.Debug("License agreement not accepted, attempting to accept it...");

                    var errorData = error.GetProperty("data");
                    int licenseAgreementId = errorData.GetProperty("licenseAgreementId").GetInt32();

                    string acceptLicensePayload =
                        $"{{\"jsonrpc\":\"2.0\",\"method\":\"acceptLicense\",\"params\":{{\"userId\":{sub},\"licenseAgreementId\":{licenseAgreementId},\"lang\":\"ru\"}},\"id\":\"{Guid.NewGuid()}\"}}";

                    Log.Debug($"Sending acceptLicense payload: {acceptLicensePayload}");
                    await SendMessage(clientWebSocket, acceptLicensePayload);

                    string acceptResponse = await ReceiveMessage(clientWebSocket);
                    Log.Debug($"Received acceptLicense response: {acceptResponse}");

                    using (JsonDocument acceptDocument = JsonDocument.Parse(acceptResponse))
                    {
                        if (
                            acceptDocument.RootElement.TryGetProperty("result", out JsonElement result)
                            && result.ValueKind == JsonValueKind.Object
                        )
                        {
                            Log.Debug("License agreement accepted, retrying createGameTokenCode...");
                            await SendMessage(clientWebSocket, payload);
                            continue;
                        }
                        else
                        {
                            Log.Error("Failed to accept license agreement");
                            throw new Exception("License agreement acceptance failed");
                        }
                    }
                }

                if (!document.RootElement.TryGetProperty("result", out JsonElement resultElement))
                {
                    Log.Error($"Response does not contain 'result': {response}");
                    throw new Exception("Unexpected response format: 'result' key is missing");
                }

                string extractedLogin = resultElement.GetProperty("login").GetString();
                string password = resultElement.GetProperty("password").GetString();
                return (extractedLogin, password);
            }
        }
        throw new Exception("Max attempts reached, exiting createGameTokenCode loop.");
    }

    private static async Task SendMessage(ClientWebSocket clientWebSocket, string message)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        await clientWebSocket.SendAsync(
            new ArraySegment<byte>(messageBytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }

    private static async Task<string> ReceiveMessage(ClientWebSocket clientWebSocket)
    {
        var buffer = new byte[4096];
        var responseSegment = new ArraySegment<byte>(buffer);
        var result = await clientWebSocket.ReceiveAsync(responseSegment, CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private static void SaveCredentials(string login, string password)
    {
        Log.Debug($"Extracted login: {login}");
        Log.Debug($"Extracted password: {password}");

        GlobalConfig.Set("RSBot.RuSro.login", login);
        GlobalConfig.Set("RSBot.RuSro.password", password);
    }
}
