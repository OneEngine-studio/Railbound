using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Test aid, client only: <c>-uisurvey DIR</c> records which UI is on screen during a match. Every 3 s it notes each active root canvas
    /// (sorting order, the scripts on it) and every visible text (path and words), and every 15 s it saves a screenshot. At the end
    /// (<c>-uisurveysecs</c>, default 240) it writes the first sighting of each to DIR/ui_survey.txt, so old and new HUD pieces that show
    /// at the same time can be told apart.
    /// </summary>
    public class UiSurveyTestAid : MonoBehaviour
    {
        private string dir;
        private float seconds = 240f;
        private readonly Dictionary<string, float> canvases = new Dictionary<string, float>();
        private readonly Dictionary<string, float> texts = new Dictionary<string, float>();
        private float started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            string d = null; float secs = 240f;
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] == "-uisurvey") d = a[i + 1];
                if (a[i] == "-uisurveysecs") float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out secs);
            }
            if (d == null || Application.isBatchMode) return;
            var go = new GameObject("[Test] UI survey");
            DontDestroyOnLoad(go);
            var aid = go.AddComponent<UiSurveyTestAid>();
            aid.dir = d; aid.seconds = secs;
        }

        private IEnumerator Start()
        {
            System.IO.Directory.CreateDirectory(dir);
            started = Time.realtimeSinceStartup;
            float nextShot = 0f; int shot = 0;
            while (Time.realtimeSinceStartup - started < seconds)
            {
                float t = Time.realtimeSinceStartup - started;
                Sample(t);
                if (t >= nextShot) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"ui_{shot++:00}.png")); nextShot = t + 15f; }
                yield return new WaitForSecondsRealtime(3f);
            }
            Write();
            Debug.Log("[UiSurvey] DONE");
        }

        private void Sample(float t)
        {
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c == null || !c.isActiveAndEnabled || !c.isRootCanvas) continue;
                var cg = c.GetComponent<CanvasGroup>();
                if (cg != null && cg.alpha <= 0.01f) continue;
                var sb = new StringBuilder();
                sb.Append(Path(c.transform)).Append("  [sort ").Append(c.sortingOrder).Append(", ").Append(c.renderMode).Append("] scripts:");
                foreach (var mb in c.GetComponentsInParent<MonoBehaviour>(true))
                    if (mb != null && !(mb is Canvas) && !(mb is CanvasScaler) && !(mb is GraphicRaycaster) && !(mb is CanvasGroup)) sb.Append(' ').Append(mb.GetType().Name);
                string key = sb.ToString();
                if (!canvases.ContainsKey(key)) canvases[key] = t;
            }
            foreach (var g in FindObjectsByType<Graphic>(FindObjectsSortMode.None))
            {
                if (g == null || !g.isActiveAndEnabled) continue;
                string words = g is TMP_Text tmp ? tmp.text : (g is Text ut ? ut.text : null);
                if (string.IsNullOrWhiteSpace(words) || g.color.a <= 0.02f || !Visible(g.transform)) continue;
                words = System.Text.RegularExpressions.Regex.Replace(words, "<.*?>", "").Replace("\n", " / ");
                if (words.Length > 80) words = words.Substring(0, 80);
                if (System.Text.RegularExpressions.Regex.IsMatch(words, @"^[\d\s:./%+-]+$")) continue;   // bare numbers change every frame
                var canvas = g.canvas != null ? g.canvas.rootCanvas : null;
                string key = (canvas != null ? canvas.name : "?") + " :: " + words;
                if (!texts.ContainsKey(key)) texts[key] = t;
            }
        }

        private static bool Visible(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                var cg = p.GetComponent<CanvasGroup>();
                if (cg != null && cg.alpha <= 0.02f) return false;
            }
            return true;
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (var p = t; p != null; p = p.parent) parts.Insert(0, p.name);
            return (t.gameObject.scene.name ?? "?") + "/" + string.Join("/", parts);
        }

        private void Write()
        {
            var sb = new StringBuilder();
            sb.AppendLine("== canvases (first seen, s)");
            foreach (var kv in canvases) sb.AppendLine($"{kv.Value,6:0}  {kv.Key}");
            sb.AppendLine("== texts (first seen, s)");
            var list = new List<KeyValuePair<string, float>>(texts);
            list.Sort((a, b) => a.Value.CompareTo(b.Value));
            foreach (var kv in list) sb.AppendLine($"{kv.Value,6:0}  {kv.Key}");
            File.WriteAllText(System.IO.Path.Combine(dir, "ui_survey.txt"), sb.ToString());
        }
    }
}
