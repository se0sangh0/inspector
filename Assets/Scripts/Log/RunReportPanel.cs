// ============================================================
// Log/RunReportPanel.cs
// 탐사 보고서 — 클리어/전멸 요약 + 다음 탐사·타이틀 선택 (P0-05)
// ============================================================
//
// [이 파일이 하는 일]
//   런 종료(보스 클리어/전멸) 직후 후면을 어둡게 딤 처리하고
//   이번 런의 요약 보고서 한 장을 표시합니다.
//   [확인] 클릭 시 런을 한 번만 정산·초기화(FinalizeRun)한 뒤,
//   다음 탐사·타이틀 선택 화면을 보여 줍니다.
//
// [계약 — 16-A §5 탐사 보고서 / 16-B §4 마감과 영속 경계]
//   - 보고서는 이미 적용한 사건·재화·소실을 요약할 뿐 결과를 다시 적용하지 않는다.
//   - 확인 버튼 하나로 출력을 완료한다.
//   - 확인 뒤 런을 한 번만 정산·초기화하고 다음 탐사·타이틀 선택 화면을 표시한다.
//   - 다음 탐사는 본 런 1층으로, 타이틀은 타이틀 화면으로 이동한다.
//   - 별도의 에필로그 문서 화면은 추가하지 않는다.
//   - 정답·오답·도덕 판정을 표시하지 않는다.
//
// [문안 — 16-E §3 T0 초안 (프로토타입 임시 문안, 16 §4 허용)]
//   제 N차 정기 탐사 · 보고 / 탐사 구역 / 도달 구역 / 기록 요약 / 획득 영혼석 / 비고.
//   N·도달 구역·요약·영혼석은 런 기록에서 자동 삽입한다.
//
// 사용:
//   RunReportPanel.Show(result, onConfirmed, onNextExploration, onTitle);
//   - onConfirmed : 확인 클릭 시 정확히 1회 (FinalizeRun 을 여기서 호출)
//   - onNext/onTitle : 각 선택 시 씬 전환
//   - 씬 배치 불필요 (자체 생성, DontDestroyOnLoad). 최상단 오버레이.
// ============================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RunReportPanel : MonoBehaviour
{
    public static RunReportPanel Instance { get; private set; }

    private CanvasGroup _group;
    private GameObject  _reportStage, _choiceStage;
    private TMP_Text    _reportText;
    private System.Action _onConfirmed, _onNext, _onTitle;
    private bool _confirmed; // 확인 1회 가드 (연타로 FinalizeRun 이 두 번 불리지 않게)

    private static readonly Color Ink   = new Color(0.90f, 0.87f, 0.80f, 1f);
    private static readonly Color Gold  = new Color(1f, 0.84f, 0.40f, 1f);
    private static readonly Color Paper = new Color(0.12f, 0.11f, 0.13f, 0.98f);

    public static void Show(RunResult result, System.Action onConfirmed, System.Action onNextExploration, System.Action onTitle)
    {
        Ensure();
        Instance._Show(result, onConfirmed, onNextExploration, onTitle);
    }

    private static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("RunReportPanel").AddComponent<RunReportPanel>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
    }

    private void _Show(RunResult result, System.Action onConfirmed, System.Action onNext, System.Action onTitle)
    {
        _onConfirmed = onConfirmed;
        _onNext      = onNext;
        _onTitle     = onTitle;
        _confirmed   = false;

        // 보고서 텍스트는 지금(FinalizeRun 전) 시점의 기록으로 만든다 —
        // 확인 시 FinalizeRun 이 Records·영혼석을 초기화해도 표시 내용은 유지된다.
        var session = RunSessionManager.Instance;
        var records = session != null ? session.Records : null;
        int runNumber = session != null ? session.CurrentRunNumber : 1;
        int soul = SoulstoneManager.Instance != null ? SoulstoneManager.Instance.Amount : 0;
        Loc.Bind(_reportText, () => BuildReportText(result, records, runNumber, soul));

        _reportStage.SetActive(true);
        _choiceStage.SetActive(false);
        Loc.Localize(_reportStage); // 정적 라벨(확인 버튼 등)을 현재 언어로 (본문은 이미 Tr 완료)
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
    }

    // ── 확인 → FinalizeRun 1회 → 선택 화면 ──
    private void OnConfirm()
    {
        if (_confirmed) return;         // 연타 차단 — FinalizeRun 정확히 1회 (16-B §4)
        _confirmed = true;
        _onConfirmed?.Invoke();         // 런 정산·초기화 (완료 런 수 +1)
        _reportStage.SetActive(false);
        _choiceStage.SetActive(true);
        Loc.Localize(_choiceStage);     // 다음 탐사/타이틀/안내 라벨을 현재 언어로
    }

    private void OnNext()
    {
        var cb = _onNext;
        Hide();
        cb?.Invoke();                   // 다음 탐사 (StartNewRun → GamePlayScene)
    }

    private void OnTitle()
    {
        var cb = _onTitle;
        Hide();
        cb?.Invoke();                   // 타이틀 화면
    }

    private void Hide()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _reportStage.SetActive(false);
        _choiceStage.SetActive(false);
    }

    // ── 보고서 문안 생성 (16-E §3 템플릿 + 기록 자동 삽입) ──
    private static string BuildReportText(RunResult result, RunRecord records, int runNumber, int soul)
    {

        // 기록 집계
        int battles = 0, choices = 0, recruits = 0, recoveries = 0, observations = 0, reached = 0;
        bool cleared = result == RunResult.Victory;
        if (records != null)
        {
            foreach (var e in records.Entries)
            {
                switch (e.type)
                {
                    case RunRecordType.BattleResolved:      battles++;    break;
                    case RunRecordType.ChoiceResolved:      choices++;    break;
                    case RunRecordType.RecruitmentResolved: recruits++;   break;
                    case RunRecordType.RecoveryResolved:    recoveries++; break;
                }
                if (e.floor > reached) reached = e.floor;
                // 현장 관찰 — 사후 관찰 문장이 기록에 포함됐는지로 카운트
                foreach (var line in e.lines)
                    if (line.Key.StartsWith("주변 탐색 결과")) { observations++; break; }
            }
        }

        string zone = cleared ? Loc.Tr("성소") : (reached > 0 ? Loc.Tr("{0}층", reached) : Loc.Tr("미상"));

        var sb = new StringBuilder();
        sb.AppendLine(Loc.Tr("제 {0}차 정기 탐사 보고", runNumber));
        sb.AppendLine();
        sb.AppendLine(cleared ? Loc.Tr("결과: 탐사 완료, 성소 도달") : Loc.Tr("결과: 탐사 실패, 파티 전멸"));
        sb.AppendLine(Loc.Tr("탐사 구역: 야생림—협곡—성소"));
        sb.AppendLine(Loc.Tr("도달 구역: {0}", zone));
        sb.AppendLine(Loc.Tr("기록 요약: 전투 {0}건, 사건 {1}건, 정비/회복 {2}건, 현장 관찰 {3}건 확인.", battles, choices, recoveries, observations));
        if (recruits > 0) sb.AppendLine(Loc.Tr("          동료 편성 변동 {0}건.", recruits));
        sb.AppendLine(Loc.Tr("획득 영혼석: {0}개", soul));
        sb.AppendLine();
        sb.Append(Loc.Tr("그들은 무엇을 지키고 있었나."));
        return sb.ToString();
    }

    // ── UI 구성 (런타임 자체 생성) ──
    private void Build()
    {
        var canvasGo = new GameObject("ReportCanvas", typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        ResponsiveUi.Configure(canvas);
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10050; // BattleResultScreen(9990)·관찰(10000) 위
        _group = canvasGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f; _group.blocksRaycasts = false;

        var font = TMP_Settings.defaultFontAsset;

        // 후면 어둡게 딤 (16-A §5: 후면 어둡게 처리)
        var dim = NewImage("Dim", canvasGo.transform);
        Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.82f);
        dim.raycastTarget = true;

        // ── Stage 1: 보고서 ──
        _reportStage = NewUI("ReportStage", canvasGo.transform);
        Stretch((RectTransform)_reportStage.transform);

        var paper = NewImage("Paper", _reportStage.transform);
        var prt = paper.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(960, 680);
        paper.color = Paper;
        var pOutline = paper.gameObject.AddComponent<Outline>();
        pOutline.effectColor = new Color(0.6f, 0.55f, 0.4f, 1f);
        pOutline.effectDistance = new Vector2(2, 2);
        pOutline.useGraphicAlpha = false;

        _reportText = NewText("Report", paper.transform, "", font, 26, Ink, FontStyles.Normal);
        var rtr = (RectTransform)_reportText.transform;
        rtr.anchorMin = new Vector2(0, 1); rtr.anchorMax = new Vector2(1, 1); rtr.pivot = new Vector2(0.5f, 1);
        rtr.anchoredPosition = new Vector2(0, -40);
        rtr.offsetMin = new Vector2(48, rtr.offsetMin.y); rtr.offsetMax = new Vector2(-48, rtr.offsetMax.y);
        rtr.sizeDelta = new Vector2(rtr.sizeDelta.x, 520);
        _reportText.alignment = TextAlignmentOptions.TopLeft;
        _reportText.enableWordWrapping = true;

        var confirmBtn = NewButton("ConfirmButton", paper.transform, "확인", font, new Vector2(0, 34));
        confirmBtn.onClick.AddListener(OnConfirm);

        // ── Stage 2: 다음 탐사 / 타이틀 선택 ──
        _choiceStage = NewUI("ChoiceStage", canvasGo.transform);
        Stretch((RectTransform)_choiceStage.transform);

        var prompt = NewText("Prompt", _choiceStage.transform, "다음 행동을 선택하십시오", font, 34, Ink, FontStyles.Bold);
        var ptr = (RectTransform)prompt.transform;
        ptr.anchorMin = ptr.anchorMax = new Vector2(0.5f, 0.5f); ptr.pivot = new Vector2(0.5f, 0.5f);
        ptr.sizeDelta = new Vector2(800, 60); ptr.anchoredPosition = new Vector2(0, 90);
        prompt.alignment = TextAlignmentOptions.Center;

        var nextBtn = NewButton("NextButton", _choiceStage.transform, "다음 탐사", font, new Vector2(-150, 0));
        ((RectTransform)nextBtn.transform).anchorMin = ((RectTransform)nextBtn.transform).anchorMax = new Vector2(0.5f, 0.5f);
        nextBtn.onClick.AddListener(OnNext);

        var titleBtn = NewButton("TitleButton", _choiceStage.transform, "타이틀", font, new Vector2(150, 0));
        ((RectTransform)titleBtn.transform).anchorMin = ((RectTransform)titleBtn.transform).anchorMax = new Vector2(0.5f, 0.5f);
        titleBtn.onClick.AddListener(OnTitle);

        _reportStage.SetActive(false);
        _choiceStage.SetActive(false);
    }

    // ── 헬퍼 (BattleResultScreen 스타일) ──
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
        t.raycastTarget = false;
        return t;
    }
    private Button NewButton(string name, Transform parent, string label, TMP_FontAsset font, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(240, 64); rt.anchoredPosition = anchoredPos;
        var img = go.GetComponent<Image>();
        var btnSprite = Resources.Load<Sprite>("Button/default_button");
        if (btnSprite != null) { img.sprite = btnSprite; img.type = Image.Type.Sliced; img.color = Color.white; }
        else img.color = new Color(0.20f, 0.18f, 0.24f, 1f);
        var btn = go.AddComponent<Button>();
        var lt = NewText(name + "Label", go.transform, label, font, 28, Gold, FontStyles.Bold);
        Stretch((RectTransform)lt.transform); lt.alignment = TextAlignmentOptions.Center;
        return btn;
    }
    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
