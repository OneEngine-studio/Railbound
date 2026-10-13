using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nakama;

namespace Railbound.UI
{
    /// <summary>
    /// Manages the Nakama Global Chat integration and interactive lobby drawer.
    /// Connects to Nakama's global room channel via AuthenticationService,
    /// receives real-time channel broadcasts, updates the lobby chat ticker,
    /// and allows players to send messages in the tactical Global Chat drawer.
    /// </summary>
    public class LobbyGlobalChatController : MonoBehaviour
    {
        [Serializable]
        public class ChatMessagePayload
        {
            public string message;
        }

        [Serializable]
        public class ChatEntry
        {
            public string sender;
            public string text;
            public string time;
            public bool isSelf;
            public bool isSystem;
        }

        [Header("Ticker Bar")]
        [SerializeField] private Button tickerButton;
        [SerializeField] private TextMeshProUGUI tickerPreviewText;
        [SerializeField] private TextMeshProUGUI tickerExpandIcon;
        [SerializeField] private RectTransform tickerExpandArrow;

        [Header("Drawer Window")]
        [SerializeField] private GameObject chatDrawer;
        [SerializeField] private Button closeDrawerButton;
        [SerializeField] private TextMeshProUGUI statusBadgeText;
        [SerializeField] private Image statusBadgeDot;

        [Header("Message Feed")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform messageContainer;
        [SerializeField] private GameObject messageRowPrefab;
        [SerializeField] private int maxMessages = 60;

        [Header("Input Bar")]
        [SerializeField] private TMP_InputField messageInputField;
        [SerializeField] private Button sendButton;

        [Header("Channel Settings")]
        [SerializeField] private string channelRoomName = "global";
        [SerializeField] private bool openOnStart = false;

        private ISocket _socket;
        private IChannel _channel;
        private readonly List<ChatEntry> _messages = new List<ChatEntry>();
        private bool _isConnecting;
        private bool _isSubscribed;

        private static readonly Color ColorLive = new Color(0.29f, 0.87f, 0.50f, 1f);      // #4ADE80
        private static readonly Color ColorOffline = new Color(1.0f, 0.62f, 0.17f, 1f);     // #FF9E2C
        private static readonly Color ColorSelf = new Color(1.0f, 0.62f, 0.17f, 1f);        // Amber
        private static readonly Color ColorOther = new Color(0.38f, 0.76f, 1.0f, 1f);       // Cyan/Blue
        private static readonly Color ColorSystem = new Color(0.70f, 0.75f, 0.82f, 1f);     // Muted tactical

        private void Awake()
        {
            if (tickerButton != null)
                tickerButton.onClick.AddListener(ToggleChatDrawer);

            if (closeDrawerButton != null)
                closeDrawerButton.onClick.AddListener(CloseChatDrawer);

            if (sendButton != null)
                sendButton.onClick.AddListener(SendMessageFromInput);

            if (messageInputField != null)
                messageInputField.onSubmit.AddListener(_ => SendMessageFromInput());

            if (chatDrawer != null)
                chatDrawer.SetActive(openOnStart);

            UpdateExpandIcon(openOnStart);
            SetStatus(false, "CONNECTING");
        }

        private void Start()
        {
            // Seed initial tactical lobby announcements so feed is immediately readable
            SeedInitialMessages();

            // Connect to Nakama Chat
            _ = InitializeNakamaChatAsync();
        }

        private void OnDestroy()
        {
            UnsubscribeSocketEvents();
            if (_socket != null && _channel != null)
            {
                try { _ = _socket.LeaveChatAsync(_channel.Id); } catch { }
            }
        }

        // ------------------------------------------------------------------ Nakama Connection

        public async Task InitializeNakamaChatAsync()
        {
            if (_isConnecting) return;
            _isConnecting = true;

            try
            {
                // Wait briefly for AuthenticationService if initializing
                float timeout = 4f;
                while ((AuthenticationService.Instance == null || !AuthenticationService.Instance.IsAuthenticationReady) && timeout > 0f)
                {
                    await Task.Delay(200);
                    timeout -= 0.2f;
                }

                if (AuthenticationService.Instance == null)
                {
                    Debug.LogWarning("[GlobalChat] AuthenticationService.Instance not found. Running in offline preview mode.");
                    SetStatus(false, "OFFLINE");
                    return;
                }

                _socket = await AuthenticationService.Instance.EnsureSocketAsync();
                if (_socket == null || !_socket.IsConnected)
                {
                    Debug.LogWarning("[GlobalChat] Nakama socket not connected. Running in offline preview mode.");
                    SetStatus(false, "OFFLINE");
                    return;
                }

                SubscribeSocketEvents();

                // Join room channel "global"
                _channel = await _socket.JoinChatAsync(channelRoomName, ChannelType.Room, persistence: true, hidden: false);
                Debug.Log($"[GlobalChat] Joined Nakama global channel: {_channel.Id} (room={_channel.RoomName})");

                SetStatus(true, "LIVE");
                AddSystemMessage($"Connected to Nakama Global Relay [{channelRoomName}].");

                // Fetch recent channel messages from history if client & session are ready
                var auth = AuthenticationService.Instance;
                if (auth.Client != null && auth.Session != null)
                {
                    try
                    {
                        var history = await auth.Client.ListChannelMessagesAsync(auth.Session, _channel.Id, 15, forward: false);
                        if (history != null && history.Messages != null)
                        {
                            var list = new List<IApiChannelMessage>(history.Messages);
                            list.Reverse(); // oldest first
                            foreach (var msg in list)
                            {
                                ProcessIncomingMessage(msg, isHistory: true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"[GlobalChat] History fetch skipped: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GlobalChat] Connection exception: {ex.Message}. Falling back to offline preview.");
                SetStatus(false, "OFFLINE");
            }
            finally
            {
                _isConnecting = false;
            }
        }

        private void SubscribeSocketEvents()
        {
            if (_socket == null || _isSubscribed) return;
            _socket.ReceivedChannelMessage += OnChannelMessageReceived;
            _isSubscribed = true;
        }

        private void UnsubscribeSocketEvents()
        {
            if (_socket == null || !_isSubscribed) return;
            try { _socket.ReceivedChannelMessage -= OnChannelMessageReceived; } catch { }
            _isSubscribed = false;
        }

        private void OnChannelMessageReceived(IApiChannelMessage message)
        {
            // Unity main-thread dispatch
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                ProcessIncomingMessage(message, isHistory: false);
            });
        }

        private void ProcessIncomingMessage(IApiChannelMessage message, bool isHistory)
        {
            if (message == null) return;
            if (_channel != null && !string.IsNullOrEmpty(message.ChannelId) && message.ChannelId != _channel.Id)
                return; // Different channel

            string parsedText = ExtractMessageText(message.Content);
            if (string.IsNullOrWhiteSpace(parsedText)) return;

            string myUserId = AuthenticationService.Instance != null ? AuthenticationService.Instance.CurrentUserId : "";
            bool isSelf = !string.IsNullOrEmpty(myUserId) && message.SenderId == myUserId;

            string senderName = !string.IsNullOrEmpty(message.Username) ? message.Username : (isSelf ? "You" : "Operator");
            string timeStr = DateTime.Now.ToString("HH:mm");

            AddMessageEntry(new ChatEntry
            {
                sender = senderName,
                text = parsedText,
                time = timeStr,
                isSelf = isSelf,
                isSystem = false
            });
        }

        private static string ExtractMessageText(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;

            try
            {
                if (content.TrimStart().StartsWith("{"))
                {
                    var payload = JsonUtility.FromJson<ChatMessagePayload>(content);
                    if (payload != null && !string.IsNullOrEmpty(payload.message))
                        return payload.message;
                }
            }
            catch { }

            // If raw text or simple JSON fallback
            return content.Trim();
        }

        // ------------------------------------------------------------------ Sending Messages

        public async void SendMessageFromInput()
        {
            if (messageInputField == null) return;
            string raw = messageInputField.text;
            if (string.IsNullOrWhiteSpace(raw)) return;

            string messageText = raw.Trim();
            messageInputField.text = string.Empty;
            messageInputField.ActivateInputField();

            string myUsername = "You";
            if (AuthenticationService.Instance != null && AuthenticationService.Instance.Session != null)
            {
                string uname = AuthenticationService.Instance.Session.Username;
                if (!string.IsNullOrEmpty(uname)) myUsername = uname;
            }

            // If connected to Nakama channel, write message over socket
            if (_socket != null && _socket.IsConnected && _channel != null)
            {
                try
                {
                    string payloadJson = JsonUtility.ToJson(new ChatMessagePayload { message = messageText });
                    await _socket.WriteChatMessageAsync(_channel.Id, payloadJson);
                    return; // Message will be received via OnChannelMessageReceived
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GlobalChat] SendMessage failed: {ex.Message}. Posting locally.");
                }
            }

            // Offline preview fallback
            AddMessageEntry(new ChatEntry
            {
                sender = myUsername,
                text = messageText,
                time = DateTime.Now.ToString("HH:mm"),
                isSelf = true,
                isSystem = false
            });
        }

        // ------------------------------------------------------------------ UI Feed & Rows

        private void AddMessageEntry(ChatEntry entry)
        {
            _messages.Add(entry);
            if (_messages.Count > maxMessages)
            {
                _messages.RemoveAt(0);
                if (messageContainer != null && messageContainer.childCount > 0)
                {
                    Destroy(messageContainer.GetChild(0).gameObject);
                }
            }

            CreateMessageRow(entry);
            UpdateTickerPreview(entry);
            ScrollToBottom();
        }

        public void AddSystemMessage(string text)
        {
            AddMessageEntry(new ChatEntry
            {
                sender = "SYSTEM",
                text = text,
                time = DateTime.Now.ToString("HH:mm"),
                isSelf = false,
                isSystem = true
            });
        }

        private void CreateMessageRow(ChatEntry entry)
        {
            if (messageContainer == null) return;

            GameObject rowObj;
            if (messageRowPrefab != null)
            {
                rowObj = Instantiate(messageRowPrefab, messageContainer);
            }
            else
            {
                rowObj = BuildDefaultMessageRow();
                rowObj.transform.SetParent(messageContainer, false);
            }

            var textComp = rowObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null)
            {
                if (entry.isSystem)
                {
                    textComp.text = $"<color=#8E9CA8><size=15>[{entry.time}]</size></color> <color=#FF9E2C><b>[SYS]</b></color> <color=#B4C0CC>{entry.text}</color>";
                }
                else
                {
                    string nameColor = entry.isSelf ? "#FF9E2C" : "#60C4FF";
                    textComp.text = $"<color=#8E9CA8><size=15>[{entry.time}]</size></color> <color={nameColor}><b>{entry.sender}:</b></color> <color=#E6EDF5>{entry.text}</color>";
                }
            }
        }

        private GameObject BuildDefaultMessageRow()
        {
            var go = new GameObject("ChatMsgRow", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(0f, 26f);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 22f;
            le.preferredHeight = -1f;
            le.flexibleWidth = 1f;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 17f;
            tmp.enableWordWrapping = true;
            tmp.color = new Color(0.9f, 0.93f, 0.96f, 1f);
            tmp.richText = true;

            // Inherit font from tickerPreviewText if available
            if (tickerPreviewText != null && tickerPreviewText.font != null)
                tmp.font = tickerPreviewText.font;

            return go;
        }

        private void UpdateTickerPreview(ChatEntry entry)
        {
            if (tickerPreviewText == null) return;
            string tag = entry.isSystem ? "<color=#FF9E2C>[SYS]</color>" : "<color=#FF9E2C>[GLOBAL]</color>";
            string sender = entry.isSelf ? "<color=#FF9E2C>You:</color>" : $"<color=#60C4FF>{entry.sender}:</color>";
            tickerPreviewText.text = $"{tag} {sender} {entry.text}";
        }

        private void ScrollToBottom()
        {
            if (scrollRect == null) return;
            StartCoroutine(ScrollToBottomCoroutine());
        }

        private IEnumerator ScrollToBottomCoroutine()
        {
            yield return null; // wait 1 frame for LayoutGroup to compute
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        private void SeedInitialMessages()
        {
            if (_messages.Count > 0) return;

            AddMessageEntry(new ChatEntry
            {
                sender = "SYSTEM",
                text = "Welcome to Railbound Global Relay channel.",
                time = "12:00",
                isSelf = false,
                isSystem = true
            });

            AddMessageEntry(new ChatEntry
            {
                sender = "Vanguard_01",
                text = "Convoy Raid departing in 02:15, squad slot open!",
                time = "12:01",
                isSelf = false,
                isSystem = false
            });

            AddMessageEntry(new ChatEntry
            {
                sender = "IronClad",
                text = "Iron Canyon loot drops boosted for Season 01.",
                time = "12:02",
                isSelf = false,
                isSystem = false
            });
        }

        // ------------------------------------------------------------------ Drawer Toggle

        public void ToggleChatDrawer()
        {
            if (chatDrawer == null) return;
            bool newState = !chatDrawer.activeSelf;
            chatDrawer.SetActive(newState);
            UpdateExpandIcon(newState);

            if (newState)
            {
                ScrollToBottom();
                if (messageInputField != null)
                    messageInputField.ActivateInputField();
            }
        }

        public void OpenChatDrawer()
        {
            if (chatDrawer == null) return;
            chatDrawer.SetActive(true);
            UpdateExpandIcon(true);
            ScrollToBottom();
            if (messageInputField != null)
                messageInputField.ActivateInputField();
        }

        public void CloseChatDrawer()
        {
            if (chatDrawer == null) return;
            chatDrawer.SetActive(false);
            UpdateExpandIcon(false);
        }

        private void UpdateExpandIcon(bool isOpen)
        {
            if (tickerExpandIcon != null)
            {
                tickerExpandIcon.text = isOpen ? "v" : "^";
            }
            if (tickerExpandArrow != null)
            {
                tickerExpandArrow.localEulerAngles = new Vector3(0f, 0f, isOpen ? -90f : 90f);
            }
        }

        private void SetStatus(bool isLive, string statusText)
        {
            if (statusBadgeText != null)
            {
                statusBadgeText.text = isLive ? "● LIVE" : $"● {statusText}";
                statusBadgeText.color = isLive ? ColorLive : ColorOffline;
            }

            if (statusBadgeDot != null)
            {
                statusBadgeDot.color = isLive ? ColorLive : ColorOffline;
            }
        }
    }

    /// <summary>
    /// Thread dispatcher helper for Nakama WebSocket callbacks.
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> ExecutionQueue = new Queue<Action>();
        private static UnityMainThreadDispatcher _instance;

        public static void Enqueue(Action action)
        {
            if (action == null) return;
            lock (ExecutionQueue)
            {
                ExecutionQueue.Enqueue(action);
            }

            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("UnityMainThreadDispatcher");
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            lock (ExecutionQueue)
            {
                while (ExecutionQueue.Count > 0)
                {
                    try
                    {
                        ExecutionQueue.Dequeue()?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
        }
    }
}
