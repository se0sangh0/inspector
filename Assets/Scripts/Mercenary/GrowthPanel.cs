// ============================================================
// Mercenary/GrowthPanel.cs
// 동료 성장 패널 — 파티/예비대에서 3명 합성 → 상위 성급
// ============================================================
//
// [동작]
//   1. 빈 합성 슬롯 클릭 → FellowSourcePickerPopup 열림 → 파티/예비대 탭에서 선택
//   2. 채워진 합성 슬롯 클릭 → 그 슬롯 비우기 (선택 해제)
//   3. 슬롯 3 채워지고 같은 성급이면 합성 버튼 활성화
//   4. 합성 버튼 클릭 → MercenaryService.TrySynthesize → 결과는 예비대로
//
// [규칙]
//   - 같은 성급 3명만 가능 (기획 백로그 §4)
//   - 비용 무료 (사용자 결정)
//   - 합성 소스는 파티원 + 예비대 모두 가능 (사용자 결정)
//   - 파티원이 소비되면 그 파티 슬롯은 빈자리가 됨 — 다음 용병소/PartyEditPanel 에서 채움
//
// [인스펙터 슬롯]
//   - synthSlot1/2/3      : 합성 슬롯 카드 (FellowCardView, 미리 배치)
//   - resultPreview       : 결과 카드 (FellowCardView, 미리 배치)
//   - synthesizeButton    : 합성 실행 버튼
//   - statusLabel         : 안내 라벨
//   - closeButton         : 닫기 버튼
//   - pickerPopup         : 합성 소스 선택 팝업 (FellowSourcePickerPopup)
// ============================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GrowthPanel : PanelBase
{
    [Header("합성 슬롯 (씬에 미리 배치된 3개)")]
    [SerializeField] private FellowCardView synthSlot1;
    [SerializeField] private FellowCardView synthSlot2;
    [SerializeField] private FellowCardView synthSlot3;

    [Header("결과 미리보기")]
    [SerializeField] private FellowCardView resultPreview;

    [Header("버튼 / 라벨")]
    [SerializeField] private Button   synthesizeButton;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button   closeButton;

    [Header("합성 소스 선택 팝업")]
    [SerializeField] private FellowSourcePickerPopup pickerPopup;

    // 슬롯에 담긴 FellowData (null = 빈 슬롯). 파티원/예비대원 둘 다 가능.
    private readonly FellowData[] _selectedFellows = new FellowData[3];

    protected override void Awake()
    {
        base.Awake();
        if (synthesizeButton != null) synthesizeButton.onClick.AddListener(HandleSynthesize);
        if (closeButton      != null) closeButton.onClick.AddListener(Close);

        // 합성 슬롯 클릭 → 빈 슬롯이면 팝업 열기, 채워진 슬롯이면 비우기
        WireSynthSlotClick(synthSlot1, 0);
        WireSynthSlotClick(synthSlot2, 1);
        WireSynthSlotClick(synthSlot3, 2);
    }

    private void WireSynthSlotClick(FellowCardView slot, int slotIndex)
    {
        if (slot == null) return;
        slot.OnActionClicked += _ => HandleSynthSlotClicked(slotIndex);
        slot.OnRemoveClicked += _ => ClearSynthSlot(slotIndex);
    }

    protected override void OnOpened()
    {
        ResetSelection();
        RefreshSynthSlots();
        RefreshResultPreview();
        RefreshStatus();
    }

    // ----------------------------------------------------------
    // 합성 슬롯 클릭 — 빈 슬롯이면 팝업, 채워진 슬롯이면 비우기
    // ----------------------------------------------------------
    private void HandleSynthSlotClicked(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;

        if (_selectedFellows[slotIndex] != null)
        {
            // 채워진 슬롯 — 비우기
            ClearSynthSlot(slotIndex);
            return;
        }

        // 빈 슬롯 — 팝업 열어 동료 선택
        if (pickerPopup == null)
        {
            Debug.LogWarning("[GrowthPanel] pickerPopup 미연결 — 인스펙터 슬롯 확인");
            return;
        }

        var excluded   = BuildExcludedSet();
        var lockedStar = BuildLockedStar();
        pickerPopup.OpenForSlot(slotIndex, excluded, lockedStar, picked => OnFellowPicked(slotIndex, picked), null);
    }

    private HashSet<FellowData> BuildExcludedSet()
    {
        var set = new HashSet<FellowData>();
        for (int i = 0; i < 3; i++)
            if (_selectedFellows[i] != null) set.Add(_selectedFellows[i]);
        return set;
    }

    // 이미 선택된 동료의 성급으로 잠금. 아무도 없으면 null(=잠금없음).
    private int? BuildLockedStar()
    {
        for (int i = 0; i < 3; i++)
            if (_selectedFellows[i] != null) return _selectedFellows[i].starLevel;
        return null;
    }

    private void OnFellowPicked(int slotIndex, FellowData picked)
    {
        if (picked == null) return;
        if (slotIndex < 0 || slotIndex >= 3) return;
        _selectedFellows[slotIndex] = picked;
        RefreshSynthSlots();
        RefreshResultPreview();
        RefreshStatus();
    }

    private void ClearSynthSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;
        _selectedFellows[slotIndex] = null;
        RefreshSynthSlots();
        RefreshResultPreview();
        RefreshStatus();
    }

    // ----------------------------------------------------------
    // 합성 슬롯 표시 갱신
    // ----------------------------------------------------------
    private void RefreshSynthSlots()
    {
        BindSynthSlot(synthSlot1, _selectedFellows[0]);
        BindSynthSlot(synthSlot2, _selectedFellows[1]);
        BindSynthSlot(synthSlot3, _selectedFellows[2]);
    }

    private void BindSynthSlot(FellowCardView slot, FellowData fellow)
    {
        if (slot == null) return;
        if (fellow == null)
        {
            slot.BindEmpty(FellowCardMode.SynthesizeSlot);
            return;
        }
        slot.Bind(fellow, FellowCardMode.SynthesizeSlot);
        slot.SetSelected(true);
    }

    // ----------------------------------------------------------
    // 결과 미리보기 + 합성 가능 여부 / 안내 라벨
    // ----------------------------------------------------------
    private void RefreshResultPreview()
    {
        if (resultPreview == null) return;
        // 미리보기는 단순 빈 카드 — 실제 결과는 합성 후에만 (4가지 케이스 + 랜덤)
        resultPreview.BindEmpty(FellowCardMode.Reserve);
    }

    private void RefreshStatus()
    {
        int filled = CountFilledSlots();
        bool canSynth = filled == 3 && AllSameStar();

        if (statusLabel != null)
        {
            if (filled < 3)
                Loc.Set(statusLabel, "{0}명을 더 선택해주세요", 3 - filled);
            else if (!AllSameStar())
                Loc.Set(statusLabel, "같은 성급의 동료 3명을 선택해주세요");
            else
                Loc.Set(statusLabel, "합성 준비 완료");
        }
        if (synthesizeButton != null) synthesizeButton.interactable = canSynth;
    }

    private int CountFilledSlots()
    {
        int n = 0;
        for (int i = 0; i < 3; i++) if (_selectedFellows[i] != null) n++;
        return n;
    }

    private bool AllSameStar()
    {
        int? baseStar = null;
        for (int i = 0; i < 3; i++)
        {
            var f = _selectedFellows[i];
            if (f == null) return false;
            if (baseStar == null) baseStar = f.starLevel;
            else if (f.starLevel != baseStar.Value) return false;
        }
        return true;
    }

    // ----------------------------------------------------------
    // 합성 실행
    // ----------------------------------------------------------
    private void HandleSynthesize()
    {
        if (MercenaryService.Instance == null) return;

        bool ok = MercenaryService.Instance.TrySynthesize(
            _selectedFellows[0],
            _selectedFellows[1],
            _selectedFellows[2],
            out var result);

        if (!ok) return;

        // 결과 카드 표시
        if (resultPreview != null && result != null)
            resultPreview.Bind(result, FellowCardMode.Reserve);

        // 선택 리셋
        ResetSelection();
        RefreshSynthSlots();
        RefreshStatus();
    }

    private void ResetSelection()
    {
        for (int i = 0; i < 3; i++) _selectedFellows[i] = null;
    }
}
