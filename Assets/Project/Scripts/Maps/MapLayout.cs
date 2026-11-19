using UnityEngine;

namespace Railbound.Maps
{
    /// <summary>
    /// One map's design. The terrain comes first (<see cref="Height"/>, <see cref="Paint"/>, trees and grass), then
    /// <see cref="Build"/> places everything on it. Every random choice goes through <see cref="B"/>, so the map is the same on
    /// every peer.
    /// </summary>
    public abstract class MapLayout
    {
        protected MapBuilder B;
        protected MapDefinition Def => B.Definition;

        internal void Bind(MapBuilder builder) { B = builder; }

        /// <summary>Flat areas pressed into the terrain (later pads win where they overlap).</summary>
        protected readonly System.Collections.Generic.List<FlatPad> Pads = new System.Collections.Generic.List<FlatPad>();

        protected void Pad(float x, float z, float halfX, float halfZ, float height, float falloff, float yaw = 0f)
            => Pads.Add(new FlatPad { X = x, Z = z, HalfX = halfX, HalfZ = halfZ, Height = height, Falloff = falloff, Yaw = yaw });

        protected void RoundPad(float x, float z, float radius, float height, float falloff)
            => Pads.Add(new FlatPad { X = x, Z = z, HalfX = radius, HalfZ = radius, Height = height, Falloff = falloff, Round = true });

        /// <summary>Blends a raw height toward the pads.</summary>
        protected float ApplyPads(float x, float z, float h)
        {
            for (int i = 0; i < Pads.Count; i++)
            {
                float w = Pads[i].Weight(x, z);
                if (w > 0f) h = Mathf.Lerp(h, Pads[i].Height, w);
            }
            return h;
        }

        /// <summary>How much a point is inside any pad (0 to 1): layouts paint pads as built ground.</summary>
        protected float PadWeight(float x, float z)
        {
            float m = 0f;
            for (int i = 0; i < Pads.Count; i++) m = Mathf.Max(m, Pads[i].Weight(x, z));
            return m;
        }

        /// <summary>The highest the terrain can be (metres): the height range of the terrain data.</summary>
        public virtual float MaxHeight => 120f;

        /// <summary>Called once before the terrain is sampled (place the flat pads and paths the heights and paint read).</summary>
        public virtual void Plan() { }

        /// <summary>The ground height in metres at a world point.</summary>
        public abstract float Height(float x, float z);

        /// <summary>The ground mix at a point: add weights to <paramref name="w"/> (indexed by <see cref="Surface"/>); they are normalised.</summary>
        public virtual void Paint(float x, float z, float height, float slope, float[] w) { w[(int)Surface.Grass] = 1f; }

        /// <summary>Trees per 100 m² (0 to about 3) at a point.</summary>
        public virtual float TreeDensity(float x, float z, float height, float slope) => 0f;

        /// <summary>Bushes per 100 m² at a point.</summary>
        public virtual float BushDensity(float x, float z, float height, float slope) => 0f;

        /// <summary>Grass cover from 0 to 1 at a point.</summary>
        public virtual float GrassDensity(float x, float z, float height, float slope) => 0f;

        /// <summary>Places the buildings, cover, props and gameplay markers.</summary>
        public abstract void Build();
    }
}
