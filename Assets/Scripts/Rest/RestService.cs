// ============================================================
// Rest/RestService.cs
// 화툿불(휴식) 노드 — HP/스트레스 회복 로직
// ============================================================
//
// [기획 참조]
//   §04_스트레스_디버프_표 §기본 회복 — "화툿불/휴식 노드: -15"
//   §02_MVP_노드_설계 §화툿불 — "체력/스트레스 회복, 다음 전투 전 정비"
//
// [수치 정책 — P0-04 갱신 (16-A §4 5층 화톳불)]
//   기본 회복 = 생존 동료 전원 HP +30. HP 30 은 H2 정식 확정 전 시험값.
//   스트레스 -15 는 기존 직렬화 시험값 유지 (16 §4: 명시 테스트값·현재 값 사용).
//
// [사용처]
//   RestPanel — 노드 방문당 1회만 호출 (확인 카드 연타·패널 재활성화로
//   회복을 중복 적용하지 않는다 — 16-A §4).
// ============================================================

using System.Linq;
using UnityEngine;

public static class RestService
{
    /// <summary>화톳불 기본 HP 회복량. 16-A §4 — H2 정식 확정 전 시험값.</summary>
    public const int HpRecoveryAmount = 30;

    /// <summary>화툿불 스트레스 회복량 (기존 시험값 유지).</summary>
    public const int RecoveryAmount = 15;

    /// <summary>회복 결과 통계 — UI 표시용.</summary>
    public struct RecoveryResult
    {
        public int affectedCount;   // 적용받은 살아있는 동료 수
        public int totalHpRecovered;
        public int totalStressRelieved;
    }

    /// <summary>
    /// 살아있는 파티원 전체에게 HP +HpRecoveryAmount / 스트레스 -RecoveryAmount 적용.
    /// HP 는 maxHp 까지 clamp, 스트레스는 0 까지 clamp.
    /// 같은 트랜잭션에서 RecoveryResolved 사건 1건을 기록한다 (16-A §5).
    /// 방문당 1회 호출 보장은 RestPanel 이 담당한다.
    /// </summary>
    public static RecoveryResult ApplyRecovery()
    {
        var result = new RecoveryResult();
        if (PartyManager.Instance == null) return result;

        var fellows = PartyManager.Instance.GetActiveFellows()
            .Where(f => f != null && !f.isDead)
            .ToList();

        var recordLines = new System.Collections.Generic.List<LocalizedMessage>();
        foreach (var f in fellows)
        {
            // HP 회복 — CurrentHp setter 가 OnHpChanged 발생 → 슬라이더 자동 갱신
            int maxHp     = f.maxHp > 0 ? f.maxHp : 100;
            int beforeHp  = f.CurrentHp;
            f.CurrentHp   = Mathf.Min(maxHp, beforeHp + HpRecoveryAmount);
            int hpGained  = f.CurrentHp - beforeHp;

            // 스트레스 회복
            int beforeStress = f.currentStress;
            f.currentStress  = Mathf.Max(0, beforeStress - RecoveryAmount);
            int stressRelieved = beforeStress - f.currentStress;

            result.affectedCount++;
            result.totalHpRecovered    += hpGained;
            result.totalStressRelieved += stressRelieved;

            string label = !string.IsNullOrEmpty(f.displayName) ? f.displayName : f.positionStack.ToString();
            //recordLines.Add($"{label} HP +{hpGained} → {f.CurrentHp}, 스트레스 -{stressRelieved} → {f.currentStress}");
            Debug.Log($"[Rest] {label} — HP {beforeHp}→{f.CurrentHp} (+{hpGained}), 스트레스 {beforeStress}→{f.currentStress} (-{stressRelieved})");
        }

        // 회복 사건 기록 — 방문(층)당 1건 (연타·재열람으로 재생성되지 않도록 dedup)
        if (result.affectedCount > 0)
            RunSessionManager.Instance?.AddRecord(RunRecordType.RecoveryResolved, "화톳불 정비", recordLines,
                dedupKey: $"rest_F{(NodeSystem.Current != null ? NodeSystem.Current.CurrentFloor : 0)}");

        Debug.Log($"[Rest] 회복 완료 — {result.affectedCount}명, 총 HP+{result.totalHpRecovered} / 스트레스-{result.totalStressRelieved}");
        return result;
    }
}
