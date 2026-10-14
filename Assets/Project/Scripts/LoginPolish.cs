using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Railbound Login polish: Sci-Fi click/hover SFX on all login buttons,
/// subtle hover scale, and a slow pulse on the orange accent bars.
/// Auth flow untouched — visuals/feedback only.
/// </summary>
public class LoginPolish : MonoBehaviour
{
    [Header("SFX (Sci-Fi pack)")]
    public AudioClip clickClip;
    public AudioClip hoverClip;
    [Range(0f, 1f)] public float clickVolume = 0.6f;
    [Range(0f, 1f)] public float hoverVolume = 0.25f;

    [Header("Hover")]
    [Range(1f, 1.15f)] public float hoverScale = 1.03f;
    public float hoverLerp = 12f;

    [Header("Accent pulse")]
    public List<Image> accentBars = new List<Image>();
    public float pulseSpeed = 1.6f;
    public float pulseMin = 0.55f;
    public float pulseMax = 1f;

    private AudioSource _sfx;
    private readonly Dictionary<Button, Vector3> _baseScale = new Dictionary<Button, Vector3>();
    private readonly HashSet<Button> _hovered = new HashSet<Button>();
    private float _hoverCooldown;

    private void Awake()
    {
        _sfx = GetComponent<AudioSource>();
        if (_sfx == null) _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        _sfx.loop = false;
        _sfx.spatialBlend = 0f;
    }

    private void Start()
    {
        if (clickClip == null)
            clickClip = UnityEngine.Resources.Load<AudioClip>("SciFi_Select2");
        AutoWire();
    }

    private void AutoWire()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            if (b == null || _baseScale.ContainsKey(b)) continue;
            _baseScale[b] = b.transform.localScale;
            var btn = b;
            btn.onClick.AddListener(() => PlayClick());
            AddHover(btn);
        }

        // Accent bars default: MoreAccent + GuestAccent.
        // Skip any bar driven by Reach UI ImagePulse to avoid fighting over alpha.
        if (accentBars.Count == 0)
        {
            foreach (var n in new[] { "Canvas/LoginPanel/MoreButton/MoreAccent", "Canvas/LoginPanel/GuestButton/GuestAccent" })
            {
                var go = GameObject.Find(n);
                if (go != null)
                {
                    var img = go.GetComponent<Image>();
                    if (img != null && img.GetComponent<Michsky.UI.Reach.ImagePulse>() == null && !accentBars.Contains(img))
                        accentBars.Add(img);
                }
            }
        }
        else
        {
            accentBars.RemoveAll(img => img != null && img.GetComponent<Michsky.UI.Reach.ImagePulse>() != null);
        }
    }

    private void AddHover(Button btn)
    {
        var trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { _hovered.Add(btn); PlayHover(); });
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => _hovered.Remove(btn));
        trigger.triggers.Add(exit);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _hoverCooldown -= dt;

        foreach (var kv in _baseScale)
        {
            if (kv.Key == null) continue;
            Vector3 target = kv.Value;
            if (_hovered.Contains(kv.Key) && kv.Key.interactable)
                target = kv.Value * hoverScale;
            kv.Key.transform.localScale = Vector3.Lerp(kv.Key.transform.localScale, target, Mathf.Clamp01(hoverLerp * dt));
        }

        if (accentBars.Count > 0)
        {
            float k = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            float a = Mathf.Lerp(pulseMin, pulseMax, k);
            foreach (var img in accentBars)
            {
                if (img == null) continue;
                var c = img.color;
                c.a = a;
                img.color = c;
            }
        }
    }

    public void PlayClick()
    {
        if (_sfx != null && clickClip != null)
            _sfx.PlayOneShot(clickClip, clickVolume);
    }

    private void PlayHover()
    {
        if (_hoverCooldown > 0f) return;
        _hoverCooldown = 0.08f;
        if (_sfx != null && hoverClip != null)
            _sfx.PlayOneShot(hoverClip, hoverVolume);
    }
}
