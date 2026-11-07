using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Railbound.Networking
{
    /// <summary>
    /// HUD and gameplay scripts in MAP-1 (health bar, medkit counter, crosshair, minimap, indicators, camera ...)
    /// cache the offline scene "TPS Character" when the scene loads. In multiplayer that character is replaced by
    /// the spawned network player, so those scripts keep watching a hidden object (health bar never moves, etc).
    /// This moves every such reference onto the matching component of the new player.
    /// </summary>
    public static class PlayerReferenceRebinder
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static int Rebind(Transform oldRoot, Transform newRoot)
        {
            if (oldRoot == null || newRoot == null || oldRoot == newRoot) return 0;

            int rebound = 0;
            var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mb in all)
            {
                if (mb == null) continue;
                var tr = mb.transform;
                if (tr.IsChildOf(oldRoot) || tr.IsChildOf(newRoot)) continue;

                Type type = mb.GetType();
                string ns = type.Namespace ?? string.Empty;
                if (ns.StartsWith("UnityEngine") || ns.StartsWith("Unity.") || ns.StartsWith("TMPro") || ns.StartsWith("Cinemachine"))
                    continue;

                List<(FieldInfo field, Object value)> changes = null;
                for (Type t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(Behaviour); t = t.BaseType)
                {
                    foreach (var f in t.GetFields(Flags | BindingFlags.DeclaredOnly))
                    {
                        if (f.IsStatic || !typeof(Object).IsAssignableFrom(f.FieldType)) continue;
                        Object current;
                        try { current = f.GetValue(mb) as Object; }
                        catch { continue; }
                        if (current == null) continue;

                        Object mapped = Map(current, oldRoot, newRoot);
                        if (mapped != null && mapped != current)
                        {
                            if (changes == null) changes = new List<(FieldInfo, Object)>();
                            changes.Add((f, mapped));
                        }
                    }
                }

                if (changes == null) continue;

                // Toggle so OnDisable/OnEnable unsubscribe from the old player and subscribe to the new one.
                bool wasEnabled = mb.enabled;
                if (wasEnabled) mb.enabled = false;
                foreach (var (field, value) in changes)
                {
                    field.SetValue(mb, value);
                    rebound++;
                }
                if (wasEnabled) mb.enabled = true;

                Debug.Log($"[PlayerReferenceRebinder] {type.Name} on '{mb.name}': rebound {changes.Count} reference(s) to the network player.");
            }

            return rebound;
        }

        private static Object Map(Object value, Transform oldRoot, Transform newRoot)
        {
            Transform t = value as Transform;
            Component comp = value as Component;
            GameObject go = value as GameObject;
            if (t == null && comp != null) t = comp.transform;
            if (t == null && go != null) t = go.transform;
            if (t == null || !t.IsChildOf(oldRoot)) return null;

            Transform target = t == oldRoot ? newRoot : newRoot.Find(RelativePath(t, oldRoot));
            if (target == null) return null;

            if (value is Transform) return target;
            if (go != null) return target.gameObject;
            if (comp != null)
            {
                var c = target.GetComponent(comp.GetType());
                return c;
            }
            return null;
        }

        private static string RelativePath(Transform t, Transform root)
        {
            var parts = new Stack<string>();
            for (var cur = t; cur != null && cur != root; cur = cur.parent)
                parts.Push(cur.name);
            return string.Join("/", parts);
        }
    }
}
