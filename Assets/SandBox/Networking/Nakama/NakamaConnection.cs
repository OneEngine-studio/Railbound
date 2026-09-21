using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;
using UnityEngine.Networking;


namespace NakamaNetwork
{
    /// <summary>
    /// Configuration for Nakama connection settings
    /// </summary>
    [Serializable]
    public class NakamaConfig
    {
        [Tooltip("HTTP or HTTPS")]
        public string scheme = "https";
        [Tooltip("Nakama server hostname or IP address")]
        public string host = "127.0.0.1";
        [Tooltip("Nakama server port (default: 7350 for HTTP, 7359 for HTTPS)")]
        public int port = 7359;
        [Tooltip("Nakama server key - found in the Nakama dashboard")]
        public string serverKey = "defaultkey";
        [Tooltip("App ID for device identifiers")]
        public string appID = "railbound.game";
    }

    /// <summary>
    /// Authentication result callback
    /// </summary>
    public enum AuthResult
    {
        Success,
        Failed,
        Cancelled,
        InvalidCredentials,
        NetworkError
    }

    /// <summary>
    /// Callback type for authentication operations
    /// </summary>
    public delegate void AuthCallback(AuthResult result, ISession session = null, string errorMessage = null);

    /// <summary>
    /// Callback type for privacy policy callbacks
    /// </summary>
    public delegate void PrivacyPolicyCheckCallback(bool accepted, string reason = null);

    /// <summary>
    /// Connection state
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Failed
    }

    /// <summary>
    /// Nakama connection manager - handles authentication and connections
    /// Supports device auth, email/password auth, custom auth
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "NakamaConnection", menuName = "Networking/NakamaConnection")]
    public class NakamaConnection : ScriptableObject
    {
        [Header("Connection Settings")]
        public NakamaConfig config = new NakamaConfig();
        
        [Header("Session Management")]
        [Tooltip("Auto-refresh session if it's about to expire")]
        public bool autoRefreshSession = true;
        [Tooltip("Session expiry time in seconds (default: 3600)")]
        public int defaultSessionExpiry = 3600;
        [Tooltip("Storage key for session token")]
        public string sessionStorageKey = "nakama.session.token";
        [Tooltip("Storage key for user ID")]
        public string userIdStorageKey = "nakama.user.id";
        [Tooltip("Storage key for device identifier")]
        public string deviceIdentifierStorageKey = "nakama.device.id";

        [Header("UI References")]
        [Tooltip("CanvasGroup to show/hide connection status/loading")]
        public CanvasGroup connectionUI;
        [Tooltip("Text to show connection status")]
        public TMPro.TMP_Text statusText;

        // Internal state
        private IClient _client;
        private ISession _session;
        private ISocket _socket;
        private ConnectionState _state = ConnectionState.Disconnected;
        
        private string _currentMatchmakingTicket;
        private string _currentMatchId;

        // Events
        public event Action<ConnectionState> OnConnectionStateChanged;
        public event Action<ISession> OnSessionCreated;
        public event Action<ISession> OnSessionRestored;
        public event Action<ISocket> OnSocketConnected;
        public event Action<Exception> OnConnectionError;
        public event Action<IMatch> OnMatchJoined;

        /// <summary>
        /// Current connection state
        /// </summary>
        public ConnectionState ConnectionState => _state;

        /// <summary>
        /// Current session (may be null if not logged in)
        /// </summary>
        public ISession Session => _session;

        /// <summary>
        /// Current socket (may be null if not connected)
        /// </summary>
        public ISocket Socket => _socket;

        /// <summary>
        /// Current match ID (if in a match)
        /// </summary>
        public string CurrentMatchId => _currentMatchId;

        #region Public Methods

        /// <summary>
        /// Initialize the Nakama client (only needs to be called once)
        /// </summary>
        public void Initialize()
        {
            if (_client == null)
            {
                _client = new Client(
                    config.scheme,
                    config.host,
                    config.port,
                    config.serverKey,
                    UnityWebRequestAdapter.Instance,
                    autoRefreshSession
                );
                Debug.Log($"[NakamaConnection] Initialized with {config.scheme}://{config.host}:{config.port}");
            }
        }

        /// <summary>
        /// Connect to Nakama server with existing session (if available)
        /// </summary>
        public async void Connect()
        {
            await ConnectAsync();
        }

        /// <summary>
        /// Asynchronously connect to Nakama server
        /// </summary>
        public async Task ConnectAsync()
        {
            if (!ValidateConfig())
            {
                SetConnectionState(ConnectionState.Failed);
                return;
            }

            Initialize();

            try
            {
                SetConnectionState(ConnectionState.Connecting);
                UpdateStatus("Connecting...");

                // Try to restore existing session
                string sessionToken = PlayerPrefs.GetString(sessionStorageKey);
                if (!string.IsNullOrEmpty(sessionToken))
                {
                    try
                    {
                        _session = Nakama.Session.Restore(sessionToken);
                        Debug.Log("[NakamaConnection] Session restored from storage");
                        
                        if (!_session.IsExpired)
                        {
                            await CompleteConnection();
                            return;
                        }
                        else
                        {
                            Debug.Log("[NakamaConnection] Restored session expired");
                            PlayerPrefs.DeleteKey(sessionStorageKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[NakamaConnection] Failed to restore session: {ex.Message}");
                        _session = null;
                    }
                }

                // If no valid session, authenticate as guest
                if (_session == null)
                {
                    await AuthenticateDeviceAsync(OnDeviceAuthResult);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NakamaConnection] Connection failed: {ex.Message}");
                SetConnectionState(ConnectionState.Failed);
                OnConnectionError?.Invoke(ex);
                UpdateStatus("Connection failed");
            }
        }

        /// <summary>
        /// Connect as a guest using device identifier
        /// </summary>
        public async Task AuthenticateDeviceAsync(AuthCallback callback = null)
        {
            try
            {
                Initialize();

                string deviceId = GetOrCreateDeviceIdentifier();
                
                Debug.Log($"[NakamaConnection] Authenticating with device ID: {deviceId}");

                _session = await _client.AuthenticateDeviceAsync(deviceId);
                
                if (callback != null)
                {
                    callback?.Invoke(AuthResult.Success, _session, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NakamaConnection] Device auth failed: {ex.Message}");
                callback?.Invoke(AuthResult.Failed, null, ex.Message);
            }
        }

        /// <summary>
        /// Authenticate with username and password
        /// </summary>
        public async Task AuthenticateUsernamePasswordAsync(string username, string password, bool createAccount = true, AuthCallback callback = null)
        {
            try
            {
                Initialize();

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    callback?.Invoke(AuthResult.InvalidCredentials, null, "Username and password cannot be empty");
                    return;
                }

                Debug.Log($"[NakamaConnection] Authenticating with username: {username}");

                _session = await _client.AuthenticateEmailAsync(
                    username, 
                    password, 
                    username, 
                    createAccount, 
                    vars: null
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NakamaConnection] Username/password auth failed: {ex.Message}");
                callback?.Invoke(AuthResult.Failed, null, ex.Message);
            }
        }

        /// <summary>
        /// Authenticate with a custom ID
        /// </summary>
        public async Task AuthenticateCustomAsync(string customId, string username = null, bool create = true, AuthCallback callback = null)
        {
            try
            {
                Initialize();

                Debug.Log($"[NakamaConnection] Authenticating with custom ID: {customId}");

                _session = await _client.AuthenticateCustomAsync(
                    customId, 
                    username, 
                    create, 
                    vars: null
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NakamaConnection] Custom auth failed: {ex.Message}");
                callback?.Invoke(AuthResult.Failed, null, ex.Message);
            }
        }

        /// <summary>
        /// Check if current session is valid
        /// </summary>
        public bool IsSessionValid()
        {
            return _session != null && !_session.IsExpired;
        }

        /// <summary>
        /// Get or create device identifier for this device
        /// </summary>
        public string GetOrCreateDeviceIdentifier()
        {
            string deviceId = PlayerPrefs.GetString(deviceIdentifierStorageKey);
            
            if (string.IsNullOrEmpty(deviceId))
            {
                // Generate unique device ID
                deviceId = SystemInfo.deviceUniqueIdentifier;
                
                // Fallback for unsupported devices
                if (deviceId == SystemInfo.unsupportedIdentifier)
                {
                    deviceId = Guid.NewGuid().ToString();
                }
                
                PlayerPrefs.SetString(deviceIdentifierStorageKey, deviceId);
            }

            return deviceId;
        }

        /// <summary>
        /// Create a socket connection
        /// </summary>
        public async void CreateSocket()
        {
            if (_session == null || !IsSessionValid())
            {
                Debug.LogError("[NakamaConnection] Cannot create socket: no valid session");
                return;
            }

            try
            {
                _socket = _client.NewSocket();
                await _socket.ConnectAsync(_session, true);
                
                Debug.Log("[NakamaConnection] Socket connected successfully");
                OnSocketConnected?.Invoke(_socket);
                SetConnectionState(ConnectionState.Connected);
                UpdateStatus("Connected");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NakamaConnection] Socket connection failed: {ex.Message}");
                SetConnectionState(ConnectionState.Failed);
            }
        }

        /// <summary>
        /// Disconnect from Nakama
        /// </summary>
        public void Disconnect()
        {
            if (_socket != null)
            {
                try
                {
                    // Disconnect socket
                    // Note: ISocket doesn't have a direct disconnect method in all versions
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NakamaConnection] Error disconnecting socket: {ex.Message}");
                }
            }
            
            _session = null;
            _socket = null;
            SetConnectionState(ConnectionState.Disconnected);
            PlayerPrefs.DeleteKey(sessionStorageKey);
            UpdateStatus("Disconnected");
            
            Debug.Log("[NakamaConnection] Disconnected from Nakama");
        }

        /// <summary>
        /// Start matchmaking with default criteria
        /// </summary>
        public async void FindMatch(int minCount = 1, int maxCount = 1, string query = "*", AuthCallback callback = null)
        {
            if (_socket == null)
            {
                CreateSocket();
                await System.Threading.Tasks.Task.Delay(1000); // Wait for socket
            }

            try
            {
                Debug.Log($"[NakamaConnection] Finding match: query={query}, min={minCount}, max={maxCount}");
                
                // Subscribe to matchmaker event
                _socket.ReceivedMatchmakerMatched += OnMatchmakerMatched;

                var matchmakerTicket = await _socket.AddMatchmakerAsync(
                    query,
                    minCount,
                    maxCount,
                    null,
                    null,
                    null
                );
                _currentMatchmakingTicket = matchmakerTicket.Ticket;
                
                Debug.Log($"[NakamaConnection] Matchmaker ticket: {matchmakerTicket.Ticket}");
                
                UpdateStatus("Matchmaking...");
                
                callback?.Invoke(AuthResult.Success, null, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NakamaConnection] Matchmaking failed: {ex.Message}");
                callback?.Invoke(AuthResult.Failed, null, ex.Message);
            }
        }

        /// <summary>
        /// Join an existing match by ID
        /// </summary>
        public async void JoinMatchAsync(string matchId, AuthCallback callback = null)
        {
            try
            {
                if (_socket == null)
                {
                    CreateSocket();
                    await System.Threading.Tasks.Task.Delay(1000);
                }

                var match = await _socket.JoinMatchAsync(matchId);
                _currentMatchId = matchId;
                
                Debug.Log($"[NakamaConnection] Joined match: {matchId}");
                
                OnMatchJoined?.Invoke(match);
                callback?.Invoke(AuthResult.Success, _session, "Joined match");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NakamaConnection] Join match failed: {ex.Message}");
                callback?.Invoke(AuthResult.Failed, null, ex.Message);
            }
        }

        #endregion

        #region Privacy Policy Methods

        /// <summary>
        /// Check if user has accepted privacy policy
        /// </summary>
        public bool HasAcceptedPrivacyPolicy()
        {
            return PlayerPrefs.HasKey("privacy_policy_accepted") && 
                   PlayerPrefs.GetInt("privacy_policy_accepted") == 1;
        }

        /// <summary>
        /// Set privacy policy acceptance
        /// </summary>
        public void AcceptPrivacyPolicy()
        {
            PlayerPrefs.SetInt("privacy_policy_accepted", 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Check privacy policy and show confirmation if not accepted
        /// </summary>
        public void CheckPrivacyPolicy(PrivacyPolicyCheckCallback callback)
        {
            bool accepted = HasAcceptedPrivacyPolicy();
            
            if (accepted)
            {
                callback?.Invoke(true);
            }
            else
            {
                // Change to show accept button in popup
                callback?.Invoke(false, "Please accept the privacy policy to continue");
            }
        }

        /// <summary>
        /// Logout and clear session data
        /// </summary>
        public void Logout()
        {
            // Remove session from storage
            PlayerPrefs.DeleteKey(sessionStorageKey);
            PlayerPrefs.DeleteKey(userIdStorageKey);
            PlayerPrefs.Save();
            
            // Disconnect
            Disconnect();
            
            Debug.Log("[NakamaConnection] Logged out");
        }

        #endregion

        #region UI Methods

        /// <summary>
        /// Update connection status UI
        /// </summary>
        public void UpdateStatus(string message)
        {
            Debug.Log($"[NakamaConnection] Status: {message}");
            
            if (connectionUI != null)
            {
                if (connectionUI.alpha >= 1f)
                {
                    connectionUI.alpha = 0f;
                }
                
                connectionUI.alpha = 1f;
                connectionUI.blocksRaycasts = true;
                connectionUI.interactable = true;
            }

            UpdateStatusUI(message);
        }

        #endregion

        #region Private Methods

        private bool ValidateConfig()
        {
            if (string.IsNullOrEmpty(config.host))
            {
                Debug.LogError("[NakamaConnection] Server host is not configured");
                return false;
            }

            if (string.IsNullOrEmpty(config.serverKey))
            {
                Debug.LogError("[NakamaConnection] Server key is not configured");
                return false;
            }

            return true;
        }

        private void SetConnectionState(ConnectionState newState)
        {
            if (_state == newState) return;
            
            _state = newState;
            Debug.Log($"[NakamaConnection] Connection state changed: {_state}");
            OnConnectionStateChanged?.Invoke(newState);
        }

        private async Task CompleteConnection()
        {
            Debug.Log("[NakamaConnection] Session is valid, completing connection...");
            
            // Save session token
            PlayerPrefs.SetString(sessionStorageKey, _session.AuthToken);
            PlayerPrefs.SetString(userIdStorageKey, _session.UserId);
            
            // Create socket
            CreateSocket();
            
            // Wait for socket to connect
            int maxAttempts = 20;
            int attempt = 0;
            
            while (_socket == null && attempt < maxAttempts)
            {
                await System.Threading.Tasks.Task.Delay(500);
                attempt++;
            }

            SetConnectionState(ConnectionState.Connected);
            UpdateStatus("Connected");
            OnSessionRestored?.Invoke(_session);
        }

        private void OnAuthComplete(Task<ISession> task)
        {
            if (task.IsFaulted)
            {
                var error = task.Exception?.InnerException;
                Debug.LogError($"[NakamaConnection] Authentication error: {error?.Message}");
                
                SetConnectionState(ConnectionState.Failed);
                OnConnectionError?.Invoke(error);
            }
            else
            {
                _session = task.Result;
                
                if (_session == null)
                {
                    Debug.LogError("[NakamaConnection] Authentication returned null session");
                    SetConnectionState(ConnectionState.Failed);
                    return;
                }

                // Save session
                PlayerPrefs.SetString(sessionStorageKey, _session.AuthToken);
                PlayerPrefs.SetString(userIdStorageKey, _session.UserId);
                PlayerPrefs.SetString("last_username", _session.Username ?? "Guest");

                Debug.Log($"[NakamaConnection] Authenticated successfully! User ID: {_session.UserId}");
                Debug.Log($"[NakamaConnection] Username: {_session.Username}");
                Debug.Log($"[NakamaConnection] Token length: {_session.AuthToken?.Length ?? 0}");

                OnSessionCreated?.Invoke(_session);
                
                // Create socket and complete connection
                CreateSocket();
            }
        }

        private async void OnDeviceAuthResult(AuthResult result, ISession session, string errorMessage)
        {
            if (result == AuthResult.Success)
            {
                _session = session;
                await CompleteConnection();
            }
            else
            {
                SetConnectionState(ConnectionState.Failed);
                UpdateStatus($"Auth failed: {errorMessage}");
                OnConnectionError?.Invoke(new Exception(errorMessage));
            }
        }

        private void OnMatchmakerMatched(IMatchmakerMatched matched)
        {
            Debug.Log("[NakamaConnection] Matchmaker callback received");
            
            // Join the match automatically
            JoinMatchAsync(matched.MatchId, (result, session, error) => {
                if (result == AuthResult.Success)
                {
                    UpdateStatus("Match joined!");
                }
            });
        }

        private IEnumerator AnimateStatus(string message)
        {
            connectionUI.alpha = 1f;
            connectionUI.blocksRaycasts = true;
            connectionUI.interactable = true;

            UpdateStatusUI(message);
            float duration = 2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                connectionUI.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            connectionUI.alpha = 0f;
            connectionUI.blocksRaycasts = false;
            connectionUI.interactable = false;
        }

        private void UpdateStatusUI(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        #endregion
    }
}
