using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Test aid for builds: "-lobbyshot DIR" walks through the WEAPONS (loadout slots, an equip, an upgrade, the armory) and BATTLE PASS
    /// screens of the lobby, saves a screenshot of each step into DIR and quits. "-socialshot DIR" does the same for the social layer:
    /// leaderboards (every board and view), the inbox (claim, filter) and the chat (send, mention, rate limit, report).
    /// Does nothing without the arguments.
    /// </summary>
    public sealed class LobbyShotAid : MonoBehaviour
    {
        private string dir;
        private bool social;
        private string partyRole = "";   // -partyflow lead|join

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string d = Arg("-lobbyshot");
            string sd = Arg("-socialshot");
            string pf = Arg("-partyflow");
            if (!string.IsNullOrEmpty(pf) && !Application.isBatchMode)
            {
                var pgo = new GameObject("[Test] Party Flow");
                DontDestroyOnLoad(pgo);
                var paid = pgo.AddComponent<LobbyShotAid>();
                paid.partyRole = pf; paid.dir = Arg("-partyshots");
                return;
            }
            if ((string.IsNullOrEmpty(d) && string.IsNullOrEmpty(sd)) || Application.isBatchMode) return;
            var go = new GameObject("[Test] Lobby Shots");
            DontDestroyOnLoad(go);
            var aid = go.AddComponent<LobbyShotAid>();
            aid.social = !string.IsNullOrEmpty(sd);
            aid.dir = aid.social ? sd : d;
        }

        private static string Arg(string name)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return "";
        }

        private IEnumerator Start()
        {
            if (!string.IsNullOrEmpty(partyRole)) { yield return PartyFlow(); yield break; }
            Directory.CreateDirectory(dir);
            float t = 0f;
            while (t < 90f && RailboundYellowHUDController.Instance == null) { t += Time.unscaledDeltaTime; yield return null; }
            if (RailboundYellowHUDController.Instance == null) { Debug.LogError("[LobbyShot] The lobby never loaded."); Application.Quit(2); yield break; }
            var load = PlayerProgression.LoadAsync();
            while (!load.IsCompleted) yield return null;
            if (social) { yield return SocialFlow(); yield break; }
            Debug.Log("[LobbyShot] profile: level " + PlayerProgression.Current.level + ", coins " + PlayerProgression.Current.coins + ", tables " + (PlayerProgression.Tables != null) + ", tier " + PlayerProgression.SeasonTier);
            yield return new WaitForSecondsRealtime(5f);   // the lobby finishes loading characters and its own data
            var hud = RailboundYellowHUDController.Instance;

            hud.SelectNavTab(2);
            yield return new WaitForSecondsRealtime(2.5f);
            var weapons = FindFirstObjectByType<LobbyWeaponsScreen>(FindObjectsInactive.Include);
            yield return Shot("01_loadout_sidearm");
            if (weapons != null) Debug.Log("[LobbyShot] " + weapons.TestDescribe());
            if (weapons != null)
            {
                string[] names = { "02_loadout_primary", "", "03_loadout_melee", "04_loadout_lethal", "05_upgrade_sonar", "06_upgrade_medkit" };
                foreach (int s in new[] { 0, 2, 3, 4, 5 })
                {
                    weapons.TestSelectSlot(s);
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return Shot(names[s]);
                }
                weapons.TestSelectSlot(4);
                yield return new WaitForSecondsRealtime(0.6f);
                weapons.TestUpgrade("sonar");
                yield return new WaitForSecondsRealtime(1.6f);
                yield return Shot("07_after_sonar_upgrade");
                weapons.TestSelectSlot(2);
                yield return new WaitForSecondsRealtime(0.6f);
                weapons.TestEquip("melee", "katana");
                yield return new WaitForSecondsRealtime(1.6f);
                yield return Shot("08_after_equip_katana");
                weapons.TestSelectSlot(1);
                yield return new WaitForSecondsRealtime(0.6f);
                weapons.TestEquip("sidearm", "p226dual");
                yield return new WaitForSecondsRealtime(1.6f);
                yield return Shot("09_after_equip_dual");
                weapons.TestSetView(1);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("10_armory");
            }

            hud.SelectNavTab(3);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("11_battlepass");
            var pass = FindFirstObjectByType<LobbyBattlePassScreen>(FindObjectsInactive.Include);
            if (pass != null)
            {
                pass.TestClaimAll();
                yield return new WaitForSecondsRealtime(3f);
                yield return Shot("12_battlepass_claimed");
            }
            Debug.Log("[LobbyShot] done: loadout " + JsonUtility.ToJson(PlayerProgression.Current.loadout) + " sonar " + PlayerProgression.SkillLevel("sonar") + " coins " + PlayerProgression.Current.coins);
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(0);
        }

        // -partyflow lead: opens SOCIAL, invites the online friend, waits for them and deploys the party
        // -partyflow join: waits for the invite, accepts it from the toast and follows the leader's deploy
        private IEnumerator PartyFlow()
        {
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            float t = 0f;
            while (t < 90f && RailboundYellowHUDController.Instance == null) { t += Time.unscaledDeltaTime; yield return null; }
            var hud = RailboundYellowHUDController.Instance;
            if (hud == null) { Debug.LogError("[PartyFlow] The lobby never loaded."); yield break; }
            var svc = Railbound.Social.SocialService.Instance;
            var party = Railbound.Networking.NakamaPartyService.Instance;
            t = 0f;
            while (t < 30f && !svc.Online) { t += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(5f);
            Debug.Log("[PartyFlow] " + partyRole + " ready as " + svc.MyUsername);

            if (partyRole == "lead")
            {
                hud.PartyPanel.Show();
                t = 0f; while (t < 20f && !party.InParty) { t += 0.25f; yield return new WaitForSecondsRealtime(0.25f); }
                yield return new WaitForSecondsRealtime(2.5f);
                yield return PartyShot("40_social_alone");
                t = 0f;
                while (t < 90f && !System.Linq.Enumerable.Any(FriendsService.All, f => f.relation == FriendsService.Relation.Friend && f.online)) { t += 1f; _ = FriendsService.RefreshAsync(); yield return new WaitForSecondsRealtime(1f); }
                hud.PartyPanel.TestInviteFirstOnline();
                yield return new WaitForSecondsRealtime(1f);
                yield return PartyShot("41_social_invited");
                t = 0f; while (t < 90f && party.MemberCount < 2) { t += 0.5f; yield return new WaitForSecondsRealtime(0.5f); }
                Debug.Log("[PartyFlow] party has " + party.MemberCount + " member(s), leader=" + party.IsLeader);
                yield return new WaitForSecondsRealtime(3f);
                yield return PartyShot("42_social_squad");
                hud.PartyPanel.Hide();
                yield return new WaitForSecondsRealtime(2.5f);
                yield return PartyShot("43_lobby_squad");
                Debug.Log("[PartyFlow] leader deploys");
                hud.TestDeploy();
            }
            else
            {
                Railbound.Social.InboxItem invite = null;
                t = 0f;
                while (t < 150f && invite == null) { t += 0.25f; yield return new WaitForSecondsRealtime(0.25f); invite = hud.SocialHub.TestFirstInvite(); }
                if (invite == null) { Debug.LogError("[PartyFlow] No party invite arrived."); yield break; }
                yield return new WaitForSecondsRealtime(1f);
                yield return PartyShot("44_invite_toast");
                Debug.Log("[PartyFlow] accepting the invite from " + invite.Str("fromName"));
                hud.SocialHub.AcceptInvite(invite);
                t = 0f; while (t < 30f && party.MemberCount < 2) { t += 0.25f; yield return new WaitForSecondsRealtime(0.25f); }
                Debug.Log("[PartyFlow] joined: party has " + party.MemberCount + " member(s), leader=" + party.IsLeader);
                yield return new WaitForSecondsRealtime(3f);
                yield return PartyShot("45_social_joined");
                hud.PartyPanel.Hide();
            }
        }

        private IEnumerator PartyShot(string name)
        {
            if (string.IsNullOrEmpty(dir)) yield break;
            yield return Shot(name);
        }

        private IEnumerator SocialFlow()
        {
            var hud = RailboundYellowHUDController.Instance;
            var svc = Railbound.Social.SocialService.Instance;
            float t = 0f;
            while (t < 40f && (hud.SocialHub == null || !svc.InboxLoaded || !svc.ChatConnected)) { t += Time.unscaledDeltaTime; yield return null; }
            Debug.Log("[SocialShot] ready: inbox " + svc.Inbox.Count + " (unread " + svc.UnreadCount + "), chat " + svc.ChatConnected + ", global lines " + svc.Lines("global").Count);
            yield return new WaitForSecondsRealtime(4f);
            var hub = hud.SocialHub;
            yield return Shot("20_lobby_social");

            hud.OpenLeaderboards();
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("21_lb_weekly_kills_global");
            var lb = hud.LeaderboardScreen;
            string[] names = { "22_lb_alltime_global", "23_lb_weekly_kills_friends", "24_lb_season_xp_around_me", "25_lb_weekly_wins_global" };
            int[][] sets = { new[] { 3, 0 }, new[] { 0, 2 }, new[] { 2, 1 }, new[] { 1, 0 } };
            for (int i = 0; i < sets.Length; i++)
            {
                lb.TestSet(sets[i][0], sets[i][1]);
                yield return new WaitForSecondsRealtime(2f);
                yield return Shot(names[i]);
            }
            hud.SelectNavTab(0);
            yield return new WaitForSecondsRealtime(1.5f);

            hub.SetInbox(true);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("26_inbox");
            var gift = hub.TestFirstGift();
            int coinsBefore = PlayerProgression.Current.coins;
            hub.TestClaim(gift);
            yield return new WaitForSecondsRealtime(2.5f);
            Debug.Log("[SocialShot] gift " + (gift != null ? gift.GiftId : "none") + " claimed=" + (gift != null && gift.claimed) + " coins " + coinsBefore + " -> " + PlayerProgression.Current.coins);
            yield return Shot("27_inbox_gift_claimed");
            hub.TestFilter(1);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("28_inbox_rewards");
            hub.TestFilter(0);
            hub.SetInbox(false);
            yield return new WaitForSecondsRealtime(1.2f);

            hub.TestHoverChat(true);
            hub.TestSend("hello from the test client, gl hf");
            yield return new WaitForSecondsRealtime(1.5f);
            hub.TestHoverChat(true);
            yield return Shot("29_chat_sent");
            float w = 0f; bool mention = false;
            while (w < 10f && !mention) { w += 0.25f; yield return new WaitForSecondsRealtime(0.25f); foreach (var l in svc.Lines("global")) if (l.mentionsMe) mention = true; }
            yield return new WaitForSecondsRealtime(0.6f);
            Debug.Log("[SocialShot] mention received: " + mention);
            yield return Shot("30_chat_mention");

            for (int i = 0; i < 7; i++) { hub.TestSend("spam test " + i); yield return new WaitForSecondsRealtime(0.35f); }
            hub.TestHoverChat(true);
            yield return new WaitForSecondsRealtime(0.4f);
            Debug.Log("[SocialShot] after the burst: error='" + hub.TestError + "'");
            yield return Shot("31_chat_rate_limited");

            var other = hub.TestFirstOtherLine();
            hub.TestReport(other);
            yield return new WaitForSecondsRealtime(2f);
            hub.TestHoverChat(true);
            Debug.Log("[SocialShot] reported " + (other != null ? other.username + ": " + other.text : "nothing") + " -> " + (other != null && other.reported));
            yield return Shot("32_chat_reported");

            Debug.Log("[SocialShot] done: inbox " + svc.Inbox.Count + ", unclaimed gifts " + svc.UnclaimedGifts + ", global lines " + svc.Lines("global").Count + ", coins " + PlayerProgression.Current.coins);
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(0);
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null; yield return null;
            Debug.Log("[LobbyShot] " + path);
        }
    }
}
