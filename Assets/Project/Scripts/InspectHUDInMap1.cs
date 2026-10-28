using UnityEngine;
using UnityEditor;

public class InspectHUDInMap1
{
    public static string Run()
    {
        var canvases = GameObject.FindObjectsOfType<Canvas>(true);
        string res = $"Canvases count: {canvases.Length}\n";
        foreach (var c in canvases)
        {
            res += $"Canvas: {c.gameObject.name} (active: {c.gameObject.activeInHierarchy}, mode: {c.renderMode})\n";
            for (int i = 0; i < c.transform.childCount; i++)
            {
                var child = c.transform.GetChild(i);
                res += $"   Child: {child.name} (active: {child.gameObject.activeSelf})\n";
            }
        }
        return res;
    }
}
