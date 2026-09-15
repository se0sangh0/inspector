// ============================================================
// Mercenary/MercenaryService.cs
// 용병소 비즈니스 로직 싱글톤
// ============================================================
//
// [기획 참조]
//   §14_용병소_시스템_명세 — 후보 3인 / 예비대 9칸 / 리롤(2→3→4...) / 노드 리셋
//   백로그/01_동료_확장_백로그 §4 합성 / §5 성급 — 4가지 케이스, 1.4×HP / 1.25×Power
//
// [라이프사이클]
//   - OnEnterNode()  : 노드 진입 시 후보 3인 새로 롤, 리롤 카운터 0
//   - OnLeaveNode()  : 노드 이탈 시 후보·리롤 카운터 초기화. 예비대는 유지 (기획 §5-2 갱신)
//   - TryHire(idx)   : 영혼석 차감 → 파티 빈 슬롯이면 즉시 합류, 만석이면 예비대로
//   - TryReroll()    : 영혼석 차감 → 후보 3인 재롤, 비용 +1 누적
//   - TrySynthesize(): 파티/예비대에서 동료 3명 선택 → 4가지 케이스 분기 → 상위 성급 동료 예비대로
//
// [후보 풀 정책]
//   fellow.json 의 5종 (캐스터/오펜더/디펜더/어택커/프리스트) 에서 중복 허용 랜덤.
//   샤먼은 백로그 — 출현 안 함. Affinity 는 매 후보마다 랜덤.
// ============================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MercenaryService : Singleton<MercenaryService>
{
    // ----------------------------------------------------------
    // 상수 (기획 §14)
    // ----------------------------------------------------------
    public const int CandidateCount      = 3;     // 화면에 표시되는 후보 수
    public const int ReservesCapacity    = 9;     // 예비대 슬롯 (기획 §5-3)
    public const int RerollBaseCost      = 2;     // 최초 리롤 비용 (기획 §5-4)
    public const int RerollIncrement     = 1;     // 노드 내 +1 누적

    // 합성에서 제외할 직업군 (백로그 — 샤먼)
    private static readonly HashSet<string> ExcludedFromPool = new() { "ally_shaman_01" };

    // ----------------------------------------------------------
    // 런타임 상태
    // ----------------------------------------------------------
    private readonly List<FellowData> _candidates = new(CandidateCount);
    private readonly List<FellowData> _reserves   = new();
    private int _rerollCount = 0;

    // ----------------------------------------------------------
    // 공개 조회
    // ----------------------------------------------------------
    public IReadOnlyList<FellowData> Candidates => _candidates;
    public IReadOnlyList<FellowData> Reserves   => _reserves;
    public int RerollCount    => _rerollCount;
    public int NextRerollCost => RerollBaseCost + _rerollCount * RerollIncrement;
    public bool ReservesFull  => _reserves.Count >= ReservesCapacity;
    public int ReservesFree   => ReservesCapacity - _reserves.Count;

    /// <summary>예비대 인덱스 안전 조회. 범위 밖이면 null.</summary>
    public FellowData GetReserve(int index)
        => (index >= 0 && index < _reserves.Count) ? _reserves[index] : null;

    // ----------------------------------------------------------
    // 라이프사이클 — NodeSystem 이 호출
    // ----------------------------------------------------------

    /// <summary>용병소 노드 진입 시 후보 3인 롤 + 리롤 카운터 초기화.</summary>
    public void OnEnterNode()
    {
        _rerollCount = 0;
        RollCandidates();
        Debug.Log($"[Mercenary] 노드 진입 — 후보 {_candidates.Count}명 롤, 예비대 {_reserves.Count}/{ReservesCapacity}");
    }

    /// <summary>용병소 노드 이탈 시 후보·리롤 리셋. 예비대는 런 종료 시까지 유지. (기획 §5-2)</summary>
    public void OnLeaveNode()
    {
        _candidates.Clear();
        _rerollCount = 0;
        Debug.Log("[Mercenary] 노드 이탈 — 후보·리롤 카운터 초기화 (예비대 유지)");
    }

    /// <summary>런 종료 → 새 런 시작 시 예비대/후보/리롤 전체 초기화 (기획 §16 로그라이크 루프).</summary>
    public void ResetForNewRun()
    {
        _reserves.Clear();
        _candidates.Clear();
        _rerollCount = 0;
        Debug.Log("[Mercenary] 새 런 — 예비대/후보/리롤 전체 초기화");
    }

    // ----------------------------------------------------------
    // 후보 롤 / 리롤
    // ----------------------------------------------------------

    /// <summary>후보 3인을 새로 생성한다. 기존 후보는 폐기.</summary>
    private void RollCandidates()
    {
        _candidates.Clear();
        if (FellowDatabase.Instance == null)
        {
            Debug.LogWarning("[Mercenary] FellowDatabase 없음 — 후보 생성 실패.");
            return;
        }

        var pool = BuildCandidatePool();
        if (pool.Count == 0)
        {
            Debug.LogWarning("[Mercenary] 후보 풀이 비어있음.");
            return;
        }

        for (int i = 0; i < CandidateCount; i++)
        {
            var def      = pool[Random.Range(0, pool.Count)]; // 중복 허용 (사용자 결정 Q4·a)
            var affinity = RandomAffinity();
            _candidates.Add(FellowDatabase.CreateRuntimeFellow(def, affinity));
        }
    }

    /// <summary>fellow.json 의 5종(샤먼 제외)을 후보 풀로 모은다.</summary>
    private List<FellowDef> BuildCandidatePool()
    {
        var pool = new List<FellowDef>();
        foreach (var role in new[] { "Dealer", "Tanker", "Support" })
            pool.AddRange(FellowDatabase.Instance.GetFellowsByRole(role));

        // 백로그 직업(샤먼 등) 제외
        pool.RemoveAll(d => d == null || ExcludedFromPool.Contains(d.id));
        return pool;
    }

    /// <summary>영혼석을 NextRerollCost 만큼 차감하고 후보 3인 재생성.</summary>
    public bool TryReroll()
    {
        int cost = NextRerollCost;
        if (SoulstoneManager.Instance == null || SoulstoneManager.Instance.Amount < cost)
        {
            Debug.Log($"[Mercenary] 리롤 실패 — 영혼석 부족 (필요 {cost})");
            return false;
        }

        SoulstoneManager.Instance.Use(cost);
        AudioManager.Instance?.PlaySfxById(SfxId.CoinSpend);
        _rerollCount++;
        RollCandidates();
        Debug.Log($"[Mercenary] 리롤 성공 (-{cost} 영혼석) | 다음 리롤 비용 {NextRerollCost}");
        return true;
    }

    // ----------------------------------------------------------
    // 고용
    // ----------------------------------------------------------

    /// <summary>
    /// 후보 idx 의 동료를 고용한다.
    /// 파티 빈 슬롯이 있으면 즉시 합류, 만석이면 예비대로.
    /// 예비대도 가득 차 있으면 거부.
    /// </summary>
    public bool TryHire(int candidateIndex)
    {
        if (candidateIndex < 0 || candidateIndex >= _candidates.Count)
        {
            Debug.LogWarning($"[Mercenary] 잘못된 후보 인덱스: {candidateIndex}");
            return false;
        }
        var candidate = _candidates[candidateIndex];
        if (candidate == null) return false;

        int cost = candidate.recruitCost;
        if (SoulstoneManager.Instance == null || SoulstoneManager.Instance.Amount < cost)
        {
            Debug.Log($"[Mercenary] 고용 실패 — 영혼석 부족 (필요 {cost})");
            return false;
        }

        bool partyHasRoom = PartyManager.Instance != null
            && PartyManager.Instance.CompanionCount < 4;

        if (!partyHasRoom && ReservesFull)
        {
            Debug.Log("[Mercenary] 고용 실패 — 파티/예비대 모두 가득 참 (먼저 정리 필요)");
            return false;
        }

        SoulstoneManager.Instance.Use(cost);
        AudioManager.Instance?.PlaySfxById(SfxId.Recruit);
        _candidates[candidateIndex] = null; // 후보 슬롯 비움 (UI 가 회색 처리)

        if (partyHasRoom)
        {
            PartyManager.Instance.RecruitFellow(candidate);
            Debug.Log($"[Mercenary] 고용 → 파티 합류 ({candidate.displayName}, -{cost} 영혼석)");
        }
        else
        {
            _reserves.Add(candidate);
            Debug.Log($"[Mercenary] 고용 → 예비대 ({candidate.displayName}, -{cost} 영혼석) | 예비대 {_reserves.Count}/{ReservesCapacity}");
        }

        // 실제 고용 결과 기록 — 같은 트랜잭션에서 RecruitmentResolved 1건 (16-A §4·§5, P0-04)
        RecordRecruitment("용병소 — 고용",
            Loc.Message("{0} ({1} | {2}★) {3}", Loc.Message(candidate.displayName), candidate.jobClass, candidate.starLevel, Loc.Message(partyHasRoom ? "파티 합류" : "예비대 보관")),
            Loc.Message("영혼석 -{0} → 보유 {1}", cost, SoulstoneManager.Instance.Amount));
        return true;
    }

    /// <summary>모집·교체 결과를 RecruitmentResolved 1건으로 기록 (상태 적용과 같은 트랜잭션).</summary>
    private static void RecordRecruitment(string title, params LocalizedMessage[] lines)
    {
        var list = new List<LocalizedMessage>();
        foreach (var l in lines)
            if (l != null) list.Add(l);
        RunSessionManager.Instance?.AddRecord(RunRecordType.RecruitmentResolved, title, list);
    }

    // ----------------------------------------------------------
    // 파티 ↔ 예비대 교환
    // ----------------------------------------------------------

    /// <summary>예비대 동료를 파티 빈 슬롯에 합류시킨다. (만석이면 false)</summary>
    public bool TryAssignReserveToParty(int reserveIndex)
    {
        var f = GetReserve(reserveIndex);
        if (f == null) return false;
        if (PartyManager.Instance == null) return false;
        if (PartyManager.Instance.CompanionCount >= 4) return false;

        _reserves.RemoveAt(reserveIndex);
        PartyManager.Instance.RecruitFellow(f);
        RecordRecruitment("파티 편성 — 교체", Loc.Message("{0} 예비대 → 파티 합류", Loc.Message(f.displayName)));
        return true;
    }

    /// <summary>파티 동료를 예비대로 빼낸다. (예비대 만석이면 false)</summary>
    public bool TryMovePartyToReserve(FellowData partyFellow)
    {
        if (partyFellow == null) return false;
        if (ReservesFull) return false;
        if (PartyManager.Instance == null) return false;

        PartyManager.Instance.RemoveFellow(partyFellow);
        _reserves.Add(partyFellow);
        RecordRecruitment("파티 편성 — 교체", Loc.Message("{0} 파티 → 예비대 보관", Loc.Message(partyFellow.displayName)));
        return true;
    }

    /// <summary>파티 동료와 예비대 동료를 1:1 교체.</summary>
    public bool TrySwapPartyAndReserve(FellowData partyFellow, int reserveIndex)
    {
        var reserveFellow = GetReserve(reserveIndex);
        if (partyFellow == null || reserveFellow == null) return false;
        if (PartyManager.Instance == null) return false;

        PartyManager.Instance.RemoveFellow(partyFellow);
        _reserves.RemoveAt(reserveIndex);
        _reserves.Add(partyFellow);
        PartyManager.Instance.RecruitFellow(reserveFellow);
        RecordRecruitment("파티 편성 — 교체", Loc.Message("{0} ↔ {1} 교체", Loc.Message(partyFellow.displayName), Loc.Message(reserveFellow.displayName)));
        return true;
    }

    // ----------------------------------------------------------
    // 합성 — 백로그 §4 합성/승급
    // ----------------------------------------------------------

    /// <summary>
    /// FellowData 3명을 받아 합성한다. 비용 무료 (기획 명시 없음).
    /// 입력은 파티원 또는 예비대 어디서든 가능 — 내부에서 소속 자동 판별 후 제거.
    /// 결과 동료는 예비대에 추가 (예비대 빈자리는 항상 충분, 입력에 예비대원 1+ 포함되면 -N+1, 전부 파티원이면 +1).
    /// </summary>
    public bool TrySynthesize(FellowData f1, FellowData f2, FellowData f3, out FellowData result)
    {
        result = null;

        var inputs = new[] { f1, f2, f3 };
        if (inputs.Any(f => f == null))
        {
            Debug.Log("[Mercenary·합성] 실패 — 입력 슬롯 중 비어있음");
            return false;
        }
        if (inputs.Distinct().Count() != 3)
        {
            Debug.Log("[Mercenary·합성] 실패 — 동일 동료 중복 선택");
            return false;
        }

        // 같은 성급 3명만 합성 가능 (기획 백로그 §4)
        int baseStar = inputs[0].starLevel;
        if (inputs.Any(f => f.starLevel != baseStar))
        {
            Debug.Log($"[Mercenary·합성] 실패 — 성급 불일치 ({string.Join(",", inputs.Select(f => f.starLevel))})");
            return false;
        }

        bool sameRole     = inputs.Select(f => f.role).Distinct().Count() == 1;
        bool sameAffinity = inputs.Select(f => f.affinity).Distinct().Count() == 1;

        // 결과 역할 / 성향 결정 (4 케이스 — 백로그 §4)
        CompanionRole resultRole = sameRole ? inputs[0].role : PickRandomRole();
        CardAffinity  resultAff  = sameAffinity ? inputs[0].affinity : RandomAffinity();
        int           resultStar = baseStar + 1;

        // 그 역할의 FellowDef 풀에서 랜덤 1명
        var def = PickRandomDefByRole(resultRole);
        if (def == null)
        {
            Debug.LogWarning($"[Mercenary·합성] 실패 — {resultRole} 역할 동료 정의 없음");
            return false;
        }

        result = FellowDatabase.CreateRuntimeFellow(def, resultAff, resultStar);

        // 입력 3명 제거 — 예비대 우선 시도, 없으면 파티원으로 간주해 PartyManager 에서 제거
        foreach (var f in inputs) RemoveFellowFromAnySource(f);

        // 결과 예비대 추가
        _reserves.Add(result);

        Debug.Log($"┌─ [Mercenary·합성 성공] {baseStar}★ ×3 → {resultStar}★");
        Debug.Log($"│  입력: {string.Join(" / ", inputs.Select(f => $"{f.role}·{f.AffinityLabel}·{f.starLevel}★"))}");
        Debug.Log($"│  결과: {result.displayName} ({result.role}·{result.AffinityLabel}·{result.starLevel}★)");
        Debug.Log($"│  케이스: sameRole={sameRole}, sameAffinity={sameAffinity}");
        Debug.Log($"└────────");
        return true;
    }

    /// <summary>예비대 우선 제거 → 없으면 파티원으로 간주해 PartyManager 에서 제거.</summary>
    private void RemoveFellowFromAnySource(FellowData f)
    {
        if (_reserves.Remove(f)) return;
        if (PartyManager.Instance != null) PartyManager.Instance.RemoveFellow(f);
    }

    /// <summary>
    /// 예비대 명단에서 동료를 방출(영구 제거). 영혼석 환불 없음.
    /// UI 의 미니 카드 X 버튼에서 호출.
    /// </summary>
    public bool DismissReserve(FellowData fellow)
    {
        if (fellow == null) return false;
        if (!_reserves.Remove(fellow)) return false;

        // 방출 트랜잭션 기록 (16-A §4: 예비대 방출·판매 시 RecruitmentResolved 한 건)
        RecordRecruitment("용병소 — 방출", Loc.Message("{0} ({1} | {2}★) 방출 (환급 없음)", Loc.Message(fellow.displayName), fellow.jobClass, fellow.starLevel));

        Debug.Log($"[Mercenary] 예비대 방출 — {fellow.displayName} ({fellow.role}·{fellow.starLevel}★) | 예비대 {_reserves.Count}/{ReservesCapacity}");
        return true;
    }

    /// <summary>
    /// 예비대 동료 판매 — 모집비 × 1/3 환급 후 영구 제거.
    /// 반환: 환급된 영혼석 수량 (실패 시 0).
    /// </summary>
    public int TrySellReserve(FellowData fellow)
    {
        if (fellow == null) return 0;
        if (!_reserves.Remove(fellow)) return 0;

        int refund = Mathf.Max(1, fellow.recruitCost / 3);
        if (SoulstoneManager.Instance != null) SoulstoneManager.Instance.Add(refund);
        AudioManager.Instance?.PlaySfxById(SfxId.Sell);

        // 판매 트랜잭션 기록 (16-A §4: 예비대 방출·판매 시 RecruitmentResolved 한 건)
        RecordRecruitment("용병소 — 판매",
            Loc.Message("{0} ({1} | {2}★) 판매", Loc.Message(fellow.displayName), fellow.jobClass, fellow.starLevel),
            Loc.Message("영혼석 +{0} → 보유 {1}", refund, SoulstoneManager.Instance?.Amount ?? 0));

        Debug.Log($"[Mercenary] 예비대 판매 — {fellow.displayName} (+{refund} 영혼석) | 예비대 {_reserves.Count}/{ReservesCapacity}");
        return refund;
    }

    /// <summary>판매가 미리 계산 — UI 라벨용. fellow null 안전.</summary>
    public static int CalcSellPrice(FellowData fellow)
        => fellow == null ? 0 : Mathf.Max(1, fellow.recruitCost / 3);

    private CompanionRole PickRandomRole()
    {
        var arr = new[] { CompanionRole.Dealer, CompanionRole.Tanker, CompanionRole.Support };
        return arr[Random.Range(0, arr.Length)];
    }

    private FellowDef PickRandomDefByRole(CompanionRole role)
    {
        if (FellowDatabase.Instance == null) return null;
        string roleStr = role.ToString();
        var defs = FellowDatabase.Instance.GetFellowsByRole(roleStr)
            .Where(d => d != null && !ExcludedFromPool.Contains(d.id))
            .ToList();
        return defs.Count == 0 ? null : defs[Random.Range(0, defs.Count)];
    }

    // ----------------------------------------------------------
    // 유틸
    // ----------------------------------------------------------
    private static CardAffinity RandomAffinity()
    {
        var arr = new[]
        {
            CardAffinity.Gambler,
            CardAffinity.Safety,
            CardAffinity.Opportunist,
            CardAffinity.Optimist,
        };
        return arr[Random.Range(0, arr.Length)];
    }
}
