using System.Collections;
using System.IO;
using System.Text;
using JUTPS;
using UnityEngine;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Test aid, client only: <c>-jumptrace DIR</c> makes the local player jump three times standing and three times running forward
    /// once the match is live, and writes every physics step of each jump to DIR/jumps.csv (height above the take-off point,
    /// vertical and horizontal speed, the controller's grounded / jumping / step flags, the animator's Grounded parameter), so the
    /// rise, the fall and the landing can be checked number by number.
    /// </summary>
    public class JumpTraceTestAid : MonoBehaviour
    {
        private string dir;
        private StringBuilder csv;
        private JUCharacterController pc;
        private Rigidbody rb;
        private int jump = -1;
        private float startY, jumpStart;
        private bool running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] != "-jumptrace" || Application.isBatchMode) continue;
                var go = new GameObject("[Test] Jump trace");
                DontDestroyOnLoad(go);
                go.AddComponent<JumpTraceTestAid>().dir = a[i + 1];
                return;
            }
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            csv = new StringBuilder("jump,running,t,height,vy,vh,grounded,jumping,adjustHeight,stepUp,animGrounded,fixedDt\n");
            float waited = 0f;
            while (waited < 240f && (JUGameManager.PlayerController == null || Railbound.BR.BRMatchCoordinator.Instance == null || !Railbound.BR.BRMatchCoordinator.Instance.IsMatchLive))
            { waited += 0.5f; yield return new WaitForSecondsRealtime(0.5f); }
            yield return new WaitForSecondsRealtime(6f);
            pc = JUGameManager.PlayerController;
            rb = pc != null ? pc.GetComponent<Rigidbody>() : null;
            if (pc == null || rb == null) { Debug.LogError("[JumpTrace] no local player"); yield break; }
            pc.UseDefaultControllerInput = false;

            for (int i = 0; i < 6; i++)
            {
                running = i >= 3;
                // stand still (or run) on the ground for a moment first
                float settle = 0f;
                while (settle < 1.5f) { settle += Time.deltaTime; if (running) Run(); yield return null; }
                while (!pc.IsGrounded) yield return null;
                jump = i; startY = pc.transform.position.y; jumpStart = Time.time;
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"jump{i}_takeoff.png"));
                pc._Jump();
                float t = 0f;
                bool shotApex = false;
                while (t < 2.5f)
                {
                    t += Time.deltaTime;
                    if (running) Run();
                    if (!shotApex && rb.velocity.y < 0f && t > 0.1f) { shotApex = true; ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"jump{i}_apex.png")); }
                    yield return null;
                }
                jump = -1;
                Stop();
            }
            pc.UseDefaultControllerInput = true;
            File.WriteAllText(Path.Combine(dir, "jumps.csv"), csv.ToString());
            Debug.Log("[JumpTrace] DONE");
        }

        private void Run()
        {
            pc.IsRunning = true;
            pc.SetMoveInput(0f, 1f);
        }

        private void Stop()
        {
            if (pc == null) return;
            pc.IsRunning = false;
            pc.SetMoveInput(0f, 0f);
        }

        private static readonly System.Reflection.FieldInfo AdjustHeightField = typeof(JUTPS.CharacterBrain.JUCharacterBrain).GetField("AdjustHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo StepUpField = typeof(JUTPS.CharacterBrain.JUCharacterBrain).GetField("GoToStepPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private void FixedUpdate()
        {
            if (jump < 0 || pc == null || rb == null) return;
            var v = rb.velocity;
            var anim = pc.anim;
            bool animGrounded = anim != null && anim.GetBool("Grounded");
            csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2:0.000},{3:0.000},{4:0.00},{5:0.00},{6},{7},{8},{9},{10},{11:0.0000}\n",
                jump, running ? 1 : 0, Time.time - jumpStart, pc.transform.position.y - startY, v.y, new Vector2(v.x, v.z).magnitude,
                pc.IsGrounded ? 1 : 0, pc.IsJumping ? 1 : 0, (AdjustHeightField != null && (bool)AdjustHeightField.GetValue(pc)) ? 1 : 0, (StepUpField != null && (bool)StepUpField.GetValue(pc)) ? 1 : 0, animGrounded ? 1 : 0, Time.fixedDeltaTime);
        }
    }
}
