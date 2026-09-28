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

    private sealed class ResolvedChoice
    {
        public EventOutcome outcome;
        public List<LocalizedMessage> summary;
    }
    private static readonly Dictionary<string, ResolvedChoice> _resolved = new();
    private static readonly HashSet<string> _resolving = new();

    /// <summary>새 런에서 확정 결과와 표시 요약을 비운다.</summary>
    public static void ResetRun()
    {
        _resolved.Clear();
        _resolving.Clear();
        _effectSummary.Clear();
    }

    /// <summary>재열람에서는 확정 결과와 실제 변화량만 복원한다.</summary>
    public static bool TryGetResolvedOutcome(EventDefinition evt, out EventOutcome outcome)
    {
        outcome = null;
        if (evt == null || string.IsNullOrEmpty(evt.id) || !_resolved.TryGetValue(evt.id, out var result)) return false;
        outcome = result.outcome;
        _effectSummary.Clear();
        _effectSummary.AddRange(result.summary);
        return true;
    }

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
    /// 같은 런의 확정 이벤트는 원래 결과를 반환하며 비용·효과·기록을 반복하지 않는다.
    /// </summary>
    public static EventOutcome ResolveChoice(EventDefinition evt, EventChoice choice)
    {
        if (TryGetResolvedOutcome(evt, out var resolved)) return resolved;
        _effectSummary.Clear();
        if (evt == null || string.IsNullOrEmpty(evt.id) || choice == null ||
            evt.choices == null || !evt.choices.Contains(choice) || !CanAfford(choice)) return null;
        // 결과가 없는 잘못된 데이터에는 비용도 지불하지 않는다.
        if (choice.outcomes == null || !choice.outcomes.Any(o => o != null)) return null;
        if (!_resolving.Add(evt.id)) return null;
        try
        {
            var before = ResolveTargets(EventTarget.All)
                .Select(f => (fellow: f, hp: f.CurrentHp, stress: f.currentStress)).ToList();
            int soulBefore = SoulstoneManager.Instance?.Amount ?? 0;
            var outcome = RollOutcome(choice);
            if (outcome == null || !PayCost(choice)) return null;
            if (outcome.effects != null)
                foreach (var eff in outcome.effects) ApplyEffect(eff);
            if (!string.IsNullOrEmpty(outcome.resultText))
                GameLog.Event(outcome.resultText, LogCategory.Status);

            var lines = new List<LocalizedMessage>
            {
                Loc.Message("확인 장소: {0}", Loc.Message(evt.title)),
            };
            if (evt.id == "evt_cold_camp")
            {
                // 16-E §4-3의 수첩 전문. 수치는 결과 적용 직전·직후의 차이다.
                _effectSummary.Clear();
                bool resting = choice.label == "휴식하기";
                bool searching = choice.label == "흩어져서 살펴보기";
                lines.Add(Loc.Message("조치: {0}", Loc.Message(resting ? "화덕에서 휴식" : searching ? "현장 수색" : "현장 미접촉")));
                lines.Add(Loc.Message("결과: {0}", Loc.Message(resting ? "생존 동료 전원 HP 회복" : searching ? "짐 뒤의 괴생물체를 쫓아내고 영혼석 회수" : "변화 없음")));
                if (resting || searching)
                    foreach (var snapshot in before)
                    {
                        var fellow = snapshot.fellow;
                        var name = RunSessionManager.GetNotebookFellowName(fellow);
                        var status = resting
                            ? Loc.Message("상태: {0} HP +{1} → {2}", name, fellow.CurrentHp - snapshot.hp, fellow.CurrentHp)
                            : Loc.Message("상태: {0} HP -{1} → {2} / 스트레스 +{3} → {4}", name,
                                snapshot.hp - fellow.CurrentHp, fellow.CurrentHp, fellow.currentStress - snapshot.stress, fellow.currentStress);
                        lines.Add(status);
                        _effectSummary.Add(status);
                    }
                if (searching)
                {
                    var gained = Loc.Message("획득: 영혼석 +{0} → 보유 {1}", (SoulstoneManager.Instance?.Amount ?? 0) - soulBefore, SoulstoneManager.Instance?.Amount ?? 0);
                    lines.Add(gained);
                    _effectSummary.Add(gained);
                }
            }
            else
            {
                lines.Add(Loc.Message("조치: {0}", Loc.Message(choice.label)));
                if (!string.IsNullOrEmpty(outcome.resultText))
                    lines.Add(Loc.Message("결과: {0}", Loc.Message(outcome.resultText)));
            }
            RunSessionManager.Instance?.AddRecord(RunRecordType.ChoiceResolved, "", lines, dedupKey: $"choice_{evt.id}");
            _resolved.Add(evt.id, new ResolvedChoice { outcome = outcome, summary = new List<LocalizedMessage>(_effectSummary) });
            return outcome;
        }
        finally { _resolving.Remove(evt.id); }
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
        var outs = choice.outcomes?.Where(o => o != null).ToList();
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
