using UnityEngine;

namespace Railbound.Maps
{
    /// <summary>
    /// A gameplay spot a map provides for its mode: where each team spawns, the free-for-all spawns, the capture zones, the bomb sites,
    /// the extraction pads, the loot areas and the camera viewpoints the map test walks through. The mode's rules read these; the
    /// map itself only places them.
    /// </summary>
    public class MapAnchor : MonoBehaviour
    {
        public enum AnchorKind { Spawn, TeamSpawn, CaptureZone, BombSite, Extraction, LootArea, Viewpoint }

        public AnchorKind Kind;
        [Tooltip("1 or 2 for a team's spawn, 0 for everyone.")]
        public int Team;
        public string Label;
        public float Radius;

        private void OnDrawGizmos()
        {
            Color c = Kind == AnchorKind.TeamSpawn ? (Team == 1 ? new Color(0.2f, 0.5f, 1f) : new Color(1f, 0.5f, 0.15f))
                : Kind == AnchorKind.Spawn ? Color.green
                : Kind == AnchorKind.Viewpoint ? Color.cyan
                : new Color(1f, 0.85f, 0.2f);
            Gizmos.color = c;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, Radius > 0f ? Radius : 0.6f);
            Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 1.5f);
        }
    }
}
