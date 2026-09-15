// ============================================================
// Log/PostBattleObservationPanel.cs
// 전투 후 현장 관찰(탐사 결과) 표시 오버레이 (P0-03 / 기획자 피드백)
// ============================================================
//
// [이 파일이 하는 일]
//   지정 인카운터의 전투 승리 뒤 현장 관찰(탐사 결과)을 표시합니다.
//   레이아웃: 제목 → 이미지 → 설명 → 단일 계속 입력.
//   팝업 크기는 이미지 비율에 맞춰 자동 조정합니다.
//
// [계약 — 16-A §2 핵심 현장 단서 / 16-B §3·§6]
//   - 표시일 뿐이다: 선택지·보상·추가 전투·재화·상태 변화를 만들지 않는다.
//   - 기록(BattleResolved 사후 관찰 필드)은 표시 전에 이미 생성돼 있다.
//   - [기획자 피드백] 탐사 완료 이동은 '계속' 버튼으로만. 빈 공간 클릭·키 입력으로는 진행하지 않는다.
//
// [이미지] Resources/ResultImage/{imageName} 스프라이트. 없으면 이미지 없이 표시.
//   화면을 벗어나지 않는 선에서 비율을 유지해 가능한 크게 표시한다.
//
// 사용:
//   PostBattleObservationPanel.Show(obs.title, obs.screenText, obs.imageName, onContinue);
//   - 씬 배치 불필요(자체 생성, DontDestroyOnLoad).
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PostBattleObservationPanel : MonoBehaviour
{
    public static PostBattleObservationPanel Instance { get; private set; }

    // 레이아웃 상수
    private const float PadTop = 28f, PadBottom = 24f, PadX = 44f;
    private const float TitleH = 52f, BodyH = 132f, BtnH = 56f;
    private const float GapTitleImg = 18f, GapImgBody = 18f, GapBodyBtn = 18f;
    private const float MinBoxW = 540f, ScreenMargin = 40f;

    private CanvasGroup   _group;
    private GameObject    _root;
    private RectTransform _box;
    private TMP_Text      _titleText, _bodyText;
    private Image         _image;
    private System.Action _onContinue;

    private static readonly Color DimColor   = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color BoxBg      = new Color(0.10f, 0.09f, 0.11f, 0.97f);
    private static readonly Color BoxBorder  = new Color(0.72f, 0.62f, 0.38f, 1f);
    private static readonly Color TitleColor = new Color(0.93f, 0.88f, 0.72f, 1f);
    private static readonly Color BodyColor  = new Color(0.88f, 0.86f, 0.82f, 1f);
    private static readonly Color BtnBg      = new Color(0.20f, 0.17f, 0.12f, 0.95f);

    /// <summary>현장 관찰을 표시한다. 계속 입력 시 onContinue 가 정확히 1회 호출된다.</summary>
    public static void Show(string title, string body, string imageName, System.Action onContinue)
    {
        Ensure();
        Instance._Show(title, body, imageName, onContinue);
    }

    private static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("PostBattleObservationPanel").AddComponent<PostBattleObservationPanel>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
    }

    private void _Show(string title, string body, string imageName, System.Action onContinue)
    {
        _onContinue = onContinue;
        if (_titleText != null) _titleText.text = title ?? "";
        if (_bodyText  != null) _bodyText.text  = body ?? "";

        // 이미지 로드 (Resources/ResultImage/{imageName}) — 없으면 이미지 생략
        Sprite sprite = null;
        if (!string.IsNullOrEmpty(imageName))
        {
            sprite = Resources.Load<Sprite>("ResultImage/" + imageName);
            if (sprite == null)
                Debug.LogWarning($"[PostBattleObservationPanel] 이미지 없음 — Resources/ResultImage/{imageName} (이미지 없이 표시)");
        }
        LayoutBox(sprite);

        _root.SetActive(true);
        Loc.Localize(_root); // 제목·설명·계속 버튼을 현재 언어로 (관찰 문안은 표에 등록됨)
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
    }

    /// <summary>
    /// 이미지 유무·비율에 따라 박스 크기와 각 요소 위치를 계산한다.
    /// 이미지는 화면(Screen)을 벗어나지 않는 최대 크기까지 비율을 유지해 키운다.
    /// </summary>
    private void LayoutBox(Sprite sprite)
    {
        // 화면 안에 들어가는 박스 최대 크기에서 이미지 외 요소 높이를 뺀 만큼이 이미지 가용 공간.
        float nonImgH   = PadTop + TitleH + GapTitleImg + GapImgBody + BodyH + GapBodyBtn + BtnH + PadBottom;
        float availImgH = Mathf.Max(120f, Screen.height - ScreenMargin * 2f - nonImgH);
        float availImgW = Mathf.Max(200f, Screen.width  - ScreenMargin * 2f - PadX * 2f);

        float imgW = 0f, imgH = 0f;
        if (sprite != null && sprite.rect.width > 0f && sprite.rect.height > 0f)
        {
            float w = sprite.rect.width, h = sprite.rect.height;
            float scale = Mathf.Min(availImgW / w, availImgH / h); // 비율 유지, 화면 안 최대
            imgW = w * scale; imgH = h * scale;
        }
        bool hasImg = imgW > 1f && imgH > 1f;

        if (_image != null)
        {
            _image.gameObject.SetActive(hasImg);
            if (hasImg)
            {
                _image.sprite = sprite;
                var irt = (RectTransform)_image.transform;
                irt.sizeDelta        = new Vector2(imgW, imgH);
                irt.anchoredPosition = new Vector2(0f, -(PadTop + TitleH + GapTitleImg));
            }
        }

        float boxW  = Mathf.Max(MinBoxW, imgW + PadX * 2f);
        float bodyY = PadTop + TitleH + (hasImg ? GapTitleImg + imgH : 0f) + GapImgBody;
        float boxH  = bodyY + BodyH + GapBodyBtn + BtnH + PadBottom;

        _box.sizeDelta = new Vector2(boxW, boxH);

        // 설명 위치 (이미지 유무에 따라 Y 이동). 폭은 앵커 스트레치라 자동 적응.
        var bort = (RectTransform)_bodyText.transform;
        bort.anchoredPosition = new Vector2(0f, -bodyY);
    }

    // 진행 — '계속' 버튼으로만 (빈 공간 클릭·키 입력 없음). activeSelf 로 중복 발화 차단.
    private void Proceed()
    {
        if (_root == null || !_root.activeSelf) return;
        var cb = _onContinue;
        _onContinue = null;
        Hide();
        cb?.Invoke();
    }

    private void Hide()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _root.SetActive(false);
    }

    // ── UI 구성 (런타임) ────────────────────────────────────────
    private void Build()
    {
        var canvasGo = new GameObject("ObservationCanvas", typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        ResponsiveUi.Configure(canvas);
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000; // BattleResultScreen(9990) 위 — 승리 팝업 다음 순서로 표시
        _group = canvasGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        var font = TMP_Settings.defaultFontAsset;

        _root = new GameObject("Observation", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)_root.transform);

        // 딤 — 뒤 화면 입력만 차단 (빈 공간 클릭으로는 진행하지 않음 — 계속 버튼 전용)
        var dim = NewImage("Dim", _root.transform);
        Stretch(dim.rectTransform);
        dim.color = DimColor;
        dim.raycastTarget = true;

        // 중앙 박스 — 하이라이트 테두리 (크기는 Show 때 이미지 비율로 조정)
        var box = NewImage("Box", _root.transform);
        _box = box.rectTransform;
        _box.anchorMin = _box.anchorMax = _box.pivot = new Vector2(0.5f, 0.5f);
        _box.sizeDelta = new Vector2(MinBoxW, 360f);
        box.color = BoxBg;
        box.raycastTarget = true; // 박스 클릭이 딤으로 새지 않게
        var outline = box.gameObject.AddComponent<Outline>();
        outline.effectColor     = BoxBorder;
        outline.effectDistance  = new Vector2(3f, 3f);
        outline.useGraphicAlpha = false;

        // 제목 (상단, 가로 스트레치)
        _titleText = NewText("Title", box.transform, font, 32f, TitleColor, FontStyles.Bold);
        var trt = (RectTransform)_titleText.transform;
        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta        = new Vector2(-PadX * 2f, TitleH);
        trt.anchoredPosition = new Vector2(0f, -PadTop);
        _titleText.alignment = TextAlignmentOptions.Center;

        // 이미지 (제목 아래, 가운데 정렬) — 크기·표시 여부는 Show 때 결정
        _image = NewImage("ResultImage", box.transform);
        var irt = _image.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f); irt.pivot = new Vector2(0.5f, 1f);
        _image.raycastTarget = false;
        _image.preserveAspect = true;
        _image.gameObject.SetActive(false);

        // 설명 (이미지 아래, 가로 스트레치) — Y 는 Show 때 이미지 유무로 조정
        _bodyText = NewText("Body", box.transform, font, 25f, BodyColor, FontStyles.Normal);
        var bort = (RectTransform)_bodyText.transform;
        bort.anchorMin = new Vector2(0f, 1f); bort.anchorMax = new Vector2(1f, 1f); bort.pivot = new Vector2(0.5f, 1f);
        bort.sizeDelta        = new Vector2(-PadX * 2f, BodyH);
        bort.anchoredPosition = new Vector2(0f, -(PadTop + TitleH + GapImgBody));
        _bodyText.alignment = TextAlignmentOptions.Top;
        _bodyText.enableWordWrapping = true;

        // 단일 계속 버튼 (하단 고정)
        var btnGo = new GameObject("ContinueButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        btnGo.transform.SetParent(box.transform, false);
        var btnRt = (RectTransform)btnGo.transform;
        btnRt.anchorMin = new Vector2(0.5f, 0f); btnRt.anchorMax = new Vector2(0.5f, 0f); btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.anchoredPosition = new Vector2(0f, PadBottom);
        btnRt.sizeDelta = new Vector2(200f, BtnH);
        btnGo.GetComponent<Image>().color = BtnBg;
        btnGo.AddComponent<Button>().onClick.AddListener(Proceed);

        var label = NewText("Label", btnGo.transform, font, 26f, TitleColor, FontStyles.Normal);
        Stretch((RectTransform)label.transform);
        label.text = "계속";
        label.alignment = TextAlignmentOptions.Center;

        _root.SetActive(false);
    }

    // ── 헬퍼 ──
    private static Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = color; t.fontStyle = style;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
