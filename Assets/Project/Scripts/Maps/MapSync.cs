using Railbound.BR;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Railbound.Maps
{
    /// <summary>
    /// The server tells every client which map it built. A client that hears it before the match scene loads simply builds that map;
    /// a client that already built a different one (an old build, a wrong <c>-map</c>) would walk on ground that is not there, so it
    /// leaves the match with a clear log line instead.
    /// </summary>
    public static class MapSync
    {
        private const string Message = "Railbound.Map";
        private static CustomMessagingManager hookedMessaging;   // one per network session

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("[Map] Sync");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Watcher>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { hookedMessaging = null; }

        // sessions start in different places (the dedicated server inside the match scene, a player's client in the lobby): a small
        // watcher hooks each session once it runs
        private sealed class Watcher : MonoBehaviour
        {
            private void Update() { TryHook(); }
        }

        private static void TryHook()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || nm.CustomMessagingManager == null || nm.CustomMessagingManager == hookedMessaging) return;
            hookedMessaging = nm.CustomMessagingManager;
            if (nm.IsServer)
            {
                nm.OnClientConnectedCallback -= SendTo;
                nm.OnClientConnectedCallback += SendTo;
                foreach (var id in nm.ConnectedClientsIds) if (id != NetworkManager.ServerClientId) SendTo(id);
            }
            else if (nm.IsClient)
            {
                nm.CustomMessagingManager.RegisterNamedMessageHandler(Message, OnMapMessage);
            }
        }

        private static void SendTo(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || clientId == NetworkManager.ServerClientId) return;
            var def = MapLoader.Active ?? MapSelection.Current ?? MapCatalog.Map2;
            var id = new FixedString64Bytes(def.Id);
            using (var w = new FastBufferWriter(64, Allocator.Temp))
            {
                w.WriteValueSafe(id);
                nm.CustomMessagingManager.SendNamedMessage(Message, clientId, w, NetworkDelivery.Reliable);
            }
        }

        private static void OnMapMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out FixedString64Bytes id);
            var serverMap = MapCatalog.Find(id.ToString());
            if (serverMap == null)
            {
                Debug.LogError("[Map] The server built a map this client does not know: '" + id + "'. Leaving the match.");
                Leave();
                return;
            }
            var built = MapLoader.Active;
            if (built == null)
            {
                // the match scene has not loaded yet: build the server's map
                MapSelection.Set(serverMap);
                Debug.Log("[Map] The server's map is " + serverMap.Id);
                return;
            }
            if (built.Id == serverMap.Id) { Debug.Log("[Map] The server built the same map: " + built.Id); return; }
            Debug.LogError($"[Map] This match is on '{serverMap.Id}' but this client built '{built.Id}': leaving the match.");
            Leave();
        }

        private static void Leave()
        {
            var c = BRMatchCoordinator.Instance;
            if (c != null) c.LeaveMatch();
            else if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        }
    }
}
