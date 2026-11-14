using System;
using System.Collections;
using System.Linq;
using Michsky.UI.Reach;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Opens the settings menu that was copied from MAP-2's pause menu (see the editor tool "Import MAP-2 Settings Menu into Lobby"):
    /// shows the canvas on its Settings page, and closes it again with Go Back or Esc. The lobby's gear button calls <see cref="Open"/>.
    /// The Sign Out button of the Account tab calls <see cref="SignOutRequested"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyReachSettings : MonoBehaviour
    {
        /// <summary>Raised by the SIGN OUT button of the Account tab (the lobby goes back to the login screen).</summary>
        public Action SignOutRequested;
        /// <summary>Raised after the menu has closed.</summary>
        public Action Closed;

        public bool IsOpen { get; private set; }
        /// <summary>True while open and on the frame it closed, so the lobby does not also act on the same Esc press.</summary>
        public bool BusyThisFrame => IsOpen || closedFrame == Time.frameCount;

        private CanvasGroup group, hudGroup;
        private PanelManager pagePanels;
        private bool wired;
        private int closedFrame = -1;
        private Coroutine routine;

        /// <summary>The copy in the open scene (it is inactive until opened).</summary>
        public static LobbyReachSettings Find()
        {
            foreach (var s in Resources.FindObjectsOfTypeAll<LobbyReachSettings>())
                if (s != null && s.gameObject.scene.IsValid()) return s;
            return null;
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            gameObject.SetActive(true);
            if (!wired) Wire();
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(OpenRoutine());
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            closedFrame = Time.frameCount;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(CloseRoutine());
        }

        private void Wire()
        {
            wired = true;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();

            // the lobby HUD fades out underneath, so its text does not show through the menu (the menu's own backdrop is not opaque)
            var hud = FindFirstObjectByType<RailboundYellowHUDController>();
            if (hud != null)
            {
                hudGroup = hud.GetComponent<CanvasGroup>();
                if (hudGroup == null) hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            }

            // the manager that switches between the pause page and the settings page
            pagePanels = GetComponentsInChildren<PanelManager>(true).FirstOrDefault(p => p.panels != null && p.panels.Any(i => i.panelName == "Settings"));

            // Go Back returns to the lobby (the copy's own Go Back listeners were removed by the import)
            foreach (var b in GetComponentsInChildren<ButtonManager>(true))
                if (b.name == "Go Back") b.onClick.AddListener(Close);

            var account = GetComponentInChildren<AccountSettingsPanel>(true);
            if (account != null) account.onSignOutRequested.AddListener(() => { Close(); if (SignOutRequested != null) SignOutRequested(); });
        }

        private IEnumerator OpenRoutine()
        {
            group.alpha = 0f;
            group.blocksRaycasts = true; group.interactable = true;
            yield return null;
            yield return null;
            // go straight to the Settings page (the copy starts on the pause page)
            if (pagePanels != null) pagePanels.OpenPanel("Settings");
            RefreshAccount();
            yield return new WaitForSecondsRealtime(0.12f);
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime) { group.alpha = t / 0.2f; SetHud(1f - group.alpha); yield return null; }
            group.alpha = 1f; SetHud(0f);
            routine = null;
        }

        private IEnumerator CloseRoutine()
        {
            group.blocksRaycasts = false; group.interactable = false;
            for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / 0.18f; SetHud(1f - group.alpha); yield return null; }
            group.alpha = 0f; SetHud(1f);
            gameObject.SetActive(false);
            routine = null;
            if (Closed != null) Closed();
        }

        private void SetHud(float alpha)
        {
            if (hudGroup == null) return;
            hudGroup.alpha = alpha;
            bool live = alpha > 0.95f;
            hudGroup.blocksRaycasts = live; hudGroup.interactable = live;
        }

        private void OnDestroy() { SetHud(1f); }

        private void RefreshAccount()
        {
            var account = GetComponentInChildren<AccountSettingsPanel>(true);
            if (account == null) return;
            var auth = AuthenticationService.Instance;
            bool signedIn = auth != null && auth.IsAuthenticated;
            string name = signedIn && auth.Session != null && !string.IsNullOrEmpty(auth.Session.Username) ? auth.Session.Username : "Guest";
            account.SetPlayerName(name);
            account.SetAccountStatus(signedIn ? "Signed in" : "Not signed in", signedIn);
            account.SetServerText(ServerConfig.NakamaHost);
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }
    }
}
