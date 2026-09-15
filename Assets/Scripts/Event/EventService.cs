// ============================================================
// Event/EventService.cs
// `?` 노드 선택지 이벤트 — 코스트 지불 + 결과 추첨 + 효과 적용
// ============================================================
//
// [흐름]
//   EventPanel 이 선택지 클릭 시 ResolveChoice(choice) 호출
//   → ① 코스트 지불 (영혼석/HP/스트레스)
//   → ② outcomes 가중 랜덤으로 결과 1건 추첨
//   → ③ 결과의 effects 를 게임 상태에 적용
//   → 선택된 EventOutcome 반환 (패널이 resultText 표시)
//
// [적용 범위]
//   영혼석 / 스트레스 / HP 는 완전 적용.
//   동료 합류·스택 선지급·성향 재굴림·오브제·오염도는 관련 시스템이
//   미결(§6)이거나 별도 연동이 필요해 GameLog + Debug 로그로 남기고
//   TODO 로 표시한다. (자원 3종만으로도 리스크-리턴 선택은 성립한다)
// ============================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class EventService
{
    // 이번 선택으로 적용된 변화의 '집계' 요약 (예: "생존 동료 스트레스 +5", "영혼석 +15").
    // 결과 창(EventPanel)에 표시하는 상세이며, 조사관 수첩에는 넣지 않는다 (수첩은 12 §1-5 기본 양식).
    private static readonly List<LocalizedMessage> _effectSummary = new();

    /// <summary>직전 ResolveChoice 에서 적용된 효과 집계 요약. EventPanel 결과 창 표시용.</summary>
    public static IReadOnlyList<LocalizedMessage> LastEffectSummary => _effectSummary;

    /// <summary>영혼석 코스트를 지불할 수 있는지. (HP/스트레스 코스트는 항상 지불 가능으로 본다)</summary>
    public static bool CanAfford(EventChoice choice)
    {
        if (choice == null) return false;
        if (choice.costType == EventCostType.SoulStone)
            return (SoulstoneManager.Instance?.Amount ?? 0) >= choice.costAmount;
        return true;
    }

    /// <summary>
    /// 선택지를 확정한다. 코스트 지불 → 결과 추첨 → 효과 적용(effects 순서대로) →
    /// ChoiceResolved 사건 1건 기록 → 선택된 결과 반환 (16-B §3: 상태 적용과 기록은 같은 트랜잭션).
    /// 코스트를 못 내면 null 반환(패널에서 사전 차단되지만 안전용).
    /// 재클릭·연타는 EventPanel 이 선택지를 숨겨 차단하고, 기록은 dedupKey 가 최종 안전망.
    /// </summary>
    public static EventOutcome ResolveChoice(EventDefinition evt, EventChoice choice)
    {
        if (choice == null) return null;
        _effectSummary.Clear(); // 시작에서만 비운다 — 반환 후 EventPanel 이 LastEffectSummary 를 읽는다
        if (!PayCost(choice)) return null;

        var outcome = RollOutcome(choice);
        if (outcome != null)
        {
            foreach (var eff in outcome.effects) ApplyEffect(eff);
            if (!string.IsNullOrEmpty(outcome.resultText))
                GameLog.Event(outcome.resultText, LogCategory.Status);
            RecordChoiceResolved(evt, choice, outcome);
        }
        return outcome;
    }

    /// <summary>
    /// 조사관 수첩 사건 기록 1건 — 탐사국 공식 현장 기록 양식 (12 §1-5): 확인 장소 / 조치 / 결과.
    /// 내부 이벤트 ID·동료별 수치 상세는 넣지 않는다(그건 결과 창·GameLog 담당).
    /// </summary>
    private static void RecordChoiceResolved(EventDefinition evt, EventChoice choice, EventOutcome outcome)
    {
        var session = RunSessionManager.Instance;
        if (session == null || !session.IsRunActive) return;

        var lines = new List<LocalizedMessage>
        {
            Loc.Message("확인 장소: {0}", evt != null ? Loc.Message(evt.title) : Loc.Message("미상")),
            Loc.Message("조치: {0}", Loc.Message(choice.label)),
        };
        if (!string.IsNullOrEmpty(outcome.resultText))
            lines.Add(Loc.Message("결과: {0}", Loc.Message(outcome.resultText)));

        // 표제는 비운다 — 헤더 [O층 | 제 N구역] 아래 확인 장소/조치/결과 항목만 표시.
        session.AddRecord(RunRecordType.ChoiceResolved, "", lines,
            dedupKey: evt != null ? $"choice_{evt.id}" : null);
    }

    // ── 코스트 지불 ─────────────────────────────────────────────
    private static bool PayCost(EventChoice choice)
    {
        switch (choice.costType)
        {
            case EventCostType.SoulStone:
                if (choice.costAmount <= 0) return true;
                if (SoulstoneManager.Instance == null) return false;
                if (!SoulstoneManager.Instance.Use(choice.costAmount))
                {
                    GameLog.Formatted($"영혼석 부족 (필요 {choice.costAmount}).", LogCategory.Default);
                    return false;
                }
                GameLog.Formatted($"영혼석 -{choice.costAmount}", LogCategory.Reward);
                return true;

            case EventCostType.Hp:
                ApplyHp(-Mathf.Abs(choice.costAmount), EventTarget.All);
                return true;

            case EventCostType.Stress:
                ApplyStress(Mathf.Abs(choice.costAmount), EventTarget.All);
                return true;

            default:
                return true; // None
        }
    }

    // ── 결과 가중 추첨 ─────────────────────────────────────────
    private static EventOutcome RollOutcome(EventChoice choice)
    {
        var outs = choice.outcomes;
        if (outs == null || outs.Count == 0) return null;
        if (outs.Count == 1) return outs[0];

        int total = 0;
        foreach (var o in outs) total += Mathf.Max(0, o.weightPercent);
        if (total <= 0) return outs[0];

        int roll = Random.Range(0, total);
        int cum = 0;
        foreach (var o in outs)
        {
            cum += Mathf.Max(0, o.weightPercent);
            if (roll < cum) return o;
        }
        return outs[outs.Count - 1];
    }

    // ── 효과 적용 ──────────────────────────────────────────────
    private static void ApplyEffect(EventEffect eff)
    {
        if (eff == null) return;
        switch (eff.type)
        {
            case EventEffectType.None:
                break;

            case EventEffectType.SoulStone:
                if (eff.value > 0) { SoulstoneManager.Instance?.Add(eff.value); GameLog.Formatted($"영혼석 +{eff.value}", LogCategory.Reward); }
                else if (eff.value < 0) { SoulstoneManager.Instance?.Use(-eff.value); GameLog.Formatted($"영혼석 {eff.value}", LogCategory.Reward); }
                if (eff.value != 0)
                    _effectSummary.Add(Loc.Message("영혼석 {0}", (eff.value > 0 ? "+" : "") + eff.value));
                break;

            case EventEffectType.Stress:
                ApplyStress(eff.value, eff.target);
                break;

            case EventEffectType.Hp:
                ApplyHp(eff.value, eff.target);
                break;

            // ── P0-04 (16-A §4 EVT-01 계약) ────────────────────────
            case EventEffectType.HpLossNoKill:
                ApplyHpLossNoKill(Mathf.Abs(eff.value), eff.target);
                break;

            case EventEffectType.StressCapped:
                ApplyStressCapped(Mathf.Abs(eff.value), eff.target);
                break;

            // ── 미결/별도 연동 필요 — 로그만 남긴다 (TODO) ──
            case EventEffectType.RecruitRandom:
                GameLog.Event("동료 합류 효과는 아직 사용할 수 없습니다.", LogCategory.Reward);
                Debug.Log($"[EventService] TODO RecruitRandom (성급 {eff.value}) — PartyManager 연동 필요.");
                break;

            case EventEffectType.NextBattleStack:
                GameLog.Formatted($"다음 전투 스택 선지급 효과는 아직 사용할 수 없습니다.", LogCategory.Status);
                Debug.Log($"[EventService] TODO NextBattleStack (+{eff.value}).");
                break;

            case EventEffectType.RerollAffinity:
                GameLog.Event("성향 재굴림 효과는 아직 사용할 수 없습니다.", LogCategory.Status);
                Debug.Log("[EventService] TODO RerollAffinity — 대상 선택 필요.");
                break;

            case EventEffectType.ObtainObject:
                GameLog.Formatted($"오브제 획득 효과는 아직 사용할 수 없습니다.", LogCategory.Reward);
                Debug.Log($"[EventService] TODO ObtainObject id={eff.value}.");
                break;

            case EventEffectType.Corruption:
                GameLog.Formatted($"오염도 변경 효과는 아직 사용할 수 없습니다.", LogCategory.Status);
                Debug.Log($"[EventService] TODO Corruption {eff.value}.");
                break;

            case EventEffectType.NarrativeHint:
                GameLog.Event("암시 텍스트를 얻었다.", LogCategory.Default);
                break;
        }
    }

    // ── 파티 대상 스트레스 (+면 증가 / -면 감소) ─────────────────
    private static void ApplyStress(int delta, EventTarget target)
    {
        if (delta == 0) return;
        var targets = ResolveTargets(target);
        if (targets.Count == 0) return;
        foreach (var f in targets) f.currentStress += delta;
        string sign = delta >= 0 ? "+" : "";
        _effectSummary.Add(Loc.Message("{0} 스트레스 {1}", TargetLabel(target, targets.Count), sign + delta));
        GameLog.Formatted($"{targets.Count}명 스트레스 {sign}{delta}", LogCategory.Status);
    }

    // ── 파티 대상 HP (+면 회복 / -면 피해) ──────────────────────
    private static void ApplyHp(int delta, EventTarget target)
    {
        if (delta == 0) return;
        var targets = ResolveTargets(target);
        if (targets.Count == 0) return;
        foreach (var f in targets) f.CurrentHp += delta;
        string sign = delta >= 0 ? "+" : "";
        _effectSummary.Add(Loc.Message("{0} HP {1}", TargetLabel(target, targets.Count), sign + delta));
        GameLog.Formatted($"{targets.Count}명 HP {sign}{delta}", delta >= 0 ? LogCategory.Heal : LogCategory.Damage);
    }

    // ── EVT-01 계약형 HP 피해 — 적용 후 HP = max(1, HP - amount) (16-A §4) ──
    //    이 결과로 동료 사망·전멸을 만들지 않는다. HP 1 동료도 스트레스 증가는 별도 적용된다.
    private static void ApplyHpLossNoKill(int amount, EventTarget target)
    {
        if (amount <= 0) return;
        var targets = ResolveTargets(target);
        if (targets.Count == 0) return;
        foreach (var f in targets)
            f.CurrentHp = Mathf.Max(1, f.CurrentHp - amount); // setter 가 0 도달 시 사망 처리하므로 최소 1 보장
        _effectSummary.Add(Loc.Message("{0} HP -{1} (사망 없음)", TargetLabel(target, targets.Count), amount));
        GameLog.Formatted($"{targets.Count}명 HP -{amount} (사망 없음)", LogCategory.Damage);
    }

    // ── EVT-01 계약형 스트레스 증가 — 적용 후 = min(99, +amount) (16-A §4) ──
    //    stressResist 미적용. 이 이벤트에서는 패닉 판정과 스트레스 재설정을 실행하지 않는다
    //    (패닉은 전투 결과 처리에서만 판정되므로 필드 증가만으로 충분).
    private static void ApplyStressCapped(int amount, EventTarget target)
    {
        if (amount <= 0) return;
        var targets = ResolveTargets(target);
        if (targets.Count == 0) return;
        foreach (var f in targets)
            f.currentStress = Mathf.Min(99, f.currentStress + amount);
        _effectSummary.Add(Loc.Message("{0} 스트레스 +{1}", TargetLabel(target, targets.Count), amount));
        GameLog.Formatted($"{targets.Count}명 스트레스 +{amount}", LogCategory.Status);
    }

    /// <summary>효과 대상 표기 — 전원/1명 등 집계 라벨 (결과 창 요약용, 현재 언어).</summary>
    private static LocalizedMessage TargetLabel(EventTarget target, int count)
        => target == EventTarget.All ? Loc.Message("생존 동료") : Loc.Message("동료 {0}명", count);

    /// <summary>효과 대상 동료 목록. ChosenOne 은 (선택 UI 미구현) RandomOne 으로 폴백.</summary>
    private static List<FellowData> ResolveTargets(EventTarget target)
    {
        var alive = PartyManager.Instance?.GetActiveFellows()
                        .Where(f => f != null && !f.isDead).ToList()
                    ?? new List<FellowData>();
        if (alive.Count == 0) return alive;

        switch (target)
        {
            case EventTarget.All:
                return alive;

            case EventTarget.RandomOne:
            case EventTarget.ChosenOne: // 대상 선택 UI 미구현 → 랜덤 1명 폴백
                return new List<FellowData> { alive[Random.Range(0, alive.Count)] };

            case EventTarget.LowestHp:
                return new List<FellowData> { alive.OrderBy(f => f.CurrentHp).First() };

            default: // None
                return new List<FellowData>();
        }
    }
}
