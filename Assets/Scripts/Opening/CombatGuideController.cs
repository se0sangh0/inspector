// ============================================================
// Opening/CombatGuideController.cs
// 첫 전투 가이드 — 실행당 1회, 비차단 안내 (P0-06)
// ============================================================
//
// [이 파일이 하는 일]
//   실행당 첫 전투 진입 시 카드 사용 → 공용 스택 누적 → 동료 자동 행동
//   순서를 짧은 문구로 한 번 안내합니다. 진행을 멈추지 않습니다.
//
// [계약 — 16-A §1 첫 전투 가이드 / 16-B §5]
//   - 실행당 첫 전투 진입 시 한 번만 표시한다.
//   - 진행을 멈추지 않는다 (안내 표시·완료는 입력·상태 적용 조건이 아니다).
//   - 표시 여부는 메모리에 보관한다. 앱 재실행 시 초기화하고 새 탐사에서는 유지한다.
//   - 기존 PlayerPrefs 완료 값과 관계없이 실행마다 첫 일반 전투에서 표시한다.
//   - 별도 교전 교육 단계를 만들지 않는다 (튜토리얼 모드는 자체 안내 유지).
//
// [미구현 — 미관/후속]
//   해당 UI 요소(카드/스택/동료) 하이라이트는 간이 생략, 3단계 문구만 표시.
//   (16 §4: 임시 연출 허용. 하이라이트는 P1-02.)
//
// 사용:
//   CombatGuideController.TryShowOnce();  // BattleManager.OnEnable 에서 호출
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatGuideController : MonoBehaviour
{
    public static CombatGuideController Instance { get; private set; }

    private CanvasGroup _group;
    private GameObject  _root;
    private Coroutine   _routine;

    private const string GuideText =
        "전투 안내\n" +
        "① 손패의 카드를 클릭해 공용 스택을 쌓으세요.\n" +
        "② [턴 종료]로 이번 턴을 확정합니다.\n" +
        "③ 동료가 쌓인 스택에 맞춰 자동으로 행동합니다.";

    private const float AppearDelay = 1.4f; // 전투 로딩 커버가 걷힌 뒤 표시
    private const float HoldSeconds = 7f;   // 표시 유지 후 서서히 사라짐

    /// <summary>
    /// 실행당 첫 전투에서 1회만 가이드를 표시한다. 이미 표시했거나
    /// 튜토리얼 모드면 아무것도 하지 않는다 (비차단).
    /// </summary>
    public static void TryShowOnce()
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorial) return; // 튜토리얼 자체 안내
        if (RunSessionManager.IsCombatGuideCompleted()) return;

        RunSessionManager.MarkCombatGuideCompleted(); // 먼저 표시 확정 (재진입 중복 방지)
        Ensure();
        Instance._Show();
    }

    private static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("CombatGuideController").AddComponent<CombatGuideController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
    }

    private void _Show()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        // 비차단 — blocksRaycasts 는 계속 false (전투 입력을 막지 않는다)
        _root.SetActive(true);
        Loc.Localize(_root); // 가이드 문구를 현재 언어로
        _group.alpha = 0f;

        yield return new WaitForSeconds(AppearDelay);

        // 페이드 인
        yield return Fade(0f, 1f, 0.4f);
        yield return new WaitForSeconds(HoldSeconds);
        // 페이드 아웃
        yield return Fade(1f, 0f, 0.6f);

        _root.SetActive(false);
        _routine = null;
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        _group.alpha = to;
    }

    private void Build()
    {
        var canvasGo = new GameObject("CombatGuideCanvas", typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9900; // 전투 UI 위, 결과/보고서(9990+) 아래
        ResponsiveUi.Configure(canvas);
        _group = canvasGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable   = false;
        _group.blocksRaycasts = false; // 비차단 — 전투 입력 통과

        var font = TMP_Settings.defaultFontAsset;

        _root = new GameObject("Guide", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        // 상단 중앙 — 손패(하단)를 가리지 않도록
        var rrt = (RectTransform)_root.transform;
        rrt.anchorMin = new Vector2(0.5f, 1f); rrt.anchorMax = new Vector2(0.5f, 1f); rrt.pivot = new Vector2(0.5f, 1f);
        rrt.anchoredPosition = new Vector2(0f, -40f);
        rrt.sizeDelta = new Vector2(960f, 230f);

        var box = _root.AddComponent<Image>();
        box.color = new Color(0.06f, 0.07f, 0.10f, 0.92f);
        box.raycastTarget = false;
        var ol = _root.AddComponent<Outline>();
        ol.effectColor = new Color(0.55f, 0.60f, 0.70f, 1f); ol.effectDistance = new Vector2(2, 2); ol.useGraphicAlpha = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(_root.transform, false);
        var txrt = (RectTransform)textGo.transform;
        txrt.anchorMin = Vector2.zero; txrt.anchorMax = Vector2.one;
        txrt.offsetMin = new Vector2(24, 18); txrt.offsetMax = new Vector2(-24, -18);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = GuideText;
        text.fontSize = 24;
        text.color = new Color(0.90f, 0.92f, 0.96f, 1f);
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        Loc.Set(text, GuideText);

        _root.SetActive(false);
    }
}
