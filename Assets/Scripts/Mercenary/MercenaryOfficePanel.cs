// ============================================================
// Mercenary/MercenaryOfficePanel.cs
// 용병소 메인 패널 — 2카드 메뉴 (모집/동료 합성). 파티 편집은 LeftPanel/RestPanel 에서 진입.
// ============================================================
//
// [흐름]
//   NodeSystem.DispatchByRoomType(Shop) → MercenaryOfficePanel.OpenFromNode()
//   2카드 중 하나 클릭 → 해당 서브 패널 Open + 자신은 일시 숨김
//   서브 패널 닫기 → 자신 다시 노출
//   "나가기" 클릭 → MercenaryService.OnLeaveNode() + NodeSystem 으로 복귀
//
// [부착]
//   Canvas 자식으로 패널 GameObject + CanvasGroup + 이 컴포넌트.
//   인스펙터에 2카드 Button + 서브 패널 참조 + 나가기 버튼 연결.
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;

public class MercenaryOfficePanel : PanelBase
{
    [Header("2카드 메뉴 버튼")]
    [SerializeField] private Button recruitMenuButton;
    [SerializeField] private Button growthMenuButton;

    [Header("나가기")]
    [SerializeField] private Button exitButton;

    [Header("서브 패널")]
    [SerializeField] private RecruitPanel recruitPanel;
    [SerializeField] private GrowthPanel  growthPanel;

    /// <summary>"나가기" 클릭 시 호출될 콜백. NodeSystem 이 구독해 노드맵으로 복귀시킨다.</summary>
    public event Action OnExit;

    protected override void Awake()
    {
        base.Awake();

        if (recruitMenuButton != null)
            recruitMenuButton.onClick.AddListener(() => OpenSub(recruitPanel));

        // 사용자 지정: 파티 편집 대신 MercenaryRoot에 연결된 GrowthPanel로 동료 합성에 진입한다.
        if (growthMenuButton != null)
        {
            var label = growthMenuButton.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null) label.text = Loc.Tr("동료 합성");
            growthMenuButton.onClick.AddListener(() => OpenSub(growthPanel));
        }

        if (exitButton != null)
            exitButton.onClick.AddListener(HandleExit);
    }

    /// <summary>NodeSystem 이 호출. 용병소 진입 — 후보 롤 + 패널 페이드 인.</summary>
    public void OpenFromNode()
    {
        if (MercenaryService.Instance != null)
            MercenaryService.Instance.OnEnterNode();
        Open();
    }

    private void OpenSub(PanelBase sub)
    {
        if (sub == null)
        {
            Debug.LogWarning("[MercenaryOfficePanel] 서브 패널 미연결 — 인스펙터 확인");
            return;
        }

        // 서브 패널이 닫히면 메인을 다시 열기 위해 1회용 콜백 구독
        Action onSubClosed = null;
        onSubClosed = () =>
        {
            sub.OnClosedEvent -= onSubClosed;
            Open();
        };
        sub.OnClosedEvent += onSubClosed;

        Close();      // 메인 숨김
        sub.Open();   // 서브 열기
    }

    private void HandleExit()
    {
        if (MercenaryService.Instance != null)
            MercenaryService.Instance.OnLeaveNode();
        OnExit?.Invoke();
        Close();
    }
}
