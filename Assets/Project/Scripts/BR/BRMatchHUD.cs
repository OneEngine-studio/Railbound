using System.Collections;
using System.Collections.Generic;
using JUTPS;
using JUTPS.CameraSystems;
using Railbound.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Railbound.BR
{
    /// <summary>
    /// Everything the player sees of the match itself, built at runtime in the same black-and-white style as the rest of the HUD:
    /// alive and kill counters, the storm timer, the killfeed, the warmup countdown, the elimination screen with placement,
    /// the spectator view and the victory / results screen. It only reads <see cref="BRMatchCoordinator"/> state and events,
    /// so it works the same offline, as a host and as a client.
    /// </summary>
    public class BRMatchHUD : MonoBehaviour
    {
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Faint = new Color(1f, 1f, 1f, 0.28f);
        private const float FeedLife = 7f;
        private const int FeedMax = 5;
        private const float DeathScreenSeconds = 4.5f;

        // ---- ui
        private Canvas canvas;
        private CanvasGroup rootGroup, hudGroup, centerGroup, spectateGroup, endGroup;
        private TextMeshProUGUI aliveNum, killsNum, centerSmall, centerBig, spectateName, spectateHint, spectatePlace;
        private TextMeshProUGUI endTitle, endWinner, endFooter;
        private TextMeshProUGUI endPlaceVal, endKillsVal, endSurvivedVal, endMatchVal, endRewards;
        private Button endPlayAgain, endReturn;
        private float localSurvived;          // match seconds at which the local player was eliminated
        private RectTransform feedRoot;
        private TMP_FontAsset fontBold, fontSemi;
        private Sprite px;

        private sealed class FeedRow { public RectTransform Rect; public CanvasGroup Group; public float Born; }
        private readonly List<FeedRow> feed = new List<FeedRow>();

        // ---- local match state
        private int localKills;
        private int localPlacement;           // 0 = still alive
        private bool eliminatedLocally, deathSequenceRunning, matchEnded, localWon, endShown;
        private string winnerName = "";
        private float endShownAt;
        private BRParticipant spectateTarget;
        private float nextTargetPick;

        private void Awake()
        {
            fontBold = FindFont("Rajdhani-Bold") ?? TMP_Settings.defaultFontAsset;
            fontSemi = FindFont("Rajdhani-SemiBold") ?? fontBold;
            px = MakePixel();
            Build();
        }

        private void OnEnable()
        {
            BRMatchCoordinator.KillOccurred += OnKill;
            BRMatchCoordinator.LocalEliminated += OnLocalEliminated;
            BRMatchCoordinator.MatchEnded += OnMatchEnded;
        }

        private void OnDisable()
        {
            BRMatchCoordinator.KillOccurred -= OnKill;
            BRMatchCoordinator.LocalEliminated -= OnLocalEliminated;
            BRMatchCoordinator.MatchEnded -= OnMatchEnded;
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }

        // ================================================================= events
        private void OnKill(BRKillInfo k)
        {
            if (k.KillerIsLocal) localKills++;
            AddFeedRow(k);
        }

        private void OnLocalEliminated(BRKillInfo k)
        {
            if (eliminatedLocally) return;
            eliminatedLocally = true;
            localPlacement = k.Placement;
            localSurvived = BRMatchCoordinator.Instance != null ? BRMatchCoordinator.Instance.MatchSeconds : 0f;
            StartCoroutine(EliminationSequence(k));
        }

        private void OnMatchEnded(string winner, bool localIsWinner)
        {
            matchEnded = true;
            winnerName = winner;
            localWon = localIsWinner;
            if (localIsWinner) { localPlacement = 1; StartCoroutine(VictorySlowMotion()); }
        }

        private IEnumerator EliminationSequence(BRKillInfo k)
        {
            deathSequenceRunning = true;
            int total = BRMatchCoordinator.Instance != null ? Mathf.Max(BRMatchCoordinator.Instance.TotalCount, k.Placement) : k.Placement;
            float start = Time.unscaledTime;
            DeathScreenUI.Show(DeathScreenSeconds, () => DeathScreenSeconds - (Time.unscaledTime - start), "", "SPECTATING IN", "#" + k.Placement + " OF " + total);
            while (Time.unscaledTime - start < DeathScreenSeconds) yield return null;
            DeathScreenUI.Hide();
            deathSequenceRunning = false;
            if (BRMatchCoordinator.Instance != null) PickTarget(BRMatchCoordinator.Instance, 0);
        }

        private IEnumerator VictorySlowMotion()
        {
            Time.timeScale = 0.35f;
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < 3f) yield return null;
            Time.timeScale = 1f;
        }

        // ================================================================= per frame
        private void Update()
        {
            var c = BRMatchCoordinator.Instance;
            if (c == null) return;

            // the elimination screen owns the whole view while it is up
            hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, DeathScreenUI.IsShowing ? 0f : 1f, Time.unscaledDeltaTime * 4f);

            UpdateCounters(c);
            UpdateCenterMessage(c);
            UpdateFeed();
            UpdateSpectator(c);
            UpdateEnd(c);
        }

        private void UpdateCounters(BRMatchCoordinator c)
        {
            aliveNum.text = c.State == BRMatchState.WaitingForPlayers ? "--" : Mathf.Max(0, c.AliveCount).ToString();
            killsNum.text = localKills.ToString();
        }

        private void UpdateCenterMessage(BRMatchCoordinator c)
        {
            // the game server is gone: say so in the middle of the screen, also to a player who was already eliminated
            if (c.ServerConnectionLost)
            {
                centerSmall.text = c.ServerConnectionLostAfterMatch ? "THE MATCH SERVER HAS CLOSED - RETURNING TO THE LOBBY" : "CONNECTION TO THE MATCH WAS LOST - RETURNING TO THE LOBBY";
                centerBig.text = "";
                centerGroup.alpha = Mathf.MoveTowards(centerGroup.alpha, 1f, Time.unscaledDeltaTime * 3f);
                return;
            }

            string small = "", big = "";
            switch (c.State)
            {
                case BRMatchState.WaitingForPlayers: small = "WAITING FOR PLAYERS"; break;
                case BRMatchState.Warmup:
                    small = "MATCH STARTS IN";
                    big = Mathf.Max(1, Mathf.CeilToInt(c.WarmupRemaining)).ToString();
                    break;
                case BRMatchState.ActiveBattle:
                    if (c.MatchSeconds < 3f) { small = "DEPLOYED"; big = ""; }
                    break;
                case BRMatchState.FinalShowdown:
                    if (c.MatchSeconds >= 0f && c.AliveCount <= c.FinalShowdownAlive && c.AliveCount > 1 && Time.unscaledTime % 12f < 4f) small = "FINAL SHOWDOWN";
                    break;
            }
            centerSmall.text = small;
            centerBig.text = big;
            float want = small.Length > 0 && !eliminatedLocally ? 1f : 0f;
            centerGroup.alpha = Mathf.MoveTowards(centerGroup.alpha, want, Time.unscaledDeltaTime * 3f);
        }

        // ================================================================= killfeed
        private void AddFeedRow(BRKillInfo k)
        {
            var row = new GameObject("Kill", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            row.SetParent(feedRoot, false);
            row.anchorMin = row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(1f, 1f);
            row.sizeDelta = new Vector2(430f, 34f);

            bool mine = k.KillerIsLocal || k.VictimIsLocal;
            var bg = NewImage("Bg", row, px, new Color(0f, 0f, 0f, mine ? 0.82f : 0.6f)); Stretch(bg.rectTransform);
            var edge = NewImage("Edge", row, px, mine ? Color.white : Faint); var er = edge.rectTransform;
            er.anchorMin = new Vector2(1f, 0f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(1f, 0.5f); er.sizeDelta = new Vector2(3f, 0f);

            string killer = k.Killer.ToUpperInvariant(), victim = k.Victim.ToUpperInvariant();
            string kc = k.KillerIsLocal ? "#FFFFFF" : "#D8D8D8", vc = k.VictimIsLocal ? "#FFFFFF" : "#A8A8A8";
            var t = NewText("Text", row, "<color=" + kc + ">" + killer + "</color>   <color=#777777>></color>   <color=" + vc + ">" + victim + "</color>", 19, fontSemi, Color.white, 3f, TextAlignmentOptions.Right);
            t.richText = true;
            var tr = t.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(10f, 0f); tr.offsetMax = new Vector2(-14f, 0f);

            feed.Insert(0, new FeedRow { Rect = row, Group = row.GetComponent<CanvasGroup>(), Born = Time.unscaledTime });
            while (feed.Count > FeedMax) { Destroy(feed[feed.Count - 1].Rect.gameObject); feed.RemoveAt(feed.Count - 1); }
        }

        private void UpdateFeed()
        {
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                var r = feed[i];
                float age = Time.unscaledTime - r.Born;
                if (age > FeedLife) { Destroy(r.Rect.gameObject); feed.RemoveAt(i); }
            }
            for (int i = 0; i < feed.Count; i++)
            {
                var r = feed[i]; float age = Time.unscaledTime - r.Born;
                Vector2 target = new Vector2(0f, -i * 40f);
                r.Rect.anchoredPosition = Vector2.Lerp(r.Rect.anchoredPosition, target + new Vector2(age < 0.25f ? Mathf.Lerp(120f, 0f, age / 0.25f) : 0f, 0f), Time.unscaledDeltaTime * 14f);
                r.Group.alpha = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((FeedLife - age) / 0.8f);
            }
        }

        // ================================================================= spectator
        private void UpdateSpectator(BRMatchCoordinator c)
        {
            bool spectating = eliminatedLocally && !deathSequenceRunning;
            spectateGroup.alpha = Mathf.MoveTowards(spectateGroup.alpha, spectating ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            if (!spectating) return;

            if (spectateTarget == null || !spectateTarget.IsAlive)
            {
                if (Time.unscaledTime >= nextTargetPick) { nextTargetPick = Time.unscaledTime + 0.5f; PickTarget(c, 0); }
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) PickTarget(c, 1);
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) PickTarget(c, -1);
            }

            spectateName.text = spectateTarget != null ? spectateTarget.DisplayName.ToUpperInvariant() : (matchEnded ? "MATCH OVER" : "NOBODY LEFT TO WATCH");
            spectatePlace.text = "PLACED #" + localPlacement + " OF " + Mathf.Max(c.TotalCount, localPlacement) + "   |   KILLS " + localKills;
            spectateHint.text = matchEnded ? "ENTER   LEAVE MATCH" : "A / D   SWITCH PLAYER      ENTER   LEAVE MATCH";
            if (kb != null && kb.enterKey.wasPressedThisFrame && !matchEnded) c.LeaveMatch();
        }

        private void PickTarget(BRMatchCoordinator c, int step)
        {
            var list = new List<BRParticipant>();
            var local = c.LocalParticipant;
            foreach (var p in c.Participants) if (p != null && p != local && p.IsAlive) list.Add(p);
            if (list.Count == 0) { spectateTarget = null; return; }

            int idx = spectateTarget != null ? list.IndexOf(spectateTarget) : -1;
            idx = step == 0 ? (idx >= 0 ? idx : 0) : (idx + step + list.Count) % list.Count;
            spectateTarget = list[Mathf.Clamp(idx, 0, list.Count - 1)];
            FollowWithCamera(spectateTarget);
        }

        private static void FollowWithCamera(BRParticipant p)
        {
            if (p == null || p.Controller == null) return;
            var cam = FindFirstObjectByType<TPSCameraController>();
            if (cam == null) return;
            cam.enabled = true;
            // Clear characterTarget so bot IsAiming or dead-player state never freezes FixedUpdate/LateUpdate pivot tracking
            cam.characterTarget = null;
            Transform follow = p.Controller.HumanoidSpine != null ? p.Controller.HumanoidSpine : p.Controller.transform;
            cam.TargetToFollow = follow;
            cam.SetCameraStateTransition(cam.GetCurrentCameraState, cam.NormalCameraState);
            cam.SetPivotCameraPosition(cam.NormalCameraState.GetCameraPivotPosition(follow), false);
        }

        // ================================================================= results
        private void UpdateEnd(BRMatchCoordinator c)
        {
            bool show = matchEnded && !deathSequenceRunning;
            if (show && !endShown)
            {
                endShown = true; endShownAt = Time.unscaledTime;
                bool won = localWon;
                endTitle.text = won ? "VICTORY ROYALE" : "MATCH OVER";
                endWinner.text = won ? "LAST ONE STANDING" : "WINNER   <color=#FFFFFF>" + winnerName.ToUpperInvariant() + "</color>";
                int place = won ? 1 : (localPlacement > 0 ? localPlacement : 0);
                int total = Mathf.Max(c.TotalCount, place);
                float matchLength = c.MatchSeconds;

                endPlaceVal.text = place > 0 ? "#" + place + "<size=55%><color=#8C8C8C>  /  " + total + "</color></size>" : "--";
                endKillsVal.text = localKills.ToString();
                // a player who was eliminated survived until then; the winner (or someone who never died) the whole match
                endSurvivedVal.text = Mmss(eliminatedLocally && !won ? localSurvived : matchLength);
                endMatchVal.text = Mmss(matchLength);

                // XP and coins for this match (the lobby plays the level-up popup if it moved the player up a level)
                PlayerProgression.MatchReward(place, total, localKills, eliminatedLocally && !won ? localSurvived : matchLength, won, out int rewardXp, out int rewardCoins);
                endRewards.text = "<color=#FFFFFF>+ " + rewardXp.ToString("N0") + "</color> XP        <color=#FFFFFF>+ " + rewardCoins.ToString("N0") + "</color> COINS";
                AwardMatchAsync(rewardXp, rewardCoins);

                // the results have buttons: free the mouse and make sure the UI can receive clicks
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                EnsureEventSystem();
            }
            endGroup.alpha = Mathf.MoveTowards(endGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 2.5f);
            bool clickable = show && endGroup.alpha > 0.5f;
            endGroup.blocksRaycasts = clickable;
            endGroup.interactable = clickable;
            if (!show) return;

            float left = Mathf.Max(0f, c.ReturnToLobbyDelay - (Time.unscaledTime - endShownAt));
            endFooter.text = "RETURNING TO LOBBY IN  <color=#FFFFFF>" + Mathf.CeilToInt(left) + "</color>      |      SPACE   PLAY AGAIN      |      ENTER   LEAVE NOW";
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) { PlayAgain(); return; }
            if (left <= 0f || (kb != null && kb.enterKey.wasPressedThisFrame)) LeaveToLobby();
        }

        private async void AwardMatchAsync(int xp, int coins)
        {
            try
            {
                var award = await PlayerProgression.AwardAsync(xp, coins);
                if (this != null && endRewards != null && award.LeveledUp)
                    endRewards.text += "        <color=#FFFFFF>LEVEL UP  " + award.oldLevel + "  >  " + award.newLevel + "</color>";
            }
            catch (System.Exception ex) { Debug.LogWarning("[BRMatchHUD] Could not record the match reward: " + ex.Message); }
        }

        /// <summary>Back to the lobby, which starts the next deployment by itself.</summary>
        private void PlayAgain()
        {
            Railbound.UI.LobbyAutoDeploy.Request();
            LeaveToLobby();
        }

        private void LeaveToLobby()
        {
            var c = BRMatchCoordinator.Instance;
            if (c == null) return;
            endShown = true; matchEnded = false;
            c.LeaveMatch();
        }

        // the HUD is built at runtime, so a map without an EventSystem still needs one for the results buttons
        private static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            new GameObject("[HUD] EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        // ================================================================= build
        private void Build()
        {
            var cgo = new GameObject("[HUD] Battle Royale", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            rootGroup = cgo.GetComponent<CanvasGroup>(); rootGroup.blocksRaycasts = false; rootGroup.interactable = false;
            var root = (RectTransform)cgo.transform;

            // ---- top centre, under the compass: alive / kills (the zone card on the left already shows the storm timer)
            var hud = NewRect("Hud", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -92), new Vector2(360, 90));
            hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            var plate = NewImage("Plate", hud, px, new Color(0f, 0f, 0f, 0.5f)); Stretch(plate.rectTransform);   // keeps the numbers readable over a bright sky
            aliveNum = StatBlock(hud, "ALIVE", new Vector2(-92, 0), out _);
            killsNum = StatBlock(hud, "KILLS", new Vector2(92, 0), out _);
            var divider = NewImage("Divider", hud, px, Faint); var dr = divider.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 1f); dr.pivot = new Vector2(0.5f, 1f); dr.anchoredPosition = new Vector2(0, -8); dr.sizeDelta = new Vector2(2, 64);


            // ---- top right: killfeed
            feedRoot = NewRect("Killfeed", hud, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(430, 220));
            feedRoot.SetParent(root, false);
            feedRoot.anchorMin = feedRoot.anchorMax = new Vector2(1f, 1f); feedRoot.pivot = new Vector2(1f, 1f); feedRoot.anchoredPosition = new Vector2(-40, -150);

            // ---- centre: waiting / countdown
            var center = NewRect("Center", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(900, 240));
            centerGroup = center.gameObject.AddComponent<CanvasGroup>(); centerGroup.alpha = 0f;
            var cplate = NewImage("Plate", center, px, new Color(0f, 0f, 0f, 0.42f)); var cpr = cplate.rectTransform;
            cpr.anchorMin = cpr.anchorMax = new Vector2(0.5f, 0.5f); cpr.pivot = new Vector2(0.5f, 0.5f); cpr.anchoredPosition = new Vector2(0, 20); cpr.sizeDelta = new Vector2(640, 230);
            centerSmall = NewText("Small", center, "", 30, fontSemi, Dim, 14f, TextAlignmentOptions.Center);
            var cs = centerSmall.rectTransform; cs.anchorMin = cs.anchorMax = new Vector2(0.5f, 0.5f); cs.pivot = new Vector2(0.5f, 0.5f); cs.anchoredPosition = new Vector2(0, 70); cs.sizeDelta = new Vector2(900, 50);
            centerBig = NewText("Big", center, "", 150, fontBold, Color.white, 6f, TextAlignmentOptions.Center);
            var cb = centerBig.rectTransform; cb.anchorMin = cb.anchorMax = new Vector2(0.5f, 0.5f); cb.pivot = new Vector2(0.5f, 0.5f); cb.anchoredPosition = new Vector2(0, -30); cb.sizeDelta = new Vector2(500, 180);

            // ---- bottom: spectator bar
            var spec = NewRect("Spectate", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(1100, 150));
            spectateGroup = spec.gameObject.AddComponent<CanvasGroup>(); spectateGroup.alpha = 0f;
            var sbg = NewImage("Shade", spec, MakeGradient(), new Color(0, 0, 0, 0.75f)); Stretch(sbg.rectTransform);
            var sl = NewText("Label", spec, "SPECTATING", 18, fontSemi, Dim, 12f, TextAlignmentOptions.Center);
            var slr = sl.rectTransform; slr.anchorMin = slr.anchorMax = new Vector2(0.5f, 0.5f); slr.pivot = new Vector2(0.5f, 0.5f); slr.anchoredPosition = new Vector2(0, 40); slr.sizeDelta = new Vector2(800, 28);
            spectateName = NewText("Name", spec, "", 46, fontBold, Color.white, 10f, TextAlignmentOptions.Center);
            var snr = spectateName.rectTransform; snr.anchorMin = snr.anchorMax = new Vector2(0.5f, 0.5f); snr.pivot = new Vector2(0.5f, 0.5f); snr.anchoredPosition = new Vector2(0, 2); snr.sizeDelta = new Vector2(1000, 60);
            spectatePlace = NewText("Place", spec, "", 18, fontSemi, Dim, 6f, TextAlignmentOptions.Center);
            var spr = spectatePlace.rectTransform; spr.anchorMin = spr.anchorMax = new Vector2(0.5f, 0.5f); spr.pivot = new Vector2(0.5f, 0.5f); spr.anchoredPosition = new Vector2(0, -34); spr.sizeDelta = new Vector2(900, 26);
            spectateHint = NewText("Hint", spec, "", 16, fontSemi, Faint + new Color(0, 0, 0, 0.3f), 6f, TextAlignmentOptions.Center);
            var shr = spectateHint.rectTransform; shr.anchorMin = shr.anchorMax = new Vector2(0.5f, 0.5f); shr.pivot = new Vector2(0.5f, 0.5f); shr.anchoredPosition = new Vector2(0, -60); shr.sizeDelta = new Vector2(900, 24);

            // ---- results
            var end = NewRect("End", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            endGroup = end.gameObject.AddComponent<CanvasGroup>(); endGroup.alpha = 0f;
            var endCanvas = end.gameObject.AddComponent<Canvas>(); endCanvas.overrideSorting = true; endCanvas.sortingOrder = 205;   // above the target indicators
            var dim = NewImage("Dim", end, px, new Color(0, 0, 0, 0.55f)); Stretch(dim.rectTransform);
            endTitle = NewText("Title", end, "", 110, fontBold, Color.white, 26f, TextAlignmentOptions.Center);
            var et = endTitle.rectTransform; et.anchorMin = et.anchorMax = new Vector2(0.5f, 0.5f); et.pivot = new Vector2(0.5f, 0.5f); et.anchoredPosition = new Vector2(0, 90); et.sizeDelta = new Vector2(1500, 140);
            var rule = NewImage("Rule", end, px, new Color(1, 1, 1, 0.4f)); var rr = rule.rectTransform;
            rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f); rr.pivot = new Vector2(0.5f, 0.5f); rr.anchoredPosition = new Vector2(0, 14); rr.sizeDelta = new Vector2(520, 2);
            endWinner = NewText("Winner", end, "", 30, fontSemi, Dim, 10f, TextAlignmentOptions.Center); endWinner.richText = true;
            var ew = endWinner.rectTransform; ew.anchorMin = ew.anchorMax = new Vector2(0.5f, 0.5f); ew.pivot = new Vector2(0.5f, 0.5f); ew.anchoredPosition = new Vector2(0, -26); ew.sizeDelta = new Vector2(1200, 44);

            // the stat tiles: placement, kills, how long the player survived, how long the match lasted
            endPlaceVal = ResultTile(end, "PLACEMENT", -345f);
            endPlaceVal.richText = true;
            endKillsVal = ResultTile(end, "KILLS", -115f);
            endSurvivedVal = ResultTile(end, "SURVIVED", 115f);
            endMatchVal = ResultTile(end, "MATCH TIME", 345f);

            // what the match earned
            endRewards = NewText("Rewards", end, "", 24, fontSemi, Dim, 6f, TextAlignmentOptions.Center); endRewards.richText = true;
            var erw = endRewards.rectTransform; erw.anchorMin = erw.anchorMax = new Vector2(0.5f, 0.5f); erw.pivot = new Vector2(0.5f, 0.5f); erw.anchoredPosition = new Vector2(0, -196); erw.sizeDelta = new Vector2(1500, 36);

            // buttons (mouse); Space and Enter do the same
            endPlayAgain = ResultButton(end, "PLAY AGAIN", new Vector2(-170, -250), true, PlayAgain);
            endReturn = ResultButton(end, "RETURN TO LOBBY", new Vector2(170, -250), false, LeaveToLobby);

            endFooter = NewText("Footer", end, "", 18, fontSemi, Faint + new Color(0, 0, 0, 0.35f), 6f, TextAlignmentOptions.Center); endFooter.richText = true;
            var ef = endFooter.rectTransform; ef.anchorMin = ef.anchorMax = new Vector2(0.5f, 0.5f); ef.pivot = new Vector2(0.5f, 0.5f); ef.anchoredPosition = new Vector2(0, -318); ef.sizeDelta = new Vector2(1500, 30);

            // the root group blocks all clicks (it is a pure display); the results are the one part that takes input
            end.gameObject.AddComponent<GraphicRaycaster>();
            endGroup.ignoreParentGroups = true;
            endGroup.blocksRaycasts = false;
            endGroup.interactable = false;
        }

        private TextMeshProUGUI ResultTile(RectTransform parent, string label, float x)
        {
            var tile = NewRect(label + " Tile", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -118), new Vector2(210, 100));
            var bg = NewImage("Bg", tile, px, new Color(0f, 0f, 0f, 0.5f)); Stretch(bg.rectTransform);
            var edge = NewImage("Edge", tile, px, new Color(1f, 1f, 1f, 0.35f)); var er = edge.rectTransform;
            er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f); er.anchoredPosition = Vector2.zero; er.sizeDelta = new Vector2(0f, 2f);
            var value = NewText("Value", tile, "", 48, fontBold, Color.white, 2f, TextAlignmentOptions.Center);
            var vr = value.rectTransform; vr.anchorMin = vr.anchorMax = new Vector2(0.5f, 0.5f); vr.pivot = new Vector2(0.5f, 0.5f); vr.anchoredPosition = new Vector2(0, 12); vr.sizeDelta = new Vector2(200, 60);
            var lab = NewText("Label", tile, label, 14, fontSemi, Dim, 10f, TextAlignmentOptions.Center);
            var lr = lab.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f); lr.pivot = new Vector2(0.5f, 0.5f); lr.anchoredPosition = new Vector2(0, -32); lr.sizeDelta = new Vector2(200, 22);
            return value;
        }

        private Button ResultButton(RectTransform parent, string label, Vector2 pos, bool primary, UnityEngine.Events.UnityAction onClick)
        {
            var r = NewRect(label + " Button", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(320, 58));
            var bg = NewImage("Bg", r, px, primary ? Color.white : new Color(1f, 1f, 1f, 0.14f)); Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            var btn = r.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary ? new Color(0.82f, 0.82f, 0.82f, 1f) : new Color(1.6f, 1.6f, 1.6f, 1f);
            colors.pressedColor = primary ? new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(2.2f, 2.2f, 2.2f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            var txt = NewText("Label", r, label, 22, fontBold, primary ? Color.black : Color.white, 8f, TextAlignmentOptions.Center);
            Stretch(txt.rectTransform);
            return btn;
        }

        private TextMeshProUGUI StatBlock(RectTransform parent, string label, Vector2 pos, out RectTransform rect)
        {
            var num = NewText(label + "Num", parent, "0", 58, fontBold, Color.white, 2f, TextAlignmentOptions.Center);
            rect = num.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f); rect.pivot = new Vector2(0.5f, 1f); rect.anchoredPosition = new Vector2(pos.x, -0); rect.sizeDelta = new Vector2(160, 66);
            var lab = NewText(label + "Label", parent, label, 14, fontSemi, Dim, 10f, TextAlignmentOptions.Center);
            var lr = lab.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 1f); lr.pivot = new Vector2(0.5f, 1f); lr.anchoredPosition = new Vector2(pos.x, -58); lr.sizeDelta = new Vector2(160, 20);
            return num;
        }

        // ================================================================= helpers
        private static string Mmss(float s)
        {
            int m = Mathf.FloorToInt(s / 60f); int sec = Mathf.FloorToInt(s % 60f);
            return m.ToString("00") + ":" + sec.ToString("00");
        }

        private static TMP_FontAsset FindFont(string namePart)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (f != null && f.name.Contains(namePart)) return f;
            return null;
        }

        private static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[16]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }

        // vertical fade: transparent at the top, solid at the bottom
        private static Sprite MakeGradient()
        {
            var tex = new Texture2D(2, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[2 * 64];
            for (int y = 0; y < 64; y++) { byte a = (byte)Mathf.RoundToInt(Mathf.SmoothStep(0f, 1f, 1f - y / 63f) * 255f); cols[y * 2] = cols[y * 2 + 1] = new Color32(255, 255, 255, a); }
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 64), new Vector2(0.5f, 0.5f), 100f);
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = amin; r.anchorMax = amax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        private static Image NewImage(string name, Transform parent, Sprite s, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = false;
            return i;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, TMP_FontAsset font, Color c, float spacing, TextAlignmentOptions al)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>(); t.font = font; t.text = text; t.fontSize = size; t.color = c; t.characterSpacing = spacing; t.alignment = al;
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }
    }
}
