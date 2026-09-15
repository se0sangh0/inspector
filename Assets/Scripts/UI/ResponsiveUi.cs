using UnityEngine;
using UnityEngine.UI;

/// <summary>FHD 기준 UI를 화면 안에 유지한다. iPad 가로에서는 추가 세로 공간을 앵커로 사용한다.</summary>
public static class ResponsiveUi
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

    public static void Configure(Canvas canvas)
    {
        if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) return;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    public static Vector2 LogicalSize(Vector2 pixels)
    {
        float scale = Mathf.Max(0.01f, Mathf.Min(pixels.x / ReferenceResolution.x, pixels.y / ReferenceResolution.y));
        return pixels / scale;
    }
}
