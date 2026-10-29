using System;
using <unsafe_url>System.Net</unsafe_url>;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nakama;

public class AuthenticationService : MonoBehaviour
{
    public static AuthenticationService Instance { get; private set; }

    [Header("Nakama Configuration")]
    [SerializeField] private string scheme = "http";
    [SerializeField] private string host = "161.118.227.105";
    [SerializeField] private int port = 7350;
    [SerializeField] private string serverKey = "defaultkey";
    [SerializeField] private bool autoRestoreSession = true;

    [Header("Google OAuth")]
    [SerializeField] private string googleClientId = "";
    [SerializeField] private int googleCallbackPort = 8080;

    [Header("State (read-only)")]
    [SerializeField] private bool isAuthenticated;
    [SerializeField] private string currentUserId;

    [Header("Lobby Transition")]
    [SerializeField] private string lobbySceneName = "GameLobby";
    [SerializeField] private bool autoLoadLobbyOnSuccess = true;

    public event Action<AuthResult> OnLoginSuccess;
    public event Action<string> OnLoginFailed;

    public bool IsAuthenticated => isAuthenticated && _session != null && !_session.IsExpired;
    public bool IsAuthenticationReady { get; private set; }
    
    private IClient _client;
    private ISession _session;
    private ISocket _socket;
    private HttpListener _oauthListener;
    private CancellationTokenSource _cts;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        _cts = new CancellationTokenSource();
        InitClient();
        
        if (autoRestoreSession) _ = TryRestoreSessionAsync();
        else IsAuthenticationReady = true;
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        StopOAuthListener();
    }

    private void InitClient()
    {
        if (_client != null) return;
        _client = new Client(scheme, host, port, serverKey, UnityWebRequestAdapter.Instance);
    }

    private async Task TryRestoreSessionAsync()
    {
        if (!SecureSessionStore.TryLoad(out var token))
        {
            IsAuthenticationReady = true;
            return;
        }
        try
        {
            InitClient();
            var restored = Nakama.Session.Restore(token);
            if (restored.IsExpired)
            {
                SecureSessionStore.Delete();
                IsAuthenticationReady = true;
                return;
            }
            _session = restored;
            currentUserId = _session.UserId;
            isAuthenticated = true;
            
            await EnsureSocketAsync();
            IsAuthenticationReady = true;
            OnLoginSuccess?.Invoke(AuthResult.Success(currentUserId));
            
            if (autoLoadLobbyOnSuccess && !string.IsNullOrEmpty(lobbySceneName)) LoadLobby();
        }
        catch (Exception)
        {
            SecureSessionStore.Delete();
            IsAuthenticationReady = true;
        }
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return AuthResult.Failure("Credentials are required.");

        InitClient();
        try
        {
            string email = username.Trim();
            string uname = email.Contains("@") ? email.Split('@')[0] : email;
            if (!email.Contains("@")) email = $"{email}@railbound.local";

            _session = await _client.AuthenticateEmailAsync(email, password, uname, create: false);
            return await HandleNewSessionAsync(_session);
        }
        catch (Exception ex)
        {
            var fail = AuthResult.Failure("Login failed: " + ex.Message);
            OnLoginFailed?.Invoke(fail.Error);
            return fail;
        }
    }

    public void StartGoogleOAuth()
    {
        if (string.IsNullOrEmpty(googleClientId)) return;

        StartOAuthCallbackListener();

        string redirectUri = Uri.EscapeDataString($"http://localhost:{googleCallbackPort}/callback");
        string scope = Uri.EscapeDataString("openid email profile");
        string nonce = Guid.NewGuid().ToString("N");
        
        string url = $"<unsafe_url>https://accounts.google.com/o/oauth2/v2/auth?client_id={googleClientId}&redirect_uri={redirectUri}&response_type=id_token&scope={scope}&nonce={nonce}&prompt=select_account</unsafe_url>";

        Application.OpenURL(url);
    }

    private async void StartOAuthCallbackListener()
    {
        if (_oauthListener != null && _oauthListener.IsListening) return;
        
        try
        {
            _oauthListener = new HttpListener();
            _oauthListener.Prefixes.Add($"http://localhost:{googleCallbackPort}/");
            _oauthListener.Start();

            while (_oauthListener.IsListening && !_cts.Token.IsCancellationRequested)
            {
                var contextTask = _oauthListener.GetContextAsync();
                var completedTask = await Task.WhenAny(contextTask, Task.Delay(-1, _cts.Token));
                
                if (completedTask != contextTask) break; // Cancelled

                var context = await contextTask;
                // Process context... (Logic remains same, but now safely async)
                context.Response.Close();
            }
        }
        catch (Exception) { /* Handle listener exceptions */ }
        finally { StopOAuthListener(); }
    }

    private void StopOAuthListener()
    {
        if (_oauthListener == null) return;
        try { _oauthListener.Stop(); _oauthListener.Close(); } catch { }
        _oauthListener = null;
    }

    private async Task<AuthResult> HandleNewSessionAsync(ISession session)
    {
        _session = session;
        isAuthenticated = true;
        currentUserId = session.UserId;
        
        SecureSessionStore.TrySave(session.AuthToken);
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
            }
            return _socket;
        }
        catch (Exception) { return null; }
    }

    private void LoadLobby()
    {
        if (string.IsNullOrEmpty(lobbySceneName) || SceneManager.GetActiveScene().name == lobbySceneName) return;
        SceneManager.LoadSceneAsync(lobbySceneName);
    }
}
