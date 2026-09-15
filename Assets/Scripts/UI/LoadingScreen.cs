// ============================================================
// UI/LoadingScreen.cs  (2026-06-11)
// 전투 진입 등 짧은 로딩 커버 — 다크 풀스크린 + "전투 준비 중…" + 진행바.
// ============================================================
//
// 사용:  LoadingScreen.Cover();                         // 즉시 커버(검은/로딩 화면)
//        yield return LoadingScreen.UncoverRoutine(0.4f); // 페이드 아웃하며 공개
//   - 씬에 배치 불필요. 첫 호출 시 자체적으로 GameObject + 최상단 오버레이 Canvas 생성,
//     DontDestroyOnLoad 로 유지(SceneTransition 과 동일 패턴, sortingOrder 더 높게).
//   - 노드맵→전투는 같은 씬 내 패널 전환이라 SceneTransition(씬 로드 페이드)이 안 걸린다.
//     이 커버로 스폰/정렬되는 찰나를 가리고 시간을 번다.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    private CanvasGroup   _group;
    private TMP_Text      _label;
    private Image         _barFill;
    private RectTransform _spinner;          // 회전 스피너(로딩 표시)
    private string        _baseText = "전투 준비 중";
    private bool          _covered;
    private float         _dotT;
    private int           _dots;

    private const float SpinSpeed = 240f;    // 스피너 회전 속도(도/초)

    private static readonly Color Gold = new Color(1f, 0.84f, 0.4f, 1f);

    /// <summary>즉시 커버(알파 1). message 로 문구 교체 가능.</summary>
    public static void Cover(string message = null)
    {
        Ensure();
        if (!string.IsNullOrEmpty(message)) Instance._baseText = message;
        Instance._group.alpha = 1f;
        Instance._group.blocksRaycasts = true;
        Instance._covered = true;
        Instance._dotT = 0f; Instance._dots = 0;
        if (Instance._label != null)   Loc.Bind(Instance._label, () => Loc.Tr(Instance._baseText) + new string('.', Instance._dots));
        if (Instance._barFill != null) Instance._barFill.fillAmount = 0f;
    }

    /// <summary>duration 동안 페이드 아웃하며 커버 해제.</summary>
    public static IEnumerator UncoverRoutine(float duration = 0.4f)
    {
        if (Instance == null) yield break;
        yield return Instance.FadeOut(duration);
    }

    public static void UncoverInstant()
    {
        if (Instance == null) return;
        Instance._group.alpha = 0f;
        Instance._group.blocksRaycasts = false;
        Instance._covered = false;
    }

    private static void Ensure()
    {
        if (Instance != null) return;
        var go = new GameObject("LoadingScreen");
        go.AddComponent<LoadingScreen>(); // Awake 에서 Instance/오버레이 셋업
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
    }

    private void Build()
    {
        var canvasGo = new GameObject("LoadingCanvas",
            typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        ResponsiveUi.Configure(canvas);
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000; // SceneTransition(9999) 보다도 위

        _group = canvasGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        // 다크 풀스크린 배경
        var bg = NewImage("BG", canvasGo.transform);
        Stretch(bg.rectTransform);
        bg.color = new Color(0.05f, 0.06f, 0.09f, 1f);

        // 회전 스피너 (로딩 표시) — 텍스트 위에 배치
        var spin = NewImage("Spinner", canvasGo.transform);
        var srt = spin.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(76, 76);
        srt.anchoredPosition = new Vector2(0, 120);
        var spinSprite = Resources.Load<Sprite>("UI/loading_spinner");
        if (spinSprite != null) spin.sprite = spinSprite;
        spin.color = Gold;
        spin.preserveAspect = true;
        _spinner = srt;

        var font = TMP_Settings.defaultFontAsset; // 게임 기본(한글) 폰트

        // 로딩 텍스트 (금색)
        var tgo = new GameObject("Label", typeof(RectTransform));
        tgo.transform.SetParent(canvasGo.transform, false);
        _label = tgo.AddComponent<TextMeshProUGUI>();
        if (font != null) _label.font = font;
        Loc.Bind(_label, () => Loc.Tr(_baseText) + new string('.', _dots));
        _label.fontSize = 52;
        _label.color = Gold;
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontStyle = FontStyles.Bold;
        _label.enableWordWrapping = false;
        var trt = (RectTransform)tgo.transform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(900, 90);
        trt.anchoredPosition = new Vector2(0, 24);

        // 진행바 (트랙 + fill)
        var track = NewImage("BarTrack", canvasGo.transform);
        var brt = track.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(380, 12);
        brt.anchoredPosition = new Vector2(0, -52);
        track.color = new Color(0.18f, 0.19f, 0.24f, 1f);

        var fill = NewImage("BarFill", track.transform);
        var frt = fill.rectTransform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        fill.color = Gold;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;
        _barFill = fill;
    }

    private Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private void Update()
    {
        if (!_covered) return;

        // 스피너 회전 (시계방향) — 로딩 느낌
        if (_spinner != null)
            _spinner.Rotate(0f, 0f, -SpinSpeed * Time.unscaledDeltaTime);

        // 점 애니메이션 ("" → "." → ".." → "...")
        _dotT += Time.unscaledDeltaTime;
        if (_dotT >= 0.35f)
        {
            _dotT = 0f; _dots = (_dots + 1) % 4;
            if (_label != null) _label.text = Loc.Tr(_baseText) + new string('.', _dots);
        }

        // 진행바 자동 채움 (~0.7초)
        if (_barFill != null && _barFill.fillAmount < 1f)
            _barFill.fillAmount = Mathf.MoveTowards(_barFill.fillAmount, 1f, Time.unscaledDeltaTime / 0.7f);
    }

    private IEnumerator FadeOut(float duration)
    {
        if (_barFill != null) _barFill.fillAmount = 1f;
        float t = 0f, start = _group.alpha;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _covered = false;
    }
}
