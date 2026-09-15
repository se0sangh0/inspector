// ============================================================
// Opening/OpeningFlowController.cs
// 임시 오프닝 — 실패 보고서 2문서 타자기 연출 (P0-06)
// ============================================================
//
// [이 파일이 하는 일]
//   검은 바탕에 임시 실패 보고서 2장을 한 글자씩(타자기) 표시합니다.
//   플레이어가 아무 입력이나 하면 즉시 다음으로 넘어가며,
//   두 문서를 모두 넘기면(또는 스킵하면) onComplete 를 1회 호출합니다.
//
// [계약 — 16-A §1·§6 / 16-B §5·§6]
//   - 진입 모드는 저장하지 않는 OnboardingEntryMode 로 구분한다.
//     FirstRun : 완료·스킵 시 이번 실행의 완료 상태 기록 → 본 런으로 이어짐 (호출측 처리)
//     Review   : 완료 플래그를 쓰지 않고 타이틀로 복귀 (호출측 처리)
//   - 검은 바탕, 고정폭 타자기 글꼴과 한 글자 작성 효과.
//   - 플레이어가 아무 입력이나 하면 즉시 스킵한다.
//   - 오프닝은 완료 상태·런 데이터를 직접 바꾸지 않는다 (플래그 저장은 호출측).
//
// [문안 — 16-A §6 프로토타입 임시 문안]
//   문서1 이전 탐사 결과 / 문서2 후속 투입 통보.
//
// [미구현 — 미관/후속]
//   고정폭(모노스페이스) 전용 폰트·정보 마스킹 연출은 간이 생략(기본 폰트 사용).
//   (16 §4: 임시 에셋 허용.)
//
// 사용:
//   OpeningFlowController.Show(OnboardingEntryMode.FirstRun, onComplete);
//   - 씬 배치 불필요 (자체 생성, DontDestroyOnLoad). 최상단 오버레이.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>오프닝 진입 모드 — 저장하지 않는 1회용 전달값 (16-A §1).</summary>
public enum OnboardingEntryMode
{
    FirstRun = 0,  // 이번 실행 최초 진입 — 완료 시 본 런
    Review   = 1,  // 타이틀에서 재열람 — 완료 시 타이틀 복귀
}

public class OpeningFlowController : MonoBehaviour
{
    public static OpeningFlowController Instance { get; private set; }

    // 문서 (제목, 본문) — 16-A §6
    private static readonly (string title, string body)[] Documents =
    {
        ("이전 탐사 결과",   "이전 탐사대와의 연락이 두절되었다.\n해당 임무는 실패로 종결한다."),
        ("후속 투입 통보",   "후속 투입 절차가 승인되었다.\n교전 수칙 숙지 후 즉시 현장으로 이동하라."),
    };

    private CanvasGroup _group;
    private GameObject  _root;
    private TMP_Text    _titleText, _bodyText, _hintText;
    private OnboardingEntryMode _mode;
    private System.Action _onComplete;
    private int  _docIndex;
    private Coroutine _typeRoutine;
    private bool _typing;
    private bool _finished;
    private float _inputReadyAt;
    private int _lastInputFrame = -1;

    private static readonly Color Ink = new Color(0.85f, 0.87f, 0.82f, 1f);

    public static void Show(OnboardingEntryMode mode, System.Action onComplete)
    {
        Ensure();
        Instance._Show(mode, onComplete);
    }

    private static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("OpeningFlowController").AddComponent<OpeningFlowController>();
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
        if (_finished || _root == null || !_root.activeSelf) return;
        bool wasTyping = _typing;
        ShowDoc(_docIndex);
        if (!wasTyping) CompleteTyping();
    }

    private void _Show(OnboardingEntryMode mode, System.Action onComplete)
    {
        _mode       = mode;
        _onComplete = onComplete;
        _finished   = false;
        _docIndex   = 0;
        _inputReadyAt = Time.unscaledTime + 0.2f;

        _root.SetActive(true);
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        ShowDoc(0);
    }

    private void Update()
    {
        if (_group == null || !_group.blocksRaycasts || _finished || Time.unscaledTime < _inputReadyAt) return;
        bool submit = Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame
            || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
        submit |= Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        if (submit) OnInput(); // 마우스/터치는 배경 버튼이 처리한다.
    }

    private void OnInput()
    {
        if (_finished || Time.unscaledTime < _inputReadyAt || _lastInputFrame == Time.frameCount) return;
        _lastInputFrame = Time.frameCount; // Submit and a UI click must not advance twice in one frame.
        if (_typing) { CompleteTyping(); return; } // 작성 중 → 즉시 전체 표시
        AdvanceDoc();                              // 이미 표시됨 → 다음 문서 / 종료
    }

    private void AdvanceDoc()
    {
        if (_docIndex < Documents.Length - 1)
            ShowDoc(_docIndex + 1);
        else
            Finish();
    }

    private void Finish()
    {
        if (_finished) return;
        _finished = true;
        var cb = _onComplete;
        _onComplete = null;
        Hide();
        cb?.Invoke(); // FirstRun: 이번 실행의 완료 상태 기록 + 본 런 / Review: 타이틀 복귀 (호출측)
    }

    private void Hide()
    {
        if (_typeRoutine != null) { StopCoroutine(_typeRoutine); _typeRoutine = null; }
        _typing = false;
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _root.SetActive(false);
    }

    // ── 문서 표시 + 타자기 ──
    private void ShowDoc(int index)
    {
        _docIndex = Mathf.Clamp(index, 0, Documents.Length - 1);
        var (title, body) = Documents[_docIndex];

        if (_titleText != null) _titleText.text = title;
        if (_bodyText  != null) _bodyText.text  = body;
        if (_hintText  != null)
            _hintText.text = _docIndex < Documents.Length - 1
                ? "화면을 누르거나 확인 키를 눌러 계속"
                : (_mode == OnboardingEntryMode.Review ? "확인하면 타이틀로 돌아갑니다" : "확인하면 현장으로 이동합니다");

        // 정적 문안(제목·본문·안내)을 현재 언어로 교체한 뒤 타자기 연출 시작
        Loc.Localize(_root);

        if (_bodyText != null)
        {
            _bodyText.ForceMeshUpdate();
            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            _typeRoutine = StartCoroutine(TypeReveal(_bodyText.textInfo.characterCount));
        }
    }

    private IEnumerator TypeReveal(int totalChars)
    {
        _typing = true;
        _bodyText.maxVisibleCharacters = 0;
        int shown = 0;
        const float charsPerSecond = 28f; // 타자기 — 보고서 톤에 맞춰 느리게
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

    private void CompleteTyping()
    {
        if (_typeRoutine != null) { StopCoroutine(_typeRoutine); _typeRoutine = null; }
        if (_bodyText != null) _bodyText.maxVisibleCharacters = int.MaxValue;
        _typing = false;
    }

    // ── UI 구성 (런타임 자체 생성) ──
    private void Build()
    {
        var canvasGo = new GameObject("OpeningCanvas", typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10100; // 오프닝은 최상단 (타이틀·보고서 위)
        ResponsiveUi.Configure(canvas);
        _group = canvasGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f; _group.blocksRaycasts = false;

        var font = TMP_Settings.defaultFontAsset;

        _root = new GameObject("Opening", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)_root.transform);

        // 검은 바탕 (입력 수신용 raycast target)
        var bg = _root.AddComponent<Image>();
        bg.color = Color.black;
        bg.raycastTarget = true;
        var advance = _root.AddComponent<Button>();
        advance.transition = Selectable.Transition.None;
        advance.onClick.AddListener(OnInput);

        // 제목 (마스킹 톤 — [기밀] 접두)
        _titleText = NewText("Title", _root.transform, font, 30, new Color(0.6f, 0.62f, 0.58f, 1f), FontStyles.Bold);
        var trt = (RectTransform)_titleText.transform;
        trt.anchorMin = new Vector2(0.5f, 0.5f); trt.anchorMax = new Vector2(0.5f, 0.5f); trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(0, 180); trt.sizeDelta = new Vector2(1160, 84);
        _titleText.alignment = TextAlignmentOptions.Center;

        // 본문
        _bodyText = NewText("Body", _root.transform, font, 32, Ink, FontStyles.Normal);
        var brt = (RectTransform)_bodyText.transform;
        brt.anchorMin = new Vector2(0.5f, 0.5f); brt.anchorMax = new Vector2(0.5f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0, 20); brt.sizeDelta = new Vector2(1160, 240);
        _bodyText.alignment = TextAlignmentOptions.Center;
        _bodyText.enableWordWrapping = true;

        // 하단 안내
        _hintText = NewText("Hint", _root.transform, font, 22, new Color(0.5f, 0.5f, 0.5f, 1f), FontStyles.Italic);
        var hrt = (RectTransform)_hintText.transform;
        hrt.anchorMin = new Vector2(0.5f, 0f); hrt.anchorMax = new Vector2(0.5f, 0f); hrt.pivot = new Vector2(0.5f, 0f);
        hrt.anchoredPosition = new Vector2(0, 60); hrt.sizeDelta = new Vector2(1160, 56);
        _hintText.alignment = TextAlignmentOptions.Center;

        _root.SetActive(false);
    }

    private TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = color; t.fontStyle = style;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
