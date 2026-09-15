// ============================================================
// Church/ChurchService.cs
// 교회 노드 — 영혼석 소비로 HP/스트레스 회복 + 사망 동료 부활
// ============================================================
//
// [기획 참조]
//   §02_MVP_노드_설계 §교회 — "체력 회복, 스트레스 관리, 사망 동료 부활"
//
// [수치] (사용자 결정 2026-05-28)
//   HpCost       = 30  (전원 HP +HpAmount)
//   StressCost   = 20  (전원 스트레스 -StressAmount)
//   ReviveCost1  = 50  (1성 동료 부활)
//   ReviveCost2  = 100 (2성 동료 부활)
//   ReviveCost3  = 200 (3성 동료 부활)
//   부활 시 HP = maxHp × 1.0 (기획자 결정 2026-05-28 — 풀피 복귀)
//
// [사용처]
//   ChurchPanel 이 각 버튼 클릭 시 TrySpend... 호출.
// ============================================================

using System.Linq;
using UnityEngine;

public static class ChurchService
{
    // ── 비용 ────────────────────────────────────────────────
    public const int HpCost       = 30;
    public const int StressCost   = 20;
    public const int ReviveCost1  = 50;
    public const int ReviveCost2  = 100;
    public const int ReviveCost3  = 200;

    // ── 회복량 ──────────────────────────────────────────────
    public const int HpAmount     = 50;
    public const int StressAmount = 25;
    public const float ReviveHpRatio = 1.0f; // 기획자 결정 2026-05-28 — 부활 시 풀피 복귀

    /// <summary>성급에 따른 부활 비용. 1성 50 / 2성 100 / 3성 200 / 그 이상 200 (안전 폴백).</summary>
    public static int GetReviveCost(int starLevel) => starLevel switch
    {
        1 => ReviveCost1,
        2 => ReviveCost2,
        3 => ReviveCost3,
        _ => ReviveCost3,
    };

    // ── HP 회복 — 살아있는 전원 +HpAmount (maxHp clamp) ─────
    public static bool TryBuyHpHeal()
    {
        if (SoulstoneManager.Instance == null) return false;
        if (SoulstoneManager.Instance.Amount < HpCost)
        {
            GameLog.Formatted($"영혼석 부족 (필요 {HpCost}).", LogCategory.Default);
            return false;
        }
        if (PartyManager.Instance == null) return false;

        var alive = PartyManager.Instance.GetActiveFellows().Where(f => f != null && !f.isDead).ToList();
        if (alive.Count == 0)
        {
            GameLog.Event("회복할 동료가 없습니다.", LogCategory.Default);
            return false;
        }

        SoulstoneManager.Instance.Use(HpCost);
        int totalGained = 0;
        var recordLines = new System.Collections.Generic.List<LocalizedMessage>();
        foreach (var f in alive)
        {
            int maxHp     = f.maxHp > 0 ? f.maxHp : 100;
            int beforeHp  = f.CurrentHp;
            f.CurrentHp   = Mathf.Min(maxHp, beforeHp + HpAmount);
            totalGained  += f.CurrentHp - beforeHp;
            recordLines.Add(Loc.Message("{0} HP +{1} → {2}", Loc.Message(f.displayName), f.CurrentHp - beforeHp, f.CurrentHp));
        }
        recordLines.Add(Loc.Message("영혼석 -{0} → 보유 {1}", HpCost, SoulstoneManager.Instance.Amount));

        // 실제 회복 결과 기록 — 상태 적용과 같은 트랜잭션 (16-A §5, P0-04)
        RunSessionManager.Instance?.AddRecord(RunRecordType.RecoveryResolved, "교회 — 회복 기도", recordLines);

        GameLog.Formatted($"교회 기도 — {alive.Count}명 HP +{HpAmount} (총 +{totalGained}, 영혼석 -{HpCost}).", LogCategory.Heal);
        return true;
    }

    // ── 스트레스 회복 — 살아있는 전원 -StressAmount ─────────
    public static bool TryBuyStressRelief()
    {
        if (SoulstoneManager.Instance == null) return false;
        if (SoulstoneManager.Instance.Amount < StressCost)
        {
            GameLog.Formatted($"영혼석 부족 (필요 {StressCost}).", LogCategory.Default);
            return false;
        }
        if (PartyManager.Instance == null) return false;

        var alive = PartyManager.Instance.GetActiveFellows().Where(f => f != null && !f.isDead).ToList();
        if (alive.Count == 0)
        {
            GameLog.Event("스트레스를 풀어줄 동료가 없습니다.", LogCategory.Default);
            return false;
        }

        SoulstoneManager.Instance.Use(StressCost);
        int totalRelieved = 0;
        var recordLines = new System.Collections.Generic.List<LocalizedMessage>();
        foreach (var f in alive)
        {
            int before        = f.currentStress;
            f.currentStress   = Mathf.Max(0, before - StressAmount);
            totalRelieved    += before - f.currentStress;
            recordLines.Add(Loc.Message("{0} 스트레스 -{1} → {2}", Loc.Message(f.displayName), before - f.currentStress, f.currentStress));
        }
        recordLines.Add(Loc.Message("영혼석 -{0} → 보유 {1}", StressCost, SoulstoneManager.Instance.Amount));

        RunSessionManager.Instance?.AddRecord(RunRecordType.RecoveryResolved, "교회 — 안정 기도", recordLines);

        GameLog.Formatted($"교회 기도 — {alive.Count}명 스트레스 -{StressAmount} (총 -{totalRelieved}, 영혼석 -{StressCost}).", LogCategory.Heal);
        return true;
    }

    // ── 부활 — 성급 비용, HP 절반 복귀 ─────────────────────
    public static bool TryReviveFellow(FellowData target)
    {
        if (target == null) return false;
        if (SoulstoneManager.Instance == null || PartyManager.Instance == null) return false;

        int cost = GetReviveCost(target.starLevel);
        if (SoulstoneManager.Instance.Amount < cost)
        {
            GameLog.Formatted($"영혼석 부족 — {target.displayName} 부활 (필요 {cost}).", LogCategory.Default);
            return false;
        }

        bool ok = PartyManager.Instance.ReviveFellow(target, ReviveHpRatio);
        if (!ok)
        {
            GameLog.Formatted($"부활 실패 — 파티 빈 슬롯이 없습니다.", LogCategory.Default);
            return false;
        }

        SoulstoneManager.Instance.Use(cost);

        // 실제 부활 결과 기록 — 상태 적용과 같은 트랜잭션 (16-A §5, P0-04)
        RunSessionManager.Instance?.AddRecord(RunRecordType.RecoveryResolved, "교회 — 부활",
            new System.Collections.Generic.List<LocalizedMessage>
            {
                Loc.Message("{0} 부활 ({1}★, HP {2})", Loc.Message(target.displayName), target.starLevel, target.CurrentHp),
                Loc.Message("영혼석 -{0} → 보유 {1}", cost, SoulstoneManager.Instance.Amount),
            });

        GameLog.Formatted($"{target.displayName} 부활! ({target.starLevel}★, 영혼석 -{cost})", LogCategory.Reward);
        return true;
    }
}
