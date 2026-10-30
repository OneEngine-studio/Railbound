using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class ClickEffect : MonoBehaviour
{
    [Header("Ring Style")]
    public Color ringColor = Color.white;
    [Range(0.5f, 3f)] public float duration = 0.6f;
    [Range(10f, 80f)] public float startSize = 20f;
    [Range(60f, 250f)] public float endSize = 90f;

    private static Texture2D _ringTex;
    private static Sprite _ringSprite;
    private Canvas _canvas;
    private readonly Queue<GameObject> _pool = new Queue<GameObject>();

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null) _canvas = GetComponent<Canvas>();

        if (_ringTex == null)
        {
            _ringTex = CreateRingTexture(128);
            _ringSprite = Sprite.Create(_ringTex, new Rect(0, 0, _ringTex.width, _ringTex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        // Pre-warm pool
        for (int i = 0; i < 5; i++)
        {
            _pool.Enqueue(CreateNewRing());
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Spawn(Input.mousePosition);
        }
        else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            Spawn(Input.GetTouch(0).position);
        }
    }

    private GameObject CreateNewRing()
    {
        var go = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(_canvas.transform, false);
        
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var img = go.GetComponent<Image>();
        img.sprite = _ringSprite;
        img.color = ringColor;
        img.raycastTarget = false;

        go.SetActive(false);
        return go;
    }

    public void Spawn(Vector2 screenPos)
    {
        if (_canvas == null) return;

        GameObject go = _pool.Count > 0 ? _pool.Dequeue() : CreateNewRing();
        go.SetActive(true);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(startSize, startSize);

        var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas.transform as RectTransform, screenPos, cam, out Vector2 local);
        rt.anchoredPosition = local;

        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 1f;

        StartCoroutine(Animate(go, rt, cg));
    }

    private IEnumerator Animate(GameObject go, RectTransform rt, CanvasGroup cg)
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
        
        go.SetActive(false);
        _pool.Enqueue(go);
    }

    private static Texture2D CreateRingTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { filterMode = FilterMode.Bilinear };
        Color clear = new Color(0, 0, 0, 0);
        float cx = size * 0.5f, cy = size * 0.5f, r = size * 0.5f - 1f;
        float thickness = 8f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float dist = Mathf.Abs(d - (r - thickness * 0.5f));
                float a = Mathf.Clamp01(1f - dist / (thickness * 0.5f));
                a = Mathf.SmoothStep(0, 1, a);
                
                tex.SetPixel(x, y, dist < thickness ? new Color(1, 1, 1, a) : clear);
            }
        }
        tex.Apply();
        return tex;
    }
}
