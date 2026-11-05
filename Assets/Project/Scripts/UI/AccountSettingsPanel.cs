using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Events;

namespace Railbound.UI
{
    /// <summary>
    /// "Account" tab of the pause-menu Settings: player / authentication / multiplayer connection info.
    /// This is the UI shell only: sign-in is NOT implemented. Hook your authentication code to
    /// <see cref="onSignInRequested"/> / <see cref="onSignOutRequested"/> and call the Set... methods to
    /// show results. The ping and server readouts follow the live Netcode connection when there is one.
    /// </summary>
    public class AccountSettingsPanel : MonoBehaviour
    {
        [Header("Readouts")]
        [SerializeField] private TMP_Text playerNameValue;
        [SerializeField] private TMP_Text accountStatusValue;
        [SerializeField] private TMP_Text connectionValue;
        [SerializeField] private TMP_Text serverValue;
        [SerializeField] private TMP_Text pingValue;

        [Header("Events (hook authentication here)")]
        public UnityEvent onSignInRequested;
        public UnityEvent onSignOutRequested;

        [Header("Ping")]
        [SerializeField] private float refreshSeconds = 1f;
        [SerializeField] private int goodPingMs = 60;
        [SerializeField] private int okPingMs = 120;

        public bool IsSignedIn { get; private set; }

        void OnEnable()
        {
            Refresh();
            StopAllCoroutines();
            StartCoroutine(RefreshLoop());
        }

        private IEnumerator RefreshLoop()
        {
            var wait = new WaitForSecondsRealtime(Mathf.Max(0.2f, refreshSeconds));
            while (true)
            {
                yield return wait;
                Refresh();
            }
        }

        // ---------- API for your authentication code ----------
        public void SetPlayerName(string value) { Set(playerNameValue, string.IsNullOrEmpty(value) ? "Guest" : value); }
        public void SetAccountStatus(string value, bool signedIn) { IsSignedIn = signedIn; Set(accountStatusValue, value); }
        public void SetServerText(string value) { Set(serverValue, value); }
        public void SetPingText(string value) { Set(pingValue, value); }

        // Wire these to the Sign In / Sign Out rows (they only raise the events for now).
        public void RequestSignIn() { onSignInRequested?.Invoke(); }
        public void RequestSignOut() { onSignOutRequested?.Invoke(); }

        // ---------- live connection readouts ----------
        public void Refresh()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !(nm.IsClient || nm.IsServer))
            {
                Set(connectionValue, "Offline");
                Set(pingValue, "-- ms");
                return;
            }

            if (nm.IsClient && !nm.IsConnectedClient) { Set(connectionValue, "Connecting..."); Set(pingValue, "-- ms"); return; }

            Set(connectionValue, nm.IsHost ? "Hosting" : "Connected");
            var utp = nm.NetworkConfig != null ? nm.NetworkConfig.NetworkTransport as UnityTransport : null;
            if (utp != null)
            {
                Set(serverValue, $"{utp.ConnectionData.Address}:{utp.ConnectionData.Port}");
                if (nm.IsClient && !nm.IsHost)
                {
                    int rtt = (int)utp.GetCurrentRtt(NetworkManager.ServerClientId);
                    Set(pingValue, rtt + " ms");
                    if (pingValue != null) pingValue.color = rtt <= goodPingMs ? new Color(0.48f, 0.92f, 0.22f) : rtt <= okPingMs ? Color.white : new Color(1f, 0.45f, 0.35f);
                }
                else Set(pingValue, "0 ms");
            }
        }

        private static void Set(TMP_Text t, string v) { if (t != null && t.text != v) t.text = v; }
    }
}
