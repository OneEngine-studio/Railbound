using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Small top-right chip: connection state, ping and number of players in the session.
    /// Replaces the old IMGUI ping badge; hidden while playing offline.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public class HudNetworkChip : MonoBehaviour
    {
        [SerializeField] private TMP_Text dot;
        [SerializeField] private TMP_Text pingText;
        [SerializeField] private TMP_Text playersText;
        [SerializeField] private float refreshSeconds = 0.5f;
        [SerializeField] private int goodPingMs = 80;
        [SerializeField] private int okPingMs = 160;

        private CanvasGroup _group;
        private float _next;
        private bool _show;

        void Awake() { _group = GetComponent<CanvasGroup>(); _group.alpha = 0f; }

        void Update()
        {
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + refreshSeconds;
                Refresh();
            }
            _group.alpha = Mathf.MoveTowards(_group.alpha, _show ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        }

        private void Refresh()
        {
            var nm = NetworkManager.Singleton;
            _show = nm != null && (nm.IsClient || nm.IsServer) && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            if (!_show) return;

            Color c = Color.white; string ping;
            if (nm.IsClient && !nm.IsConnectedClient) { ping = "CONNECTING"; c = new Color(1f, 0.72f, 0.2f); }
            else if (nm.IsHost || (nm.IsServer && !nm.IsClient)) { ping = "HOST"; }
            else
            {
                int rtt = 0;
                var utp = nm.NetworkConfig != null ? nm.NetworkConfig.NetworkTransport as UnityTransport : null;
                if (utp != null) rtt = (int)utp.GetCurrentRtt(NetworkManager.ServerClientId);
                ping = rtt + " ms";
                c = rtt <= goodPingMs ? new Color(0.48f, 0.92f, 0.22f) : rtt <= okPingMs ? Color.white : new Color(1f, 0.45f, 0.35f);
            }

            if (dot != null) dot.color = c;
            if (pingText != null && pingText.text != ping) pingText.text = ping;
            if (playersText != null)
            {
                int n = FindObjectsByType<JUNetworkPlayer>(FindObjectsSortMode.None).Length;
                string pt = n > 0 ? $"{n} PLAYER{(n == 1 ? "" : "S")}" : "";
                if (playersText.text != pt) playersText.text = pt;
            }
        }
    }
}
