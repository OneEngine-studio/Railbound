using System.Collections;
using System.IO;
using JUTPS;
using JUTPS.CameraSystems;
using UnityEngine;
using UnityEngine.AI;

namespace Railbound.Maps
{
    /// <summary>
    /// Test aid, only with <c>-maptest DIR</c> on a client: once the map is built and the player is in, it logs the map's checks
    /// (build time, objects, NavMesh, every spawn and objective marker on walkable ground, the player standing on the ground), then
    /// flies the camera through the map's viewpoints and saves a screenshot of each to DIR.
    /// </summary>
    public class MapTestAid : MonoBehaviour
    {
        private string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] != "-maptest" || FindFirstObjectByType<MapTestAid>() != null) continue;
                var go = new GameObject("[Test] Map");
                DontDestroyOnLoad(go);
                go.AddComponent<MapTestAid>().dir = a[i + 1];
                return;
            }
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            float waited = 0f;
            while ((MapLoader.Active == null || JUGameManager.PlayerController == null) && waited < 240f) { waited += 0.5f; yield return new WaitForSecondsRealtime(0.5f); }
            var def = MapLoader.Active;
            if (def == null) { Debug.LogError("[MapTest] no map was built"); yield break; }
            yield return new WaitForSecondsRealtime(10f);   // the player lands, bots and loot are placed

            var b = MapLoader.LastBuild;
            var tri = NavMesh.CalculateTriangulation();
            Debug.Log($"[MapTest] map {def.Name} ({def.Id}) mode {def.ModeId}: build {MapLoader.LastBuildSeconds:0.0} s, " +
                      (b != null ? $"{b.Boxes} boxes, {b.Props} props, {b.Anchors.Count} markers, " : "") +
                      $"navmesh {tri.indices.Length / 3} triangles");

            if (b != null)
            {
                int ok = 0, bad = 0;
                foreach (var a in b.Anchors)
                {
                    if (a == null || a.Kind == MapAnchor.AnchorKind.Viewpoint || a.Kind == MapAnchor.AnchorKind.LootArea) continue;
                    bool walk = NavMesh.SamplePosition(a.transform.position, out NavMeshHit hit, 2.5f, NavMesh.AllAreas);
                    if (walk) ok++; else { bad++; Debug.LogWarning($"[MapTest] marker not on walkable ground: {a.name} at {a.transform.position}"); }
                }
                Debug.Log($"[MapTest] markers on walkable ground: {ok} ok, {bad} not");
            }

            var pc = JUGameManager.PlayerController;
            if (pc != null)
            {
                Vector3 p = pc.transform.position;
                bool hitGround = Physics.Raycast(p + Vector3.up * 1f, Vector3.down, out RaycastHit gh, 6f, ~0, QueryTriggerInteraction.Ignore);
                Debug.Log($"[MapTest] player at {p} grounded={pc.IsGrounded} ground below={(hitGround ? gh.collider.name + " " + (p.y - gh.point.y).ToString("0.00") + " m" : "none")} " +
                          $"inside play area={Mathf.Abs(p.x) <= def.PlayArea.x * 0.5f + 2f && Mathf.Abs(p.z) <= def.PlayArea.y * 0.5f + 2f}");
            }
            int bots = 0, botsGrounded = 0;
            foreach (var c in FindObjectsByType<JUCharacterController>(FindObjectsSortMode.None))
            {
                if (c == pc || !c.isActiveAndEnabled) continue;
                bots++;
                if (Physics.Raycast(c.transform.position + Vector3.up, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore)) botsGrounded++;
            }
            Debug.Log($"[MapTest] other characters: {bots}, standing on something: {botsGrounded}");

            // the viewpoints
            var cam = Camera.main;
            var controllers = FindObjectsByType<JUCameraController>(FindObjectsSortMode.None);
            foreach (var c in controllers) c.enabled = false;
            int shot = 0;
            // the camera hangs on the player's camera pivot, which keeps turning: take it off for the tour
            Transform camParent = cam != null ? cam.transform.parent : null;
            Vector3 camLocalPos = cam != null ? cam.transform.localPosition : Vector3.zero;
            Quaternion camLocalRot = cam != null ? cam.transform.localRotation : Quaternion.identity;
            if (cam != null) cam.transform.SetParent(null, true);
            if (cam != null && b != null)
            {
                foreach (var a in b.Anchors)
                {
                    if (a == null || a.Kind != MapAnchor.AnchorKind.Viewpoint) continue;
                    cam.transform.SetPositionAndRotation(a.transform.position, a.transform.rotation);
                    yield return new WaitForSecondsRealtime(5f);   // auto exposure adapts (outdoor to indoor takes seconds) and streaming settles
                    string file = Path.Combine(dir, $"{def.Id}_{shot:00}_{a.Label.Replace(' ', '_')}.png");
                    ScreenCapture.CaptureScreenshot(file);
                    shot++;
                    yield return new WaitForSecondsRealtime(0.6f);
                }
            }
            if (cam != null && camParent != null)
            {
                cam.transform.SetParent(camParent, false);
                cam.transform.localPosition = camLocalPos;
                cam.transform.localRotation = camLocalRot;
            }
            foreach (var c in controllers) if (c != null) c.enabled = true;
            Debug.Log($"[MapTest] {shot} screenshots");
            Debug.Log("[MapTest] DONE");
        }
    }
}
