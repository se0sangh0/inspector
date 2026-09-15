// ============================================================
// UI/BattleResultScreen.cs  (2026-06-11 / 기획자 피드백 수정)
// 전투 결과 화면 — 승리 팝업(Panel_1 + 획득 재화, 아무 키/클릭 진행) / 패배 게임오버.
// ============================================================
//
// 사용:
//   BattleResultScreen.ShowVictory(영혼석, onNext);   // 아무 키/클릭 시 onNext (탐사 결과 팝업으로)
//   BattleResultScreen.ShowDefeat(onContinue);        // 클릭 시 onContinue (현재 미사용 — 전멸은 탐사 보고서 직행)
//   - 씬 배치 불필요(자체 생성, DontDestroyOnLoad). 최상단 오버레이.
//
// [기획자 피드백] 승리 팝업의 '다음으로' 버튼 삭제 — 아무 키나 누르면 탐사 결과 팝업이 뜬다.
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class BattleResultScreen : MonoBehaviour
{
    public static BattleResultScreen Instance { get; private set; }

    private CanvasGroup _group;
    private GameObject  _victory, _defeat;
    private TMP_Text    _rewardText;
    private System.Action _onNext, _onContinue;
    private Coroutine _autoRoutine;
    private float _victoryArmTime; // 이 시각 전 입력 무시 (전투 종료 키 입력이 즉시 넘기는 것 방지)

    // 패배(미사용) 경로만 자동 진행 유지. 승리는 아무 키/클릭 대기.
    private const float AutoAdvanceDelay = 2.5f;

    private static readonly Color Gold = new Color(1f, 0.84f, 0.4f, 1f);
    private static readonly Color Red  = new Color(0.86f, 0.12f, 0.12f, 1f);

    public static void ShowVictory(int soulstone, System.Action onNext)
    {
        Ensure(); Instance._ShowVictory(soulstone, onNext);
    }
    public static void ShowDefeat(System.Action onContinue)
    {
        Ensure(); Instance._ShowDefeat(onContinue);
    }

    private static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("BattleResultScreen").AddComponent<BattleResultScreen>();
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
        var canvasGo = new GameObject("ResultCanvas", typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        ResponsiveUi.Configure(canvas);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9990;
        _group = canvasGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f; _group.blocksRaycasts = false;

        var font = TMP_Settings.defaultFontAsset;

        // ── 승리 ──────────────────────────────────────────────
        _victory = NewUI("Victory", canvasGo.transform); Stretch((RectTransform)_victory.transform);
        // 딤 — 빈 공간 클릭으로는 진행하지 않는다 (뒤 화면 입력만 차단). 진행은 팝업(패널) 클릭·아무 키.
        var vdim = NewImage("Dim", _victory.transform); Stretch(vdim.rectTransform); vdim.color = new Color(0f, 0f, 0f, 0.6f); vdim.raycastTarget = true;

        var panel = NewImage("Panel", _victory.transform);
        var prt = panel.rectTransform; prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f); prt.sizeDelta = new Vector2(660, 460);
        var p1 = Resources.Load<Sprite>("UI/panel_1");
        if (p1 != null) { panel.sprite = p1; panel.type = Image.Type.Sliced; panel.color = Color.white; }
        else panel.color = new Color(0.12f, 0.12f, 0.16f, 0.98f);
        // 팝업(패널) 클릭 시 진행 — '다음으로' 버튼 대체 (기획자 피드백: 빈 공간 → 팝업 화면)
        var panelBtn = panel.gameObject.AddComponent<Button>(); panelBtn.transition = Selectable.Transition.None;
        panelBtn.onClick.AddListener(ProceedVictory);

        var vt = NewText("Title", panel.transform, "전투 승리", font, 50, Gold, FontStyles.Bold);
        var vtr = (RectTransform)vt.transform; vtr.anchorMin = new Vector2(0, 1); vtr.anchorMax = new Vector2(1, 1); vtr.pivot = new Vector2(0.5f, 1); vtr.sizeDelta = new Vector2(-40, 80); vtr.anchoredPosition = new Vector2(0, -44); vt.alignment = TextAlignmentOptions.Center;

        var rl = NewText("RewardLabel", panel.transform, "획득 재화", font, 28, new Color(0.82f, 0.82f, 0.9f), FontStyles.Normal);
        var rlr = (RectTransform)rl.transform; rlr.anchorMin = rlr.anchorMax = new Vector2(0.5f, 0.5f); rlr.pivot = new Vector2(0.5f, 0.5f); rlr.sizeDelta = new Vector2(560, 40); rlr.anchoredPosition = new Vector2(0, 50); rl.alignment = TextAlignmentOptions.Center;

        _rewardText = NewText("RewardAmount", panel.transform, "", font, 34, Gold, FontStyles.Bold);
        var rar = (RectTransform)_rewardText.transform; rar.anchorMin = rar.anchorMax = new Vector2(0.5f, 0.5f); rar.pivot = new Vector2(0.5f, 0.5f); rar.sizeDelta = new Vector2(580, 50); rar.anchoredPosition = new Vector2(0, -4); _rewardText.alignment = TextAlignmentOptions.Center;

        // '다음으로' 버튼 삭제 (기획자 피드백) — 대신 안내 문구 + 아무 키/클릭 진행.
        var vhint = NewText("Hint", panel.transform, "아무 키나 누르면 계속됩니다", font, 24, new Color(0.72f, 0.72f, 0.78f), FontStyles.Italic);
        var vhr = (RectTransform)vhint.transform; vhr.anchorMin = new Vector2(0.5f, 0f); vhr.anchorMax = new Vector2(0.5f, 0f); vhr.pivot = new Vector2(0.5f, 0f); vhr.sizeDelta = new Vector2(560, 36); vhr.anchoredPosition = new Vector2(0, 40); vhint.alignment = TextAlignmentOptions.Center; vhint.raycastTarget = false;

        // ── 패배(게임오버) ────────────────────────────────────
        _defeat = NewUI("Defeat", canvasGo.transform); Stretch((RectTransform)_defeat.transform);
        var ddim = NewImage("Dim", _defeat.transform); Stretch(ddim.rectTransform); ddim.color = new Color(0f, 0f, 0f, 0.9f); ddim.raycastTarget = true;
        var ddimBtn = ddim.gameObject.AddComponent<Button>(); ddimBtn.transition = Selectable.Transition.None;
        ddimBtn.onClick.AddListener(ProceedDefeat);

        var go = NewText("GameOver", _defeat.transform, "게임오버", font, 96, Red, FontStyles.Bold);
        var gor = (RectTransform)go.transform; gor.anchorMin = gor.anchorMax = new Vector2(0.5f, 0.5f); gor.pivot = new Vector2(0.5f, 0.5f); gor.sizeDelta = new Vector2(900, 160); gor.anchoredPosition = new Vector2(0, 20); go.alignment = TextAlignmentOptions.Center; go.raycastTarget = false;

        var hint = NewText("Hint", _defeat.transform, "잠시 후 계속됩니다 (클릭 시 즉시)", font, 26, new Color(0.7f, 0.7f, 0.7f), FontStyles.Normal);
        var hr = (RectTransform)hint.transform; hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f); hr.pivot = new Vector2(0.5f, 0.5f); hr.sizeDelta = new Vector2(600, 40); hr.anchoredPosition = new Vector2(0, -90); hint.alignment = TextAlignmentOptions.Center; hint.raycastTarget = false;

        _victory.SetActive(false);
        _defeat.SetActive(false);
    }

    private void _ShowVictory(int soul, System.Action onNext)
    {
        _onNext = onNext;
        if (_rewardText != null) Loc.Set(_rewardText, "영혼석 +{0}", soul);
        _victory.SetActive(true); _defeat.SetActive(false);
        Loc.Localize(_victory); // 정적 라벨(전투 승리·획득 재화·안내)을 현재 언어로
        _group.alpha = 1f; _group.blocksRaycasts = true;
        // 자동 진행 없음 — 아무 키/클릭 대기. 전투 종료 키 입력이 즉시 넘기지 않게 잠깐 아밍.
        _victoryArmTime = Time.unscaledTime + 0.4f;
    }

    // 승리 팝업 표시 중 — 아무 키나 누르면 진행 (기획자 피드백). 클릭은 딤 버튼이 처리.
    private void Update()
    {
        if (_victory == null || !_victory.activeSelf) return;
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            ProceedVictory();
    }

    private void _ShowDefeat(System.Action onContinue)
    {
        _onContinue = onContinue;
        _defeat.SetActive(true); _victory.SetActive(false);
        _group.alpha = 1f; _group.blocksRaycasts = true;
        RestartAuto(ProceedDefeat);
    }

    // 진행 — 아무 키/딤 클릭이 같은 경로 (중복은 activeSelf, 표시 직후 잔여 입력은 아밍 시각으로 차단)
    private void ProceedVictory()
    {
        if (_victory == null || !_victory.activeSelf) return;
        if (Time.unscaledTime < _victoryArmTime) return; // 표시 직후 잔여 키 입력 무시
        var cb = _onNext; Hide(); cb?.Invoke();
    }

    private void ProceedDefeat()
    {
        if (_defeat == null || !_defeat.activeSelf) return;
        var cb = _onContinue; Hide(); cb?.Invoke();
    }

    private void RestartAuto(System.Action proceed)
    {
        if (_autoRoutine != null) StopCoroutine(_autoRoutine);
        _autoRoutine = StartCoroutine(AutoAdvance(proceed));
    }

    private System.Collections.IEnumerator AutoAdvance(System.Action proceed)
    {
        yield return new WaitForSeconds(AutoAdvanceDelay);
        _autoRoutine = null;
        proceed();
    }

    private void Hide()
    {
        if (_autoRoutine != null) { StopCoroutine(_autoRoutine); _autoRoutine = null; }
        _group.alpha = 0f; _group.blocksRaycasts = false;
        _victory.SetActive(false); _defeat.SetActive(false);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────
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
        t.enableWordWrapping = false; t.raycastTarget = false;
        return t;
    }
    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
