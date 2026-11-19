using System.Collections.Generic;
using System.Text;
using JUTPS;
using JUTPS.WeaponSystem;
using Project.AI.RL;
using UnityEngine;

namespace Project.AI
{
    /// <summary>
    /// Test aid, only with <c>-bottest SECONDS</c> where the bots run (the server, or offline): once the match is live it measures
    /// every bot for SECONDS and logs how they did, the same way for the tactical brain and the previous policy (<c>-oldbots</c>):
    ///   stuck: share of the time a bot wanted to move but did not; against obstacle: share of the time it pushed into something
    ///   solid; shots, hits and the hit rate; kills; and (tactical brain) the states it spent its time in.
    /// </summary>
    public class BotTestAid : MonoBehaviour
    {
        private float seconds;
        private bool measuring, done;
        private float started;
        private float wantMoveTime, stuckTime, pressTime;
        private int shots, hits, deaths;
        private readonly Dictionary<string, float> stateTime = new Dictionary<string, float>();
        private readonly HashSet<JUHealth> deadSeen = new HashSet<JUHealth>();
        private readonly Dictionary<JUCharacterRLAgent, Vector3> lastPos = new Dictionary<JUCharacterRLAgent, Vector3>();
        private float nextReport;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] != "-bottest" || FindFirstObjectByType<BotTestAid>() != null) continue;
                var go = new GameObject("[Test] Bots");
                DontDestroyOnLoad(go);
                go.AddComponent<BotTestAid>().seconds = float.TryParse(a[i + 1], out float s) ? s : 150f;
                return;
            }
        }

        private void OnEnable()
        {
            Weapon.OnAnyShot += OnShot;
            JUHealth.OnAnyDamaged += OnDamaged;
        }

        private void OnDisable()
        {
            Weapon.OnAnyShot -= OnShot;
            JUHealth.OnAnyDamaged -= OnDamaged;
        }

        private void OnShot(Weapon w)
        {
            if (measuring && w != null && w.GetComponentInParent<JUCharacterRLAgent>() != null) shots++;
        }

        private void OnDamaged(JUHealth h, float dmg, Vector3 at)
        {
            if (!measuring || dmg <= 0f) return;
            var attacker = JUHealth.PendingAttacker;
            if (attacker != null && attacker.GetComponent<JUCharacterRLAgent>() != null && attacker != h.gameObject) hits++;
        }

        private void Update()
        {
            if (done) return;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer) { enabled = false; return; }   // bots run on the server only
            var coord = Railbound.BR.BRMatchCoordinator.Instance;
            if (!measuring)
            {
                if (coord == null || !coord.IsMatchLive) return;
                measuring = true;
                started = Time.time;
                nextReport = Time.time + 30f;
                Debug.Log($"[BotTest] measuring {seconds:0} s, brain: {(AnyTactical() ? "tactical" : "previous policy")}");
            }

            float dt = Time.deltaTime;
            foreach (var bot in FindObjectsByType<JUCharacterRLAgent>(FindObjectsSortMode.None))
            {
                if (bot == null || bot.SelfHealth == null) continue;
                if (bot.SelfHealth.IsDead)
                {
                    if (deadSeen.Add(bot.SelfHealth)) deaths++;
                    continue;
                }
                Vector3 p = bot.transform.position;
                lastPos.TryGetValue(bot, out Vector3 prev);
                lastPos[bot] = p;
                if (prev == Vector3.zero) continue;
                Vector3 want = bot.IntendedMove;
                want.y = 0f;
                if (want.magnitude < 0.5f) continue;
                wantMoveTime += dt;
                Vector3 step = p - prev; step.y = 0f;
                float speed = step.magnitude / Mathf.Max(dt, 1e-4f);
                if (speed < 0.35f) stuckTime += dt;
                // pressing into something solid: a wall, rock or crate right ahead, and hardly moving
                if (speed < 1.0f && Physics.SphereCast(p + Vector3.up * 0.9f, 0.3f, want.normalized, out RaycastHit hit, 0.55f, ~0, QueryTriggerInteraction.Ignore)
                    && !(hit.collider is TerrainCollider) && !hit.transform.IsChildOf(bot.transform) && hit.collider.GetComponentInParent<JUCharacterController>() == null)
                    pressTime += dt;
                if (bot.TacticalBrainActive)
                {
                    string s = bot.State.ToString();
                    stateTime.TryGetValue(s, out float t);
                    stateTime[s] = t + dt;
                }
            }
            foreach (var h in FindObjectsByType<JUHealth>(FindObjectsSortMode.None))
                if (h != null && h.IsDead && h.GetComponent<JUCharacterRLAgent>() == null && deadSeen.Add(h)) deaths++;

            if (Time.time >= nextReport) { nextReport = Time.time + 30f; Report(false); }
            if (Time.time - started >= seconds) { Report(true); done = true; }
        }

        private static bool AnyTactical()
        {
            foreach (var b in FindObjectsByType<JUCharacterRLAgent>(FindObjectsSortMode.None)) if (b.TacticalBrainActive) return true;
            return false;
        }

        private void Report(bool final)
        {
            float elapsed = Time.time - started;
            var sb = new StringBuilder();
            sb.Append(final ? "[BotTest] RESULT " : "[BotTest] at ").Append(elapsed.ToString("0")).Append(" s: ");
            sb.Append("stuck ").Append(Pct(stuckTime, wantMoveTime)).Append(" of moving time, against obstacle ").Append(Pct(pressTime, wantMoveTime));
            sb.Append(", shots ").Append(shots).Append(", hits ").Append(hits).Append(" (").Append(Pct(hits, Mathf.Max(1, shots))).Append(")");
            sb.Append(", deaths ").Append(deaths);
            if (stateTime.Count > 0)
            {
                float total = 0f; foreach (var v in stateTime.Values) total += v;
                sb.Append(" | states:");
                foreach (var kv in stateTime) sb.Append(' ').Append(kv.Key).Append('=').Append(Pct(kv.Value, total));
                sb.Append(" | cover taken ").Append(JUCharacterRLAgent.TacticalCoverTaken).Append(", flanks ").Append(JUCharacterRLAgent.TacticalFlanks)
                  .Append(", engagements ").Append(JUCharacterRLAgent.TacticalEngagements).Append(", unstuck ").Append(JUCharacterRLAgent.TacticalStuckEvents);
            }
            Debug.Log(sb.ToString());
        }

        private static string Pct(float a, float b) => b <= 0f ? "0%" : (100f * a / b).ToString("0.0") + "%";
    }
}
