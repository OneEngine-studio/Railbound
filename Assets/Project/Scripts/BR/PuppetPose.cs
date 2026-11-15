using System;
using Unity.Netcode;
using UnityEngine;
using JUTPS;

namespace Railbound.BR
{
    /// <summary>
    /// What a peer needs, besides the animator parameters, to pose a character that another machine controls: whether it is in
    /// firing mode (hands on the weapon, spine and head turned to the target), whether it is reloading, and the point it looks at.
    /// A player publishes it from its own machine, a bot from the server.
    /// </summary>
    public struct PuppetPose : INetworkSerializable, IEquatable<PuppetPose>
    {
        public bool Firing;
        public bool Reloading;
        public Vector3 Look;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Firing);
            serializer.SerializeValue(ref Reloading);
            serializer.SerializeValue(ref Look);
        }

        public bool Equals(PuppetPose o) { return Firing == o.Firing && Reloading == o.Reloading && (Look - o.Look).sqrMagnitude < 0.05f; }
        public override bool Equals(object obj) { return obj is PuppetPose p && Equals(p); }
        public override int GetHashCode() { return Firing ? 1 : 0; }
    }

    public static class PuppetPoseSync
    {
        /// <summary>Writes the controlled character's pose into the network variable (at most about 15 times a second, only on change).</summary>
        public static void Publish(JUCharacterController c, NetworkVariable<PuppetPose> variable, ref float nextTime)
        {
            if (c == null || Time.unscaledTime < nextTime) return;
            nextTime = Time.unscaledTime + 0.066f;
            c.CapturePuppetState(out bool firing, out bool reloading, out Vector3 look);
            var pose = new PuppetPose { Firing = firing, Reloading = reloading, Look = look };
            if (!pose.Equals(variable.Value)) variable.Value = pose;
        }

        /// <summary>
        /// Puts the same item in the hand that the owner holds. A puppet goes through the controller (it also takes the second item of a dual
        /// wielded pistol into the other hand, which the hand IK needs); otherwise only the inventory is switched.
        /// </summary>
        public static void SwitchItem(JUCharacterController c, JUTPS.InventorySystem.JUInventory inventory, int id)
        {
            if (c != null && c.IsRemotePuppet) c.SwitchToItem(id, true);
            else if (inventory != null) inventory.SwitchToItem(id, true);
        }

        /// <summary>Makes the character a puppet that shows another machine's pose (not on a headless server, which draws nothing).</summary>
        public static bool MakePuppet(JUCharacterController c)
        {
            if (c == null || Project.Character.CharacterSkinApplier.NoVisuals) return false;
            c.IsRemotePuppet = true;
            c.UseDefaultControllerInput = false;
            c.enabled = true;
            return true;
        }
    }
}
