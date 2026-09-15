// ============================================================
// Mercenary/PartyEditPanel.cs
// 파티 편집 패널 — 파티 4슬롯 + 예비대 + 교체/제거
// ============================================================
//
// [동작]
//   - 파티 슬롯 1~4 표시 (FellowCardView, PartySlot 모드 — 풀 카드)
//   - 예비대 카드 표시 (FellowCardView, Reserve 모드 — 풀 카드, ScrollRect 안에 배치)
//   - 파티 슬롯 클릭 → 선택. 다시 예비대 카드 클릭하면 교체.
//   - 파티 슬롯의 [제거] 클릭 → 그 동료를 예비대로 빼냄.
//   - 예비대 카드 클릭(파티 선택 없음) → 파티 빈 슬롯에 즉시 합류.
//   - 예비대 카드의 [제거] 클릭 → 예비대에서 영구 제거 (방출, DismissReserve)
//
// [인스펙터 슬롯]
//   - fellowCardPrefab    : 풀 카드 (FellowCardView)
//   - partySlotsParent    : 파티 4슬롯 부모
//   - reservesParent      : 예비대 부모 (ScrollRect Content)
//   - partyCountLabel     : "파티 인원: 2/4" 표시
//   - closeButton         : 닫기 버튼
// ============================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartyEditPanel : PanelBase
{
    [Header("프리팹 / 부모")]
    [SerializeField] private FellowCardView fellowCardPrefab;
    [SerializeField] private Transform      reservesParent;

    [Header("파티 슬롯 (4개 고정 — Inspector 에 직접 배치)")]
    [Tooltip("파티 슬롯 4개. 인덱스 0~3 단일 순번. 비워두면 폴백으로 instantiate.")]
    [SerializeField] private FellowCardView[] partySlots = new FellowCardView[4];

    [Header("UI 라벨 / 버튼")]
    [SerializeField] private TMP_Text partyCountLabel;
    [SerializeField] private Button   closeButton;

    private const int PartySize = 4;

    // 카드 인스턴스 풀 — 예비대는 가변, 파티는 고정 슬롯이라 풀 불필요
    private readonly List<FellowCardView> _reserveCards = new();

    // 현재 선택된 파티 슬롯 인덱스 (없으면 -1).
    private int _selectedPartyIndex = -1;

    // 현재 선택된 예비대 인덱스 (없으면 -1). 파티 슬롯과 동시에 선택되지 않는다(둘 중 하나).
    private int _selectedReserveIndex = -1;

    protected override void Awake()
    {
        base.Awake();
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        // 고정 슬롯 → 핸들러 1회 구독 (fellow 는 클릭 시점에 PartyManager 에서 동적 조회).
        if (partySlots != null)
        {
            for (int i = 0; i < partySlots.Length && i < PartySize; i++)
            {
                int capIdx = i;
                var slot = partySlots[i];
                if (slot == null) continue;
                slot.OnActionClicked += _ => HandlePartySlotClicked(capIdx);
                slot.OnRemoveClicked += _ => HandlePartyRemove(capIdx);
            }
        }
    }

    // 전투 노드 진입 중에는 파티 편집 금지 — 무시하고 Open 자체를 건너뜀.
    // BattleManager.gameObject 의 부모(RightMainArea)는 BattleNode 진입 시에만 활성화되므로
    // activeInHierarchy 가 곧 "전투 노드 안" 신호.
    public override void Open()
    {
        if (BattleManager.Instance != null
            && BattleManager.Instance.gameObject.activeInHierarchy
            && BattleManager.Instance.currentPhase != BattlePhase.BattleEnd)
        {
            Debug.Log("[PartyEditPanel] 전투 노드 진행 중 — 파티 편집 진입 차단");
            return;
        }
        base.Open();
    }

    protected override void OnOpened()
    {
        _selectedPartyIndex   = -1;
        _selectedReserveIndex = -1;
        RebuildAll();
    }

    // ----------------------------------------------------------
    // 전체 갱신
    // ----------------------------------------------------------
    private void RebuildAll()
    {
        RebuildPartySlots();
        RebuildReserves();
        RefreshHeader();
    }

    private void RefreshHeader()
    {
        if (partyCountLabel != null && PartyManager.Instance != null)
            Loc.Set(partyCountLabel, "파티 인원: {0}/{1}", PartyManager.Instance.CompanionCount, PartySize);
    }

    // ----------------------------------------------------------
    // 파티 슬롯 빌드 (항상 4칸 — Inspector 의 고정 슬롯에 Bind)
    // ----------------------------------------------------------
    private void RebuildPartySlots()
    {
        if (PartyManager.Instance == null) return;
        if (partySlots == null || partySlots.Length < PartySize) return;

        var fellows = PartyManager.Instance.GetActiveFellows();
        for (int i = 0; i < PartySize; i++)
        {
            var card = partySlots[i];
            if (card == null) continue;

            FellowData fellow = (i < fellows.Count) ? fellows[i] : null;
            card.Bind(fellow, FellowCardMode.PartySlot);
            // 핸들러는 Awake 에서 1회 구독되어 있으므로 여기서는 Bind 만.
        }
        RefreshPartySelectionVisual();
    }

    private void RefreshPartySelectionVisual()
    {
        for (int i = 0; i < PartySize; i++)
        {
            if (partySlots[i] != null)
                partySlots[i].SetSelected(i == _selectedPartyIndex);
        }
    }

    private void RefreshReserveSelectionVisual()
    {
        for (int i = 0; i < _reserveCards.Count; i++)
            if (_reserveCards[i] != null)
                _reserveCards[i].SetSelected(i == _selectedReserveIndex);
    }

    /// <summary>파티/예비대 선택 모두 해제 + 하이라이트 갱신.</summary>
    private void ClearSelection()
    {
        _selectedPartyIndex   = -1;
        _selectedReserveIndex = -1;
        RefreshPartySelectionVisual();
        RefreshReserveSelectionVisual();
    }

    // ----------------------------------------------------------
    // 예비대 빌드 — 풀 카드 (FellowCardView, Reserve 모드)
    // ----------------------------------------------------------
    private void RebuildReserves()
    {
        ClearCardList(_reserveCards);
        if (fellowCardPrefab == null || reservesParent == null) return;
        if (MercenaryService.Instance == null) return;

        var list = MercenaryService.Instance.Reserves;
        for (int i = 0; i < list.Count; i++)
        {
            var card = Instantiate(fellowCardPrefab, reservesParent);
            card.Bind(list[i], FellowCardMode.Reserve);
            int capturedIndex = i;
            var capturedFellow = list[i];
            card.OnActionClicked += _ => HandleReserveClicked(capturedIndex);
            card.OnRemoveClicked += _ => HandleReserveDismiss(capturedFellow);
            _reserveCards.Add(card);
        }
        RefreshReserveSelectionVisual();
    }

    private void HandleReserveDismiss(FellowData fellow)
    {
        if (MercenaryService.Instance == null) return;
        if (!MercenaryService.Instance.DismissReserve(fellow)) return;
        ClearSelection();
        RebuildAll();
    }

    // ----------------------------------------------------------
    // 클릭 핸들러
    // ----------------------------------------------------------

    /// <summary>파티 슬롯 클릭 — 선택 토글, 다른 슬롯 두 번째 클릭 시 두 슬롯 간 순서 교환.</summary>
    private void HandlePartySlotClicked(int slotIndex)
    {
        // 예비대가 먼저 선택된 상태 → 그 예비대원을 이 슬롯으로 (빈 슬롯=합류 / 채워진 슬롯=교체)
        if (_selectedReserveIndex >= 0 && MercenaryService.Instance != null)
        {
            var target = GetPartyFellowAt(slotIndex);
            bool ok = (target == null)
                ? MercenaryService.Instance.TryAssignReserveToParty(_selectedReserveIndex)
                : MercenaryService.Instance.TrySwapPartyAndReserve(target, _selectedReserveIndex);
            ClearSelection();
            if (ok) RebuildAll();
            return;
        }

        var fellow = GetPartyFellowAt(slotIndex);
        if (fellow == null) return;

        // 이미 다른 파티 슬롯이 선택된 상태에서 다른 채워진 슬롯 클릭 → 두 슬롯 순서 교환
        if (_selectedPartyIndex >= 0 && _selectedPartyIndex != slotIndex)
        {
            if (PartyManager.Instance != null && PartyManager.Instance.SwapFellows(_selectedPartyIndex, slotIndex))
            {
                ClearSelection();
                RebuildAll();
                return;
            }
        }

        // 같은 슬롯 재클릭 → 선택 해제, 처음 선택이면 → 선택 설정 (예비대 선택은 해제)
        _selectedPartyIndex   = (_selectedPartyIndex == slotIndex) ? -1 : slotIndex;
        _selectedReserveIndex = -1;
        RefreshPartySelectionVisual();
        RefreshReserveSelectionVisual();
    }

    /// <summary>파티 슬롯의 [제거] 클릭 — 예비대로 빼낸다.</summary>
    private void HandlePartyRemove(int slotIndex)
    {
        var partyFellow = GetPartyFellowAt(slotIndex);
        if (partyFellow == null) return;
        if (MercenaryService.Instance == null) return;
        if (!MercenaryService.Instance.TryMovePartyToReserve(partyFellow)) return;
        _selectedPartyIndex = -1;
        RebuildAll();
    }

    private static FellowData GetPartyFellowAt(int slotIndex)
    {
        if (PartyManager.Instance == null) return null;
        var fellows = PartyManager.Instance.GetActiveFellows();
        return (slotIndex >= 0 && slotIndex < fellows.Count) ? fellows[slotIndex] : null;
    }

    /// <summary>예비대 카드 클릭 — 선택된 파티원과 교체, 없으면 빈 슬롯 합류.</summary>
    private void HandleReserveClicked(int reserveIndex)
    {
        if (MercenaryService.Instance == null) return;

        // 파티 슬롯이 먼저 선택된 상태 → 교체/합류
        if (_selectedPartyIndex >= 0)
        {
            var fellows = PartyManager.Instance.GetActiveFellows();
            bool ok = (_selectedPartyIndex >= fellows.Count)
                ? MercenaryService.Instance.TryAssignReserveToParty(reserveIndex)
                : MercenaryService.Instance.TrySwapPartyAndReserve(fellows[_selectedPartyIndex], reserveIndex);
            ClearSelection();
            if (ok) RebuildAll();
            return;
        }

        // 파티에 빈 자리가 있으면 → 예비대 클릭 = 즉시 합류 (자동 채우기).
        //   빈 파티 슬롯은 버튼이 비활성이라 "선택 후 빈 슬롯 클릭"이 불가능하므로 바로 채운다.
        if (PartyManager.Instance != null && PartyManager.Instance.CompanionCount < PartySize)
        {
            bool joined = MercenaryService.Instance.TryAssignReserveToParty(reserveIndex);
            ClearSelection();
            if (joined) RebuildAll();
            return;
        }

        // 파티 만석 → 예비대 선택 토글 (이후 파티 슬롯을 클릭하면 교체된다 — 양방향 스왑)
        _selectedReserveIndex = (_selectedReserveIndex == reserveIndex) ? -1 : reserveIndex;
        _selectedPartyIndex   = -1;
        RefreshReserveSelectionVisual();
        RefreshPartySelectionVisual();
    }

    // ----------------------------------------------------------
    // 유틸
    // ----------------------------------------------------------
    private static void ClearCardList(List<FellowCardView> list)
    {
        foreach (var c in list)
            if (c != null) Destroy(c.gameObject);
        list.Clear();
    }
}
