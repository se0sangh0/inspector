// ============================================================
// BattleManager.Combat.cs
// 전투 로직 (파셜 클래스 분리 파일)
// ============================================================
//
// [이 파일이 하는 일]
//   실제 전투 계산과 관련된 모든 로직이 담겨 있습니다:
//   - 선공 판정 (DecideInitiative): 아군 선공 고정 (기획 2026.05.11 결정 — 선공 판정 폐기)
//   - 행동 실행 (ExecuteAction): 패닉 체크 + 스택 소비 + 스킬 실행
//   - 데미지 적용 (ApplyDamageToAlly): 실드 흡수 → HP 감소 → 스트레스 증가
//   - 패닉 처리 (TriggerPanicIfNeeded): 스트레스 100 → 공포경직 or 과호흡
//   - 카드 풀 생성 (GenerateCardPool)
//   - 사망/스트레스 처리 (ProcessDeathAndStress)
//   - 전투 종료 판정 (CheckBattleEndCondition)
//
// [이 파일에는 없는 것]
//   필드 선언, 초기화, 공개 API → BattleManager.cs
//   페이즈 핸들러 → BattleManager.Phases.cs
// ============================================================

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// partial 키워드: 이 파일이 BattleManager 클래스의 일부임을 선언
public partial class BattleManager
{
    // ===========================================================
    // 선공 판정 — 기획 2026.05.11 결정사항
    //   §02_전투_시스템_명세 §선공 판정 — "선공 판정 폐기, 아군 선으로 지정"
    //   기존 아군/적 스킬 코스트 합산 비교 + 동점 코인 토스 로직 전부 폐기.
    //   메소드 골격(_initiativeDecided 1회 호출 보장)만 유지.
    // ===========================================================

    /// <summary>
    /// 아군 선공을 고정 지정한다. (기획 2026.05.11 결정 — 선공 판정 폐기)
    /// 전투 1회만 실행.
    /// </summary>
    private void DecideInitiative()
    {
        isAllyFirstAttacker = true;
        Debug.Log("[선공 판정] 아군 선공 고정 (기획 2026.05.11 — 선공 판정 폐기)");
    }

    // ===========================================================
    // 행동 실행
    // - 아군 턴: 패닉 체크 → 스택 확인 → 스킬 실행 / 이월
    // - 적군 턴: 살아있는 적 순서대로 아군[0] 에게 공격
    // ===========================================================

    /// <summary>
    /// isAllyTurn=true 이면 아군 행동, false 이면 적군 행동을 실행한다.
    /// </summary>
    private IEnumerator ExecuteAction(bool isAllyTurn)
    {
    	if (isAllyTurn)
        {
            // 진형(allies) 순서대로 행동. 미행동자는 이번 턴 종료 후 allies 맨 앞으로 이동
            //  → 진형(위치) 변경 + 다음 턴 우선 행동 + 적 FrontFirst 타겟도 새 앞열을 향함.
            //  (2026-06-05 사용자 요청 — 2026-05-29 의 actionOrder 분리 방식을 원래 동작으로 복원)
            var snapshot = new List<FellowData>(allies);
            var noAction = new List<FellowData>();

            foreach (var ally in snapshot)
            {
                // 적이 전부 사망했으면 즉시 아군 행동 중단 — 남은 아군이 빈 적에게 스킬 쓰는 것 방지 (사용자 요청).
                if (enemies.Count > 0 && enemies.All(e => e.isDead))
                {
                    Debug.Log("[아군 행동] 적 전멸 감지 → 남은 아군 행동 중단");
                    break;
                }

                if (ally == null || ally.isDead) continue;

                // 공포 경직: 이번 턴 행동 불가
                if (ally.isFrozen)
                {
                    ally.isFrozen = false;
                    GameLog.Formatted($"{ally.displayName ?? ally.positionStack.ToString()}이(가) 공포에 질려 행동하지 못한다.", LogCategory.Status);
                    Debug.Log($"[공포 경직] {ally.positionStack} — 이번 턴 행동 불가");
                    continue;
                }

                StackType allyRole = ally.positionStack;
                string    allyName = !string.IsNullOrEmpty(ally.displayName) ? ally.displayName : allyRole.ToString();
                var skills         = ally.GetSkills();

                // 과호흡 페널티: 이번 턴 첫 스킬 시도에만 +1, 이후 해소
                int panicCostBonus = ally.isOverBreathing ? 1 : 0;
                if (ally.isOverBreathing) ally.isOverBreathing = false;

                // 이번 턴에 이 동료가 스킬을 하나라도 발동했는지 추적
                bool usedAny = false;

                if (skills.Count > 0)
                {
                    // ── ✨ 스킬 선택 — 발동 가능한 것 중 역할별 우선순위 1개 (기획자 피드백 #11) ──
                    int currentStack = PlayerRoleCost.Instance.GetAmount(allyRole);
                    List<SkillData> affordable = skills
                        .Where(s => currentStack >= s.costAmount + panicCostBonus)
                        .ToList();
                    SkillData bestSkill = SelectSkillByRole(allyRole, affordable);

                    if (bestSkill != null)
                    {
                        int effectiveCost = bestSkill.costAmount + panicCostBonus;
                        PlayerRoleCost.Instance.Use(allyRole, effectiveCost);
                        int afterStack = currentStack - effectiveCost;
                        GameLog.Formatted($"{allyName}이(가) [{bestSkill.displayName}]을(를) 사용했다!", LogCategory.Skill);
                        Debug.Log($"[아군 행동] {allyName} ({allyRole}) → {bestSkill.displayName}  (스택 {effectiveCost} 사용 / {currentStack}→{afterStack}{(panicCostBonus > 0 ? $", 과호흡 +{panicCostBonus}" : "")})");
                        yield return StartCoroutine(UseSkill(ally, bestSkill));
                        usedAny = true;
                        yield return new WaitForSeconds(actionDelayTime);
                    }
                    else
                    {
                        foreach (var s in skills)
                            Debug.Log($"[아군 스킵-개별스킬] {allyRole} {s.displayName} — 스택 부족 ({currentStack}/{s.costAmount + panicCostBonus})");
                    }
                }
                else
                {
                    // 보유 스킬 0 — 이전엔 carryover 없이 조용히 건너뛰어 "미행동인데 스택 안 들어옴"의 원인이었음.
                    // 이제 아래 미행동 보상으로 통일 처리한다 (#7 수정).
                    Debug.LogWarning($"[BattleManager] {allyName}({allyRole}) — 보유 스킬 없음 → 미행동 처리");
                }

                // 스킬을 하나도 발동 못 한 경우(스택 부족 OR 스킬 없음) → 미행동 보상 (기획 §코어루프 §동료 행동)
                //  ─ 역할 스택 +1 (다음 턴 이월) / 진형 맨 앞으로 / 인게임 로그 표시
                if (!usedAny)
                {
                    _carryoverBonus.TryGetValue(allyRole, out int prev);
                    _carryoverBonus[allyRole] = prev + 1;
                    noAction.Add(ally);
                    GameLog.Formatted($"{allyName}이(가) 행동하지 않아 다음 턴 스택 +1 (행동 우선)", LogCategory.Status);
                    Debug.Log($"[아군 미행동] {allyName}({allyRole}) → 다음 턴 이월 +1, 진형 앞으로");
                }
            }

            // 미행동자를 allies 맨 앞으로 이동 — 진형/위치 변경 + 다음 턴 우선 행동.
            //  기획 코어루프 §동료 행동: "배치 순서 → 미행동자 우선 재정렬".
            //  감지된(배치) 순서대로 각자 맨 앞에 삽입 → 나중에 미행동한 동료가 더 앞.
            //  예: 1-2-3-4 → 3 미행동 → 3-1-2-4 / 3,4 미행동 → 4-3-1-2.
            //  시각 배치도 새 진형을 따르도록 battleSlotIndex 재할당 후 즉시 재배치.
            if (noAction.Count > 0)
            {
                foreach (var f in noAction)
                    if (allies.Remove(f)) allies.Insert(0, f);
                for (int i = 0; i < allies.Count; i++)
                    if (allies[i] != null) allies[i].battleSlotIndex = i;
                // 사전 호흡 — 직전 행동 연출과 재정렬을 분리해 "재정렬 시작"이 읽히게 (겹침 체감 개선, 사용자 요청).
                yield return new WaitForSeconds(0.25f);
                DefaultSetting.AllyLayout?.RelayoutNow();
                Debug.Log($"[아군 미행동] {noAction.Count}명 → allies 맨 앞으로 재정렬 (진형/위치 변경, 순차 이동)");
                // 진형 재배치(카드 순차 이동 포함)가 끝날 때까지 대기 — 적이 배치 변경 도중 공격하지 않도록.
                int aliveAllies = allies.Count(f => f != null && !f.isDead);
                yield return new WaitForSeconds(DefaultSetting.RelayoutDuration + DefaultSetting.RelayoutStagger * Mathf.Max(0, aliveAllies - 1));
            }
        }
        else
        {
            // 적 스킬 시스템(EnemyAction.cs) 위임 — 가중치 랜덤 스킬 + 타겟팅 분기 + 다중 대상 데미지
            // 기획 §11_적_스킬_시트: 약탈자/보스의 다중 스킬·다중 타겟 처리는 ExecuteEnemyTurn 이 담당.
            // skillIds 비었거나 EnemySkillDatabase 없으면 ExecuteEnemyTurn 내부 fallback 으로 attackPower 직타.
            var liveEnemies = enemies.Where(e => !e.isDead).ToList();
            Debug.Log($"[적 행동] 살아있는 적 수: {liveEnemies.Count}");

            foreach (var enemy in liveEnemies)
            {
                if (allies.All(a => a.isDead)) break;
                yield return StartCoroutine(ExecuteEnemyTurn(enemy));
                yield return new WaitForSeconds(actionDelayTime);
            }
        }

        // 턴 끝 — 진행 중인 근접 공격 시퀀스(dash→모션→복귀)가 모두 완료될 때까지 대기.
        // 다음 턴 (적 또는 아군) 의 모션이 마지막 카드 모션과 겹치지 않도록.
        yield return new WaitUntil(() => !BattleCardSprites.AnyAttacking());
    }

    // ===========================================================
    // 데미지 적용
    // 실드 → HP 순서로 데미지를 흡수하고 스트레스를 증가시킨다.
    // ===========================================================

    /// <summary>
    /// 아군에게 데미지를 입힌다.
    /// 실드가 있으면 먼저 소모하고, 남은 데미지를 HP 에 적용한다.
    /// 피격 시 스트레스가 증가하며 100 도달 시 패닉이 발동된다.
    /// </summary>
    private void ApplyDamageToAlly(FellowData target, int damage, bool allowBondRedirect = true)
    {
        // ── 딜러 중증 디버프 (기획 §04) — 받는 피해 +30% ──
        if (target.role == CompanionRole.Dealer && target.hasSevereDebuff)
        {
            int boosted = Mathf.RoundToInt(damage * 1.30f);
            Debug.Log($"[중증디버프·딜러] {target.positionStack} 받는 피해 {damage} → {boosted} (+30%)");
            damage = boosted;
        }

        // ── 불굴의 벽 (디펜더 본인, 기획 §16) — 받는 피해 -20% ──
        if (target.activePassiveId == MetaPassiveManager.DefenderWall)
        {
            int r = Mathf.RoundToInt(damage * (1f - MetaPassiveManager.DefenderWallReduce));
            Debug.Log($"[불굴의벽] {target.positionStack} 받는 피해 {damage}→{r}");
            damage = r;
        }

        // ── 투혼 (어택커 생존 시, 기획 §16) — 모든 아군 받는 피해 -10%. 원 피해에만 1회 적용(수호 결속 분담분 재적용 방지) ──
        if (allowBondRedirect && allies.Any(a => !a.isDead && a.activePassiveId == MetaPassiveManager.AttackerSpirit))
        {
            int r = Mathf.RoundToInt(damage * (1f - MetaPassiveManager.AttackerSpiritReduce));
            if (r != damage) Debug.Log($"[투혼] {target.positionStack} 받는 피해 {damage}→{r}");
            damage = r;
        }

        // ── 수호 결속 (디펜더, 기획 §16) — 다른 아군 피해의 25%를 실드 보유 디펜더가 분담 ──
        if (allowBondRedirect)
        {
            var defender = allies.FirstOrDefault(a => a != target && !a.isDead && a.activePassiveId == MetaPassiveManager.DefenderBond && a.shield > 0);
            if (defender != null)
            {
                int share = Mathf.RoundToInt(damage * MetaPassiveManager.DefenderBondRatio);
                if (share > 0)
                {
                    damage -= share;
                    GameLog.Formatted($"수호 결속 — {defender.displayName ?? defender.positionStack.ToString()}가 피해 {share} 분담.", LogCategory.Shield);
                    Debug.Log($"[수호결속] {defender.positionStack}가 {target.positionStack} 피해 {share} 분담");
                    ApplyDamageToAlly(defender, share, false); // 분담분은 재분담 안 함
                }
            }
        }

        AudioManager.Instance?.PlaySfxById(SfxId.HurtAlly);
        // 피격 알림 — 쉴드 흡수량/HP 감소량 분리 발행. UI 가 노란(흡수)/빨강(HP) popup 각각 표시.
        if (damage > 0)
        {
            int preAbsorbed = target.shield > 0 ? Mathf.Min(target.shield, damage) : 0;
            int preHpLoss   = damage - preAbsorbed;
            target.OnDamaged?.Invoke(preAbsorbed, preHpLoss);
        }
        int remaining = damage;
        // 동료 표시명 — data.displayName 우선, 없으면 역할명
        string targetName = !string.IsNullOrEmpty(target.displayName) ? target.displayName : target.positionStack.ToString();

        // ── 실드 흡수 ────────────────────────────────────────────
        if (target.shield > 0)
        {
            int absorbed  = Mathf.Min(target.shield, remaining);
            target.shield -= absorbed;
            remaining     -= absorbed;
            target.OnShieldChanged?.Invoke();
            GameLog.Formatted($"{targetName}의 실드가 {absorbed}을(를) 흡수!", LogCategory.Shield);
            Debug.Log($"  └ [실드 흡수] {targetName} — {absorbed} 흡수 (남은 실드: {target.shield})");
        }

        // ── HP 감소 ──────────────────────────────────────────────
        if (remaining > 0)
        {
            int beforeHp = target.CurrentHp;
            int maxHp    = target.maxHp > 0 ? target.maxHp : 100;
            target.CurrentHp = Mathf.Max(0, target.CurrentHp - remaining);
            UpdateAllyHpUI(target);
            GameLog.Formatted($"{targetName}이(가) {remaining}의 피해를 입었다!", LogCategory.Damage);
            Debug.Log($"  └ [데미지] {targetName} ({target.positionStack}) ← {remaining} 데미지  (HP: {beforeHp} → {target.CurrentHp}/{maxHp})");
        }

        // ── 스트레스 증가 (기획서 §스트레스: damage×0.25 - stressResist, 최소 0) ──
        int stressGain = Mathf.Max(0, Mathf.RoundToInt(damage * 0.25f) - target.stressResist);

        // 기획 §04 §51~99 압박 — 이미 압박 구간이면 받는 스트레스 +N%(기본 +10%). 스트레스 악화 가속.
        if (target.currentStress >= 51 && target.currentStress <= 99 && stressGain > 0)
        {
            int before = stressGain;
            stressGain = Mathf.RoundToInt(stressGain * (1f + pressureStressGainPercent / 100f));
            Debug.Log($"[압박 디버프] {targetName} 스트레스 {target.currentStress}(압박) → 피격 스트레스 {before}→{stressGain} (+{pressureStressGainPercent}%)");
        }

        target.currentStress = Mathf.Min(100, target.currentStress + stressGain);
        GameLog.Formatted($"{targetName}의 스트레스 +{stressGain} ({target.currentStress}/100)", LogCategory.Status);
        Debug.Log($"  └ [스트레스] {targetName} +{stressGain} → {target.currentStress}");

        TriggerPanicIfNeeded(target);
    }

    /// <summary>
    /// 아군 HP 슬라이더 UI 를 현재 HP 값으로 갱신한다.
    /// </summary>
    private void UpdateAllyHpUI(FellowData target)
    {
        if (target.HpSlider != null)
            target.HpSlider.value = target.CurrentHp;
        else
            Debug.LogWarning($"[BattleManager] {target.positionStack} 의 HpSlider 가 null 입니다. DefaultSetting.InitHp() 가 호출되었는지 확인하세요.");
    }

    // ===========================================================
    // 패닉 처리
    // 스트레스 100 도달 시 공포 경직 or 과호흡 중 하나를 발동한다.
    // ===========================================================

    /// <summary>
    /// 스트레스가 100 이상이면 패닉을 발동한다.
    /// 스트레스를 50 으로 줄이고 50% 확률로 공포 경직 / 과호흡 중 하나 적용.
    /// </summary>
    private void TriggerPanicIfNeeded(FellowData ally)
    {
        if (ally.currentStress < 100) return;

        ally.currentStress = 50;

        // ── 역할별 중증 디버프 (기획 §04) — 첫 패닉 시 부착, 전투 종료까지 유지 ──
        if (!ally.hasSevereDebuff)
        {
            ally.hasSevereDebuff = true;
            string dbuff = ally.role switch
            {
                CompanionRole.Dealer => "받는 피해 +30%",
                CompanionRole.Tanker => "부여 실드 -50%",
                CompanionRole.Support => "광역 회복 → 단일 회복",
                _ => "없음"
            };
            GameLog.Formatted($"{ally.displayName ?? ally.positionStack.ToString()} 중증 디버프 — {dbuff} (전투 종료까지).", LogCategory.Status);
            Debug.Log($"[중증디버프] {ally.positionStack} ({ally.role}) — {dbuff}");
        }

        if (Random.value > 0.5f)
        {
            ally.isFrozen = true;
            GameLog.Formatted($"{ally.displayName ?? ally.positionStack.ToString()}이(가) 패닉! 공포 경직 발동.", LogCategory.Status);
            Debug.Log($"[패닉] {ally.positionStack} — 공포 경직 발동! (다음 턴 행동 불가)");
        }
        else
        {
            ally.isOverBreathing = true;
            GameLog.Formatted($"{ally.displayName ?? ally.positionStack.ToString()}이(가) 과호흡! 다음 턴 스킬 코스트 +1.", LogCategory.Status);
            Debug.Log($"[패닉] {ally.positionStack} — 과호흡 발동! (다음 턴 스킬 코스트 +1)");
        }
    }

    // ===========================================================
    // 카드 풀 생성
    // ===========================================================

    /// <summary>
    /// 역할별 더미 CardData 를 런타임에 생성하여 카드 풀을 반환한다.
    /// </summary>
    private List<CardData> GenerateCardPool()
    {
        var pool = new List<CardData>();

        foreach (StackType role in System.Enum.GetValues(typeof(StackType)))
        {
            for (int i = 0; i < 10; i++)
            {
                var card = ScriptableObject.CreateInstance<CardData>();
                card.id         = $"card_{role}_{i}";
                card.stackType  = role;
                pool.Add(card);
            }
        }

        return pool;
    }

    // ===========================================================
    // 탈진 페널티 — 손패 0 + 덱 0 인 턴의 결과 처리에서 호출
    // 기획 §02 §1) Hand Empty — "정해진 페널티(데미지 또는 스트레스)"
    // → OR 해석상 스트레스만 채택. 수치는 임시값.
    // ===========================================================
    private void ApplyExhaustionPenaltyIfNeeded()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsExhausted()) return;

        var live = allies.Where(a => !a.isDead).ToList();
        if (live.Count == 0) return;

        GameLog.Formatted($"탈진! 살아있는 동료 전원 스트레스 +{exhaustionStressPenalty}.", LogCategory.Status);
        Debug.Log($"[탈진] 손패 0 + 덱 0 — 살아있는 동료 {live.Count}명 스트레스 +{exhaustionStressPenalty}");
        foreach (var ally in live)
        {
            ally.currentStress = Mathf.Min(100, ally.currentStress + exhaustionStressPenalty);
            TriggerPanicIfNeeded(ally);
        }
    }

    // ===========================================================
    // 사망 처리 + 스트레스 전파
    // 기획서: 동료 사망 시 생존 동료 전원 스트레스 +20
    // ===========================================================

    // ===========================================================
    // 역할별 스킬 우선순위 선택 (기획자 피드백 #11)
    // ===========================================================
    /// <summary>
    /// 발동 가능한 스킬 후보 중 역할 성향에 맞는 스킬을 우선 선택한다.
    ///   서포터: 아군 HP 위급(60% 미만) 시 힐 우선 / 탱커: 실드 우선 / 딜러: 데미지 우선.
    /// 선호 효과가 후보에 없으면 기존 규칙(코스트 최고)으로 폴백한다.
    /// </summary>
    private SkillData SelectSkillByRole(StackType role, List<SkillData> affordable)
    {
        if (affordable == null || affordable.Count == 0) return null;

        System.Func<SkillData, bool> prefer = null;
        switch (role)
        {
            case StackType.Support:
                bool anyHurt = allies.Any(a => a != null && !a.isDead &&
                    (float)a.CurrentHp / (a.maxHp > 0 ? a.maxHp : 100) < 0.6f);
                if (anyHurt) prefer = IsHealSkill;
                break;
            case StackType.Tank:
                prefer = IsShieldSkill;
                break;
            case StackType.Dealer:
                prefer = IsDamageSkill;
                break;
        }

        if (prefer != null)
        {
            var preferred = affordable.Where(prefer)
                .OrderByDescending(s => s.costAmount)
                .FirstOrDefault();
            if (preferred != null) return preferred;
        }

        // 폴백 — 기존 규칙(발동 가능한 것 중 코스트 최고)
        return affordable.OrderByDescending(s => s.costAmount).FirstOrDefault();
    }

    // effectType: "Damage"/"Heal"/"Shield"/"Buff"/"Debuff"/"MixedDamageShield"/"MixedDamageTaunt"
    private static bool IsHealSkill(SkillData s)
        => s != null && !string.IsNullOrEmpty(s.effectType) && s.effectType.Contains("Heal");
    private static bool IsShieldSkill(SkillData s)
        => s != null && !string.IsNullOrEmpty(s.effectType) && s.effectType.Contains("Shield");
    private static bool IsDamageSkill(SkillData s)
        => s != null && !string.IsNullOrEmpty(s.effectType) && s.effectType.Contains("Damage");

    /// <summary>
    /// 이번 턴에 HP 가 0 이하가 된 아군/적군을 처리한다.
    /// </summary>
    private void ProcessDeathAndStress()
    {
        // 이번 턴 "새로" 죽은 동료만 처리 — deathHandled 로 1회만 (매 턴 재처리 방지, #9/#6).
        var dyingAllies = allies.Where(a => a.isDead && a.CurrentHp <= 0 && !a.deathHandled).ToList();

        foreach (var ally in dyingAllies)
        {
            ally.isDead = true;
            ally.deathHandled = true; // 중복 처리(스트레스 반복·손패 재파괴) 방지
            GameLog.Formatted($"{ally.displayName ?? ally.positionStack.ToString()}이(가) 쓰러졌다.", LogCategory.Death);
            Debug.Log($"[사망] {ally.positionStack} 사망 처리됨.");

            GameManager.Instance?.RemoveCardsOfFellow(ally);
            PartyManager.Instance?.RemoveFellow(ally);

            foreach (var survivor in allies.Where(a => !a.isDead))
            {
                survivor.currentStress = Mathf.Min(100, survivor.currentStress + 20);
                TriggerPanicIfNeeded(survivor);
                GameLog.Formatted($"{survivor.displayName ?? survivor.positionStack.ToString()}의 스트레스 +20 (동료 사망)", LogCategory.Status);
                Debug.Log($"[스트레스] {survivor.positionStack} +20 (동료 사망 패널티) → {survivor.currentStress}");
            }
        }

        var dyingEnemies = enemies.Where(e => e.isDead && e.CurrentHp <= 0).ToList();
        Debug.Log($"[ProcessDeath] 이번 턴 사망한 적: {dyingEnemies.Count}명");
        foreach (var enemy in dyingEnemies)
        {
            Debug.Log($"  └ {enemy.displayName} | HP:{enemy.CurrentHp} | isDead:{enemy.isDead}");
            // 기획 §15 보상 — 적 처치 시 영혼석 드롭 (고블린 8 / 약탈자 12 / 보스 20)
            // Pool 이 있으면 시각 연출 후 가산, 없으면(폴백) 즉시 가산.
            if (enemy.soulstoneDrop > 0)
            {
                Vector3 dropPos = FindEnemyWorldPos(enemy);
                if (SoulstoneDropPool.Instance != null)
                {
                    SoulstoneDropPool.Instance.SpawnAt(dropPos, enemy.soulstoneDrop);
                }
                else if (SoulstoneManager.Instance != null)
                {
                    SoulstoneManager.Instance.Add(enemy.soulstoneDrop);
                }
                GameLog.Formatted($"{enemy.displayName} 처치! 영혼석 +{enemy.soulstoneDrop}", LogCategory.Reward);
                Debug.Log($"  └ [보상] {enemy.displayName} 처치 → 영혼석 +{enemy.soulstoneDrop}");
            }
        }

        // 보스 사망 시 모든 소환체(까마귀 등 isPassive) 동반 사망 처리.
        // 기획: "보스가 죽으면 까마귀도 동반으로 죽는다"
        bool bossDiedThisTurn = dyingEnemies.Any(e => e.tier == EnemyTier.Boss);
        if (bossDiedThisTurn)
        {
            var aliveSummons = enemies.Where(e => e != null && !e.isDead && e.isPassive).ToList();
            foreach (var s in aliveSummons)
            {
                GameLog.Formatted($"{s.displayName}이(가) 보스와 함께 사라졌다.", LogCategory.Death);
                Debug.Log($"  └ [보스 동반 사망] {s.displayName} — 보스 사망에 의해 강제 제거");
                s.CurrentHp = 0; // setter 가 isDead=true + OnDied 처리
            }
        }
    }

    // ===========================================================
    // 전투 종료 판정
    // ===========================================================

    /// <summary>
    /// 적 전멸 또는 아군 전멸 시 true 를 반환한다.
    /// </summary>
    private bool CheckBattleEndCondition()
    {
        DebugPrintEnemyStates("[CheckBattleEnd]");

        bool allEnemiesDead = enemies.Count > 0 && enemies.All(e => e.isDead);
        bool allAlliesDead  = allies.Count  > 0 && allies.All(a  => a.isDead);

        if (allEnemiesDead) Debug.Log("[CheckBattleEnd] → 적 전멸 조건 충족!");
        if (allAlliesDead)  Debug.Log("[CheckBattleEnd] → 아군 전멸 조건 충족!");

        return allEnemiesDead || allAlliesDead;
    }

    // ===========================================================
    // 스킬 실행
    // ===========================================================

    /// <summary>동료가 스킬을 사용한다. OnSkillCast 모션 발동 후 카테고리별 타이밍(근접=휘두름 순간 / 원거리=이펙트 종료 후 / 제자리)으로 이펙트+데미지/힐/실드 적용.</summary>
    private IEnumerator UseSkill(FellowData user, SkillData skill)
    {
        // 기획 백로그 §5 성급 — 데미지 배율 1.25^(star-1) 적용 (배율은 FellowData 가 보유)
        int scaledPower = Mathf.RoundToInt(skill.power * user.skillPowerMultiplier);

        // 기획 §04 §51~99 압박 — 스킬 퍼포먼스 -N% (기본 -10%, 인스펙터 조정 가능)
        if (user.currentStress >= 51 && user.currentStress <= 99)
        {
            float multiplier = 1f - pressureSkillPenaltyPercent / 100f;
            int before = scaledPower;
            scaledPower = Mathf.Max(1, Mathf.RoundToInt(scaledPower * multiplier));
            Debug.Log($"[압박 디버프] {user.displayName} 스트레스 {user.currentStress} → 스킬 파워 {before}→{scaledPower} (-{pressureSkillPenaltyPercent}%)");
        }
        // if (user.currentStress >= 51 && user.currentStress <= 99)
        //     scaledPower = Mathf.RoundToInt(scaledPower * (1f - 0.10f)); // N=10% 가정
        string userName = user.displayName ?? user.positionStack.ToString();

        Debug.Log($"┌─────────────────────────────────────────");
        Debug.Log($"│ [스킬 사용] {userName} ({user.starLevel}★)  →  {skill.displayName}");
		Debug.Log($"│  파워: {skill.power} × {user.skillPowerMultiplier:F2} = {scaledPower}"); 
        Debug.Log($"│  효과: {skill.effectType}  |  대상: {skill.targeting}  |  파워: {skill.power}  (배율: ×{user.skillPowerMultiplier:F2})");
        Debug.Log($"│  사용 스택값: {skill.costAmount} ({skill.costType})");
        Debug.Log($"│  설명: {skill.description}");
        if (skill.statusEffect != "None")
            Debug.Log($"│  상태이상: {skill.statusEffect}  (수치: {skill.statusValue})");
        Debug.Log($"└─────────────────────────────────────────");

        // 모션 트리거 — View 가 effectType + jobClass 보고 Ranged/Melee/Stationary 결정.
        // skillIndex 는 skillIds 배열 내 위치 (0=1번 스킬, 1=2번 스킬). View 가 Attack/Attack2 분기에 사용.
        int skillIndex = user.skillIds != null ? System.Array.IndexOf(user.skillIds, skill.id) : 0;
        if (skillIndex < 0) skillIndex = 0;
        // 동료 스킬은 isRanged=false (jobClass="캐스터" 면 Resolver 가 자동으로 Ranged 처리)
        user.OnSkillCast?.Invoke(skill.effectType, skillIndex, skill.isRanged);

        // 스킬 전용음은 '시전'이 아니라 '타격 순간' 에 재생 — 아래 임팩트 지점에서 _castImpactSfx 로 처리 (사용자 요청 2026-06-11).

        // ── 카테고리별 타이밍 + 이펙트 (2026-06-10) ─────────────────────────────
        //  Ranged     : 시전 윈드업 → 지팡이에서 투사체 발사 → 비행(이펙트 길이)이 끝난 뒤 데미지.
        //  Melee      : 전진 dash 후 '휘두르는 순간' 에 이펙트+데미지 동시 (발도/일섬/무모한강타 등).
        //  Stationary : 시전 후 이펙트와 함께 힐/실드/버프 적용.
        var motionCat = MotionCategoryResolver.Resolve(user.jobClass, skill.effectType, skill.isRanged);
        bool hasFx = SkillEffectFx.Has(skill.id);

        if (motionCat == MotionCategory.Ranged)
        {
            yield return new WaitForSeconds(rangedCastWindup);
            if (hasFx) SkillEffectFx.Play(skill.id, GetUnitFxPos(user), GetPrimaryEnemyFxPos(user));
            float travel = hasFx ? SkillEffectFx.GetDuration(skill.id) : rangedDefaultTravel;
            yield return new WaitForSeconds(travel);   // 투사체가 적에 도달/이펙트 종료 후 데미지
        }
        else if (motionCat == MotionCategory.Melee)
        {
            yield return new WaitForSeconds(meleeImpactDelay);  // 전진 후 휘두르는 순간
            if (hasFx) SkillEffectFx.Play(skill.id, GetUnitFxPos(user), GetPrimaryEnemyFxPos(user)); // 휘두름과 동시
        }
        else // Stationary (힐/실드/버프)
        {
            yield return new WaitForSeconds(stationaryImpactDelay);
            if (hasFx) SkillEffectFx.Play(skill.id, GetUnitFxPos(user), GetPrimaryEnemyFxPos(user));
        }

        // 스킬 전용음 — 부여된 스킬은 타격 순간에 전용음이 '디폴트 타격음 대신' 1회 재생 (DealDamageToEnemy 가 소비).
        _castImpactSfx = SkillCustomImpactSfx(skill);
        _castImpactSfxPlayed = false;

        switch (skill.effectType)
        {
            case "Damage": ApplySkillDamage(user, skill, scaledPower);  break;
            case "Heal":   ApplySkillHeal(user, skill, scaledPower);    break;
            case "Shield": ApplySkillShield(user, skill, scaledPower);  break;
            case "Buff":   Debug.Log($"[UseSkill] Buff — 추후 구현 예정 (상태이상: {skill.statusEffect})");   break;
            case "Debuff": Debug.Log($"[UseSkill] Debuff — 추후 구현 예정 (상태이상: {skill.statusEffect})"); break;

            // 혼합 스킬 — Damage + 보조 효과 동시 적용 (2026-05-29 추가)
            case "MixedDamageShield":
                // 전장의 방패 — AllEnemies 분산 데미지 + AllAllies 실드 부여
                ApplySkillDamage(user, skill, scaledPower);
                ApplyMixedShield(user, skill);
                break;
            case "MixedDamageTaunt":
                // 워크라이 — AllEnemies 분산 데미지 + 시전자에게 도발
                ApplySkillDamage(user, skill, scaledPower);
                ApplyTaunt(user, skill);
                break;

            default:       Debug.LogWarning($"[UseSkill] 알 수 없는 effectType: '{skill.effectType}'");       break;
        }

        _castImpactSfx = SfxId.None;   // 이번 시전 종료 — 다음 시전/일반 공격은 디폴트 타격음으로
    }

    // ── 스킬 전용 타격음 (2026-06-11) ────────────────────────────────
    //   부여된 스킬은 '맞는 순간' 디폴트 타격음(AttackSword) 대신 전용음을 1회 재생.
    //   (피격 반응음 HurtEnemy 는 그대로 유지)
    private SfxId _castImpactSfx = SfxId.None;
    private bool  _castImpactSfxPlayed;

    private static SfxId SkillCustomImpactSfx(SkillData skill)
    {
        string n = skill.displayName ?? "";
        if (n.Contains("파이어볼")) return SfxId.SkillFireball;
        if (n.Contains("무모한 강타") || n.Contains("매직 미사일")) return SfxId.SkillStrike;
        return SfxId.None;
    }

    // ── 스킬 이펙트 위치 헬퍼 (2026-06-10) — 유닛에 바인딩된 BattleCardView 의 월드 위치 ──
    private Vector3 GetUnitFxPos(FellowData u)
    {
        if (u != null)
            foreach (var v in Object.FindObjectsByType<BattleCardView>(FindObjectsSortMode.None))
                if (v != null && v.Fellow == u) return v.transform.position;
        return Vector3.zero;
    }
    private Vector3 GetPrimaryEnemyFxPos(FellowData fallbackUser)
    {
        var live = enemies.Where(e => e != null && !e.isDead).ToList();
        if (live.Count > 0)
        {
            var t = live[0];
            foreach (var v in Object.FindObjectsByType<BattleCardView>(FindObjectsSortMode.None))
                if (v != null && v.Enemy == t) return v.transform.position;
        }
        return GetUnitFxPos(fallbackUser);
    }

    /// <summary>
    /// MixedDamageShield 의 보조 실드 — AllAllies 에 skill.shieldPower 실드 부여.
    /// 기획 §10 §디펜더 전장의 방패: "AllEnemies Damage + AllAllies Shield".
    /// </summary>
    private void ApplyMixedShield(FellowData user, SkillData skill)
    {
        if (skill.shieldPower <= 0) return;

        int shieldAmt = skill.shieldPower;
        // ── 견고한 방벽 (디펜더 패시브, 기획 §16) — 부여 실드 +30% ──
        if (user.activePassiveId == MetaPassiveManager.DefenderBulwark)
            shieldAmt = Mathf.RoundToInt(shieldAmt * (1f + MetaPassiveManager.DefenderBulwarkBonus));
        // ── 탱커 중증 디버프 (기획 §04) — 부여 실드 -50% ──
        if (user.role == CompanionRole.Tanker && user.hasSevereDebuff)
        {
            int reduced = Mathf.RoundToInt(shieldAmt * 0.5f);
            Debug.Log($"[중증디버프·탱커] {user.positionStack} 전장의 방패 실드 {shieldAmt} → {reduced} (-50%)");
            shieldAmt = reduced;
        }

        var live = allies.Where(a => !a.isDead).ToList();
        foreach (var ally in live)
        {
            ally.AddShield(shieldAmt);
            GameLog.Formatted($"{ally.displayName ?? ally.positionStack.ToString()}에게 {shieldAmt} 실드.", LogCategory.Shield);
            Debug.Log($"[ApplyMixedShield] {ally.displayName} +{shieldAmt} 실드 (현재: {ally.shield})");
        }
    }

    /// <summary>
    /// MixedDamageTaunt 의 보조 도발 — 살아있는 적 전체에 시전자 우선 타격 N턴 적용.
    /// 적의 SingleEnemy/FrontFirst/BackLast 류 타겟팅 시 우선 시전자에게 향함.
    /// AllAllies 류는 영향 없음 (도발 불가). 기획 §10 §어택커 워크라이.
    /// </summary>
    private void ApplyTaunt(FellowData user, SkillData skill)
    {
        int turns = skill.tauntTurns > 0 ? skill.tauntTurns : 1;
        var live = enemies.Where(e => e != null && !e.isDead).ToList();
        int applied = 0;
        foreach (var e in live)
        {
            e.tauntTurnsLeft = turns;
            e.taunter = user;
            e.OnTauntChanged?.Invoke(); // 상태 칩 갱신
            applied++;
        }
        GameLog.Formatted($"{user.displayName}이(가) {applied}마리 적을 {turns}턴 도발!", LogCategory.Status);
        Debug.Log($"[ApplyTaunt] {user.displayName} → 적 {applied}마리 도발 {turns}턴");
    }

    /// <summary>
    /// 스킬 데미지 적용. 기획 §02 §자동 타겟팅 §아군 공격 — "전열(앞)에 배치된 적 우선".
    /// 예외 타겟팅(LowestHpEnemy / HighestHpEnemy) 은 스킬 description 에 명시된 경우에만 사용.
    /// </summary>
    private void ApplySkillDamage(FellowData user, SkillData skill, int power)
    {
        var liveEnemies = enemies.Where(e => !e.isDead).ToList();
        if (liveEnemies.Count == 0) { Debug.Log("[ApplySkillDamage] 살아있는 적 없음."); return; }
        int liveBefore = liveEnemies.Count;

        // 딜러 계열 데미지 배율 패시브 (배수의진/주문증폭/일기당천, 기획 §16)
        power = ApplyDamagePassives(user, power, liveBefore);

        switch (skill.targeting)
        {
            // 기획 §아군 공격: 전열 우선.
            //   보스는 후열 강제 배치(DefaultSetting.RelayoutCards) → 비-보스(앞열) 먼저 타겟.
            //   비-보스끼리는 원본 인덱스 순. 보스밖에 안 남으면 보스 타격.
            case "SingleEnemy":
                {
                    var front = liveEnemies
                        .OrderBy(e => e.tier == EnemyTier.Boss ? 1 : 0)
                        .ThenBy(e => enemies.IndexOf(e))
                        .First();
                    // 거합집중/처형인 (오펜더, 기획 §16) — 단일 타격 직전 배율
                    int hitPower = ApplySingleHitPassives(user, front, power);
                    DealDamageToEnemy(front, hitPower);
                    // 비전 연쇄 (캐스터) — 매직미사일 처치 시 1연쇄
                    TryArcaneChain(user, skill, front, power);
                }
                break;

            // 예외 타겟팅 — 체력 비율 최저 적 (보스 처형 등 향후 스킬에서 사용)
            case "LowestHpEnemy":
                {
                    var target = liveEnemies.OrderBy(e => EnemyHpRatio(e)).First();
                    DealDamageToEnemy(target, power);
                }
                break;

            // 예외 타겟팅 — 체력 비율 최고 적 (보스 우선 공격 등 향후 스킬에서 사용)
            case "HighestHpEnemy":
                {
                    var target = liveEnemies.OrderByDescending(e => EnemyHpRatio(e)).First();
                    DealDamageToEnemy(target, power);
                }
                break;

            case "AllEnemies":
                {
                    // 기획: 전체 데미지 스킬은 총 power 를 적 수로 분산 (적 1명당 power/적수)
                    int targetCount = liveEnemies.Count;
                    int splitPower  = targetCount > 0 ? Mathf.Max(1, power / targetCount) : power;
                    Debug.Log($"[ApplySkillDamage] AllEnemies 분산 — 총 {power} ÷ {targetCount}명 = {splitPower}/적");
                    foreach (var e in liveEnemies)
                        DealDamageToEnemy(e, splitPower);
                    // 멸절 (캐스터) — HP 최저 적에게 추가 단일타
                    TryAnnihilate(user, splitPower);
                }
                break;

            default:
                Debug.LogWarning($"[ApplySkillDamage] 미지원 targeting: '{skill.targeting}'");
                break;
        }

        // 피의 갈망 (어택커) — 이번 캐스트로 적이 1명이라도 죽었으면 회복 (캐스트당 1회)
        int liveAfter = enemies.Count(e => !e.isDead);
        if (liveAfter < liveBefore) TryLifesteal(user);
    }

    // ── 동료 시그니처 패시브 (전투 관여형, 기획 §16) — user.activePassiveId 로 판정 ──

    /// <summary>딜러 계열 데미지 배율 패시브 일괄 적용 (배수의진/주문증폭/일기당천 — 인스턴스당 1개만 활성).</summary>
    private int ApplyDamagePassives(FellowData user, int power, int liveEnemyCount)
    {
        if (user == null) return power;
        string p = user.activePassiveId;

        // 배수의 진 (어택커) — HP 낮을수록 +최대50%
        if (p == MetaPassiveManager.AttackerFrenzy)
        {
            int maxHp = user.maxHp > 0 ? user.maxHp : 1;
            float hpRatio = Mathf.Clamp01((float)user.CurrentHp / maxHp);
            float bonus = Mathf.Clamp01((0.5f - hpRatio) / 0.5f) * MetaPassiveManager.AttackerFrenzyMax;
            if (bonus > 0f) { int b = Mathf.RoundToInt(power * (1f + bonus)); Debug.Log($"[배수의진] {user.positionStack} {power}→{b}"); return b; }
        }
        // 주문 증폭 (캐스터) — +15%
        else if (p == MetaPassiveManager.CasterAmp)
        {
            int b = Mathf.RoundToInt(power * (1f + MetaPassiveManager.CasterAmpBonus));
            Debug.Log($"[주문증폭] {user.positionStack} {power}→{b}");
            return b;
        }
        // 일기당천 (오펜더) — 적 2마리 이하 +30%
        else if (p == MetaPassiveManager.OffenderDuel && liveEnemyCount <= MetaPassiveManager.OffenderDuelMaxEnemies)
        {
            int b = Mathf.RoundToInt(power * (1f + MetaPassiveManager.OffenderDuelBonus));
            Debug.Log($"[일기당천] {user.positionStack} 적{liveEnemyCount} {power}→{b}");
            return b;
        }
        return power;
    }

    /// <summary>단일 타격 직전 패시브 (거합집중/처형인 — 인스턴스당 1개만 활성).</summary>
    private int ApplySingleHitPassives(FellowData user, EnemyData target, int power)
    {
        if (user == null) return power;
        string p = user.activePassiveId;

        // 거합 집중 (오펜더) — 동일 대상 연속타 +20%/스택(최대 +60%)
        if (p == MetaPassiveManager.OffenderCombo)
        {
            int iid = target.GetInstanceID();
            if (user.comboTargetIid == iid)
                user.comboStacks = Mathf.Min(user.comboStacks + 1, MetaPassiveManager.OffenderComboMaxStack);
            else { user.comboTargetIid = iid; user.comboStacks = 0; }
            float bonus = user.comboStacks * MetaPassiveManager.OffenderComboPerStack;
            if (bonus > 0f) { int b = Mathf.RoundToInt(power * (1f + bonus)); Debug.Log($"[거합집중] {user.positionStack} {user.comboStacks}스택 {power}→{b}"); return b; }
        }
        // 처형인 (오펜더) — HP 30% 이하 적 +50%
        else if (p == MetaPassiveManager.OffenderExecute)
        {
            if (EnemyHpRatio(target) <= MetaPassiveManager.OffenderExecThreshold)
            {
                int b = Mathf.RoundToInt(power * (1f + MetaPassiveManager.OffenderExecBonus));
                Debug.Log($"[처형인] {user.positionStack} 저HP적 {power}→{b}");
                return b;
            }
        }
        return power;
    }

    /// <summary>비전 연쇄 (캐스터) — 매직미사일로 적 처치 시 다른 적 1명에게 1회 추가 발동(연쇄 없음).</summary>
    private void TryArcaneChain(FellowData user, SkillData skill, EnemyData firstTarget, int power)
    {
        if (user == null || user.activePassiveId != MetaPassiveManager.CasterChain) return;
        if (skill == null || skill.id != "skill_magic_missile") return;
        if (!firstTarget.isDead) return; // 처치했을 때만 연쇄
        var others = enemies.Where(e => e != firstTarget && !e.isDead).ToList();
        if (others.Count == 0) return;
        var next = others
            .OrderBy(e => e.tier == EnemyTier.Boss ? 1 : 0)
            .ThenBy(e => enemies.IndexOf(e))
            .First();
        GameLog.Formatted($"비전 연쇄! {next.displayName}에게 추가 타격 (+{power})", LogCategory.Damage);
        Debug.Log($"[비전연쇄] {user.positionStack} 처치 연쇄 → {next.displayName} +{power}");
        DealDamageToEnemy(next, power);
    }

    /// <summary>멸절 (캐스터) — 광역 데미지 시 HP 최저 적에게 추가 단일타.</summary>
    private void TryAnnihilate(FellowData user, int basePower)
    {
        if (user == null || user.activePassiveId != MetaPassiveManager.CasterExec) return;
        var live = enemies.Where(e => !e.isDead).ToList();
        if (live.Count == 0) return;
        var lowest = live.OrderBy(e => EnemyHpRatio(e)).First();
        int extra = Mathf.RoundToInt(basePower * MetaPassiveManager.CasterExecBonus);
        if (extra <= 0) return;
        Debug.Log($"[멸절] {user.positionStack} → {lowest.displayName} 추가타 +{extra}");
        DealDamageToEnemy(lowest, extra);
    }

    /// <summary>피의 갈망 (어택커) — 적 처치 시 자기 HP 15%(maxHp) 회복. 캐스트당 1회.</summary>
    private void TryLifesteal(FellowData user)
    {
        if (user == null || user.isDead || user.activePassiveId != MetaPassiveManager.AttackerLifesteal) return;
        int heal = Mathf.RoundToInt((user.maxHp > 0 ? user.maxHp : 100) * MetaPassiveManager.AttackerLifestealRatio);
        if (heal <= 0) return;
        user.CurrentHp = Mathf.Min(user.maxHp > 0 ? user.maxHp : user.CurrentHp + heal, user.CurrentHp + heal);
        UpdateAllyHpUI(user);
        GameLog.Formatted($"피의 갈망 — {user.displayName ?? user.positionStack.ToString()} HP +{heal}.", LogCategory.Heal);
        Debug.Log($"[피의갈망] {user.positionStack} 처치 회복 +{heal}");
    }

    /// <summary>
    /// 적 1명에게 데미지를 적용하고 로그를 남긴다.
    /// hitCountToDie > 0 (까마귀 등) 인 적은 HP 무관 hit-count 기반 처치.
    /// </summary>
    private void DealDamageToEnemy(EnemyData target, int power)
    {
        // 전용음 부여 스킬이면 디폴트 타격음 대신 전용음 1회 (AOE 도 첫 타격에서만)
        if (_castImpactSfx != SfxId.None)
        {
            if (!_castImpactSfxPlayed)
            {
                AudioManager.Instance?.PlaySfxById(_castImpactSfx);
                _castImpactSfxPlayed = true;
            }
        }
        else
            AudioManager.Instance?.PlaySfxById(SfxId.AttackSword);
        AudioManager.Instance?.PlaySfxById(SfxId.HurtEnemy);
        // 적은 쉴드 없음 — 흡수=0, HP 감소=power
        if (power > 0) target.OnDamaged?.Invoke(0, power);
        // 기획 §11 §3 까마귀: "공격 횟수 2회, 데미지 수치 무관"
        // HP 바 가시성을 위해 hit 카운트에 비례해 CurrentHp 도 줄여준다.
        // (예: maxHp=2, hitCountToDie=2 → 1 hit 마다 HP 1씩 감소)
        if (target.hitCountToDie > 0)
        {
            target.currentHits++;
            int remaining = Mathf.Max(0, target.maxHp - target.currentHits);
            target.CurrentHp = remaining; // setter 가 0 이면 isDead + OnDied 처리
            Debug.Log($"  └ [공격] {target.displayName} ← {target.currentHits}/{target.hitCountToDie} 히트 (HP {remaining}/{target.maxHp})");
            if (target.currentHits >= target.hitCountToDie)
            {
                GameLog.Formatted($"{target.displayName} 처치!", LogCategory.Death);
                Debug.Log($"  └ [처치] {target.displayName} — 공격 {target.hitCountToDie}회로 처치됨");
            }
            return;
        }

        // 기본: HP 기반 처치
        int beforeHp = target.CurrentHp;
        target.CurrentHp -= power;
        GameLog.Formatted($"{target.displayName}이(가) {power}의 피해를 입었다!", LogCategory.Damage);
        Debug.Log($"  └ [데미지] {target.displayName} ← {power} 데미지  (HP: {beforeHp} → {target.CurrentHp}/{target.maxHp})");
    }

    /// <summary>적의 현재 HP 비율을 0~1 사이로 반환한다. maxHp 가 0 이하면 0.</summary>
    private static float EnemyHpRatio(EnemyData e)
    {
        return e.maxHp > 0 ? (float)e.CurrentHp / e.maxHp : 0f;
    }

    /// <summary>아군의 현재 HP 비율을 0~1 사이로 반환한다. data 가 null 이면 maxHp 100 가정.</summary>
    private static float AllyHpRatio(FellowData a)
    {
        int max = a.maxHp > 0 ? a.maxHp : 100;
        return max > 0 ? (float)a.CurrentHp / max : 0f;
    }

    /// <summary>스킬 회복 적용. Self / SingleAlly / AllAllies 분기.</summary>
    private void ApplySkillHeal(FellowData user, SkillData skill, int power)
    {
        AudioManager.Instance?.PlaySfxById(SfxId.Heal);
        var liveAllies = allies.Where(a => !a.isDead).ToList();

        // ── 축복받은 손길 (프리스트 패시브, 기획 §16) — 힐량 +25% ──
        if (user.activePassiveId == MetaPassiveManager.PriestBlessing)
        {
            int b = Mathf.RoundToInt(power * (1f + MetaPassiveManager.PriestBlessingBonus));
            Debug.Log($"[축복받은손길] {user.positionStack} 힐 {power}→{b}");
            power = b;
        }

        // ── 서포터 중증 디버프 (기획 §04) — 광역 회복 → 단일 회복(최저 HP) 강제 ──
        string targeting = skill.targeting;
        if (user.role == CompanionRole.Support && user.hasSevereDebuff && targeting == "AllAllies")
        {
            Debug.Log($"[중증디버프·서포터] {user.positionStack} 광역 회복 → 단일 회복으로 약화");
            targeting = "SingleAlly";
        }

        switch (targeting)
        {
            case "Self":
                user.CurrentHp += power;
                UpdateAllyHpUI(user);
                GameLog.Formatted($"{user.displayName ?? user.positionStack.ToString()}의 HP +{power} 회복.", LogCategory.Heal);
                Debug.Log($"[ApplySkillHeal] {user.displayName ?? user.positionStack.ToString()} 자신 +{power} HP (현재: {user.CurrentHp})");
                ApplyPriestHealBonus(user, user, power);
                break;
            // 기획 §02 §자동 타겟팅 §아군 지원 — "HP 비율 최저 아군 우선" (혼자 생존 시 자기 자신)
            case "SingleAlly":
                if (liveAllies.Count == 0) break;
                var healTarget = liveAllies.OrderBy(a => AllyHpRatio(a)).First();
                healTarget.CurrentHp += power;
                UpdateAllyHpUI(healTarget);
                GameLog.Formatted($"{healTarget.displayName ?? healTarget.positionStack.ToString()}의 HP +{power} 회복.", LogCategory.Heal);
                Debug.Log($"[ApplySkillHeal] {healTarget.displayName ?? healTarget.positionStack.ToString()} +{power} HP (현재: {healTarget.CurrentHp})");
                ApplyPriestHealBonus(user, healTarget, power);
                break;
            case "AllAllies":
                foreach (var ally in liveAllies)
                {
                    ally.CurrentHp += power;
                    UpdateAllyHpUI(ally);
                    GameLog.Formatted($"{ally.displayName ?? ally.positionStack.ToString()}의 HP +{power} 회복.", LogCategory.Heal);
                    Debug.Log($"[ApplySkillHeal] {ally.displayName ?? ally.positionStack.ToString()} +{power} HP (현재: {ally.CurrentHp})");
                    ApplyPriestHealBonus(user, ally, power);
                }
                break;
            default:
                Debug.LogWarning($"[ApplySkillHeal] 미지원 targeting: '{skill.targeting}'");
                break;
        }
    }

    /// <summary>프리스트 힐 부가 패시브 (기획 §16) — 정화의 빛(스트레스 감소) / 수호 기도(실드 부여). 인스턴스당 1개만 활성.</summary>
    private void ApplyPriestHealBonus(FellowData user, FellowData ally, int healAmount)
    {
        if (user == null || ally == null) return;
        // 정화의 빛 — 힐량의 50%만큼 스트레스 감소
        if (user.activePassiveId == MetaPassiveManager.PriestCleanse)
        {
            int reduce = Mathf.RoundToInt(healAmount * MetaPassiveManager.PriestCleanseRatio);
            if (reduce > 0)
            {
                ally.currentStress = Mathf.Max(0, ally.currentStress - reduce);
                GameLog.Formatted($"정화의 빛 — {ally.displayName ?? ally.positionStack.ToString()} 스트레스 -{reduce}.", LogCategory.Status);
                Debug.Log($"[정화의빛] {ally.positionStack} 스트레스 -{reduce}");
            }
        }
        // 수호 기도 — 힐량의 30%만큼 실드 부여
        else if (user.activePassiveId == MetaPassiveManager.PriestGuard)
        {
            int sh = Mathf.RoundToInt(healAmount * MetaPassiveManager.PriestGuardRatio);
            if (sh > 0)
            {
                ally.AddShield(sh);
                GameLog.Formatted($"수호 기도 — {ally.displayName ?? ally.positionStack.ToString()} 실드 +{sh}.", LogCategory.Shield);
                Debug.Log($"[수호기도] {ally.positionStack} 실드 +{sh}");
            }
        }
    }

    /// <summary>실드 스킬 적용. targeting 에 따라 Self / SingleAlly / AllAllies 분기.</summary>
    private void ApplySkillShield(FellowData user, SkillData skill, int power)
    {
        // ── 견고한 방벽 (디펜더 패시브, 기획 §16) — 부여 실드 +30% ──
        if (user.activePassiveId == MetaPassiveManager.DefenderBulwark)
        {
            int b = Mathf.RoundToInt(power * (1f + MetaPassiveManager.DefenderBulwarkBonus));
            Debug.Log($"[견고한방벽] {user.positionStack} 부여 실드 {power}→{b}");
            power = b;
        }

        // ── 탱커 중증 디버프 (기획 §04) — 부여 실드 -50% ──
        if (user.role == CompanionRole.Tanker && user.hasSevereDebuff)
        {
            int reduced = Mathf.RoundToInt(power * 0.5f);
            Debug.Log($"[중증디버프·탱커] {user.positionStack} 부여 실드 {power} → {reduced} (-50%)");
            power = reduced;
        }

        var liveAllies = allies.Where(a => !a.isDead).ToList();

        switch (skill.targeting)
        {
            case "Self":
                user.shield += power;
                user.OnShieldChanged?.Invoke();
                GameLog.Formatted($"{user.displayName ?? user.positionStack.ToString()}의 실드 +{power}.", LogCategory.Shield);
                Debug.Log($"[ApplySkillShield] {user.displayName ?? user.positionStack.ToString()} 자신 +{power} 실드 (현재: {user.shield})");
                break;
            // 기획 §02 §자동 타겟팅 §아군 지원 — "HP 비율 최저 아군 우선" (정책 통일)
            case "SingleAlly":
                if (liveAllies.Count == 0) break;
                var shieldTarget = liveAllies.OrderBy(a => AllyHpRatio(a)).First();
                shieldTarget.shield += power;
                shieldTarget.OnShieldChanged?.Invoke();
                GameLog.Formatted($"{shieldTarget.displayName ?? shieldTarget.positionStack.ToString()}의 실드 +{power}.", LogCategory.Shield);
                Debug.Log($"[ApplySkillShield] {shieldTarget.displayName ?? shieldTarget.positionStack.ToString()} +{power} 실드 (현재: {shieldTarget.shield})");
                break;
            case "AllAllies":
                foreach (var ally in liveAllies)
                {
                    ally.shield += power;
                    ally.OnShieldChanged?.Invoke();
                    GameLog.Formatted($"{ally.displayName ?? ally.positionStack.ToString()}의 실드 +{power}.", LogCategory.Shield);
                    Debug.Log($"[ApplySkillShield] {ally.displayName ?? ally.positionStack.ToString()} +{power} 실드 (현재: {ally.shield})");
                }
                break;
            default:
                Debug.LogWarning($"[ApplySkillShield] 미지원 targeting: '{skill.targeting}'");
                break;
        }
    }

    private void DebugPrintEnemyStates(string context = "")
    {
        Debug.Log($"{context} ── 적 상태 전체 ({enemies.Count}명) ──────────────");
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null)
            {
                Debug.LogError($"  [{i}] NULL ← Inspector 리스트에 빈 슬롯 있음!");
                continue;
            }
            string status = e.isDead ? "사망" : "생존";
            Debug.Log($"  [{i}] {e.displayName} | HP:{e.CurrentHp}/{e.maxHp} | isDead:{e.isDead} | {status}");
        }
    }

    // ===========================================================
    // [ContextMenu] 에디터 테스트 메서드
    // ===========================================================

    /// <summary>[에디터 테스트] 살아있는 아군 전체에게 10 데미지를 입힌다.</summary>
    [ContextMenu("TEST / 아군 전체 10 데미지")]
    private void TestDamageAllAllies()
    {
        foreach (var ally in allies.Where(a => !a.isDead))
            ApplyDamageToAlly(ally, 10);
    }

    /// <summary>[에디터 테스트] 살아있는 아군 중 랜덤 1명에게 10 데미지를 입힌다.</summary>
    [ContextMenu("TEST / 아군 랜덤 1명 10 데미지")]
    private void TestDamageRandomAlly()
    {
        var targets = allies.Where(a => !a.isDead).ToList();
        if (targets.Count == 0)
        {
            Debug.LogWarning("[BattleManager] 살아있는 아군이 없습니다.");
            return;
        }
        ApplyDamageToAlly(targets[Random.Range(0, targets.Count)], 10);
    }

    /// <summary>[에디터 테스트] 현재 아군 HP 상태를 콘솔에 출력한다.</summary>
    [ContextMenu("TEST / 아군 HP 상태 출력")]
    private void TestPrintAlliesHP()
    {
        Debug.Log($"[BattleManager] 아군 HP 상태 ({allies.Count}명):");
        foreach (var ally in allies)
            Debug.Log($"  {ally.positionStack} | HP: {ally.CurrentHp}/{ally.maxHp} | 사망: {ally.isDead} | 스트레스: {ally.currentStress} | 실드: {ally.shield}");
    }

    /// <summary>[에디터 테스트] 현재 전투 페이즈 및 선공 정보를 출력한다.</summary>
    [ContextMenu("TEST / 전투 상태 출력")]
    private void TestPrintBattleState()
    {
        Debug.Log($"[BattleManager] 현재 페이즈: {currentPhase}");
        Debug.Log($"[BattleManager] 선공: {(isAllyFirstAttacker ? "아군" : "적군")}");
        Debug.Log($"[BattleManager] 아군 {allies.Count}명 / 적군 {enemies.Count}명");
    }

    /// <summary>[에디터 테스트] 아군 스킬 배정 무결성 검사.</summary>
    [ContextMenu("TEST / 스킬 배정 무결성 검사")]
    private void TestSkillAssignmentIntegrity()
    {
        Debug.Log("[BattleManager] ── 스킬 배정 무결성 검사 시작 ──");
        int errors = 0;

        if (SkillDatabase.Instance == null)
        { Debug.LogError("  [오류] SkillDatabase 인스턴스 없음 — 씬에 배치하세요."); return; }

        if (allies.Count == 0)
        { Debug.LogWarning("  [경고] allies 없음 — 전투 시작 후 테스트하세요."); return; }

        foreach (var ally in allies)
        {
            string name = !string.IsNullOrEmpty(ally.displayName) ? ally.displayName : ally.positionStack.ToString();
            if (!ally.HasSkills)      { Debug.LogError($"  [오류] {name}: 스킬 배정 안 됨 (HasSkills == false)"); errors++; continue; }

            var skills = ally.GetSkills();
            Debug.Log($"  [{name}] 스킬 {skills.Count}개:");
            for (int i = 0; i < skills.Count; i++)
            {
                string tag = (i == 0) ? "[활성]         " : "[비활성-테스트용]";
                Debug.Log($"    {tag} {skills[i].displayName}  |  {skills[i].effectType}  {skills[i].targeting}  파워:{skills[i].power}  코스트:{skills[i].costAmount}");
            }
        }

        if (errors == 0) Debug.Log($"[BattleManager] 무결성 검사 통과! ({allies.Count}명)");
        else             Debug.LogError($"[BattleManager] 무결성 검사 실패: {errors}개 오류.");
    }

    /// <summary>[에디터 테스트] 아군 [0] 스킬 강제 실행.</summary>
    [ContextMenu("TEST / 아군 [0] 스킬 강제 실행")]
    private void TestForceUseFirstAllySkill()
    {
        var live = allies.Where(a => !a.isDead).ToList();
        if (live.Count == 0) { Debug.LogWarning("[BattleManager] 살아있는 아군 없음."); return; }
        var skills = live[0].GetSkills();
        if (skills.Count == 0) { Debug.LogWarning("[BattleManager] 배정된 스킬 없음."); return; }
        UseSkill(live[0], skills[0]);
    }

    /// <summary>[에디터 테스트] 아군 [0] 에게 패닉 강제 발동.</summary>
    [ContextMenu("TEST / 아군 [0] 패닉 강제 발동")]
    private void TestForcePanic()
    {
        var live = allies.Where(a => !a.isDead).ToList();
        if (live.Count == 0) { Debug.LogWarning("[BattleManager] 살아있는 아군 없음."); return; }
        live[0].currentStress = 100;
        TriggerPanicIfNeeded(live[0]);
        Debug.Log($"[TEST] {live[0].positionStack} — 패닉 발동 (경직:{live[0].isFrozen} 과호흡:{live[0].isOverBreathing})");
    }

    /// <summary>EnemyData 에 바인딩된 BattleCardView 의 world position 을 반환. 없으면 (0,0,0).</summary>
    private Vector3 FindEnemyWorldPos(EnemyData target)
    {
        if (target == null) return Vector3.zero;
        var views = Object.FindObjectsByType<BattleCardView>(FindObjectsSortMode.None);
        foreach (var v in views)
            if (v != null && v.Enemy == target) return v.transform.position;
        return Vector3.zero;
    }
}
