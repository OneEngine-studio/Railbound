using Unity.Netcode;
using UnityEngine;
using JUTPS;

namespace Railbound.Audio
{
    /// <summary>
    /// Plays the item pickup sound (pickup.wav) for the local human player only.
    /// Called from every pickup path: JUInventory item/weapon pickup, ammo boxes and medkits.
    /// Bots, remote players and the headless dedicated server never play it.
    /// </summary>
    public static class PickupSound
    {
        // Loaded from Assets/Project/Resources/pickup.wav so it exists in builds.
        private const string ClipName = "pickup";
        private const float Volume = 0.85f;

        private static AudioClip _clip;
        private static AudioSource _source;

        public static void Play(GameObject picker)
        {
            if (picker == null) return;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return; // dedicated server

            var controller = picker.GetComponent<JUCharacterController>() ?? picker.GetComponentInParent<JUCharacterController>();
            if (controller == null || controller.IsArtificialIntelligence) return;

            var netObj = controller.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned && !netObj.IsOwner) return; // another player's character

            if (_clip == null) _clip = Resources.Load<AudioClip>(ClipName);
            if (_clip == null)
            {
                Debug.LogWarning($"[PickupSound] '{ClipName}' not found in a Resources folder.");
                return;
            }

            if (_source == null)
            {
                var go = new GameObject("[PickupSound]");
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f; // 2D: plays for the player, not from a world position
            }

            _source.PlayOneShot(_clip, Volume);
        }
    }
}
