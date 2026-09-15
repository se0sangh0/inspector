// LeftPanelView.cs
// LeftPanel.prefab 루트에 부착하는 좌측 사이드 패널 컨트롤러.
//
// ── 역할 ────────────────────────────────────────────────────────
//   - 파티 카드(최대 4슬롯) 표시 — CardSlotView 4개 위임
//   - 덱 구성 요약 — 성향별 그룹(파티에 존재하는 성향만)
//   - 캐릭별 스트레스 행 (이름 + Bar + 점수)
//   - 하단 버튼 — 설정 / 로그 (도움말 제거됨)
//
// ── 갱신 트리거 ────────────────────────────────────────────────
//   - OnEnable 시 PartyManager.Instance.GetActiveFellows() 로 멤버 동기화
//   - FellowData.OnHpChanged / OnShieldChanged / OnStressChanged 이벤트 구독
//   - 외부에서 Refresh() 직접 호출도 가능 (모집/사망 등 파티 멤버 변경 시)

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeftPanelView : MonoBehaviour
{
    [Header("파티 카드 슬롯 (Card_Base_1~4)")]
    [SerializeField] private CardSlotView[] cardSlots = new CardSlotView[4];

    [Header("덱 구성 요약")]
    [Tooltip("성향별 줄을 출력할 TMP_Text. 줄바꿈 join.")]
    [SerializeField] private TMP_Text deckSummaryText;

    // 스트레스 행은 각 파티 카드(CardSlotView)의 HP 아래 스트레스 바로 이전됨 (기획자 피드백 #3).
    // 좌패널의 별도 스트레스 섹션은 제거 — 관련 필드/로직 삭제.

    [Header("재화 텍스트 (LeftPanel 내부)")]
    [Tooltip("영혼석 ValueText (Item_SoulStone > ValueText). SoulstoneManager 이벤트로 자동 갱신.")]
    [SerializeField] private TMP_Text soulstoneText;
    [Tooltip("마석 ValueText (Magic_SoulStone > ValueText). ManastoneManager 이벤트로 자동 갱신.")]
    [SerializeField] private TMP_Text manastoneText;

    [Header("영혼석 드롭 트윈 목적지 (SoulstoneDropPool 용)")]
    [Tooltip("영혼석 아이콘 컨테이너(Item_SoulStone) 의 Transform. 드롭 오브젝트가 이쪽으로 빨려들어감.")]
    [SerializeField] private Transform soulstoneIconTransform;

    /// <summary>외부에서 풀이 참조 — SoulstoneDropPool.SetTarget 에 연결.</summary>
    public Transform SoulstoneIconTransform => soulstoneIconTransform;

    [Header("하단 버튼")]
    [SerializeField] private Button partyEditButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button logButton;
    // 팝업은 PopupManager 싱글톤이 관리 — 직접 GameObject 참조 불필요.

    [Header("파티 편집 패널 (씬 인스턴스)")]
    [Tooltip("좌측패널 PartyEdit 버튼 클릭 시 Open(). 씬의 PartyEditPanel 을 연결.")]
    [SerializeField] private PartyEditPanel partyEditPanel;

    private readonly List<FellowData> _bound = new();

    // ──────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        WireButtons();
        Refresh();
        SubscribeParty(true);
        SubscribeCurrency(true);
    }

    // PartyManager.Start() 가 InitDefaultParty() 를 호출한 직후 한 번 더 갱신.
    // OnEnable 시점에는 아직 파티가 비어 있을 수 있다.
    private void Start()
    {
        Refresh();
        SubscribeParty(true);    // PartyManager 가 Start 에 늦게 등장한 경우 대비
        SubscribeCurrency(true); // SoulstoneManager/ManastoneManager 도 동일
    }

    private void OnDisable()
    {
        SubscribeParty(false);
        SubscribeCurrency(false);
        UnbindAll();
    }

    // 재화 매니저 이벤트 구독 — 인스펙터 amountText 연결 의존 제거
    private void SubscribeCurrency(bool subscribe)
    {
        if (SoulstoneManager.Instance != null)
        {
            SoulstoneManager.Instance.OnCurrencyChanged -= UpdateSoulstoneText;
            if (subscribe)
            {
                SoulstoneManager.Instance.OnCurrencyChanged += UpdateSoulstoneText;
                UpdateSoulstoneText(SoulstoneManager.Instance.Amount); // 초기값 즉시 반영
            }
        }
        if (ManastoneManager.Instance != null)
        {
            ManastoneManager.Instance.OnCurrencyChanged -= UpdateManastoneText;
            if (subscribe)
            {
                ManastoneManager.Instance.OnCurrencyChanged += UpdateManastoneText;
                UpdateManastoneText(ManastoneManager.Instance.Amount);
            }
        }
    }

    private void UpdateSoulstoneText(int amount)
    {
        if (soulstoneText != null) soulstoneText.text = amount.ToString("N0");
    }

    private void UpdateManastoneText(int amount)
    {
        if (manastoneText != null) manastoneText.text = amount.ToString("N0");
    }

    // PartyManager.OnPartyChanged 구독/해제 — 멤버 변경 시 자동 Refresh.
    private void SubscribeParty(bool subscribe)
    {
        if (PartyManager.Instance == null) return;
        PartyManager.Instance.OnPartyChanged -= Refresh;
        if (subscribe) PartyManager.Instance.OnPartyChanged += Refresh;
    }

    private void WireButtons()
    {
        if (partyEditButton != null)
        {
            partyEditButton.onClick.RemoveListener(OpenPartyEdit);
            partyEditButton.onClick.AddListener(OpenPartyEdit);
        }
        if (settingButton != null)
        {
            settingButton.onClick.RemoveListener(OpenSetting);
            settingButton.onClick.AddListener(OpenSetting);
        }
        if (logButton != null)
        {
            logButton.onClick.RemoveListener(OpenLog);
            logButton.onClick.AddListener(OpenLog);
        }
    }

    private void OpenSetting()
    {
        if (PopupManager.Instance != null) PopupManager.Instance.OpenSetting();
    }

    private void OpenLog()
    {
        if (PopupManager.Instance != null) PopupManager.Instance.OpenLog();
    }

    // PartyEditPanel.Open() 내부에 전투 노드 진입 시 차단 가드 있음 (A-6).
    private void OpenPartyEdit()
    {
        if (partyEditPanel == null)
        {
            Debug.LogWarning("[LeftPanelView] partyEditPanel 미연결 — 인스펙터 슬롯 확인");
            return;
        }
        partyEditPanel.Open();
    }

    /// <summary>
    /// 외부에서 호출 가능한 강제 갱신. 파티 멤버 변경(모집/사망) 시 호출.
    /// </summary>
    public void Refresh()
    {
        UnbindAll();

        // 사망자는 즉시 제외 — 아군 사망 시 빈칸 없이 곧바로 압축 정렬 (기획자 피드백 #12)
        var party = (PartyManager.Instance != null
            ? PartyManager.Instance.GetActiveFellows()
            : new List<FellowData>())
            .Where(f => f != null && !f.isDead).ToList();

        // 카드 슬롯
        for (int i = 0; i < cardSlots.Length; i++)
        {
            var slot = cardSlots[i];
            if (slot == null) continue;

            if (i < party.Count)
            {
                slot.Bind(party[i]);
            }
            else
            {
                slot.Unbind();
                slot.gameObject.SetActive(false);
            }
        }

        // 스트레스 이벤트 구독 + 행 표시
        foreach (var f in party)
        {
            f.OnDied += OnFellowDied;   // 사망 즉시 재빌드 (#12)
            _bound.Add(f);
        }

        // 덱 요약
        RefreshDeckSummary(party);
        // (파티 아코디언 기본 펼침은 사용자 요청으로 제거 — 모든 섹션 기본 접힘 상태로 시작. 2026-06-11)

        // 열린 아코디언 높이 재측정 — 사망 등으로 내용이 줄어도 빈 공간이 남지 않도록 (QA ②)
        foreach (var acc in GetComponentsInChildren<AccordionController>(true))
            acc.RefreshHeight();
    }

    private void UnbindAll()
    {
        foreach (var f in _bound)
            if (f != null) f.OnDied -= OnFellowDied;
        _bound.Clear();
    }

    // 파티 멤버 사망 시 즉시 재빌드 — 빈칸 없이 곧바로 압축 정렬 (#12)
    private void OnFellowDied() => Refresh();

    private void RefreshDeckSummary(IList<FellowData> party)
    {
        if (deckSummaryText == null) return;

        Loc.Bind(deckSummaryText, () => BuildDeckSummary(party));
    }

    private static string BuildDeckSummary(IList<FellowData> party)
    {
        // 파티에 존재하는 성향만, 등장 순서 유지
        var order = new List<CardAffinity>();
        var groups = new Dictionary<CardAffinity, List<string>>();

        foreach (var f in party)
        {
            var aff = f.affinity;
            if (!groups.ContainsKey(aff))
            {
                groups[aff] = new List<string>();
                order.Add(aff);
            }
            groups[aff].Add(!string.IsNullOrEmpty(f.displayName) ? Loc.Tr(f.displayName) : f.id);
        }

        var lines = new List<string>();
        foreach (var aff in order)
        {
            var names = string.Join(", ", groups[aff]);
            lines.Add($"{Loc.Tr(AffinityHelper.GetLabel(aff))}: {groups[aff].Count}({names})");
        }

        return lines.Count > 0
            ? string.Join("\n", lines)
            : Loc.Tr("(파티 없음)");
    }
}
