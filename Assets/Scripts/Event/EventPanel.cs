// ============================================================
// Event/EventPanel.cs
// `?` 노드 선택지 이벤트 팝업 — 런타임 생성 UI (PanelBase 페이드)
// ============================================================
//
// [무엇을 하나]
//   NodeSystem 이 `?`(Event) 노드 클릭 시 EventPanel.CreateUnder(canvas) 로
//   런타임 생성 → OpenRandom() 으로 무작위 이벤트를 띄운다.
//
// [레이아웃] (기획자 요구)
//   - 뒤 화면을 실시간 캡처해 블러 처리한 전체 화면 배경(모달) + 중앙 프레임.
//   - 프레임 = 배경 이미지(panel_1) 원본 비율을 유지한 채 화면에 가득 차게(AspectRatioFitter.FitInParent).
//   - 프레임 배경 = Resources/UI/panel_1.png.
//   - 내용(제목/본문/선택지)은 프레임 테두리 안쪽에 인셋 배치.
//   - 선택지 버튼 구조(첨부 사진 참조) = [더미 이미지] 위 + [선택지 설명] 아래.
//     · 이미지는 추후 교체용 UI Image(회색 더미). 이름 "ChoiceImage".
//     · 버튼 크기는 프레임 크기에 맞춰 커짐(레이아웃 그룹 자동 확장 + 상향된 수치).
//
// [블러]
//   URP 파이프라인 무관하게 동작하도록, 팝업이 보이기 직전(alpha 0) 프레임을
//   ScreenCapture 로 캡처 → 다단계 다운샘플(바이리니어)로 블러 → 배경 RawImage 에 표시.
//   (커스텀 셰이더/렌더러 피처 불필요, 노드맵은 정적이라 1회 캡처로 충분)
//
// [흐름]
//   OpenRandom → Bind(evt) → (캡처+블러) → 페이드 인 → 선택지 클릭
//   → EventService.ResolveChoice → 결과 텍스트 + [다음 층] → OnExit + Close
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EventPanel : PanelBase
{
    // ── 런타임 참조 ─────────────────────────────────────────────
    private RectTransform _choiceRow;      // 선택지 버튼들의 부모 (가로 정렬)
    private TMP_Text      _titleText;
    private TMP_Text      _bodyText;
    private Button        _confirmButton;
    private TMP_Text      _confirmLabel;
    private TMP_FontAsset _font;
    private bool          _built;

    private RawImage      _blurImage;       // 전체 화면 블러 배경 (캡처 텍스처 표시)
    private RenderTexture _blurRt;          // 블러 결과 RT (열릴 때 생성, 닫힐 때 해제)
    private Coroutine     _openRoutine;

    private EventDefinition _current;
    private readonly List<GameObject> _choiceButtons = new();

    /// <summary>"다음 층" 클릭 시 NodeSystem 이 구독해 노드맵으로 복귀시킨다. (ChurchPanel 과 동일 패턴)</summary>
    public event Action OnExit;

    // ── 팔레트 ──────────────────────────────────────────────────
    private static readonly Color DimColor      = new Color(0f, 0f, 0f, 0.62f);        // 블러 실패 시 폴백(단색 딤)
    private static readonly Color BlurTint      = new Color(0.55f, 0.55f, 0.58f, 1f);  // 블러 텍스처 위 어둡게 곱하는 틴트
    private static readonly Color TextColor     = new Color(0.94f, 0.92f, 0.86f, 1f);
    private static readonly Color BodyColor     = new Color(0.86f, 0.84f, 0.80f, 1f);
    private static readonly Color DummyFill     = new Color(0.28f, 0.28f, 0.30f, 1f);
    private static readonly Color DummyBorder   = new Color(0.55f, 0.52f, 0.46f, 1f);
    private static readonly Color ChoiceBg      = new Color(0.10f, 0.09f, 0.11f, 0.55f);
    private static readonly Color ChoiceBgDim   = new Color(0.10f, 0.09f, 0.11f, 0.25f);
    private static readonly Color ConfirmBg     = new Color(0.20f, 0.17f, 0.12f, 0.95f);

    // ============================================================
    // 생성 — NodeSystem 이 캔버스 아래에 런타임 인스턴스화
    // ============================================================
    public static EventPanel CreateUnder(Transform canvas)
    {
        if (canvas == null)
        {
            Debug.LogWarning("[EventPanel] 부모 캔버스가 없어 생성할 수 없습니다.");
            return null;
        }
        var go = new GameObject("EventPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
        go.layer = canvas.gameObject.layer;
        go.transform.SetParent(canvas, false);
        return go.AddComponent<EventPanel>(); // Awake → BuildUI
    }

    protected override void Awake()
    {
        if (!_built) BuildUI();
        base.Awake(); // canvasGroup 을 잡아 alpha=0 으로 숨김
    }

    // ============================================================
    // 열기 — 무작위 이벤트 바인딩 후 페이드 인
    // ============================================================
    public void OpenRandom()
    {
        int floor = (NodeSystem.Current != null ? NodeSystem.Current.CurrentFloor : 0) + 1; // 1-base
        var evt = EventCatalog.GetRandom(floor);
        if (evt == null)
        {
            Debug.LogWarning("[EventPanel] 표시할 이벤트가 없습니다 — 즉시 종료.");
            OnExit?.Invoke();
            return;
        }
        Bind(evt);

        // 팝업이 아직 보이지 않는(alpha 0) 이 시점에 뒤 화면을 캡처→블러→표시한 뒤 페이드 인.
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (_openRoutine != null) StopCoroutine(_openRoutine);
        if (isActiveAndEnabled) _openRoutine = StartCoroutine(CaptureBlurThenOpen());
        else Open(); // 비활성 폴백 (블러 없이)
    }

    /// <summary>이번 프레임 렌더 후(팝업 미표시 상태) 화면을 캡처·블러해 배경에 깔고 페이드 인.</summary>
    private IEnumerator CaptureBlurThenOpen()
    {
        yield return new WaitForEndOfFrame(); // 팝업이 그려지기 전 프레임(=뒤 화면)을 확보

        Texture2D shot = null;
        try { shot = ScreenCapture.CaptureScreenshotAsTexture(); }
        catch (Exception e) { Debug.LogWarning($"[EventPanel] 화면 캡처 실패 — 단색 딤으로 대체. {e.Message}"); }

        if (shot != null && _blurImage != null)
        {
            ReleaseBlur();
            _blurRt = BuildBlur(shot);
            _blurImage.texture = _blurRt;
            _blurImage.color   = BlurTint;   // 블러 위 어두운 틴트
            Destroy(shot);
        }
        // 캡처 실패 시 _blurImage 는 DimColor(단색 딤) 유지

        _openRoutine = null;
        Open(); // PanelBase 페이드 인
    }

    private void Bind(EventDefinition evt)
    {
        _current = evt;

        if (_titleText != null) Loc.Set(_titleText, evt.title);
        if (_bodyText  != null) Loc.Set(_bodyText, evt.bodyText);

        // 이전 선택지 정리
        foreach (var b in _choiceButtons) if (b != null) Destroy(b);
        _choiceButtons.Clear();

        // 선택지 버튼 생성
        var choices = evt.choices ?? new List<EventChoice>();
        foreach (var choice in choices)
        {
            var btnGo = BuildChoiceButton(choice);
            _choiceButtons.Add(btnGo);
        }

        if (_choiceRow != null) _choiceRow.gameObject.SetActive(true);
        if (_confirmButton != null) _confirmButton.gameObject.SetActive(false);
    }

    // ── 선택지 클릭 ─────────────────────────────────────────────
    private void OnChoiceClicked(EventChoice choice)
    {
        if (choice == null || _current == null) return;

        var outcome = EventService.ResolveChoice(_current, choice);

        // 결과 텍스트 + 효과 요약을 결과 창에 표시 (상세 수치는 여기, 조사관 수첩은 기본 양식만).
        string result = outcome != null && !string.IsNullOrEmpty(outcome.resultText)
            ? outcome.resultText
            : "변화 없음.";
        var effects = LocalizedMessage.Join("\n", EventService.LastEffectSummary);
        Loc.Bind(_bodyText, () => Loc.Tr(result) + (string.IsNullOrEmpty(effects.Render()) ? "" : "\n\n" + effects.Render()));

        // 선택지 숨기고 확인 버튼 노출
        if (_choiceRow != null) _choiceRow.gameObject.SetActive(false);
        if (_confirmButton != null) _confirmButton.gameObject.SetActive(true);
    }

    private void OnConfirm()
    {
        OnExit?.Invoke();
        Close();
    }

    // ============================================================
    // UI 구성 (런타임)
    // ============================================================
    private void BuildUI()
    {
        _built = true;
        _font  = ResolveFont();

        // ── 루트 = 전체 화면 블러 배경(모달). 캡처 전엔 단색 딤으로 폴백 ──
        var rootRt = (RectTransform)transform;
        Stretch(rootRt, Vector2.zero, Vector2.one);
        _blurImage = gameObject.AddComponent<RawImage>();
        _blurImage.color = DimColor;      // 텍스처 없을 때: 반투명 검정 딤
        _blurImage.raycastTarget = true;  // 뒤(노드맵) 클릭 차단

        // ── 중앙 프레임 = 배경 이미지 원본 비율 유지 + 화면에 가득(FitInParent) ──
        var frame = NewUI("Frame", transform);
        var frameRt = frame.rt;
        frameRt.anchorMin = frameRt.anchorMax = new Vector2(0.5f, 0.5f);
        frameRt.pivot     = new Vector2(0.5f, 0.5f);

        var sprite = LoadPanelSprite();
        var frameImg = frame.go.AddComponent<Image>();
        frameImg.sprite = sprite;
        frameImg.type   = Image.Type.Simple;
        frameImg.color  = sprite != null ? Color.white : new Color(0.06f, 0.05f, 0.08f, 0.98f);
        frameImg.raycastTarget = true;

        // 원본 비율(가로/세로)을 유지하며 부모(전체 화면)에 맞춰 최대 크기로 채운다.
        float aspect = (sprite != null && sprite.rect.height > 0f)
            ? sprite.rect.width / sprite.rect.height
            : 989f / 656f; // 폴백: panel_1 원본 비율
        var fitter = frame.go.AddComponent<AspectRatioFitter>();
        fitter.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = aspect;

        // ── 내용 컨테이너 — 프레임 테두리 '안쪽'에 인셋 (좌우 12% / 상하 10%) ──
        var content = NewUI("Content", frame.go.transform);
        Stretch(content.rt, new Vector2(0.12f, 0.10f), new Vector2(0.88f, 0.90f));
        var vlg = content.go.AddComponent<VerticalLayoutGroup>();
        vlg.padding          = new RectOffset(24, 24, 24, 24);
        vlg.spacing          = 26;
        vlg.childAlignment   = TextAnchor.UpperCenter;
        vlg.childControlWidth  = true;  vlg.childControlHeight  = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        // 제목
        _titleText = NewText("Title", content.go.transform, 50, FontStyles.Bold, TextColor, TextAlignmentOptions.Center);
        SetLayout(_titleText.gameObject, minHeight: 72, flexibleHeight: 0);

        // 본문 (상황 텍스트 / 결과 텍스트 공용) — 남는 세로 공간을 차지
        _bodyText = NewText("Body", content.go.transform, 32, FontStyles.Normal, BodyColor, TextAlignmentOptions.Top);
        _bodyText.enableAutoSizing = true; _bodyText.fontSizeMin = 22; _bodyText.fontSizeMax = 40;
        SetLayout(_bodyText.gameObject, minHeight: 160, flexibleHeight: 1);

        // 선택지 버튼 행 (가로 정렬)
        var row = NewUI("ChoiceRow", content.go.transform);
        _choiceRow = row.rt;
        var hlg = row.go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing          = 28;
        hlg.childAlignment   = TextAnchor.LowerCenter;
        hlg.childControlWidth  = true;  hlg.childControlHeight  = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
        SetLayout(row.go, minHeight: 420, flexibleHeight: 0);

        // 확인(다음 층) 버튼 — 선택 후 노출
        _confirmButton = BuildConfirmButton(content.go.transform, out _confirmLabel);
        _confirmButton.gameObject.SetActive(false);
    }

    /// <summary>선택지 버튼 1개: [더미 이미지] 위 + [선택지 설명] 아래. (첨부 사진 구조)</summary>
    private GameObject BuildChoiceButton(EventChoice choice)
    {
        bool affordable = EventService.CanAfford(choice);

        var btn = NewUI("ChoiceButton", _choiceRow.transform);
        var bg = btn.go.AddComponent<Image>();
        bg.color = affordable ? ChoiceBg : ChoiceBgDim;
        var button = btn.go.AddComponent<Button>();
        button.targetGraphic = bg;
        button.interactable = affordable;

        // 모든 선택지 버튼을 '동일 폭'으로 통일한다.
        //   원인: HorizontalLayoutGroup(childForceExpandWidth)은 각 버튼의 preferredWidth
        //         (= 내부 VLG 가 보고하는 라벨 폭)만큼 먼저 배분하고 '남는 폭'만 균등 분배한다.
        //         라벨 길이가 제각각이라 버튼 폭이 달라졌다.
        //   해결: LayoutElement 로 폭 기여를 (preferredWidth=0, flexibleWidth=1) 로 덮어써
        //         라벨 길이와 무관하게 행 폭을 균등 분할 → 개수(1/2/3…) 상관없이 항상 균일.
        var sizeLe = btn.go.AddComponent<LayoutElement>();
        sizeLe.minWidth = 0; sizeLe.preferredWidth = 0; sizeLe.flexibleWidth = 1;

        // 버튼 내부 세로 정렬 (이미지 → 설명). 프레임이 커진 만큼 여백/크기 상향.
        var vlg = btn.go.AddComponent<VerticalLayoutGroup>();
        vlg.padding        = new RectOffset(18, 18, 22, 22);
        vlg.spacing        = 18;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth  = true;  vlg.childControlHeight  = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        // ① 더미 이미지 (추후 교체용 UI Image) — 회색 플레이스홀더
        var img = NewUI("ChoiceImage", btn.go.transform);
        var imgComp = img.go.AddComponent<Image>();
        imgComp.color = DummyFill;                 // 더미: 스프라이트 비움
        var outline = img.go.AddComponent<Outline>();
        outline.effectColor = DummyBorder;
        outline.effectDistance = new Vector2(2f, 2f);
        var imgLe = img.go.AddComponent<LayoutElement>();
        imgLe.minHeight = 200; imgLe.preferredHeight = 220; imgLe.flexibleHeight = 0;

        // ② 선택지 설명 (선택지 label)
        var label = NewText("Label", btn.go.transform, 30, FontStyles.Normal, TextColor, TextAlignmentOptions.Center);
        Loc.Bind(label, () => Loc.Tr(choice.label) + (!affordable && choice.SoulStoneCost > 0
            ? "\n<size=70%><color=#C0554E>" + Loc.Tr("(영혼석 {0} 필요)", choice.SoulStoneCost) + "</color></size>" : ""));
        SetLayout(label.gameObject, minHeight: 116, flexibleHeight: 0);

        button.onClick.AddListener(() => OnChoiceClicked(choice));
        return btn.go;
    }

    private Button BuildConfirmButton(Transform parent, out TMP_Text label)
    {
        var go = NewUI("ConfirmButton", parent);
        var bg = go.go.AddComponent<Image>();
        bg.color = ConfirmBg;
        var button = go.go.AddComponent<Button>();
        button.targetGraphic = bg;
        SetLayout(go.go, minHeight: 92, flexibleHeight: 0);

        label = NewText("Label", go.go.transform, 32, FontStyles.Bold, TextColor, TextAlignmentOptions.Center);
        label.text = "다음 층으로";
        Stretch(label.rectTransform, Vector2.zero, Vector2.one);

        button.onClick.AddListener(OnConfirm);
        return button;
    }

    // ============================================================
    // UI 헬퍼
    // ============================================================
    private struct UINode { public GameObject go; public RectTransform rt; }

    private static UINode NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return new UINode { go = go, rt = (RectTransform)go.transform };
    }

    private TMP_Text NewText(string name, Transform parent, float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        var node = NewUI(name, parent);
        var t = node.go.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.fontSize      = size;
        t.fontStyle     = style;
        t.color         = color;
        t.alignment     = align;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    private static void SetLayout(GameObject go, float minHeight, float flexibleHeight)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minHeight = minHeight;
        le.flexibleHeight = flexibleHeight;
    }

    private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>Resources/UI/panel_1 을 Sprite 로 로드. Texture 로만 임포트된 경우 런타임 Sprite 생성 폴백.</summary>
    private static Sprite LoadPanelSprite()
    {
        var sp = Resources.Load<Sprite>("UI/panel_1");
        if (sp != null) return sp;

        var tex = Resources.Load<Texture2D>("UI/panel_1");
        if (tex != null)
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

        Debug.LogWarning("[EventPanel] Resources/UI/panel_1 을 찾지 못했습니다 — 단색 배경으로 대체.");
        return null;
    }

    /// <summary>씬의 기존 TMP 에서 한글 폰트를 확보 (LocationToast 와 동일 방식).</summary>
    private static TMP_FontAsset ResolveFont()
    {
        var anyTmp = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
        return anyTmp != null ? anyTmp.font : null;
    }

    // ============================================================
    // 블러 — 캡처 텍스처를 셰이더 없이 다단계 다운샘플(바이리니어)로 흐린다
    // ============================================================
    /// <summary>src 를 절반씩 축소(바이리니어 = 2×2 평균)해 소형 RT 로 만든다. RawImage 가 이를 전체 화면으로 업스케일하면 부드러운 블러가 된다.</summary>
    private static RenderTexture BuildBlur(Texture2D src)
    {
        int w = Mathf.Max(1, src.width);
        int h = Mathf.Max(1, src.height);

        RenderTexture cur = RenderTexture.GetTemporary(w, h, 0);
        cur.filterMode = FilterMode.Bilinear;
        Graphics.Blit(src, cur);

        // 최소 변이 ~120px 가 될 때까지 절반씩 (누적 박스 블러)
        while (w > 120 && h > 120)
        {
            w = Mathf.Max(1, w / 2);
            h = Mathf.Max(1, h / 2);
            var next = RenderTexture.GetTemporary(w, h, 0);
            next.filterMode = FilterMode.Bilinear;
            Graphics.Blit(cur, next);
            RenderTexture.ReleaseTemporary(cur);
            cur = next;
        }

        // 표시용 영구 RT 로 복사(임시 RT 는 반납)
        var outRt = new RenderTexture(w, h, 0) { filterMode = FilterMode.Bilinear };
        Graphics.Blit(cur, outRt);
        RenderTexture.ReleaseTemporary(cur);
        return outRt;
    }

    /// <summary>블러 RT 해제 + 배경을 단색 딤으로 되돌림.</summary>
    private void ReleaseBlur()
    {
        if (_blurImage != null) _blurImage.texture = null;
        if (_blurRt != null)
        {
            _blurRt.Release();
            Destroy(_blurRt);
            _blurRt = null;
        }
    }

    protected override void OnClosed()
    {
        // 닫힐 때 블러 RT 를 반납해 메모리 누수 방지. 다음 열림에서 새로 캡처한다.
        ReleaseBlur();
        if (_blurImage != null) _blurImage.color = DimColor;
    }

    private void OnDestroy()
    {
        if (_openRoutine != null) { StopCoroutine(_openRoutine); _openRoutine = null; }
        ReleaseBlur();
    }
}
