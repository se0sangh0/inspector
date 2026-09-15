// ============================================================
// Log/InvestigatorNotebookController.cs
// 조사관 수첩 — 런 기록 열람 + 타이핑 연출/스킵 + 페이지 (P0-05)
// ============================================================
//
// [이 파일이 하는 일]
//   한 런 동안 누적된 사건 기록(RunSessionManager.Records)을
//   페이지 단위로 열람하는 수첩 UI 입니다.
//   노드맵에서 [조사관 수첩] 버튼으로 상시 열 수 있습니다.
//
// [계약 — 16-A §5 조사관 수첩 / 16-B §6]
//   - 공식 명칭은 조사관 수첩. 상단 고정 세로 패드(간이 구현: 상단 앵커 패널).
//   - 한 런 동안 기록 누적, 1개 런당 약 5페이지 내외.
//   - 새 기록은 한 글자씩 작성. 작성 중 노트 옆 빈 공간 클릭·스페이스 키로
//     타이핑을 즉시 완료해 해당 페이지 전체 문안을 표시한다.
//   - 다음 이동·사건 선택 전에 열어 이전 기록을 볼 수 있다 (상시 허용).
//   - 정답·오답·추천·확률·위험 등급을 판단하지 않는다 (원자료만 표시).
//   - 수치 행동 로그(GameLogService)와 분리 — Records 만 읽는다.
//     준 피해·받은 피해·실드 등 전투 수치는 애초에 Records 에 없다.
//
// [미구현 — 미관/후속]
//   종이 아래→위 넘김 애니메이션은 즉시 페이지 전환으로 간이 구현.
//   (16 §4: 임시 에셋·간이 연출 허용. 최종 연출은 P1-02.)
//
// 사용:
//   InvestigatorNotebookController.EnsureOpenButton(canvas); // 노드맵 Start
//   InvestigatorNotebookController.Open();                   // 열기
// ============================================================

using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class InvestigatorNotebookController : MonoBehaviour
{
    public static InvestigatorNotebookController Instance { get; private set; }

    private const int EntriesPerPage = 4;

    private CanvasGroup _group;
    private GameObject  _root;
    private TMP_Text    _headerText;
    private TMP_Text    _bodyText;
    private Button      _prevButton, _nextButton;

    private readonly List<string> _pages = new(); // 각 페이지 본문 문자열
    private int       _pageIndex;
    private Coroutine _typeRoutine;
    private bool      _typing;

    private static readonly Color Paper = new Color(0.14f, 0.13f, 0.11f, 0.99f);
    private static readonly Color Ink   = new Color(0.88f, 0.85f, 0.78f, 1f);
    private static readonly Color Tab   = new Color(0.30f, 0.26f, 0.18f, 1f);

    // ── 열기 버튼 (노드맵 상시 접근) ──────────────────────────────
    /// <summary>노드맵 캔버스에 [조사관 수첩] 열기 버튼을 1회 생성한다 (이미 있으면 무시).</summary>
    public static void EnsureOpenButton(Transform canvas)
    {
        if (canvas == null) return;
        if (canvas.Find("NotebookOpenButton") != null) return;

        var font = TMP_Settings.defaultFontAsset;
        var go = new GameObject("NotebookOpenButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas, false);
        go.layer = canvas.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -24f);
        rt.sizeDelta = new Vector2(280f, 56f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.16f, 0.12f, 0.95f);
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0.55f, 0.48f, 0.32f, 1f); ol.effectDistance = new Vector2(2, 2);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(Open);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = Loc.Tr("조사관 수첩"); label.fontSize = 24; label.color = Ink;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
    }

    // ── 열기 ─────────────────────────────────────────────────────
    public static void Open()
    {
        Ensure();
        Instance._Open();
    }

    private static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("InvestigatorNotebook").AddComponent<InvestigatorNotebookController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
        LocalizationManager.OnLanguageChanged += RefreshLanguage;
    }

    private void OnDestroy() => LocalizationManager.OnLanguageChanged -= RefreshLanguage;

    private void RefreshLanguage()
    {
        if (_root == null || !_root.activeSelf) return;
        bool animate = _typing;
        int page = _pageIndex;
        RebuildPages();
        ShowPage(page, animate);
    }

    private void _Open()
    {
        RebuildPages();
        _pageIndex = 0;
        _root.SetActive(true);
        Loc.Localize(_root); // 이전/다음/닫기 등 정적 라벨을 현재 언어로 (본문·헤더는 Tr로 이미 현재 언어)
        _group.alpha = 1f; _group.blocksRaycasts = true;
        ShowPage(0, animate: true);
    }

    private void Close()
    {
        if (_typeRoutine != null) { StopCoroutine(_typeRoutine); _typeRoutine = null; }
        _typing = false;
        _group.alpha = 0f; _group.blocksRaycasts = false;
        _root.SetActive(false);
    }

    private void Update()
    {
        if (_group == null || !_group.blocksRaycasts) return;
        // 스페이스 = 타이핑 즉시 완료 (작성 중일 때만)
        if (_typing && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            SkipTyping();
    }

    // ── 페이지 데이터 구성 ────────────────────────────────────────
    private void RebuildPages()
    {
        _pages.Clear();
        var s = RunSessionManager.Instance;
        var entries = s != null && s.Records != null ? s.Records.Entries : null;

        if (entries == null || entries.Count == 0)
        {
            _pages.Add(Loc.Tr("아직 기록된 조사 내용이 없다."));
            return;
        }

        var sb = new StringBuilder();
        int onPage = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            sb.Append(FormatEntry(e));
            onPage++;
            bool last = (i == entries.Count - 1);
            if (onPage >= EntriesPerPage || last)
            {
                _pages.Add(sb.ToString().TrimEnd());
                sb.Clear();
                onPage = 0;
            }
            else sb.AppendLine();
        }
    }

    /// <summary>
    /// 사건 기록 1건을 탐사국 공식 현장 기록 양식으로 포맷 (12 §1-5).
    /// 헤더 = [O층 | 제 N구역] (O=층, N=노드 선택 위치 왼1/중2/오3).
    /// 표제(전투 결과 등)가 있으면 헤더 아래 한 줄, 이어서 항목당 한 줄 (불릿 없음).
    /// 판정·추천·내부 식별자를 넣지 않는다.
    /// </summary>
    private static string FormatEntry(RunRecordEntry e)
    {
        var sb = new StringBuilder();
        sb.AppendLine(e.node > 0
            ? Loc.Tr("[{0}층 | 제 {1}구역]", e.floor, e.node)
            : Loc.Tr("[{0}층 | 현장 기록]", e.floor));
        if (e.title != null)
            sb.AppendLine(e.title.Render());          // 전투: "고블린 2체 사살"/"Slew ..." 등 (§1-4)
        foreach (var line in e.lines)
            sb.AppendLine(line.Render());             // 항목당 한 줄 (§1-5)
        return sb.ToString();
    }

    // ── 페이지 표시 + 타이핑 연출 ─────────────────────────────────
    private void ShowPage(int index, bool animate)
    {
        _pageIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _pages.Count - 1));

        int run = RunSessionManager.Instance != null ? RunSessionManager.Instance.CurrentRunNumber : 1;
        if (_headerText != null)
            _headerText.text = Loc.Tr("조사관 수첩 — 제 {0}차 탐사   ({1}/{2})", run, _pageIndex + 1, _pages.Count);

        string body = _pages.Count > 0 ? _pages[_pageIndex] : "";
        if (_bodyText != null)
        {
            _bodyText.text = body;
            _bodyText.ForceMeshUpdate();
        }

        UpdateNavButtons();

        if (_typeRoutine != null) { StopCoroutine(_typeRoutine); _typeRoutine = null; }
        if (animate && _bodyText != null && body.Length > 0)
            _typeRoutine = StartCoroutine(TypeReveal(_bodyText.textInfo.characterCount));
        else
            SkipTyping();
    }

    // maxVisibleCharacters 를 0→전체로 늘려 한 글자씩 작성 (문자열 재슬라이스 없이 가볍게)
    private IEnumerator TypeReveal(int totalChars)
    {
        _typing = true;
        _bodyText.maxVisibleCharacters = 0;
        int shown = 0;
        // 페이지가 길어도 답답하지 않도록 프레임당 최소 1자, 대략 60자/초
        const float charsPerSecond = 60f;
        float acc = 0f;
        while (shown < totalChars)
        {
            acc += Time.unscaledDeltaTime * charsPerSecond;
            int step = Mathf.FloorToInt(acc);
            if (step > 0)
            {
                acc -= step;
                shown = Mathf.Min(totalChars, shown + step);
                _bodyText.maxVisibleCharacters = shown;
            }
            yield return null;
        }
        _bodyText.maxVisibleCharacters = totalChars;
        _typing = false;
        _typeRoutine = null;
    }

    /// <summary>타이핑 즉시 완료 — 전체 문안 표시 (빈 공간 클릭·스페이스).</summary>
    private void SkipTyping()
    {
        if (_typeRoutine != null) { StopCoroutine(_typeRoutine); _typeRoutine = null; }
        if (_bodyText != null) _bodyText.maxVisibleCharacters = int.MaxValue;
        _typing = false;
    }

    private void OnBackdropClick()
    {
        // 노트 옆 빈 공간 클릭 — 작성 중이면 즉시 완료. (닫기는 별도 버튼)
        if (_typing) SkipTyping();
    }

    private void OnPrev()
    {
        if (_typing) { SkipTyping(); return; } // 작성 중 첫 입력은 완료 우선
        if (_pageIndex > 0) ShowPage(_pageIndex - 1, animate: false);
    }

    private void OnNext()
    {
        if (_typing) { SkipTyping(); return; }
        if (_pageIndex < _pages.Count - 1) ShowPage(_pageIndex + 1, animate: true);
    }

    private void UpdateNavButtons()
    {
        if (_prevButton != null) _prevButton.interactable = _pageIndex > 0;
        if (_nextButton != null) _nextButton.interactable = _pageIndex < _pages.Count - 1;
    }

    // ── UI 구성 (런타임 자체 생성) ────────────────────────────────
    private void Build()
    {
        var canvasGo = new GameObject("NotebookCanvas", typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        ResponsiveUi.Configure(canvas);
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10030; // 이벤트/용병소 패널 위 (상시 열람)
        _group = canvasGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f; _group.blocksRaycasts = false;

        var font = TMP_Settings.defaultFontAsset;

        _root = NewUI("Notebook", canvasGo.transform);
        Stretch((RectTransform)_root.transform);

        // 빈 공간(딤) — 클릭 시 타이핑 완료 (닫기는 아님)
        var backdrop = NewImage("Backdrop", _root.transform);
        Stretch(backdrop.rectTransform);
        backdrop.color = new Color(0f, 0f, 0f, 0.55f);
        var bdBtn = backdrop.gameObject.AddComponent<Button>();
        bdBtn.transition = Selectable.Transition.None;
        bdBtn.onClick.AddListener(OnBackdropClick);

        // 상단 고정 세로 패드 (상단 앵커 패널)
        var pad = NewImage("Pad", _root.transform);
        var padRt = pad.rectTransform;
        padRt.anchorMin = new Vector2(0.5f, 1f); padRt.anchorMax = new Vector2(0.5f, 1f); padRt.pivot = new Vector2(0.5f, 1f);
        padRt.anchoredPosition = new Vector2(0f, -20f);
        padRt.sizeDelta = new Vector2(940f, 840f);
        pad.color = Paper;
        pad.raycastTarget = true; // 패드 클릭이 backdrop 으로 새지 않게
        var padOl = pad.gameObject.AddComponent<Outline>();
        padOl.effectColor = new Color(0.5f, 0.44f, 0.3f, 1f); padOl.effectDistance = new Vector2(2, 2); padOl.useGraphicAlpha = false;

        // 상단 접착 탭 (장식)
        var tab = NewImage("Tab", pad.transform);
        var tabRt = tab.rectTransform;
        tabRt.anchorMin = new Vector2(0.5f, 1f); tabRt.anchorMax = new Vector2(0.5f, 1f); tabRt.pivot = new Vector2(0.5f, 0f);
        tabRt.anchoredPosition = new Vector2(0f, -2f); tabRt.sizeDelta = new Vector2(160f, 24f);
        tab.color = Tab; tab.raycastTarget = false;

        // 헤더
        _headerText = NewText("Header", pad.transform, "", font, 26, Ink, FontStyles.Bold);
        var hrt = (RectTransform)_headerText.transform;
        hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1); hrt.pivot = new Vector2(0.5f, 1);
        hrt.anchoredPosition = new Vector2(0, -28); hrt.offsetMin = new Vector2(36, hrt.offsetMin.y); hrt.offsetMax = new Vector2(-36, hrt.offsetMax.y);
        hrt.sizeDelta = new Vector2(hrt.sizeDelta.x, 40);
        _headerText.alignment = TextAlignmentOptions.Left; _headerText.raycastTarget = false;

        // 본문
        _bodyText = NewText("Body", pad.transform, "", font, 22, Ink, FontStyles.Normal);
        var brt = (RectTransform)_bodyText.transform;
        brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1);
        brt.offsetMin = new Vector2(40, 84); brt.offsetMax = new Vector2(-40, -80);
        _bodyText.alignment = TextAlignmentOptions.TopLeft; _bodyText.enableWordWrapping = true; _bodyText.raycastTarget = false;

        // 페이지 넘김 + 닫기 (하단)
        _prevButton = NewButton("Prev", pad.transform, "이전", font, new Vector2(-200, 20));
        _prevButton.onClick.AddListener(OnPrev);
        _nextButton = NewButton("Next", pad.transform, "다음", font, new Vector2(0, 20));
        _nextButton.onClick.AddListener(OnNext);
        var closeBtn = NewButton("Close", pad.transform, "닫기", font, new Vector2(200, 20));
        closeBtn.onClick.AddListener(Close);

        _root.SetActive(false);
    }

    // ── 헬퍼 ──
    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
    private Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }
    private TMP_Text NewText(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
        return t;
    }
    private Button NewButton(string name, Transform parent, string label, TMP_FontAsset font, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(170, 56); rt.anchoredPosition = anchoredPos;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.22f, 0.19f, 0.14f, 1f);
        var btn = go.AddComponent<Button>();
        var labelText = NewText(name + "Label", go.transform, label, font, 24, Ink, FontStyles.Bold);
        Stretch((RectTransform)labelText.transform); labelText.alignment = TextAlignmentOptions.Center; labelText.raycastTarget = false;
        return btn;
    }
    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
