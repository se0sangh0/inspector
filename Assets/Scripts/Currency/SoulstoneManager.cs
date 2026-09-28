// ============================================================
// Currency/SoulstoneManager.cs
// 영혼석(Soulstone) 재화 싱글톤 매니저
// ============================================================
//
// [이 파일이 하는 일]
//   영혼석 재화를 관리하고, 화면의 영혼석 숫자 UI 를 업데이트합니다.
//   SoulstoneManager.Instance.Add(3) 으로 어디서나 영혼석을 추가할 수 있습니다.
//
// [어디서 쓰이나요?]
//   - 동료 모집, 특수 아이템 구매 등 영혼석이 필요한 모든 곳
//
// [연결된 파일]
//   - Currency/BaseCurrency.cs : 재화 관리 공통 베이스 클래스
//   - Currency/ManastoneManager.cs : 마나석 매니저 (이것과 쌍을 이룸)
//
// [인스펙터 설정]
//   - amountText : 영혼석 개수를 표시할 TMP 텍스트를 연결하세요.
//   - 시작 영혼석: 10개 (StartingAmount = 10)
// ============================================================

using UnityEngine;
using TMPro;

/// <summary>
/// 영혼석 재화 싱글톤 매니저.
/// SoulstoneManager.Instance 로 전역 접근 가능.
/// </summary>
public class SoulstoneManager : BaseCurrency<SoulstoneManager>
{
    // ----------------------------------------------------------
    // [amountText] — 화면에 영혼석 개수를 보여주는 텍스트
    // Inspector 에서 TMP 텍스트 오브젝트를 연결하세요.
    // ----------------------------------------------------------
    [SerializeField]
    [Tooltip("영혼석 개수를 표시할 TMP_Text 오브젝트를 연결하세요.")]
    private TMP_Text amountText;

    // ----------------------------------------------------------
    // [SaveKey] — PlayerPrefs 저장 키
    // 키 문자열의 단일 소유 지점. 외부(RunSessionManager·TutorialManager)는
    // 반드시 이 상수를 참조한다 — 문자열 중복 선언 금지 (16 §3).
    // ----------------------------------------------------------
    public const string PrefsKey = "SoulStone";
    protected override string SaveKey => PrefsKey;

    // ----------------------------------------------------------
    // [StartingAmount] — 게임 시작 시 기본 영혼석 개수
    // 시작값 20 = 리롤 5회 가능 (비용 2+3+4+5+6 누적 20).
    // 게임오버/스테이지 클리어 시 진행 노드 수 × 1 만큼 추가 보상 (후속 작업).
    // ----------------------------------------------------------
    // ⚠️ PlayerPrefs("SoulStone") 에 이미 저장된 값이 있으면 그 값이 우선됨.
    //    초기값을 즉시 반영하려면 PlayerPrefs 삭제 필요.
    protected override int StartingAmount => 20;

    /// <summary>보고서는 현재 잔액이 아니라 런의 총획득량을 사용한다 (16-E §9).</summary>
    public override void Add(int value)
    {
        int before = Amount;
        base.Add(value);
        var session = RunSessionManager.Instance;
        if (session != null && session.IsRunActive)
            session.Records.RecordSoulstoneGain(Amount - before);
    }

    // ----------------------------------------------------------
    // UpdateText — 영혼석 값이 바뀔 때마다 화면 텍스트 갱신
    // OnCurrencyChanged 이벤트에 자동 구독됨 (BaseCurrency.Awake 에서 처리)
    // ----------------------------------------------------------
    protected override void UpdateText(int amount)
    {
        if (amountText == null) return;

        // N0 형식: 천 단위 쉼표 (예: 1,000)
        amountText.text = $"{amount:N0}";
    }
}
