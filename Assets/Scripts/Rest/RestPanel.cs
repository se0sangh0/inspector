// ============================================================
// Rest/RestPanel.cs
// 화툿불 노드 UI 패널 — 자동 회복 + 파티 편집 진입점 + 다음 층
// ============================================================
//
// [흐름]
//   NodeSystem.DispatchByRoomType(Rest) → RestPanel.OpenFromNode()
//   OnOpened 즉시 RestService.ApplyRecovery() 호출 (자동 회복)
//   회복 결과 라벨 표시
//   [파티 편집] 클릭 → 자신 닫고 PartyEditPanel 열기 → 닫히면 자신 다시 열기
//   [다음 층]   클릭 → 자신 닫고 OnExit 발생 → NodeSystem 이 노드맵 복귀
//
// [PartyEditPanel 공유]
//   용병소와 동일한 PartyEditPanel 인스턴스를 인스펙터 슬롯으로 참조.
//   ⚠️ 씬 구조: PartyEditPanel 은 Canvas 직속 자식이어야 함 (MercenaryRoot 자식 X)
//
// [인스펙터 슬롯]
//   - canvasGroup        : (자동)
//   - titleLabel         : "화툿불" 제목 TMP_Text
//   - recoveryResultLabel: 회복 결과 표시 TMP_Text
//   - partyEditButton    : "파티 편집" Button
//   - nextNodeButton     : "다음 층으로" Button
//   - partyEditPanel     : 공유 PartyEditPanel (씬 인스턴스)
// ============================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RestPanel : PanelBase
{
    [Header("UI 라벨 / 버튼")]
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text recoveryResultLabel;
    [SerializeField] private Button   partyEditButton;
    [SerializeField] private Button   nextNodeButton;

    [Header("공유 패널")]
    [Tooltip("용병소와 동일한 PartyEditPanel 인스턴스 참조 (Canvas 직속).")]
    [SerializeField] private PartyEditPanel partyEditPanel;

    /// <summary>"다음 층" 클릭 시 NodeSystem 이 구독해 노드맵으로 복귀시킨다.</summary>
    public event Action OnExit;

    protected override void Awake()
    {
        base.Awake();
        if (partyEditButton != null) partyEditButton.onClick.AddListener(HandlePartyEdit);
        if (nextNodeButton  != null) nextNodeButton.onClick.AddListener(HandleNextNode);
    }

    // 이번 노드 방문에서 회복을 이미 적용했는지 — 파티 편집 복귀·패널 재활성화로
    // 회복이 중복 적용되는 것을 막는다 (16-A §4: 기본 회복을 한 번만 적용).
    private bool _recoveryAppliedThisVisit;
    private RestService.RecoveryResult _lastResult;

    /// <summary>NodeSystem 이 호출. 화툿불 진입 — 자동 회복(방문당 1회) + 페이드 인.</summary>
    public void OpenFromNode()
    {
        _recoveryAppliedThisVisit = false; // 새 방문 — 이번 방문의 1회 회복 허용
        Open();
    }

    protected override void OnOpened()
    {
        // 자동 회복 적용 — 방문당 1회만. 파티 편집에서 복귀(Open 재호출) 시 재적용하지 않는다.
        if (!_recoveryAppliedThisVisit)
        {
            _recoveryAppliedThisVisit = true;
            _lastResult = RestService.ApplyRecovery();
        }
        RefreshRecoveryLabel(_lastResult);
    }

    private void RefreshRecoveryLabel(RestService.RecoveryResult result)
    {
        if (titleLabel != null) Loc.Set(titleLabel, "화톳불");
        if (recoveryResultLabel == null) return;

        if (result.affectedCount <= 0)
        {
            Loc.Set(recoveryResultLabel, "회복할 동료가 없습니다.");
            return;
        }
        Loc.Set(recoveryResultLabel, "휴식 완료 — {0}명에게 HP +{1} / 스트레스 -{2}",
            result.affectedCount, RestService.HpRecoveryAmount, RestService.RecoveryAmount);
    }

    // ----------------------------------------------------------
    // 파티 편집 진입 — 자신 닫고 PartyEditPanel 열기, 닫히면 자신 다시 열기
    // ----------------------------------------------------------
    private void HandlePartyEdit()
    {
        if (partyEditPanel == null)
        {
            Debug.LogWarning("[RestPanel] PartyEditPanel 미연결 — 인스펙터 슬롯 확인");
            return;
        }

        Action onSubClosed = null;
        onSubClosed = () =>
        {
            partyEditPanel.OnClosedEvent -= onSubClosed;
            Open(); // 화툿불로 복귀
        };
        partyEditPanel.OnClosedEvent += onSubClosed;

        Close();
        partyEditPanel.Open();
    }

    // ----------------------------------------------------------
    // 다음 층 — 노드맵 복귀
    // ----------------------------------------------------------
    private void HandleNextNode()
    {
        OnExit?.Invoke();
        Close();
    }
}
