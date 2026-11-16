using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nakama;

/// <summary>
/// Real Nakama-backed AuthenticationService for Unity 2023 + nakama-unity 3.21.1.
/// Replaces the placeholder with device + email/password + restore + socket.
/// Keeps the same public API that LoginUI already calls: LoginAsync, LoginWithGoogleAsync.
/// Persists as DontDestroyOnLoad singleton. On success fires OnLoginSuccess and can auto-load lobby.
/// Persists the Nakama session token only through the OS-protected secure store.
/// </summary>
public class AuthenticationService : MonoBehaviour
{
    public static AuthenticationService Instance { get; private set; }

    // The Nakama endpoint comes from ServerConfig only. These fields used to be serialized, and the LoginMenu scene still held
    // the old direct address (http://161.118.255.172:7350), which silently overrode ServerConfig and made the login screen
    // report "UNABLE TO CONNECT". Not serialized, so a stale scene value can never win again.
    private string scheme = ServerConfig.NakamaScheme;
    private string host = ServerConfig.NakamaHost;
    private int port = ServerConfig.NakamaPort;
    private string serverKey = ServerConfig.NakamaServerKey;
    [Tooltip("Auto-restore saved session on Awake")]
    [SerializeField] private bool autoRestoreSession = true;

    [Header("Google OAuth")]
    [Tooltip("Google Cloud Console OAuth 2.0 Client ID (Web Application type)")]
    [SerializeField] private string googleClientId = "";
    [Tooltip("Google OAuth redirect URI. For local testing use http://localhost:7350/auth/google")]
    [SerializeField] private string googleRedirectUri = "http://localhost:7350/auth/google";
    [Tooltip("Local port to listen for OAuth callback (editor only)")]
    [SerializeField] private int googleCallbackPort = 8080;

    [Header("State (read-only)")]
    [SerializeField] private bool isAuthenticated;
    [SerializeField] private string currentUserId;

    [Header("Lobby Transition")]
    [Tooltip("Scene to load after successful login. Leave empty to not auto-load.")]
    [SerializeField] private string lobbySceneName = "GameLobby";
    [SerializeField] private bool autoLoadLobbyOnSuccess = true;

    public event Action<AuthResult> OnLoginSuccess;
    public event Action<string> OnLoginFailed;

    public bool IsAuthenticated => isAuthenticated && _session != null && !_session.IsExpired;
    public bool IsAuthenticationReady { get; private set; }
    public string CurrentUserId => currentUserId;
    public string ApiBaseUrl => $"{scheme}://{host}:{port}";
    public string GoogleClientId => googleClientId;
    public ISession Session => _session;
    public ISocket Socket => _socket;
    public IClient Client => _client;

    [SerializeField, HideInInspector] private string apiBaseUrl = "https://api.example.com";

    private IClient _client;
    private ISession _session;
    private ISocket _socket;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitClient();
        if (autoRestoreSession) _ = TryRestoreSessionAsync();
        else IsAuthenticationReady = true;
    }

    private void InitClient()
    {
        if (_client != null) return;
        _client = new Client(scheme, host, port, serverKey, UnityWebRequestAdapter.Instance);
        Debug.Log($"[Auth] Nakama client {scheme}://{host}:{port}");
    }

    private async Task TryRestoreSessionAsync()
    {
        if (!SecureSessionStore.TryLoad(out var stored))
        {
            IsAuthenticationReady = true;
            return;
        }
        try
        {
            InitClient();
            // stored as "<auth token>\n<refresh token>" (older saves have only the auth token)
            string[] parts = stored.Split('\n');
            var restored = Nakama.Session.Restore(parts[0], parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null);
            if (restored.HasExpired(DateTime.UtcNow.AddMinutes(RenewMinutesBeforeExpiry)))
            {
                // a restored session that is about to run out mid-match is renewed now, not when the next request fails
                var renewed = await RenewAsync(restored);
                if (renewed == null)
                {
                    Debug.Log("[Auth] Saved session expired and could not be renewed");
                    SecureSessionStore.Delete();
                    IsAuthenticationReady = true;
                    return;
                }
                restored = renewed;
                SaveSession(restored);
            }
            _session = restored;
            currentUserId = _session.UserId;
            isAuthenticated = true;
            Debug.Log($"[Auth] Session restored user={_session.UserId} username={_session.Username}");
            await EnsureSocketAsync();
            IsAuthenticationReady = true;
            OnLoginSuccess?.Invoke(AuthResult.Success(currentUserId));
            if (autoLoadLobbyOnSuccess && !string.IsNullOrEmpty(lobbySceneName)) LoadLobby();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Auth] Restore failed: {ex.Message}");
            SecureSessionStore.Delete();
            IsAuthenticationReady = true;
        }
    }

    // ------------------------------------------------------------------ public API used by LoginUI

    /// <summary>Login with username/email + password. Fails if account does not exist (create:false).</summary>
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
            return AuthResult.Failure("Username is required.");
        if (string.IsNullOrWhiteSpace(password))
            return AuthResult.Failure("Password is required.");

        InitClient();
        try
        {
            string email = username.Trim();
            string uname = email;
            if (!email.Contains("@"))
                email = $"{email}@railbound.local";
            else
                uname = email.Split('@')[0];

            _session = await _client.AuthenticateEmailAsync(email, password, uname, create: false);
            PlayerPrefs.SetString(LoginMethodKey, "email");
            return await HandleNewSessionAsync(_session);
        }
        catch (ApiResponseException apiEx)
        {
            string msg = apiEx.Message;
            Debug.LogWarning($"[Auth] Login failed: {msg} (code {apiEx.StatusCode})");
            string friendly = apiEx.StatusCode == 404 || msg.Contains("not found") ? "Account not found. Please create an account first." : CleanError(msg);
            var fail = AuthResult.Failure(friendly);
            OnLoginFailed?.Invoke(fail.Error);
            return fail;
        }
        catch (Exception ex)
        {
            var fail = AuthResult.Failure("Login failed: " + ex.Message);
            OnLoginFailed?.Invoke(fail.Error);
            return fail;
        }
    }

    /// <summary>Create new account with validation. Uses create:true, fails if duplicate.</summary>
    public async Task<AuthResult> CreateAccountAsync(string username, string password, string confirmPassword)
    {
        string userErr = ValidateUsername(username);
        if (userErr != null) return AuthResult.Failure(userErr);
        string passErr = ValidatePassword(password);
        if (passErr != null) return AuthResult.Failure(passErr);
        if (password != confirmPassword) return AuthResult.Failure("Passwords do not match.");

        InitClient();
        try
        {
            string email = username.Trim();
            string uname = email;
            if (!email.Contains("@"))
                email = $"{email}@railbound.local";
            else
            {
                uname = email.Split('@')[0];
                // if real email, also validate format
                if (!email.Contains(".") || email.Length < 5) return AuthResult.Failure("Please enter a valid email address.");
            }

            _session = await _client.AuthenticateEmailAsync(email, password, uname, create: true);
            PlayerPrefs.SetString(LoginMethodKey, "email");
            return await HandleNewSessionAsync(_session);
        }
        catch (ApiResponseException apiEx)
        {
            string msg = apiEx.Message;
            bool duplicate = apiEx.StatusCode == 409 || msg.ToLower().Contains("already") || msg.ToLower().Contains("exists");
            string friendly = duplicate ? "Account already exists. Please log in instead." : CleanError(msg);
            Debug.LogWarning($"[Auth] CreateAccount failed: {msg} (code {apiEx.StatusCode})");
            var fail = AuthResult.Failure(friendly);
            OnLoginFailed?.Invoke(fail.Error);
            return fail;
        }
        catch (Exception ex)
        {
            var fail = AuthResult.Failure("Create account failed: " + ex.Message);
            OnLoginFailed?.Invoke(fail.Error);
            return fail;
        }
    }

    private static string ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return "Username is required.";
        string u = username.Trim();
        string baseName = u.Contains("@") ? u.Split('@')[0] : u;
        if (baseName.Length < 3) return "Username must be at least 3 characters.";
        if (baseName.Length > 16) return "Username must be at most 16 characters.";
        if (u.Contains("@") && (!u.Contains(".") || u.Length < 5)) return "Please enter a valid email address.";
        return null;
    }

    private static string ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return "Password is required.";
        if (password.Length < 6) return "Password must be at least 6 characters.";
        if (password.Length > 64) return "Password is too long.";
        return null;
    }

    public async Task<AuthResult> LoginAsGuestAsync()
    {
        InitClient();
        try
        {
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            if (deviceId == SystemInfo.unsupportedIdentifier || string.IsNullOrEmpty(deviceId))
                deviceId = PlayerPrefs.GetString("nakama.device.id", Guid.NewGuid().ToString());
            PlayerPrefs.SetString("nakama.device.id", deviceId);

            _session = await _client.AuthenticateDeviceAsync(deviceId);
            PlayerPrefs.SetString(LoginMethodKey, "guest");   // lets the session be renewed with the device id later
            return await HandleNewSessionAsync(_session);
        }
        catch (Exception ex)
        {
            var fail = AuthResult.Failure("Guest login failed: " + ex.Message);
            OnLoginFailed?.Invoke(fail.Error);
            return fail;
        }
    }

    /// <summary>Login with Google id_token (received from browser OAuth redirect).</summary>
    public async Task<AuthResult> LoginWithGoogleAsync(string idToken)
    {
        if (string.IsNullOrEmpty(idToken))
            return AuthResult.Failure("No Google token received.");
        InitClient();
        try
        {
            _session = await _client.AuthenticateGoogleAsync(idToken, create: true, username: null);
            PlayerPrefs.SetString(LoginMethodKey, "google");
            return await HandleNewSessionAsync(_session);
        }
        catch (Exception ex)
        {
            var fail = AuthResult.Failure("Google login failed: " + ex.Message);
            OnLoginFailed?.Invoke(fail.Error);
            return fail;
        }
    }

    /// <summary>Open browser for Google OAuth. Uses local HTTP listener to capture callback.</summary>
    public void StartGoogleOAuth()
    {
        if (string.IsNullOrEmpty(googleClientId))
        {
            Debug.LogError("[Auth] Google Client ID not set! Configure it in the Inspector.");
            return;
        }

        // Start local listener to capture the OAuth redirect
        StartOAuthCallbackListener();

        // Build Google OAuth URL
        string redirectUri = $"http://localhost:{googleCallbackPort}/callback";
        string scope = "openid email profile";
        string nonce = Guid.NewGuid().ToString("N");
        string url = $"https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={Uri.EscapeDataString(googleClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=id_token" +
            $"&scope={Uri.EscapeDataString(scope)}" +
            $"&nonce={nonce}" +
            $"&prompt=select_account";

        Debug.Log($"[Auth] Opening Google OAuth: {url}");
        Application.OpenURL(url);
    }

    // ---- Local HTTP listener for OAuth callback ----
    private System.Net.HttpListener _oauthListener;
    private bool _oauthListening = false;

    // HTML page that reads the URL fragment (#id_token=...) in JS and posts it back as a query param
    private const string OauthCallbackHtml = @"<!DOCTYPE html>
<html><head><title>Google Login</title></head>
<body style='font-family:system-ui;text-align:center;padding-top:60px;background:#1a1a2e;color:#eee'>
<h2>Processing Google login...</h2>
<script>
(function(){
  var hash = window.location.hash.substring(1);
  var params = {};
  hash.split('&').forEach(function(p){
    var kv = p.split('=');
    if(kv.length===2) params[kv[0]]=kv[1];
  });
  var token = params['id_token'] || '';
  if(token){
    // Send token back to our listener as a query param
    window.location.href = '/callback?token=' + encodeURIComponent(token);
  } else {
    document.body.innerHTML = '<h2>Login failed</h2><p>No id_token received.</p>';
  }
})();
</script>
</body></html>";

    private string _pendingIdToken;

    private async void StartOAuthCallbackListener()
    {
        if (_oauthListening) return;
        try
        {
            _oauthListener = new System.Net.HttpListener();
            _oauthListener.Prefixes.Add($"http://localhost:{googleCallbackPort}/");
            _oauthListener.Start();
            _oauthListening = true;
            Debug.Log($"[Auth] OAuth callback listener started on port {googleCallbackPort}");

            // ---- First request: Google redirects here with fragment, serve HTML to read it ----
            var context1 = await _oauthListener.GetContextAsync();
            string path1 = context1.Request.Url.AbsolutePath;
            string query1 = context1.Request.Url.Query;
            Debug.Log($"[Auth] OAuth request 1: path={path1} query={query1}");

            if (!string.IsNullOrEmpty(query1) && query1.Contains("token="))
            {
                // Direct token in query (shouldn't happen first time, but handle it)
                string token = ExtractParam(query1, "token");
                var buf = System.Text.Encoding.UTF8.GetBytes(SuccessHtml());
                context1.Response.ContentType = "text/html";
                context1.Response.ContentLength64 = buf.Length;
                await context1.Response.OutputStream.WriteAsync(buf, 0, buf.Length);
                context1.Response.Close();
                if (!string.IsNullOrEmpty(token)) _ = ProcessGoogleToken(token);
                return;
            }

            // Serve the JS page that reads the fragment and redirects with token as query param
            var buf1 = System.Text.Encoding.UTF8.GetBytes(OauthCallbackHtml);
            context1.Response.ContentType = "text/html";
            context1.Response.ContentLength64 = buf1.Length;
            await context1.Response.OutputStream.WriteAsync(buf1, 0, buf1.Length);
            context1.Response.Close();

            // ---- Second request: JS redirects to /callback?token=XYZ ----
            var context2 = await _oauthListener.GetContextAsync();
            string query2 = context2.Request.Url.Query;
            Debug.Log($"[Auth] OAuth request 2: query={query2}");
            string token2 = ExtractParam(query2, "token");

            string responseHtml;
            if (!string.IsNullOrEmpty(token2))
            {
                responseHtml = SuccessHtml();
                _ = ProcessGoogleToken(token2);
            }
            else
            {
                responseHtml = "<html><body style='font-family:system-ui;text-align:center;padding-top:60px'><h2>Login failed</h2><p>No token received. Please try again.</p></body></html>";
                Debug.LogError("[Auth] No id_token in OAuth callback");
            }

            var buf2 = System.Text.Encoding.UTF8.GetBytes(responseHtml);
            context2.Response.ContentType = "text/html";
            context2.Response.ContentLength64 = buf2.Length;
            await context2.Response.OutputStream.WriteAsync(buf2, 0, buf2.Length);
            context2.Response.Close();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Auth] OAuth listener error: {ex.Message}");
        }
        finally
        {
            StopOAuthListener();
        }
    }

    private static string ExtractParam(string query, string key)
    {
        if (string.IsNullOrEmpty(query)) return null;
        foreach (var pair in query.TrimStart('?').Split('&'))
        {
            var kv = pair.Split('=');
            if (kv.Length == 2 && kv[0] == key)
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }

    private static string SuccessHtml() =>
        "<html><body style='font-family:system-ui;text-align:center;padding-top:60px;background:#1a1a2e;color:#eee'>" +
        "<h2>Login successful!</h2><p>You can close this window and return to the game.</p>" +
        "<script>setTimeout(function(){window.close()},2000);</script></body></html>";

    private void StopOAuthListener()
    {
        _oauthListening = false;
        try { _oauthListener?.Stop(); _oauthListener?.Close(); } catch { }
        _oauthListener = null;
    }

    private async Task ProcessGoogleToken(string idToken)
    {
        Debug.Log($"[Auth] Processing Google token (len={idToken.Length})");
        var result = await LoginWithGoogleAsync(idToken);
        if (!result.IsSuccess)
        {
            Debug.LogError($"[Auth] Google auth failed: {result.Error}");
        }
    }

    /// <summary>Placeholder kept for no-arg compat — falls back to guest auth.</summary>
    public Task<AuthResult> LoginWithGoogleAsync() => LoginAsGuestAsync();

    public void Logout()
    {
        isAuthenticated = false;
        currentUserId = null;
        _session = null;
        try { _socket?.CloseAsync(); } catch { }
        _socket = null;
        SecureSessionStore.Delete();
        PlayerPrefs.DeleteKey(LoginMethodKey);
        Debug.Log("[Auth] Logged out");
    }

    // ------------------------------------------------------------------ session lifetime
    // Nakama sessions expire (token_expiry_sec, 2 h on the cluster). The client used to keep the token it got at login (or restored
    // from disk) and never renewed it: after the expiry IsAuthenticated turned false in the middle of a play session, and the next
    // DEPLOY silently started a LOCAL host match with its own bots instead of asking for a game server. The session is now renewed
    // before it runs out: with the refresh token, or for a guest by signing in again with the same device id (same account).

    private const int RenewMinutesBeforeExpiry = 10;
    private const string LoginMethodKey = "auth.method";
    private Task<bool> renewing;

    /// <summary>
    /// Makes sure the session is valid for at least the next few minutes, renewing it if needed. True when the player is signed in
    /// afterwards. Safe to call often (it only talks to Nakama when the session is close to its expiry).
    /// </summary>
    public Task<bool> EnsureFreshSessionAsync()
    {
        if (_session == null) return Task.FromResult(false);
        if (!_session.HasExpired(DateTime.UtcNow.AddMinutes(RenewMinutesBeforeExpiry))) return Task.FromResult(IsAuthenticated);
        if (renewing == null || renewing.IsCompleted) renewing = RenewCurrentAsync();
        return renewing;
    }

    private async Task<bool> RenewCurrentAsync()
    {
        var renewed = await RenewAsync(_session);
        if (renewed == null) return IsAuthenticated;   // could not renew: still fine if the old token has some time left
        bool sameUser = renewed.UserId == currentUserId || string.IsNullOrEmpty(currentUserId);
        _session = renewed;
        currentUserId = renewed.UserId;
        isAuthenticated = true;
        SaveSession(renewed);
        Debug.Log($"[Auth] Session renewed until {renewed.ExpireTime} (unix){(sameUser ? "" : " - NOTE: a different user id")}");
        return true;
    }

    /// <summary>A new session for the same account, or null: refresh token first, then (guests only) the device id again.</summary>
    private async Task<ISession> RenewAsync(ISession old)
    {
        if (old == null) return null;
        InitClient();
        if (!string.IsNullOrEmpty(old.RefreshToken) && !old.HasRefreshExpired(DateTime.UtcNow.AddMinutes(1)))
        {
            try { return await _client.SessionRefreshAsync(old); }
            catch (Exception ex) { Debug.LogWarning("[Auth] Session refresh failed: " + ex.Message); }
        }
        // guests (and saves from before the login method was recorded): the device id signs in to the same account again;
        // the user-id check below makes sure it never switches the player to another account
        string method = PlayerPrefs.GetString(LoginMethodKey, "");
        if (method == "guest" || (method.Length == 0 && PlayerPrefs.HasKey("nakama.device.id")))
        {
            try
            {
                string deviceId = PlayerPrefs.GetString("nakama.device.id", "");
                if (string.IsNullOrEmpty(deviceId)) deviceId = SystemInfo.deviceUniqueIdentifier;
                if (!string.IsNullOrEmpty(deviceId) && deviceId != SystemInfo.unsupportedIdentifier)
                {
                    var s = await _client.AuthenticateDeviceAsync(deviceId, null, false);   // create:false - only the existing guest account
                    if (s != null && (string.IsNullOrEmpty(old.UserId) || s.UserId == old.UserId)) return s;
                    Debug.LogWarning("[Auth] Guest sign-in returned a different account; not using it.");
                }
            }
            catch (Exception ex) { Debug.LogWarning("[Auth] Guest re-sign-in failed: " + ex.Message); }
        }
        return null;
    }

    private static bool SaveSession(ISession s)
    {
        return s != null && SecureSessionStore.TrySave(s.AuthToken + "\n" + (s.RefreshToken ?? ""));
    }

    private float nextSessionCheck;
    private void Update()
    {
        if (Time.unscaledTime < nextSessionCheck) return;
        nextSessionCheck = Time.unscaledTime + 60f;
        if (isAuthenticated && _session != null) _ = EnsureFreshSessionAsync();
    }

    // ------------------------------------------------------------------ internals

    private async Task<AuthResult> HandleNewSessionAsync(ISession session)
    {
        _session = session;
        isAuthenticated = true;
        currentUserId = session.UserId;
        if (!SaveSession(session))
            Debug.LogWarning("[Auth] Secure session storage is unavailable. The user must log in again next launch.");
        Debug.Log($"[Auth] Success user={session.UserId} username={session.Username} tokenLen={session.AuthToken?.Length}");
        await EnsureSocketAsync();
        IsAuthenticationReady = true;
        var result = AuthResult.Success(currentUserId);
        OnLoginSuccess?.Invoke(result);
        if (autoLoadLobbyOnSuccess && !string.IsNullOrEmpty(lobbySceneName)) LoadLobby();
        return result;
    }

    public async Task<ISocket> EnsureSocketAsync()
    {
        if (_session == null || _session.IsExpired) return null;
        try
        {
            if (_socket == null || !_socket.IsConnected)
            {
                if (_socket != null) { try { await _socket.CloseAsync(); } catch { } }
                _socket = _client.NewSocket();
                await _socket.ConnectAsync(_session, true);
                Debug.Log("[Auth] Socket connected");
            }
            return _socket;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Auth] Socket connect failed: {ex.Message} (auth still valid)");
            return null;
        }
    }

    /// <summary>
    /// Drops the current websocket and opens a new one. A socket can look connected while its messages go unanswered
    /// (calls end in "A task was canceled"); the lobby uses this to retry once on a fresh connection.
    /// </summary>
    public async Task<ISocket> ReconnectSocketAsync()
    {
        if (_session == null || _session.IsExpired) return null;
        if (_socket != null)
        {
            try { await _socket.CloseAsync(); } catch { }
            _socket = null;
        }
        return await EnsureSocketAsync();
    }

    private void LoadLobby()
    {
        if (string.IsNullOrEmpty(lobbySceneName)) return;
        // Avoid double-load if already in lobby
        var active = SceneManager.GetActiveScene().name;
        if (active == lobbySceneName) return;
        Debug.Log($"[Auth] Loading lobby scene: {lobbySceneName}");
        Railbound.UI.SceneTransition.Go(lobbySceneName, "ENTERING LOBBY");
    }

    private static string CleanError(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return "Unknown error";
        // Nakama messages can be verbose JSON - keep short
        if (msg.Length > 200) msg = msg.Substring(0, 200);
        return msg;
    }

    // Inspector helpers
    [ContextMenu("Test Guest Login")]
    private async void ContextTestGuest() { var r = await LoginAsGuestAsync(); Debug.Log($"Test guest: {r.IsSuccess} {r.Error}"); }

    [ContextMenu("Clear Saved Session (Logout)")]
    private void ContextClearSession()
    {
        Logout();
        Debug.Log("[Auth] Session cleared via Inspector context menu.");
    }
}

/// <summary>Result of an authentication attempt - kept identical to original API so LoginUI compiles.</summary>
public struct AuthResult
{
    public bool IsSuccess { get; }
    public string UserId { get; }
    public string Error { get; }

    private AuthResult(bool success, string userId, string error)
    {
        IsSuccess = success;
        UserId = userId;
        Error = error;
    }

    public static AuthResult Success(string userId) => new AuthResult(true, userId, null);
    public static AuthResult Failure(string error) => new AuthResult(false, null, error);
}
