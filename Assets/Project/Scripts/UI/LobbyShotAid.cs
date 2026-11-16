using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Test aid for builds: "-lobbyshot DIR" walks through the WEAPONS (loadout slots, an equip, an upgrade, the armory) and BATTLE PASS
    /// screens of the lobby, saves a screenshot of each step into DIR and quits. Does nothing without the argument.
    /// </summary>
    public sealed class LobbyShotAid : MonoBehaviour
    {
        private string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string d = Arg("-lobbyshot");
            if (string.IsNullOrEmpty(d) || Application.isBatchMode) return;
            var go = new GameObject("[Test] Lobby Shots");
            DontDestroyOnLoad(go);
            go.AddComponent<LobbyShotAid>().dir = d;
        }

        private static string Arg(string name)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return "";
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            float t = 0f;
            while (t < 90f && RailboundYellowHUDController.Instance == null) { t += Time.unscaledDeltaTime; yield return null; }
            if (RailboundYellowHUDController.Instance == null) { Debug.LogError("[LobbyShot] The lobby never loaded."); Application.Quit(2); yield break; }
            var load = PlayerProgression.LoadAsync();
            while (!load.IsCompleted) yield return null;
            Debug.Log("[LobbyShot] profile: level " + PlayerProgression.Current.level + ", coins " + PlayerProgression.Current.coins + ", tables " + (PlayerProgression.Tables != null) + ", tier " + PlayerProgression.SeasonTier);
            yield return new WaitForSecondsRealtime(5f);   // the lobby finishes loading characters and its own data
            var hud = RailboundYellowHUDController.Instance;

            hud.SelectNavTab(2);
            yield return new WaitForSecondsRealtime(2.5f);
            var weapons = FindFirstObjectByType<LobbyWeaponsScreen>(FindObjectsInactive.Include);
            yield return Shot("01_loadout_sidearm");
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
