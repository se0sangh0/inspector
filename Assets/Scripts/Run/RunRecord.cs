// ============================================================
// Run/RunRecord.cs
// 런 사건 기록 데이터 — 경로·사건·전투·결과 (P0-03)
// ============================================================
//
// [이 파일이 하는 일]
//   한 런 동안 확정된 결과를 사건 단위로 누적합니다 (추가 전용).
//   조사관 수첩(P0-05)과 탐사 보고서가 이 데이터를 읽어 표시합니다.
//
// [계약 — 16-A §5 기록 단위 / 16-B §4]
//   - RouteSelected      : 경로 카드의 제목·환경·징조 (P0-02 에서 사용)
//   - LocationRevealed   : 실제 장소 (P0-02 에서 사용)
//   - BattleResolved     : 위치·조우 대상·승리/전멸·사후 관찰·주요 획득
//   - ChoiceResolved     : 사건 ID·선택한 행동·확정 결과 (P0-04 에서 사용)
//   - RecruitmentResolved: 실제 고용·교체 결과 (P0-04 에서 사용)
//   - RecoveryResolved   : 실제 회복·부활 결과 (P0-04 에서 사용)
//   - RunResolved        : 클리어/전멸과 최종 도달 구역
//   - 상태 적용과 기록 생성은 같은 트랜잭션. 결과마다 정확히 1건.
//     화면 재열람·연타로 같은 사건을 추가하지 않는다 (호출측 가드 + dedupKey).
//   - GameLogService(수치 행동 로그)와 분리 — 수첩 데이터에 로그 문자열을
//     복사하지 않는다. 준 피해·받은 피해·회복·실드 수치는 영구 기록하지 않는다.
// ============================================================

using System.Collections.Generic;

/// <summary>런 사건 기록의 종류 (16-A §5 구현 참고).</summary>
public enum RunRecordType
{
    RouteSelected       = 0,
    LocationRevealed    = 1,
    BattleResolved      = 2,
    ChoiceResolved      = 3,
    RecruitmentResolved = 4,
    RecoveryResolved    = 5,
    RunResolved         = 6,
}

/// <summary>사건 기록 한 건. 추가 전용 — 생성 후 수정하지 않는다.</summary>
public class RunRecordEntry
{
    public RunRecordType type;
    public int    floor;                 // 사건이 일어난 층 (1-base, 불명이면 0)
    public int    node;                  // 노드 선택 위치 (왼1/중2/오3, 0=미상) — 수첩 '제 N구역'
    public LocalizedMessage title;                 // 기록 표제 (전투 결과 등). 비면 헤더만 표시
    public List<LocalizedMessage> lines = new();   // 본문 줄들 (항목당 한 줄, 수첩 표시용 원자료)

    public override string ToString()
        => $"[{type}] {floor}층 - {title} | {string.Join(" / ", lines)}";
}

/// <summary>
/// 한 런의 사건 기록 묶음. RunSessionManager 가 소유하며
/// StartNewRun 에서 새로 만들고 FinalizeRun(보고서 생성 뒤)에서 폐기한다.
/// </summary>
public class RunRecord
{
    private readonly List<RunRecordEntry> _entries = new();
    private readonly HashSet<string> _dedupKeys = new();

    public IReadOnlyList<RunRecordEntry> Entries => _entries;

    /// <summary>
    /// 사건 기록 1건 추가. dedupKey 가 이미 기록된 키면 무시하고 false —
    /// 화면 재열람·연타로 같은 사건이 중복 기록되는 것을 막는 최종 안전망.
    /// </summary>
    public bool Add(RunRecordEntry entry, string dedupKey = null)
    {
        if (entry == null) return false;
        if (dedupKey != null && !_dedupKeys.Add(dedupKey))
        {
            UnityEngine.Debug.Log($"[RunRecord] 중복 기록 무시 — {dedupKey}");
            return false;
        }
        _entries.Add(entry);
        UnityEngine.Debug.Log($"[RunRecord] +{entry}");
        return true;
    }
}
