using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// A one-shot message for the next lobby screen. A match or the loading screen can end by sending the player back to the lobby
    /// (the connection failed, the server closed, the connection was lost); this carries the reason across the scene change so the
    /// lobby can say it in plain words instead of showing nothing. The lobby takes it once with <see cref="TryTake"/>.
    /// </summary>
    public static class LobbyNotice
    {
        public enum Kind
        {
            /// <summary>Something the player should know, not an error (for example "the match server has closed").</summary>
            Info,
            /// <summary>The player could not do what they wanted; the lobby offers a retry.</summary>
            Problem,
        }

        private const float MaxAgeSeconds = 120f;   // a notice that nobody read is not shown much later

        private static string text;
        private static Kind kind;
        private static float setAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { text = null; }

        public static void Set(string message, Kind noticeKind)
        {
            if (string.IsNullOrEmpty(message)) return;
            text = message;
            kind = noticeKind;
            setAt = Time.realtimeSinceStartup;
        }

        /// <summary>Returns the pending notice once; false when there is none or it is too old.
        public static bool TryTake(out string message, out Kind noticeKind)
        {
            message = text;
            noticeKind = kind;
            bool fresh = !string.IsNullOrEmpty(text) && Time.realtimeSinceStartup - setAt <= MaxAgeSeconds;
            text = null;
            return fresh;
        }
    }
}

namespace Railbound.UI
{
    /// <summary>
    /// "PLAY AGAIN" on the results screen: the match sends the player back to the lobby and asks it to start the next deployment on
    /// its own. The lobby takes the request once with <see cref="TryConsume"/>.
    /// </summary>
    public static class LobbyAutoDeploy
    {
        private const float MaxAgeSeconds = 30f;

        private static bool requested;
        private static float requestedAt;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { requested = false; }

        public static void Request()
        {
            requested = true;
            requestedAt = UnityEngine.Time.realtimeSinceStartup;
        }

        public static bool TryConsume()
        {
            bool fresh = requested && UnityEngine.Time.realtimeSinceStartup - requestedAt <= MaxAgeSeconds;
            requested = false;
            return fresh;
        }
    }
}
