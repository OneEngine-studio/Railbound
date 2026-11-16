using JUTPS;
using Unity.Netcode;
using UnityEngine;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Who may collect a world pickup (medkit, ammo box): only the player character that THIS machine controls.
    ///
    /// The pickups used to accept any character. A dying bot's drop appears at its body, so the bot's own ragdoll touched the ammo box
    /// at once and "collected" it (lid opened, box emptied, reported taken to every peer) before a player could get there; bots running
    /// over loot did the same, and on a client the bot copies even showed "+AMMO COLLECTED" on the player's HUD. On the dedicated server
    /// the server's copy of a player could take a medkit too. Now the owner of the player character collects, and the existing taken
    /// report (BRLootSync) tells the server and the other players.
    /// </summary>
    public static class PickupAuthority
    {
        public static bool TryGetLocalCollector(Collider other, out JUCharacterController collector)
        {
            collector = other != null ? other.GetComponentInParent<JUCharacterController>() : null;
            if (collector == null) return false;
            if (DedicatedServerRuntime.IsDedicatedServer) return false;      // the server never collects for anybody
            if (collector.IsRemotePuppet) return false;                       // another player's or a bot's copy on this machine
            if (collector.GetComponent<Railbound.BR.BRNetBot>() != null) return false;   // bots (also on a host)
            if (collector.IsDead) return false;                               // a ragdoll

            var networkPlayer = collector.GetComponent<JUNetworkPlayer>();
            if (networkPlayer != null) return networkPlayer.IsSpawned && networkPlayer.IsOwner;

            var nm = NetworkManager.Singleton;
            bool online = nm != null && nm.IsListening;
            if (online) return false;                                         // in a network match only network players collect
            return collector == JUGameManager.PlayerController || collector.CompareTag("Player");   // single player
        }
    }
}
