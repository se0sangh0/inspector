// ============================================================
// Mercenary/FellowSourcePickerPopup.cs
// 동료 명단 모달 — 파티/예비대 영역을 동시에 표시
// ============================================================
//
// [모드]
//   Synthesize : GrowthPanel — 빈 합성 슬롯 채울 동료 선택
//                · 카드 [선택] → onPicked 콜백 + 자동 Close
//                · 카드 [X]    → "지원하지 않는 기능입니다" 토스트
//   Recruit    : RecruitPanel — 파티/예비대 전체 명단 열람
//                · 파티 카드   [선택]/[X] → 토스트
//                · 예비대 카드 [선택]      → 토스트
//                · 예비대 카드 [X]         → 방출 (DismissReserve)
//
// [공통]
//   [×] 닫기 또는 배경 클릭 → onCanceled 콜백 + Close
//   파티 영역과 예비대 영역이 각자 카드 그리드를 가짐 (탭 없음)
//
// [인스펙터 슬롯]
//   - titleLabel         : 상단 제목 TMP_Text
//   - toastLabel         : 하단 토스트 TMP_Text (2초 안내)
//   - partyGridParent    : 파티 카드 그리드 부모 (Grid Layout)
//   - reserveGridParent  : 예비대 카드 그리드 부모 (Grid Layout)
//   - fellowCardPrefab   : CardPrefab (FellowCardView)
//   - closeButton        : [×] 닫기 Button
//   - backgroundButton   : (선택) 어두운 배경 Button — 클릭 시 닫기
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FellowSourcePickerPopup : PanelBase
{
    public enum PickerMode { Synthesize, Recruit, Sell }

    [Header("UI")]
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text toastLabel;
    [Tooltip("예비대 인원 표시 라벨 (선택). 형식: \"N/9\"")]
    [SerializeField] private TMP_Text reserveCountLabel;
    [SerializeField] private Transform partyGridParent;
    [SerializeField] private Transform reserveGridParent;
    [SerializeField] private FellowCardView fellowCardPrefab;
    [SerializeField] private Button   closeButton;
    [SerializeField] private Button   backgroundButton;

    [Header("토스트")]
    [SerializeField] private float toastDuration = 2f;

    private const string UnsupportedMessage = "지원하지 않는 기능입니다";

    private readonly List<FellowCardView> _partyCards   = new();
    private readonly List<FellowCardView> _reserveCards = new();
    private HashSet<FellowData> _excluded;
    private int?                _lockedStar;   // 합성: 첫 카드 성급 잠금. null=잠금없음
    private Action<FellowData>  _onPicked;
    private Action              _onCanceled;
    private PickerMode          _mode = PickerMode.Synthesize;
    private Coroutine           _toastRoutine;

    protected override void Awake()
    {
        base.Awake();
        if (closeButton      != null) closeButton.onClick.AddListener(HandleCancel);
        if (backgroundButton != null) backgroundButton.onClick.AddListener(HandleCancel);
        HideToast();
    }

    // ----------------------------------------------------------
    // 진입점 — GrowthPanel (합성 소스 선택)
    // ----------------------------------------------------------
    public void OpenForSlot(int slotIndex, HashSet<FellowData> excluded, int? lockedStar, Action<FellowData> onPicked, Action onCanceled)
    {
        _mode       = PickerMode.Synthesize;
        _excluded   = excluded ?? new HashSet<FellowData>();
        _lockedStar = lockedStar;
        _onPicked   = onPicked;
        _onCanceled = onCanceled;

        if (titleLabel != null)
        {
            if (lockedStar.HasValue)
                Loc.Set(titleLabel, "슬롯 {0} — {1} 동료만 선택 가능", slotIndex + 1, new string('★', lockedStar.Value));
            else Loc.Set(titleLabel, "슬롯 {0} 에 넣을 동료 선택", slotIndex + 1);
        }
        Open();
    }

    // ----------------------------------------------------------
    // 진입점 — RecruitPanel (파티/예비대 명단 열람)
    // ----------------------------------------------------------
    public void OpenForRecruit(Action onClosed = null)
    {
        _mode       = PickerMode.Recruit;
        _excluded   = new HashSet<FellowData>();
        _lockedStar = null;
        _onPicked   = null;
        _onCanceled = onClosed;

        if (titleLabel != null) Loc.Set(titleLabel, "동료 명단");
        Open();
    }

    // ----------------------------------------------------------
    // 진입점 — RecruitPanel 판매 모드 (예비대만 표시 + "판매" 버튼)
    // ----------------------------------------------------------
    public void OpenForSell(Action onClosed = null)
    {
        _mode       = PickerMode.Sell;
        _excluded   = new HashSet<FellowData>();
        _lockedStar = null;
        _onPicked   = null;
        _onCanceled = onClosed;

        // titleLabel 은 prefab 인스펙터에서 SetActive(false) 처리 권장.
        // 코드에서도 비워둠 (사용자 요청 — "예비대 보기 타이틀 없애기").
        if (titleLabel != null) titleLabel.text = string.Empty;

        // 파티 그리드는 Sell 모드에서 사용 안 함 — 부모 GO 비활성화.
        if (partyGridParent != null)
            partyGridParent.gameObject.SetActive(false);

        Open();
    }

    protected override void OnOpened()
    {
        HideToast();
        RebuildAll();
    }

    protected override void OnClosed()
    {
        ClearCardList(_partyCards);
        ClearCardList(_reserveCards);
        HideToast();
    }

    // ----------------------------------------------------------
    // 카드 그리드 빌드 — 파티/예비대 동시
    // ----------------------------------------------------------
    private void RebuildAll()
    {
        // Sell 모드는 파티 그리드 안 그림 (사용자 요청 — 예비대만)
        if (_mode != PickerMode.Sell) RebuildPartyCards();
        RebuildReserveCards();
    }

    private void RebuildPartyCards()
    {
        ClearCardList(_partyCards);
        if (fellowCardPrefab == null || partyGridParent == null) return;
        if (PartyManager.Instance == null) return;

        foreach (var fellow in PartyManager.Instance.GetActiveFellows())
        {
            if (fellow == null) continue;
            var card = Instantiate(fellowCardPrefab, partyGridParent);
            card.Bind(fellow, FellowCardMode.SynthesizeSlot);
            WirePartyCard(card, fellow);
            _partyCards.Add(card);
        }
    }

    private void RebuildReserveCards()
    {
        ClearCardList(_reserveCards);
        RefreshReserveCount();
        if (fellowCardPrefab == null || reserveGridParent == null) return;
        if (MercenaryService.Instance == null) return;

        foreach (var fellow in MercenaryService.Instance.Reserves)
        {
            if (fellow == null) continue;
            var card = Instantiate(fellowCardPrefab, reserveGridParent);
            // Sell 모드면 "판매 (+N)" 라벨 + costOverride 로 환급가 표시
            if (_mode == PickerMode.Sell)
                card.Bind(fellow, FellowCardMode.Sell, costOverride: MercenaryService.CalcSellPrice(fellow));
            else
                card.Bind(fellow, FellowCardMode.SynthesizeSlot);
            WireReserveCard(card, fellow);
            _reserveCards.Add(card);
        }
    }

    private void RefreshReserveCount()
    {
        if (reserveCountLabel == null) return;
        int n = MercenaryService.Instance != null ? MercenaryService.Instance.Reserves.Count : 0;
        reserveCountLabel.text = $"{n}/{MercenaryService.ReservesCapacity}";
    }

    private void WirePartyCard(FellowCardView card, FellowData fellow)
    {
        if (_mode == PickerMode.Synthesize)
        {
            bool isExcluded   = _excluded.Contains(fellow);
            bool starMismatch = _lockedStar.HasValue && fellow.starLevel != _lockedStar.Value;
            bool blocked      = isExcluded || starMismatch;
            card.SetInteractable(!blocked);
            card.SetSelected(isExcluded);
            if (!blocked) card.OnActionClicked += _ => HandlePicked(fellow);
            card.OnRemoveClicked += _ => ShowToast(UnsupportedMessage);
            return;
        }
        // Recruit: 파티 카드는 선택/X 모두 지원 안 함
        card.OnActionClicked += _ => ShowToast(UnsupportedMessage);
        card.OnRemoveClicked += _ => ShowToast(UnsupportedMessage);
    }

    private void WireReserveCard(FellowCardView card, FellowData fellow)
    {
        if (_mode == PickerMode.Synthesize)
        {
            bool isExcluded   = _excluded.Contains(fellow);
            bool starMismatch = _lockedStar.HasValue && fellow.starLevel != _lockedStar.Value;
            bool blocked      = isExcluded || starMismatch;
            card.SetInteractable(!blocked);
            card.SetSelected(isExcluded);
            if (!blocked) card.OnActionClicked += _ => HandlePicked(fellow);
            card.OnRemoveClicked += _ => ShowToast(UnsupportedMessage);
            return;
        }
        if (_mode == PickerMode.Sell)
        {
            // 판매 — 모집비 1/3 환급. 환급 후 카드 목록 갱신.
            card.OnActionClicked += _ => HandleReserveSell(fellow);
            card.OnRemoveClicked += _ => ShowToast(UnsupportedMessage);
            return;
        }
        // Recruit: 예비대 선택=토스트, X=방출
        card.OnActionClicked += _ => ShowToast(UnsupportedMessage);
        card.OnRemoveClicked += _ => HandleReserveDismiss(fellow);
    }

    private void HandleReserveSell(FellowData fellow)
    {
        if (MercenaryService.Instance == null) return;
        int refund = MercenaryService.Instance.TrySellReserve(fellow);
        if (refund <= 0) return;
        ShowToast(Loc.Message("+{0} 영혼석 환급", refund));
        RebuildReserveCards();
    }

    private void HandleReserveDismiss(FellowData fellow)
    {
        if (MercenaryService.Instance == null) return;
        if (!MercenaryService.Instance.DismissReserve(fellow)) return;
        RebuildReserveCards();
    }

    private static void ClearCardList(List<FellowCardView> list)
    {
        foreach (var c in list)
            if (c != null) Destroy(c.gameObject);
        list.Clear();
    }

    // ----------------------------------------------------------
    // 토스트
    // ----------------------------------------------------------
    private void ShowToast(LocalizedMessage message)
    {
        if (toastLabel == null) return;
        Loc.Bind(toastLabel, message.Render);
        toastLabel.gameObject.SetActive(true);
        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        _toastRoutine = StartCoroutine(HideToastAfter(toastDuration));
    }

    private IEnumerator HideToastAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        HideToast();
        _toastRoutine = null;
    }

    private void HideToast()
    {
        if (toastLabel == null) return;
        toastLabel.text = string.Empty;
        toastLabel.gameObject.SetActive(false);
    }

    // ----------------------------------------------------------
    // 선택 / 취소 (Synthesize 모드 전용)
    // ----------------------------------------------------------
    private void HandlePicked(FellowData fellow)
    {
        var cb = _onPicked;
        _onPicked   = null;
        _onCanceled = null;
        Close();
        cb?.Invoke(fellow);
    }

    private void HandleCancel()
    {
        var cb = _onCanceled;
        _onPicked   = null;
        _onCanceled = null;
        Close();
        cb?.Invoke();
    }
}
