// ============================================================
// Church/ChurchPanel.cs
// 교회 노드 UI 패널 — HP 회복 / 스트레스 회복 / 사망자 인라인 가로 리스트 / 다음 층
// ============================================================
//
// [흐름]
//   NodeSystem.DispatchByRoomType(Event) → ChurchPanel.OpenFromNode()
//   [HP 회복]   클릭 → ChurchService.TryBuyHpHeal()
//   [스트레스]  클릭 → ChurchService.TryBuyStressRelief()
//   [부활 카드] 클릭 → ChurchService.TryReviveFellow(target) — 인라인 카드 액션 버튼
//   [다음 층]   클릭 → OnExit 발생 → NodeSystem 이 노드맵 복귀
//
// [인스펙터 슬롯]
//   - titleLabel            : "교회" 제목
//   - soulstoneLabel        : 현재 영혼석 표시
//   - hpButton / hpLabel    : HP 회복 버튼 + 라벨
//   - stressButton / 라벨   : 스트레스 회복 버튼 + 라벨
//   - reviveListContainer   : 사망 동료 카드 부모 (ScrollRect.content, HorizontalLayoutGroup)
//   - reviveCardPrefab      : FellowCardView 카드 prefab (Revive 모드로 Bind)
//   - reviveEmptyLabel      : 사망자 0명일 때 표시할 안내 라벨 (선택)
//   - nextNodeButton        : "다음 층" 버튼
// ============================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChurchPanel : PanelBase
{
    [Header("타이틀 / 잔액")]
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text soulstoneLabel;

    [Header("HP 회복")]
    [SerializeField] private Button   hpButton;
    [SerializeField] private TMP_Text hpButtonLabel;

    [Header("스트레스 회복")]
    [SerializeField] private Button   stressButton;
    [SerializeField] private TMP_Text stressButtonLabel;

    [Header("사망자 인라인 리스트 (가로 스크롤)")]
    [Tooltip("ScrollRect 의 Content (HorizontalLayoutGroup). 카드들이 가로로 정렬됨.")]
    [SerializeField] private Transform      reviveListContainer;
    [Tooltip("Revive 모드로 Bind 할 FellowCardView prefab. 보통 Assets/Prefab/UI/CardPrefab.prefab")]
    [SerializeField] private FellowCardView reviveCardPrefab;
    [Tooltip("(선택) 사망자 0명일 때 표시할 라벨")]
    [SerializeField] private TMP_Text       reviveEmptyLabel;

    [Header("다음 층")]
    [SerializeField] private Button nextNodeButton;

    /// <summary>"다음 층" 클릭 시 NodeSystem 이 구독해 노드맵으로 복귀시킨다.</summary>
    public event Action OnExit;

    protected override void Awake()
    {
        base.Awake();
        if (hpButton       != null) hpButton.onClick.AddListener(HandleHp);
        if (stressButton   != null) stressButton.onClick.AddListener(HandleStress);
        if (nextNodeButton != null) nextNodeButton.onClick.AddListener(HandleNextNode);
    }

    /// <summary>
    /// NodeSystem 이 호출. 교회 진입 — 뒤 화면 캡처·블러 후 페이드 인 + UI 갱신.
    /// 블러 배경으로 교회 UI 에 시선을 집중시킨다 (닫힐 때 자동 해제).
    /// </summary>
    public void OpenFromNode() => PanelBlurBackdrop.CaptureThenOpen(this);

    protected override void OnOpened()
    {
        if (titleLabel != null) Loc.Set(titleLabel, "교회");
        RefreshAll();
        Loc.Localize(gameObject); // 정적 라벨(버튼·안내)을 현재 언어로

        if (SoulstoneManager.Instance != null) SoulstoneManager.Instance.OnCurrencyChanged += OnCurrencyChanged;
        if (PartyManager.Instance     != null) PartyManager.Instance.OnPartyChanged        += RefreshAll;
    }

    protected override void OnClosed()
    {
        if (SoulstoneManager.Instance != null) SoulstoneManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;
        if (PartyManager.Instance     != null) PartyManager.Instance.OnPartyChanged        -= RefreshAll;
    }

    private void OnCurrencyChanged(int _) => RefreshAll();

    private void RefreshAll()
    {
        int balance = SoulstoneManager.Instance != null ? SoulstoneManager.Instance.Amount : 0;

        if (soulstoneLabel    != null) Loc.Set(soulstoneLabel, "영혼석 {0}", balance);
        if (hpButtonLabel     != null) { Loc.Set(hpButtonLabel, "HP +{0} (영혼석 {1})", ChurchService.HpAmount, ChurchService.HpCost);       Loc.AutoFit(hpButtonLabel); }
        if (stressButtonLabel != null) { Loc.Set(stressButtonLabel, "스트레스 -{0} (영혼석 {1})", ChurchService.StressAmount, ChurchService.StressCost); Loc.AutoFit(stressButtonLabel); }
        if (hpButton          != null) hpButton.interactable     = balance >= ChurchService.HpCost;
        if (stressButton      != null) stressButton.interactable = balance >= ChurchService.StressCost;

        RebuildReviveCards();
    }

    private void RebuildReviveCards()
    {
        if (reviveListContainer == null) return;

        // 기존 동적 자식 정리 — DestroyImmediate 로 같은 프레임 다중 호출 시 잔재 누적 방지
        for (int i = reviveListContainer.childCount - 1; i >= 0; i--)
            DestroyImmediate(reviveListContainer.GetChild(i).gameObject);

        var dead = PartyManager.Instance != null ? PartyManager.Instance.DeadFellows : null;
        int count = dead != null ? dead.Count : 0;

        if (reviveEmptyLabel != null) reviveEmptyLabel.gameObject.SetActive(count == 0);
        if (count == 0 || reviveCardPrefab == null) return;

        int balance = SoulstoneManager.Instance != null ? SoulstoneManager.Instance.Amount : 0;
        for (int i = 0; i < dead.Count; i++)
        {
            var fellow = dead[i];
            if (fellow == null) continue;
            int cost = ChurchService.GetReviveCost(fellow.starLevel);
            var card = Instantiate(reviveCardPrefab, reviveListContainer);
            card.gameObject.SetActive(true);
            card.Bind(fellow, FellowCardMode.Revive, costOverride: cost);
            card.SetInteractable(balance >= cost);
            var captured = fellow;
            card.OnActionClicked += _ => HandleReviveClicked(captured);
        }
    }

    private void HandleReviveClicked(FellowData target)
    {
        if (target == null) return;
        if (ChurchService.TryReviveFellow(target)) RefreshAll();
    }

    private void HandleHp()
    {
        if (ChurchService.TryBuyHpHeal()) RefreshAll();
    }
    private void HandleStress()
    {
        if (ChurchService.TryBuyStressRelief()) RefreshAll();
    }
    private void HandleNextNode()
    {
        OnExit?.Invoke();
        Close();
    }
}
