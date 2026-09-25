using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ClickEffect : MonoBehaviour
{
    [Header("Ring Style")]
    public Color ringColor = Color.white;
    [Range(0.5f, 3f)] public float duration = 0.6f;
    [Range(10f, 80f)] public float startSize = 20f;
    [Range(60f, 250f)] public float endSize = 90f;

    static Texture2D ringTex;
    Canvas canvas;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (ringTex == null) ringTex = CreateRingTexture(128);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) Spawn(Input.mousePosition);
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) Spawn(Input.GetTouch(0).position);
    }

    public void Spawn(Vector2 screenPos)
    {
        if (canvas == null) return;
        var go = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(startSize, startSize);
        Vector2 local;
        // ScreenSpace-Overlay canvas.worldCamera is null; use null, for Camera mode use canvas.worldCamera
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, screenPos, cam, out local);
        rt.anchoredPosition = local;
        var img = go.GetComponent<Image>();
        img.sprite = Sprite.Create(ringTex, new Rect(0, 0, ringTex.width, ringTex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        img.color = ringColor;
        img.raycastTarget = false;
        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 1f;
        StartCoroutine(Animate(go, rt, cg));
    }

    IEnumerator Animate(GameObject go, RectTransform rt, CanvasGroup cg)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = t / duration;
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            float sz = Mathf.Lerp(startSize, endSize, ease);
            rt.sizeDelta = new Vector2(sz, sz);
            cg.alpha = 1f - k;
            yield return null;
        }
        Destroy(go);
    }

    static Texture2D CreateRingTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color clear = new Color(0, 0, 0, 0);
        float cx = size * 0.5f, cy = size * 0.5f, r = size * 0.5f - 1f;
        float thickness = 8f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float dx = x - cx, dy = y - cy;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float dist = Mathf.Abs(d - (r - thickness * 0.5f));
            float a = Mathf.Clamp01(1f - dist / (thickness * 0.5f));
            a = Mathf.SmoothStep(0, 1, a);
            if (dist < thickness) tex.SetPixel(x, y, new Color(1, 1, 1, a));
            else tex.SetPixel(x, y, clear);
        }
        tex.Apply();
        return tex;
    }
}
