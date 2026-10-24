using System;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Explicit high-level GameLobby Controller facade for Railbound.
    /// Bridges the scene's LobbyUIController with the centralized UIManager,
    /// providing direct control over sub-screen transitions, modals, and tactical audio feedback.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LobbyUIController))]
    public class GameLobbyController : MonoBehaviour
    {
        public static GameLobbyController Instance { get; private set; }

        private LobbyUIController _lobbyUI;

        public LobbyUIController LobbyUI => _lobbyUI;

        private void Awake()
        {
            Instance = this;
            _lobbyUI = GetComponent<LobbyUIController>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void OpenOperators() => _lobbyUI?.OpenOperators();
        public void OpenArsenal() => _lobbyUI?.OpenArsenal();
        public void OpenLocker() => _lobbyUI?.OpenLocker();
        public void OpenRankings() => _lobbyUI?.OpenRankings();
        public void OpenCareer() => _lobbyUI?.OpenCareer();
        public void ReturnToLobby() => _lobbyUI?.ReturnToLobby();
        public void OpenMatchmaking() => _lobbyUI?.OpenMatchmaking();
        public void ToggleGlobalChat() => _lobbyUI?.ToggleGlobalChat();
        public void CloseGlobalChat() => _lobbyUI?.CloseGlobalChat();
        public void ShowSettings() => UIManager.Instance?.ShowSettings();
        public void SelectTab(int index) => _lobbyUI?.SetActiveTab(index);
        public void HandleEscape() => _lobbyUI?.HandleEscapeKey();
    }
}
