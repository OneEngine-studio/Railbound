using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.AI.Exploration
{
    /// <summary>
    /// Per-agent "visited cells" grid used to reward and steer map exploration.
    /// Cells are keyed sparsely so the map does not need to know the world bounds up front.
    /// </summary>
    public sealed class AIExplorationMap
    {
        private readonly HashSet<long> visited = new HashSet<long>();
        private readonly float cellSize;
        private readonly float mapRadius;
        private readonly Vector3 mapCenter;
        private readonly int estimatedTotalCells;

        public int VisitedCount => visited.Count;
        public float Coverage => estimatedTotalCells > 0 ? Mathf.Clamp01((float)visited.Count / estimatedTotalCells) : 0f;

        public AIExplorationMap(float cellSize, float mapRadius, Vector3 mapCenter)
        {
            this.cellSize = Mathf.Max(1f, cellSize);
            this.mapRadius = Mathf.Max(this.cellSize * 2f, mapRadius);
            this.mapCenter = mapCenter;
            estimatedTotalCells = Mathf.Max(1, Mathf.RoundToInt(Mathf.PI * this.mapRadius * this.mapRadius / (this.cellSize * this.cellSize)));
        }

        public void Clear()
        {
            visited.Clear();
        }

        private long Key(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x / cellSize);
            int z = Mathf.FloorToInt(p.z / cellSize);
            return ((long)x << 32) ^ (uint)z;
        }

        public bool IsVisited(Vector3 p)
        {
            return visited.Contains(Key(p));
        }

        /// <summary>Marks the cell under p as visited. Returns true when the cell was new.</summary>
        public bool MarkVisited(Vector3 p)
        {
            return visited.Add(Key(p));
        }

        /// <summary>
        /// Finds a destination in the least-explored direction. Casts 16 rays of cells around the origin,
        /// scores unexplored cells (and mild forward continuity), and falls back to a random far point
        /// inside the map when everything nearby is already visited.
        /// isWalkable lets the caller reject destinations that are not on the NavMesh.
        /// </summary>
        public bool TryFindFrontier(Vector3 origin, Vector3 forward, Func<Vector3, bool> isWalkable, out Vector3 target)
        {
            const int DirectionCount = 16;
            const int Steps = 6;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            float bestScore = 0f;
            Vector3 bestTarget = origin;
            bool found = false;
            float startAngle = UnityEngine.Random.value * 360f;

            for (int i = 0; i < DirectionCount; i++)
            {
                float angle = (startAngle + i * (360f / DirectionCount)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                float score = 0f;
                for (int s = 1; s <= Steps; s++)
                {
                    Vector3 p = origin + dir * (cellSize * s);
                    if (FlatDistance(p, mapCenter) > mapRadius) break;
                    if (!visited.Contains(Key(p))) score += 1f + (Steps - s) * 0.15f;
                }

                score += Mathf.Max(0f, Vector3.Dot(dir, forward)) * 0.4f;

                if (score <= bestScore) continue;

                Vector3 candidate = origin + dir * (cellSize * 4f);
                if (isWalkable != null && !isWalkable(candidate)) continue;

                bestScore = score;
                bestTarget = candidate;
                found = true;
            }

            if (found && bestScore >= 1f)
            {
                target = bestTarget;
                return true;
            }

            // Everything nearby is explored: pick a random distant point so exploration continues.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 circle = UnityEngine.Random.insideUnitCircle * (mapRadius * 0.85f);
                Vector3 candidate = new Vector3(mapCenter.x + circle.x, origin.y, mapCenter.z + circle.y);
                if (visited.Contains(Key(candidate))) continue;
                if (isWalkable != null && !isWalkable(candidate)) continue;
                target = candidate;
                return true;
            }

            target = origin;
            return false;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
