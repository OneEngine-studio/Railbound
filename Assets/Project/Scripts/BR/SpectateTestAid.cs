using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using JUTPS;
using JUTPS.InventorySystem;
using UnityEngine;

namespace Railbound.BR
{
    /// <summary>
    /// Test aid, only with <c>-spectest DIR</c> on a client: logs what the nearest fighters hold (in play), then eliminates the local
    /// player and, in the spectator view, switches through three fighters, logging what each holds and whether it can be seen
    /// (weapon object active, renderers on, layer drawn by the camera, in the hand) and saving a screenshot of each.
    /// </summary>
    public class SpectateTestAid : MonoBehaviour
    {
        private string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] != "-spectest" || FindFirstObjectByType<SpectateTestAid>() != null) continue;
                var go = new GameObject("[Test] Spectate");
                DontDestroyOnLoad(go);
                go.AddComponent<SpectateTestAid>().dir = a[i + 1];
                return;
            }
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            float waited = 0f;
            while (waited < 240f && (BRMatchCoordinator.Instance == null || !BRMatchCoordinator.Instance.IsMatchLive || JUGameManager.PlayerController == null)) { waited += 0.5f; yield return new WaitForSecondsRealtime(0.5f); }
            yield return new WaitForSecondsRealtime(12f);

            // in play: what the closest fighters hold
            var me = JUGameManager.PlayerController;
            Debug.Log("[SpecTest] in play: " + Describe(Nearest(me, 0)) + " || " + Describe(Nearest(me, 1)) + " || " + Describe(Nearest(me, 2)));
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "play.png"));
            yield return new WaitForSecondsRealtime(1f);

            // eliminate the local player (the server applies it)
            var h = me != null ? me.GetComponent<JUHealth>() : null;
            if (h != null) h.DoDamage(9999f);
            Debug.Log("[SpecTest] local player eliminated");
            yield return new WaitForSecondsRealtime(14f);

            var hud = FindFirstObjectByType<BRMatchHUD>();
            var pick = typeof(BRMatchHUD).GetMethod("PickTargetButton", BindingFlags.NonPublic | BindingFlags.Instance);
            var targetField = typeof(BRMatchHUD).GetField("spectateTarget", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < 3; i++)
            {
                if (i > 0 && hud != null && pick != null) pick.Invoke(hud, new object[] { 1 });
                yield return new WaitForSecondsRealtime(3.5f);
                var p = hud != null && targetField != null ? targetField.GetValue(hud) as BRParticipant : null;
                Debug.Log($"[SpecTest] spectating {i}: " + (p != null ? p.DisplayName + " " + Describe(p.Controller) : "nobody"));
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"spectate_{i}.png"));
                yield return new WaitForSecondsRealtime(0.8f);
            }
            Debug.Log("[SpecTest] DONE");
        }

        private static JUCharacterController Nearest(JUCharacterController me, int rank)
        {
            var all = new System.Collections.Generic.List<JUCharacterController>(FindObjectsByType<JUCharacterController>(FindObjectsSortMode.None));
            all.RemoveAll(c => c == null || c == me || !c.gameObject.activeInHierarchy);
            Vector3 p = me != null ? me.transform.position : Vector3.zero;
            all.Sort((a, b) => Vector3.Distance(a.transform.position, p).CompareTo(Vector3.Distance(b.transform.position, p)));
            return rank < all.Count ? all[rank] : null;
        }

        /// <summary>What a fighter holds on this peer and whether the camera can see it.</summary>
        public static string Describe(JUCharacterController c)
        {
            if (c == null) return "none";
            var sb = new StringBuilder();
            var inv = c.GetComponent<JUInventory>();
            sb.Append(c.name).Append(" puppet=").Append(c.IsRemotePuppet).Append(" ctrl=").Append(c.enabled);
            sb.Append(" curR=").Append(inv != null ? inv.CurrentRightHandItemID : -9);
            var item = inv != null ? inv.HoldableItemInUseInRightHand : null;
            sb.Append(" inUse=").Append(item != null ? item.ItemName : "none").Append(" ctrlWeapon=").Append(c.WeaponInUseRightHand != null ? c.WeaponInUseRightHand.ItemName : "none");
            var net = c.GetComponent<BRNetBot>();
            if (net != null) sb.Append(" netHeld=").Append(net.HeldWeapon != null ? net.HeldWeapon.ItemName : "none");
            if (item != null)
            {
                int on = 0, all = 0;
                foreach (var r in item.GetComponentsInChildren<Renderer>(true)) { all++; if (r.enabled && r.gameObject.activeInHierarchy) on++; }
                var cam = Camera.main;
                bool layerDrawn = cam != null && (cam.cullingMask & (1 << item.gameObject.layer)) != 0;
                var hand = c.anim != null ? c.anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
                sb.Append(" active=").Append(item.gameObject.activeInHierarchy).Append(" renderers=").Append(on).Append('/').Append(all)
                  .Append(" layer=").Append(LayerMask.LayerToName(item.gameObject.layer)).Append(" drawn=").Append(layerDrawn)
                  .Append(" fromHand=").Append(hand != null ? Vector3.Distance(hand.position, item.transform.position).ToString("0.00") : "?");
            }
            // every weapon object that is switched on under this character
            sb.Append(" activeItems:");
            if (inv != null && inv.HoldableItensRightHand != null)
                foreach (var it in inv.HoldableItensRightHand) if (it != null && it.gameObject.activeInHierarchy) sb.Append(it.ItemName).Append('#').Append(it.ItemSwitchID).Append(',');
            return sb.ToString();
        }
    }
}
