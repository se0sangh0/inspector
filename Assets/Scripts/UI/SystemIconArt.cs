using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>런타임 시스템 아이콘 연결. 에셋이 없으면 기존 UI를 그대로 둔다.</summary>
public static class SystemIconArt
{
    private const string ResourceRoot = "RemakeV1/SystemIcons/";

    public static bool Apply(Image image, string iconName)
    {
        if (image == null || string.IsNullOrEmpty(iconName)) return false;
        var sprite = Resources.Load<Sprite>(ResourceRoot + iconName);
        if (sprite == null) return false;

        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
        return true;
    }

    public static bool ApplyToNamedChild(Transform root, string childName, string iconName)
    {
        if (root == null) return false;
        var image = root.Find(childName)?.GetComponent<Image>();
        return Apply(image, iconName);
    }

    /// <summary>설정처럼 짧은 문구의 버튼은 아이콘과 글자를 한 묶음으로 가운데 정렬한다.</summary>
    public static bool EnsureCenteredLeadingIcon(Button button, string iconName)
    {
        if (button == null || Resources.Load<Sprite>(ResourceRoot + iconName) == null) return false;
        var root = button.transform;
        var label = root.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return false;

        var content = root.Find("SystemIconContent") as RectTransform;
        if (content == null)
        {
            var go = new GameObject("SystemIconContent", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(root, false);
            content = (RectTransform)go.transform;
            content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(12f, 0f); content.offsetMax = new Vector2(-12f, 0f);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        }

        var icon = content.Find("SystemIcon_" + iconName) ?? root.Find("SystemIcon_" + iconName) ?? root.Find("Icon_Image");
        if (icon == null)
            icon = new GameObject("SystemIcon_" + iconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).transform;
        icon.SetParent(content, false);
        icon.name = "SystemIcon_" + iconName;
        icon.SetAsFirstSibling();
        if (!Apply(icon.GetComponent<Image>(), iconName)) return false;
        var iconSize = icon.GetComponent<LayoutElement>() ?? icon.gameObject.AddComponent<LayoutElement>();
        iconSize.minWidth = iconSize.preferredWidth = 48f;
        iconSize.preferredHeight = 48f;
        iconSize.flexibleWidth = iconSize.flexibleHeight = 0f;

        label.transform.SetParent(content, false);
        label.transform.SetAsLastSibling();
        label.raycastTarget = false;
        LayoutRebuilder.MarkLayoutForRebuild(content);
        return true;
    }

    /// <summary>버튼 우측 장식 아이콘을 1회 생성한다. 이미지가 없으면 버튼을 변경하지 않는다.</summary>
    public static bool EnsureRightIcon(Button button, string iconName, float size = 30f, float right = 12f)
    {
        if (button == null) return false;
        var sprite = Resources.Load<Sprite>(ResourceRoot + iconName);
        if (sprite == null) return false;

        var root = button.transform;
        var icon = root.Find("Icon_Image") ?? root.Find("SystemIcon_" + iconName);
        Image image;
        if (icon == null)
        {
            var go = new GameObject("SystemIcon_" + iconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root, false);
            icon = go.transform;
        }
        image = icon.GetComponent<Image>();
        Apply(image, iconName);
        var rt = (RectTransform)icon;
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-right, 0f);
        rt.sizeDelta = new Vector2(size, size);
        ShiftLabelForRightIcon(root, right + size + 8f);
        return true;
    }

    /// <summary>
    /// X/× 하나만 있는 닫기 버튼을 아이콘 전용으로 정리한다.
    /// 필요한 리소스가 없거나 실제 문구가 있는 버튼이면 변경하지 않는다.
    /// </summary>
    public static bool EnsureGlyphOnlyIcon(Button button, string iconName, float size = 40f)
    {
        if (button == null || string.IsNullOrEmpty(iconName)) return false;

        var iconSprite = Resources.Load<Sprite>(ResourceRoot + iconName);
        if (iconSprite == null) return false;

        var root = button.transform;
        var labels = root.GetComponentsInChildren<TMP_Text>(true);
        bool hasGlyphLabel = false;
        foreach (var label in labels)
        {
            if (label.transform.parent != root) continue;
            if (!IsCloseGlyph(label.text)) return false;
            hasGlyphLabel = true;
        }
        if (!hasGlyphLabel) return false;

        // Keep the Button, RectTransform, target graphic and click events intact.
        var background = button.targetGraphic as Image;
        if (background != null)
        {
            background.sprite = null;
            background.overrideSprite = null;
            background.type = Image.Type.Simple;
            background.color = new Color(0.18f, 0.16f, 0.12f, 1f);
            background.preserveAspect = false;
            background.raycastTarget = true;
            var outline = background.GetComponent<Outline>() ?? background.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.54f, 0.47f, 0.31f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        // SpriteSwap transitions must not restore the old X background on hover.
        button.spriteState = default;
        button.transition = Selectable.Transition.ColorTint;

        foreach (var label in labels)
        {
            if (label.transform.parent != root) continue;
            label.enabled = false;
            label.raycastTarget = false;
        }

        var icon = root.Find("SystemIcon_" + iconName) ?? root.Find("Icon_Image");
        if (icon == null)
        {
            var go = new GameObject("SystemIcon_" + iconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root, false);
            icon = go.transform;
        }

        var image = icon.GetComponent<Image>();
        if (!Apply(image, iconName)) return false;

        var rt = (RectTransform)icon;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        return true;
    }

    /// <summary>버튼 좌측 장식 아이콘을 1회 생성한다. 이미지가 없으면 버튼을 변경하지 않는다.</summary>
    public static bool EnsureLeftIcon(Button button, string iconName, float size = 32f, float left = 12f)
    {
        if (button == null) return false;
        var sprite = Resources.Load<Sprite>(ResourceRoot + iconName);
        if (sprite == null) return false;

        var root = button.transform;
        var icon = root.Find("SystemIcon_" + iconName);
        if (icon == null)
        {
            var go = new GameObject("SystemIcon_" + iconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root, false);
            icon = go.transform;
        }
        Apply(icon.GetComponent<Image>(), iconName);
        var rt = (RectTransform)icon;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(left, 0f);
        rt.sizeDelta = new Vector2(size, size);
        ShiftLabelForLeftIcon(root, left + size + 8f);
        return true;
    }

    private static void ShiftLabelForLeftIcon(Transform root, float inset)
    {
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.transform.parent != root) continue;
            var rt = label.rectTransform;
            rt.offsetMin = new Vector2(inset, rt.offsetMin.y);
            label.raycastTarget = false;
        }
    }

    private static void ShiftLabelForRightIcon(Transform root, float inset)
    {
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.transform.parent != root) continue;
            var rt = label.rectTransform;
            rt.offsetMax = new Vector2(-inset, rt.offsetMax.y);
            label.raycastTarget = false;
        }
    }

    private static bool IsCloseGlyph(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var glyph = text.Trim();
        return glyph == "X" || glyph == "x" || glyph == "×" || glyph == "✕" || glyph == "✖" || glyph == "✗" || glyph == "╳";
    }
}
