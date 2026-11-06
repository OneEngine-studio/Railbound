using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Railbound.UI.Anim;

public sealed partial class PlayerOnboardingController
{
    private static void SetPreferredHeight(GameObject objectRoot, float height)
    {
        var layout = objectRoot.GetComponent<LayoutElement>();
        if (layout == null) layout = objectRoot.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
    }

    private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void ApplyChamfer(Image image)
    {
        if (image == null)
            return;
        var sprite = ResolveChamferSprite();
        if (sprite == null)
            return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
    }

    private static Sprite ResolveChamferSprite()
    {
        if (_chamfer != null)
            return _chamfer;
#if UNITY_EDITOR
        _chamfer = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Resources/UI/Onboarding_Chamfer.png");
#endif
        if (_chamfer == null)
            _chamfer = Resources.Load<Sprite>("UI/Onboarding_Chamfer");
        return _chamfer;
    }

    private static Sprite ResolveRingSprite()
    {
        if (_ring != null)
            return _ring;
#if UNITY_EDITOR
        _ring = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Resources/UI/Ring_Chamfer.png");
#endif
        if (_ring == null)
            _ring = Resources.Load<Sprite>("UI/Ring_Chamfer");
        return _ring;
    }

    private static TMP_FontAsset ResolveBoldFont()
    {
        if (_boldFont != null) return _boldFont;
#if UNITY_EDITOR
        _boldFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-Bold SDF.asset");
#endif
        return _boldFont;
    }

    private static TMP_FontAsset ResolveMedFont()
    {
        if (_medFont != null) return _medFont;
#if UNITY_EDITOR
        _medFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Reach - Complete Sci-Fi UI/Fonts/Rajdhani-Medium SDF.asset");
#endif
        return _medFont;
    }

    private static Sprite _crosshair;
    private static Sprite _randomize;

    private static Sprite ResolveCrosshairSprite()
    {
        if (_crosshair != null) return _crosshair;
#if UNITY_EDITOR
        _crosshair = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Resources/UI/Lobby/Icon_DiamondCrosshair.png");
#endif
        if (_crosshair == null)
            _crosshair = Resources.Load<Sprite>("UI/Lobby/Icon_DiamondCrosshair");
        return _crosshair;
    }

    private static Sprite ResolveRandomizeSprite()
    {
        if (_randomize != null) return _randomize;
#if UNITY_EDITOR
        _randomize = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Resources/UI/Icon_Dice.png");
#endif
        if (_randomize == null)
            _randomize = Resources.Load<Sprite>("UI/Icon_Dice");
        if (_randomize == null)
            _randomize = Resources.Load<Sprite>("UI/Icon_Randomize");
        return _randomize;
    }

    private static Sprite _operatorSprite;

    private static Sprite ResolveOperatorSprite()
    {
        if (_operatorSprite != null) return _operatorSprite;
#if UNITY_EDITOR
        _operatorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Resources/UI/Operator_Dossier.png");
#endif
        if (_operatorSprite == null)
            _operatorSprite = Resources.Load<Sprite>("UI/Operator_Dossier");
        return _operatorSprite;
    }
}
