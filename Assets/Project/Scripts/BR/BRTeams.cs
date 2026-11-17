using System.Collections.Generic;
using JUTPS;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.BR
{
    /// <summary>
    /// Squads in the battle royale. A party that queued together is one team (Nakama gives every player a team number with the join
    /// ticket; the game server puts it on the player's <see cref="JUNetworkPlayer"/>). Teammates cannot hurt each other, spawn next to each
    /// other, see each other through walls (<see cref="TeammateMarkers"/>), and the match ends when one team is left. A player without
    /// a team (bots, offline play, a server started without Nakama) is a team of one: everything behaves as solo.
    /// </summary>
    public static class BRTeams
    {
        /// <summary>The team of a character (-1 = none / a team of one).</summary>
        public static int TeamOf(GameObject go)
        {
            if (go == null) return -1;
            var np = go.GetComponentInParent<JUNetworkPlayer>();
            return np != null ? np.TeamId : -1;
        }

        public static bool AreTeammates(GameObject a, GameObject b)
        {
            if (a == null || b == null) return false;
            var ra = a.GetComponentInParent<JUNetworkPlayer>();
            var rb = b.GetComponentInParent<JUNetworkPlayer>();
            if (ra == null || rb == null || ra == rb) return false;
            return ra.TeamId >= 0 && ra.TeamId == rb.TeamId;
        }

        /// <summary>The team of the player on this machine (-1 = none).</summary>
        public static int LocalTeam
        {
            get
            {
                var np = LocalPlayer;
                return np != null ? np.TeamId : -1;
            }
        }

        private static JUNetworkPlayer localPlayer;
        public static JUNetworkPlayer LocalPlayer
        {
            get
            {
                if (localPlayer != null && localPlayer.IsSpawned) return localPlayer;
                localPlayer = null;
                foreach (var np in Object.FindObjectsByType<JUNetworkPlayer>(FindObjectsSortMode.None))
                    if (np != null && np.IsSpawned && np.IsOwner) { localPlayer = np; break; }
                return localPlayer;
            }
        }

        private static float nextBlockLog;

        // no friendly fire: damage from a teammate is dropped where it is applied (the shooter's machine and the server)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallDamageFilter()
        {
            JUHealth.DamageFilter = (victim, attacker) =>
            {
                if (victim == null || !AreTeammates(victim.gameObject, attacker)) return true;
                if (Time.unscaledTime >= nextBlockLog) { nextBlockLog = Time.unscaledTime + 1f; Debug.Log("[BRTeams] Friendly fire blocked: " + attacker.name + " -> " + victim.name + (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer ? " (server)" : " (client)")); }
                return false;
            };
        }

        /// <summary>Where a squad member stands at the start: the team's spawn point, members side by side on the ground.</summary>
        public static Vector3 SquadOffset(Vector3 spawn, Quaternion rotation, int slot)
        {
            if (slot <= 0) return spawn;
            Vector2[] offsets = { Vector2.zero, new Vector2(2.4f, 0f), new Vector2(-2.4f, 0f), new Vector2(0f, -2.6f) };
            var o = offsets[Mathf.Clamp(slot, 0, offsets.Length - 1)];
            Vector3 p = spawn + rotation * new Vector3(o.x, 0f, o.y);
            // stand on the ground there (the spawn is on terrain; a slope or a rock next to it is not)
            if (Physics.Raycast(p + Vector3.up * 8f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point + Vector3.up * 0.05f;
            return p;
        }
    }

    /// <summary>
    /// Squad markers on the local player's screen: a diamond over every teammate (through walls), the name, the distance and a health
    /// bar; a teammate off screen is pinned to the screen edge. Nothing shows when the player has no teammates.
    /// </summary>
    public sealed class TeammateMarkers : MonoBehaviour
    {
        private static readonly Color Squad = new Color(0.35f, 0.85f, 1f, 1f);
        private static readonly Color Down = new Color(1f, 0.35f, 0.32f, 1f);
        private sealed class Marker { public RectTransform root; public Image diamond, fill, back; public TextMeshProUGUI name, dist; public CanvasGroup group; }

        private readonly Dictionary<JUNetworkPlayer, Marker> markers = new Dictionary<JUNetworkPlayer, Marker>();
        private RectTransform canvasRect;
        private Sprite px;
        private TMP_FontAsset font;
        private float nextScan;
        private readonly List<JUNetworkPlayer> mates = new List<JUNetworkPlayer>();

        public static void Ensure()
        {
            if (Application.isBatchMode || FindFirstObjectByType<TeammateMarkers>() != null) return;
            var go = new GameObject("[BR] Teammate Markers", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920f, 1080f); sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<TeammateMarkers>();
        }

        private void Awake()
        {
            canvasRect = (RectTransform)transform;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var cols = new Color32[16]; for (int i = 0; i < 16; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            px = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            font = TMP_Settings.defaultFontAsset;
            var anyText = FindFirstObjectByType<TextMeshProUGUI>();
            if (anyText != null && anyText.font != null) font = anyText.font;
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            var me = BRTeams.LocalPlayer;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.5f;
                mates.Clear();
                int team = me != null ? me.TeamId : -1;
                if (team >= 0)
                    foreach (var np in FindObjectsByType<JUNetworkPlayer>(FindObjectsSortMode.None))
                        if (np != null && np != me && np.IsSpawned && np.TeamId == team) mates.Add(np);
                // drop markers of players who left
                var gone = new List<JUNetworkPlayer>();
                foreach (var kv in markers) if (kv.Key == null || !mates.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (var g in gone) { if (markers[g].root != null) Destroy(markers[g].root.gameObject); markers.Remove(g); }
            }
            if (cam == null) return;

            Vector2 size = canvasRect.rect.size;
            foreach (var mate in mates)
            {
                if (mate == null) continue;
                if (!markers.TryGetValue(mate, out var m)) { m = Build(mate); markers[mate] = m; }
                var health = mate.GetComponent<JUHealth>();
                bool dead = health != null && (health.IsDead || health.Health <= 0f);
                Vector3 head = HeadOf(mate) + Vector3.up * 0.45f;
                Vector3 sp = cam.WorldToScreenPoint(head);
                bool behind = sp.z < 0f;
                Vector2 vp = new Vector2(sp.x / Screen.width, sp.y / Screen.height);
                if (behind) vp = new Vector2(1f - vp.x, 0.02f);   // behind the camera: show it at the bottom edge on the right side
                bool off = behind || vp.x < 0.02f || vp.x > 0.98f || vp.y < 0.04f || vp.y > 0.96f;
                vp.x = Mathf.Clamp(vp.x, 0.03f, 0.97f); vp.y = Mathf.Clamp(vp.y, 0.05f, 0.95f);
                m.root.anchoredPosition = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
                float meters = me != null ? Vector3.Distance(me.transform.position, mate.transform.position) : 0f;
                m.dist.text = Mathf.RoundToInt(meters) + "M";
                m.name.text = NameOf(mate);
                float hp = health != null && health.MaxHealth > 0f ? Mathf.Clamp01(health.Health / health.MaxHealth) : 1f;
                m.fill.rectTransform.anchorMax = new Vector2(hp, 1f);
                var c = dead ? Down : Squad;
                m.diamond.color = c; m.fill.color = c; m.name.color = dead ? Down : Color.white;
                // names fade with distance and on the screen edge only the diamond stays
                m.name.gameObject.SetActive(!off);
                m.back.gameObject.SetActive(!off && !dead);
                m.group.alpha = Mathf.Lerp(1f, 0.75f, Mathf.InverseLerp(30f, 250f, meters));
                m.root.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, Mathf.InverseLerp(20f, 200f, meters));
                if (dead) m.dist.text = "DOWN";
            }
        }

        private static Vector3 HeadOf(JUNetworkPlayer p)
        {
            var anim = p.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                var head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (head != null) return head.position;
            }
            return p.transform.position + Vector3.up * 1.8f;
        }

        private static string NameOf(JUNetworkPlayer p)
        {
            var part = p.GetComponent<BRParticipant>();
            return part != null && !string.IsNullOrEmpty(part.DisplayName) ? part.DisplayName : "SQUADMATE";
        }

        private Marker Build(JUNetworkPlayer p)
        {
            var m = new Marker();
            var go = new GameObject("Mate " + p.OwnerClientId, typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            m.root = (RectTransform)go.transform;
            m.root.sizeDelta = new Vector2(160f, 60f);
            m.group = go.AddComponent<CanvasGroup>(); m.group.blocksRaycasts = false;
            m.diamond = Img(m.root, Squad, new Vector2(0f, 0f), new Vector2(14f, 14f));
            m.diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var outline = Img(m.root, new Color(0f, 0f, 0f, 0.6f), Vector2.zero, new Vector2(18f, 18f));
            outline.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            outline.transform.SetAsFirstSibling();
            m.name = Txt(m.root, "", 15, new Vector2(0f, 26f), new Vector2(220f, 20f));
            m.name.fontStyle = FontStyles.Bold;
            m.dist = Txt(m.root, "", 11, new Vector2(0f, -16f), new Vector2(80f, 16f));
            m.dist.color = new Color(1f, 1f, 1f, 0.75f);
            m.back = Img(m.root, new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, 14f), new Vector2(64f, 4f));
            m.fill = Img(m.back.rectTransform, Squad, Vector2.zero, Vector2.zero);
            var fr = m.fill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero; fr.pivot = new Vector2(0f, 0.5f);
            return m;
        }

        private Image Img(RectTransform parent, Color c, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("I", typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = px; i.color = c; i.raycastTarget = false;
            var r = i.rectTransform; r.anchoredPosition = pos; r.sizeDelta = size;
            return i;
        }

        private TextMeshProUGUI Txt(RectTransform parent, string text, float size, Vector2 pos, Vector2 box)
        {
            var go = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; t.enableWordWrapping = false;
            t.characterSpacing = 2f; t.outlineWidth = 0.18f; t.outlineColor = new Color32(0, 0, 0, 200);
            var r = t.rectTransform; r.anchoredPosition = pos; r.sizeDelta = box;
            return t;
        }
    }
}
