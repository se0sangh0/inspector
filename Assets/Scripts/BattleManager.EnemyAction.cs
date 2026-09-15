// ============================================================
// BattleManager.EnemyAction.cs
// 적 턴 행동 로직 (파셜 클래스 분리 파일)
// ============================================================
//
// [왜 partial 로 분리했나요?]
//   기존 BattleManager.Combat.cs 의 적 행동 부분은 14줄짜리 단순 코드였지만,
//   이제 (1) 가중치 랜덤 스킬 선택 + (2) 타겟 결정 + (3) 다중 대상 데미지 적용
//   세 단계가 추가되어 분량이 늘어납니다.
//   기존 Combat.cs 를 비대화시키지 않고, 적 행동이라는 단일 책임을 따로 두는 게
//   읽기/수정에 유리해서 partial 로 떼어냈습니다.
//
// [Combat.cs 와의 연결 지점]
//   Combat.cs 의 ExecuteAction(false) 분기에서
//   기존의 attackPower 직타 코드 14줄을 ExecuteEnemyTurn(enemy) 호출 1줄로 교체합니다.
//
// [동작 흐름]
//   1) PickEnemySkill(enemy)   — enemy.skillIds 중 weight 가중치 랜덤 1개
//      └ 스킬 DB 없거나 skillIds 비면 null 반환 → fallback 으로 attackPower 직타
//   2) EnemySkillExecutor.ResolveTargets(skill, allies) — 살아있는 아군 중 대상 결정
//   3) 각 대상에 ApplyDamageToAlly(target, skill.power) — 실드/HP/스트레스 처리
//
// [Fallback 정책 — 안전장치]
//   - skillIds 가 빈 경우 (구 데이터 호환)
//   - EnemySkillDatabase 가 씬에 없는 경우 (사용자가 GameObject 추가 잊은 경우)
//   → 둘 다 기존 attackPower 단순 공격으로 동작 → 적이 절대 멈추지 않음
// ============================================================

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class BattleManager
{
    // ============================================================
    // 적 1명의 턴 행동 — Combat.cs 의 적 행동 블록에서 호출
    // ============================================================
    private IEnumerator ExecuteEnemyTurn(EnemyData enemy)
    {
        if (enemy == null || enemy.isDead) yield break;

        // 까마귀 같은 패시브 소환체는 행동 안 함 (기획 §11 §3)
        if (enemy.isPassive)
        {
            Debug.Log($"[적 행동/스킵] {enemy.displayName} — passive 소환체");
            yield break;
        }

        // 살아있는 아군이 0명이면 행동 자체가 의미 없음
        if (allies.All(a => a.isDead)) yield break;

        // ── 1) 스킬 선택 ────────────────────────────────────────
        var skill = PickEnemySkill(enemy);

        // ── 2) 시전 (사운드/쿨다운/모션/효과) — 디버그 툴도 이 경로를 그대로 재사용 ──
        yield return StartCoroutine(ExecuteEnemySkillCast(enemy, skill));
    }

    /// <summary>
    /// 선택된 스킬 1개의 실제 시전 — 사운드/쿨다운/모션 트리거/효과(데미지·소환·순간이동·수확) 전부.
    /// ExecuteEnemyTurn(스킬 자동선택) 과 DebugCastAllEnemySkills(인덱스 지정) 가 공유한다. (2026-06-11 분리)
    /// </summary>
    // 2026-06-13(QA): 소환 모션을 다 취한 뒤 까마귀가 등장하도록 대기할 시간(보스 공격 모션 attackHoldDuration ~1.0s 기준).
    private const float SummonMotionDelay = 1.0f;

    private IEnumerator ExecuteEnemySkillCast(EnemyData enemy, EnemySkillData skill)
    {
        // 적 스킬음 — 도끼 던지기(약탈자)는 전용음, 그 외는 일반 적 스킬음. (2026-06-09)
        if (skill != null && (skill.displayName ?? "").Contains("도끼 던지기"))
            AudioManager.Instance?.PlaySfxById(SfxId.SkillAxeThrow);
        else
            AudioManager.Instance?.PlaySfxById(SfxId.EnemySkill);

        // 선택된 스킬에 cooldown 설정이 있으면 즉시 시작 (다음 N 턴 동안 룰렛 제외).
        // 단 소환 스킬은 예외 — 기획 §11 §3: 재소환 쿨다운은 '까마귀 처치/만료 시점'부터 (ExecuteSummonSkill 의 OnDied 훅이 시작).
        if (skill != null && skill.cooldownTurns > 0 && skill.effectType != "Summon")
        {
            enemy.StartSkillCooldown(skill.id, skill.cooldownTurns);
            Debug.Log($"[적 스킬/쿨다운 시작] {enemy.displayName} → {skill.displayName} | {skill.cooldownTurns}턴 동안 사용 불가");
        }

        // 모션 트리거 — View 가 effectType 기반 카테고리 결정 (적은 jobClass 없음 → Damage 면 Melee)
        // Fallback (skill null) 도 attackPower 직타이므로 "Damage" 로 발행. 적은 보통 skillIndex 0 (Attack2 미사용).
        int enemySkillIndex = (skill != null && enemy.skillIds != null) ? System.Array.IndexOf(enemy.skillIds, skill.id) : 0;
        if (enemySkillIndex < 0) enemySkillIndex = 0;
        // 적 원거리 스킬(독침/도끼던지기/까마귀 부름/수확/순간이동) — JSON 의 isRanged 플래그가 dash 시퀀스 차단.
        bool enemySkillIsRanged = skill != null && skill.isRanged;
        enemy.OnSkillCast?.Invoke(skill != null ? skill.effectType : "Damage", enemySkillIndex, enemySkillIsRanged);

        if (skill == null)
        {
            // ── Fallback: 스킬 미정의 / 스킬 DB 없음 → 기존 단순 공격 ──
            var firstAlive = allies.FirstOrDefault(a => !a.isDead);
            if (firstAlive == null) yield break;

            yield return new WaitForSeconds(meleeImpactDelay);   // 아군과 동일 — 휘두르는 순간 데미지
            ApplyDamageToAlly(firstAlive, enemy.attackPower);
            GameLog.Formatted($"{enemy.displayName}이(가) {firstAlive.displayName ?? firstAlive.positionStack.ToString()}을(를) 공격!", LogCategory.Skill);
            Debug.Log($"[적 행동/Fallback] {enemy.displayName} → {firstAlive.positionStack} 에게 {enemy.attackPower} 데미지 (스킬 미정의)");
            yield break;
        }

        // ── 2) effectType 별 분기 — Teleport 는 즉시 효과. Summon 은 모션 재생 후 소환 (2026-06-13 QA).
        if (skill.effectType == "Summon")
        {
            // 소환 모션(원거리 제자리)을 다 취한 뒤 까마귀가 등장하도록 모션 길이만큼 대기 후 소환.
            yield return new WaitForSeconds(SummonMotionDelay);
            ExecuteSummonSkill(enemy, skill);
            yield break;
        }
        if (skill.effectType == "Teleport")
        {
            yield return StartCoroutine(ExecuteTeleportSkill(enemy, skill));   // 연출 완료 후 배치 역전까지 대기
            yield break;
        }
        if (skill.effectType == "Harvest")
        {
            yield return StartCoroutine(ExecuteHarvestSkill(enemy, skill));
            yield break;
        }

        // ── 3) 타겟 결정 + 데미지 적용 ──────────────────────────
        var targets = EnemySkillExecutor.ResolveTargets(skill, allies, enemy);
        if (targets.Count == 0)
        {
            Debug.Log($"[적 스킬] {enemy.displayName} → {skill.displayName} 타겟 없음 (전 아군 사망)");
            yield break;
        }

        string targetNames = string.Join(", ",
            targets.Select(t => !string.IsNullOrEmpty(t.displayName) ? t.displayName : t.positionStack.ToString()));

        GameLog.Formatted($"{enemy.displayName}이(가) [{skill.displayName}]을(를) 사용했다!", LogCategory.Skill);
        Debug.Log($"┌─────────────────────────────────────────");
        Debug.Log($"│ [적 스킬] {enemy.displayName} → {skill.displayName}");
        Debug.Log($"│  타겟 종류: {skill.targeting} ({targets.Count}명) → {targetNames}");
        Debug.Log($"│  파워: {skill.power}");
        Debug.Log($"│  설명: {skill.description}");
        Debug.Log($"└─────────────────────────────────────────");

        // 타격 순간(전진 후 휘두를 때) 데미지 적용 — 아군 근접과 동일 타이밍 (기존: 모션 종료 후 1.25s 라 늦게 들어갔음)
        yield return new WaitForSeconds(meleeImpactDelay);
        foreach (var t in targets)
        {
            ApplyDamageToAlly(t, skill.power);

            // DoT 부착 — 기획 §11 §독침. dotTurns > 0 이면 즉시 데미지에 더해 다음 N턴 dotPower 누적.
            // 사망한 대상은 제외 (기획 §02 §2 사망 시 효과 대상 제외).
            if (skill.dotTurns > 0 && skill.dotPower > 0 && !t.isDead)
            {
                t.dotTurnsLeft = skill.dotTurns;        // 덮어쓰기 (스택 없음)
                t.dotPerTurn   = skill.dotPower;
                t.OnDotChanged?.Invoke(); // UI 초록 tint 토글
                GameLog.Formatted($"{t.displayName ?? t.positionStack.ToString()}이(가) 중독되었다! ({skill.dotPower}×{skill.dotTurns}턴)", LogCategory.Status);
                Debug.Log($"[DoT 부착] {t.displayName} ← {skill.dotPower}/턴 × {skill.dotTurns}턴");
            }
        }
    }

    // ============================================================
    // 소환 스킬 실행 — effectType="Summon" 전용
    //   기획 §11 §3 보스 까마귀 부름: 까마귀 2마리 소환
    //   summonEnemyId 의 적을 summonCount 마릿수만큼 enemies 리스트에 추가.
    // ============================================================
    private void ExecuteSummonSkill(EnemyData caster, EnemySkillData skill)
    {
        if (string.IsNullOrEmpty(skill.summonEnemyId))
        {
            Debug.LogWarning($"[Summon] {skill.id} — summonEnemyId 비어있음.");
            return;
        }
        if (EnemyDatabase.Instance == null)
        {
            Debug.LogWarning("[Summon] EnemyDatabase 없음.");
            return;
        }

        var def = EnemyDatabase.Instance.GetEnemy(skill.summonEnemyId);
        if (def == null)
        {
            Debug.LogWarning($"[Summon] 적 정의 없음: {skill.summonEnemyId}");
            return;
        }

        int count = Mathf.Max(1, skill.summonCount);

        Debug.Log($"┌─────────────────────────────────────────");
        Debug.Log($"│ [적 소환] {caster.displayName} → {skill.displayName}");
        Debug.Log($"│  소환 대상: {def.displayName} × {count}");
        Debug.Log($"└─────────────────────────────────────────");

        for (int i = 0; i < count; i++)
        {
            var summoned = EnemyDatabase.CreateRuntimeEnemy(def);
            // 소환된 턴 끝 ResultProcessing 에서 -1 되어도 정확히 summonLifeTurns 후 만료되도록 +1 보정
            summoned.currentLifeTurns = summoned.summonLifeTurns + 1;
            enemies.Add(summoned);
            RaiseEnemySpawned(summoned); // 시각 카드/이펙트/사운드 구독자에게 알림 (BattleCardView 가 이 시점에 BindEnemy 호출, 초기 표시는 BindEnemy 내부에서 처리)
            GameLog.Formatted($"{summoned.displayName}이(가) 등장!", LogCategory.Skill);
            Debug.Log($"  └ [소환됨] {summoned.displayName} (수명 {summoned.summonLifeTurns}턴 / {summoned.hitCountToDie} hit 처치)");

            // 기획 §11 §3 — 재소환 쿨다운 3턴은 '까마귀가 죽은 시점'(처치·자폭 공통)부터 시작.
            // 마지막으로 죽은 까마귀 기준으로 갱신된다 (OnDied 는 CurrentHp=0 시 1회 발동).
            var owner   = caster;
            var skillId = skill.id;
            int cd      = Mathf.Max(1, skill.cooldownTurns);
            summoned.OnDied += () =>
            {
                if (owner == null || owner.isDead) return;
                owner.StartSkillCooldown(skillId, cd);
                Debug.Log($"[소환체 사망] {owner.displayName} — 재소환 쿨다운 {cd}턴 시작 (기획: 처치 시점 기준)");
            };
        }

        // 까마귀 소환 완료. 순간이동은 '까마귀 부름 실패'(까마귀 생존/쿨다운으로 소환 불가) 시 PickBossBaseSkill 에서 선택 (2026-06-13).
    }

    // ============================================================
    // 가중치 랜덤으로 적 스킬 1개 선택 — 강제 트리거 우선
    // ============================================================
    private EnemySkillData PickEnemySkill(EnemyData enemy)
    {
        // ── 1) 강제 스킬 (조건부 1회 발동) 우선 체크 ────────────
        var forced = TryGetForcedSkill(enemy);
        if (forced != null)
        {
            enemy.usedOnceSkills.Add(forced.id);
            Debug.Log($"[적 행동/강제] {enemy.displayName} → {forced.displayName} 발동 (1회 한정)");
            return forced;
        }

        // ── 1.5) 보스(거두는 자) 전용 상태머신 — 휘두르기/까마귀부름 룰렛, 소환 실패 시 순간이동 (기획 §11 §3, 2026-06-13) ──
        if (enemy.tier == EnemyTier.Boss)
        {
            var bossPick = PickBossBaseSkill(enemy);
            if (bossPick != null) return bossPick;
        }

        // ── 2) 가중치 룰렛 (1회 스킬·weight 0 스킬 제외) ──────────
        if (enemy.skillIds == null || enemy.skillIds.Length == 0) return null;
        if (EnemySkillDatabase.Instance == null) return null;

        var skills = new List<EnemySkillData>();
        int totalWeight = 0;
        foreach (var id in enemy.skillIds)
        {
            var s = EnemySkillDatabase.Instance.GetSkill(id);
            if (s == null) continue;

            // weight 0 = 가중치 룰렛 제외 (강제 트리거 전용 스킬 표시)
            if (s.weight <= 0) continue;

            // 1회 한정 스킬이 이미 사용됐다면 룰렛에서 제외
            if (enemy.usedOnceSkills.Contains(id)) continue;

            // 쿨다운 중이면 룰렛에서 제외 (예: 까마귀 부름 사용 후 3턴)
            if (enemy.GetSkillCooldown(id) > 0)
            {
                Debug.Log($"[적 스킬/쿨다운] {enemy.displayName} → {s.displayName} 잔여 {enemy.GetSkillCooldown(id)}턴 — 룰렛 제외");
                continue;
            }

            // 소환 스킬이고 소환 대상이 이미 필드에 살아있으면 룰렛에서 제외
            // (예: 까마귀가 필드에 1마리라도 살아있으면 까마귀 부름 안 함 → 다른 스킬 우선)
            if (s.effectType == "Summon" && !string.IsNullOrEmpty(s.summonEnemyId))
            {
                int sameIdCount  = enemies.Count(e => e != null && e.id == s.summonEnemyId);
                int aliveIdCount = enemies.Count(e => e != null && !e.isDead && e.id == s.summonEnemyId);
                Debug.Log($"[적 스킬/소환체크] {s.displayName} 대상='{s.summonEnemyId}' | enemies 매칭 {sameIdCount}개 (살아있음 {aliveIdCount})");
                if (aliveIdCount > 0)
                {
                    Debug.Log($"[적 스킬/소환제외] {enemy.displayName} → {s.displayName} — 룰렛 제외");
                    continue;
                }
            }

            skills.Add(s);
            totalWeight += s.weight;
        }
        if (skills.Count == 0 || totalWeight <= 0) return null;

        // 디버그: 룰렛 후보 목록 + 가중치 (까마귀 부름 안 나오는 케이스 추적용)
        Debug.Log($"[적 스킬/룰렛 후보] {enemy.displayName}: " + string.Join(", ", skills.Select(x => $"{x.displayName}(w={x.weight})")) + $" | totalWeight={totalWeight}");

        // 가중치 누적 합으로 룰렛 휠
        int roll = Random.Range(0, totalWeight);
        int acc  = 0;
        foreach (var s in skills)
        {
            acc += s.weight;
            if (roll < acc) return s;
        }
        return skills[skills.Count - 1]; // 부동소수 오차/엣지케이스 대비
    }

    // ============================================================
    // 보스(거두는 자) 기본 상태 행동 — 기획 §11 §3 상태머신 (2026-06-13)
    //   휘두르기/까마귀 부름 가중치 룰렛(JSON weight). 까마귀 부름이 막히면(까마귀 생존 or 쿨다운)
    //   "까마귀 부름 실패" → 순간이동으로 대체 (기획 비고 "까마귀 부름 실패 후에만 발동").
    //   데이터 미비 시 null → 일반 룰렛 폴백.
    // ============================================================
    private EnemySkillData PickBossBaseSkill(EnemyData boss)
    {
        var db = EnemySkillDatabase.Instance;
        if (db == null) return null;

        var swing    = db.GetSkill("enemy_skill_reaper_swing");
        var summon   = db.GetSkill("enemy_skill_reaper_summon");
        var teleport = db.GetSkill("enemy_skill_reaper_teleport");
        if (swing == null || summon == null) return null;

        int wSwing  = Mathf.Max(0, swing.weight);
        int wSummon = Mathf.Max(0, summon.weight);
        int total   = wSwing + wSummon;
        if (total <= 0) return null;

        int roll = Random.Range(0, total);
        if (roll < wSwing)
        {
            Debug.Log($"[보스 상태머신] {boss.displayName} → 휘두르기 (룰렛 {roll}/{total})");
            return swing;
        }

        // 까마귀 부름 시도 — 까마귀 생존 중이거나 쿨다운이면 "실패" → 순간이동
        bool crowsAlive = !string.IsNullOrEmpty(summon.summonEnemyId)
                          && enemies.Any(e => e != null && !e.isDead && e.id == summon.summonEnemyId);
        bool onCooldown = boss.GetSkillCooldown(summon.id) > 0;
        if (!crowsAlive && !onCooldown)
        {
            Debug.Log($"[보스 상태머신] {boss.displayName} → 까마귀 부름 (소환 가능)");
            return summon;
        }

        // 까마귀 부름 실패 → 순간이동 (기획 §11 §3 "까마귀 부름 실패 후에만 발동")
        if (teleport != null)
        {
            Debug.Log($"[보스 상태머신] {boss.displayName} → 까마귀 부름 실패(생존={crowsAlive}/쿨={onCooldown}) → 순간이동");
            return teleport;
        }
        Debug.LogWarning("[보스 상태머신] 순간이동 스킬 데이터 없음 — 휘두르기 폴백");
        return swing;
    }

    // ============================================================
    // 소환체 수명 카운터 + 만료 처리 — HandleResultProcessing 에서 매 턴 호출
    // ============================================================
    //   기획 §11 §3 보스 까마귀:
    //     소환 후 3턴 카운터, 0 도달 시:
    //       1) 패널티 발동 — 1마리당 expirePenaltyPower 데미지를 파티 전체에 분산 적용
    //       2) 까마귀 사망 처리
    //   까마귀가 hit-count 로 처치된 경우는 만료가 아니므로 패널티 안 발동.
    //   (2026-06-13: 만료→순간이동 연결 제거 — 순간이동은 '까마귀 부름 실패'(PickBossBaseSkill)에서 처리)
    // ============================================================
    private void ProcessSummonExpiration()
    {
        var summonsAlive = enemies
            .Where(e => e != null && !e.isDead && e.summonLifeTurns > 0)
            .ToList();
        if (summonsAlive.Count == 0) return;

        var aliveAllies = allies.Where(a => !a.isDead).ToList();
        if (aliveAllies.Count == 0) return; // 패널티 대상 없음

        foreach (var summon in summonsAlive)
        {
            summon.currentLifeTurns--;
            summon.OnLifeTurnsChanged?.Invoke(summon.currentLifeTurns); // 카운트다운 UI 갱신
            if (summon.currentLifeTurns > 0)
            {
                Debug.Log($"[소환체] {summon.displayName} 남은 수명 {summon.currentLifeTurns}턴");
                continue;
            }

            // ── 수명 만료 — 패널티 발동 + 사망 처리 ──
            // 적 → 아군 데미지는 고정 (분산 X). 각 아군 모두 expirePenaltyPower 데미지.
            GameLog.Formatted($"{summon.displayName} 만료! 각 아군에게 {summon.expirePenaltyPower}의 피해.", LogCategory.Damage);
            Debug.Log($"[소환체 만료] {summon.displayName} — 패널티 {summon.expirePenaltyPower} 데미지 (각 아군 고정)");

            if (summon.expirePenaltyPower > 0)
            {
                foreach (var ally in aliveAllies)
                    ApplyDamageToAlly(ally, summon.expirePenaltyPower);
            }

            // CurrentHp=0 setter 가 isDead=true + OnDied 자동 발동 → 시각 침몰 트윈 트리거
            summon.CurrentHp = 0;
            // 2026-06-13: 만료는 기획대로 40 패널티만. 순간이동은 '까마귀 부름 실패' 시 PickBossBaseSkill 에서 처리.
        }
    }

    // ============================================================
    // 적별 조건부 강제 스킬 — 기획 §11_적_스킬_시트 §행동 패턴
    // ============================================================
    private EnemySkillData TryGetForcedSkill(EnemyData enemy)
    {
        if (EnemySkillDatabase.Instance == null || enemy == null) return null;

        // [I] 약탈자 — HP ≤ 30% 시 도끼던지기 강제 발동 (1회 한정)
        //     기획 §11 §2 약탈자 §행동 패턴
        if (enemy.id == "enemy_raider_01")
        {
            const string AXE_THROW = "enemy_skill_raider_throw";
            if (enemy.HpRatio <= 0.30f && !enemy.usedOnceSkills.Contains(AXE_THROW))
                return EnemySkillDatabase.Instance.GetSkill(AXE_THROW);
        }

        // [L] 보스 — HP ≤ 50% 시 수확 강제 발동 (1회 한정).
        //     기획 §11 §3 거두는 자 §행동 패턴 — "[HP ≤ 50% && 수확 미사용] → 수확 강제 발동 → [기본 상태] 복귀"
        //     수확 발동 후 까마귀/순간이동 흐름 계속.
        if (enemy.tier == EnemyTier.Boss)
        {
            const string HARVEST = "enemy_skill_reaper_harvest";
            if (enemy.HpRatio <= 0.50f && !enemy.usedOnceSkills.Contains(HARVEST))
            {
                var harvest = EnemySkillDatabase.Instance.GetSkill(HARVEST);
                if (harvest != null)
                {
                    Debug.Log($"[적 스킬/강제] {enemy.displayName} → 수확 (HP {enemy.CurrentHp}/{enemy.maxHp} ≤ 50% — 확정 발동)");
                    return harvest;
                }
            }
        }

        // [J] (폐기, 2026-06-11) "필드에 까마귀 없으면 확정 소환" 규칙 제거 — 사용자 결정으로 기획 §11 §3 원안 복귀:
        //     기본 상태 매 턴 60% 휘두르기 / 40% 까마귀 부름 룰렛. (생존 중·쿨다운 중 제외는 룰렛 가드가 처리)

        // [K] (제거, 2026-06-13) 만료→pendingTeleport→순간이동 강제분기 폐기 — 기획 §11 §3 재해석.
        //     순간이동은 '까마귀 부름 실패'(까마귀 생존/쿨다운으로 소환 불가) 시 PickBossBaseSkill 에서 직접 선택.

        return null;
    }

    // ============================================================
    // 수확 스킬 — effectType="Harvest"
    //   기획 §11 §3 거두는 자 4번 수확:
    //     · 각 살아있는 아군에게 skill.power 데미지 (실드 우선 흡수)
    //     · 실드를 초과해 HP 에 실제 들어간 데미지 총합만큼 보스 HP 회복 (드레인)
    //     · 예: 실드 20 인 아군 (50 데미지) → 실드 20 흡수, HP 30 차감 → 보스 +30 회복
    //   TryGetForcedSkill [L] 이 HP ≤ 50% 시 1회 강제 발동.
    // ============================================================
    private IEnumerator ExecuteHarvestSkill(EnemyData caster, EnemySkillData skill)
    {
        GameLog.Formatted($"{caster.displayName}이(가) [{skill.displayName}]을(를) 사용했다!", LogCategory.Skill);
        Debug.Log($"┌─────────────────────────────────────────");
        Debug.Log($"│ [적 스킬·수확] {caster.displayName} → {skill.displayName} (각 아군 {skill.power} 데미지 + 드레인)");
        Debug.Log($"└─────────────────────────────────────────");

        // 타격 순간 데미지 + 드레인 적용 — 아군 근접과 동일 타이밍
        yield return new WaitForSeconds(meleeImpactDelay);

        int totalDrain = 0;
        foreach (var ally in allies)
        {
            if (ally == null || ally.isDead) continue;
            // 실드 흡수분은 드레인에서 제외 — 기획 명시. 오버킬 시 실제 차감 HP 까지만 흡수 (HP 5 잔여에 50 데미지면 5 흡수).
            int absorbed     = ally.shield > 0 ? Mathf.Min(ally.shield, skill.power) : 0;
            int hpLoss       = skill.power - absorbed;
            int actualHpLoss = Mathf.Min(hpLoss, ally.CurrentHp);
            totalDrain      += actualHpLoss;
            ApplyDamageToAlly(ally, skill.power);
        }

        if (totalDrain > 0)
        {
            int beforeHp = caster.CurrentHp;
            int maxHp    = caster.maxHp > 0 ? caster.maxHp : 1;
            caster.CurrentHp = Mathf.Min(maxHp, caster.CurrentHp + totalDrain);
            int healed = caster.CurrentHp - beforeHp;
            GameLog.Formatted($"{caster.displayName}이(가) {healed}의 HP를 흡수!", LogCategory.Heal);
            Debug.Log($"  └ [수확 드레인] 실드 초과 데미지 총 {totalDrain} → {caster.displayName} HP +{healed} ({beforeHp} → {caster.CurrentHp}/{maxHp})");
        }
        else
        {
            Debug.Log($"  └ [수확 드레인] 실드가 모두 흡수 — 보스 회복 없음");
        }
    }

    // ============================================================
    // 순간이동 스킬 — effectType="Teleport"
    //   기획 §11 §3 거두는 자: 파티 배치 순서를 역순으로 변경
    //   [탱커, 딜러, 서포터, 힐러] → [힐러, 서포터, 딜러, 탱커]
    //   allies 리스트 전체 reverse (사망 자리 빈칸 포함).
    // ============================================================
    private IEnumerator ExecuteTeleportSkill(EnemyData caster, EnemySkillData skill)
    {
        Debug.Log($"┌─────────────────────────────────────────");
        Debug.Log($"│ [적 스킬·순간이동] {caster.displayName} → {skill.displayName}");
        Debug.Log($"│  효과: 파티 배치 순서 역전");
        Debug.Log($"└─────────────────────────────────────────");

        // ① 연출 먼저 — 기획 §11 §3 가이드: Attack3 모션 → 비가시 → 아군 후방 잔상 → 원위치 재등장.
        //    배치 역전은 모션이 전부 끝난 뒤에 (사용자 요청 2026-06-11).
        float visualTotal = 0.6f;   // 폴백(연출 생략) 시 최소 호흡
        var bossCardSprites = FindCardSprites(caster);
        if (bossCardSprites != null)
        {
            float backX = float.MaxValue;
            foreach (var v in Object.FindObjectsByType<BattleCardView>(FindObjectsSortMode.None))
                if (v != null && v.Fellow != null && !v.Fellow.isDead)
                    backX = Mathf.Min(backX, v.transform.position.x);
            if (backX < float.MaxValue)
            {
                var p = bossCardSprites.transform.position;
                bossCardSprites.PlayTeleportGhost(new Vector3(backX - 1.6f, p.y, p.z));
                visualTotal = 2.1f;   // preDelay 0.45 + 페이드들 + 잔상 0.5 ≈ 2.0
            }
            else { bossCardSprites.PlayTeleport(); visualTotal = 0.9f; }   // 아군 전멸 등 — 기본 페이드 폴백
        }
        else
            Debug.LogWarning($"[Teleport] {caster.displayName} 의 BattleCardView 를 찾지 못해 연출 생략.");
        yield return new WaitForSeconds(visualTotal);

        // ② 모션 종료 후 배치 역전 + 카드 순차 재배치
        string before = string.Join(", ",
            allies.Select(a => !string.IsNullOrEmpty(a?.displayName) ? a.displayName : (a?.positionStack.ToString() ?? "?")));
        allies.Reverse();
        for (int i = 0; i < allies.Count; i++)
            if (allies[i] != null) allies[i].battleSlotIndex = i;
        DefaultSetting.AllyLayout?.RelayoutNow();
        string after = string.Join(", ",
            allies.Select(a => !string.IsNullOrEmpty(a?.displayName) ? a.displayName : (a?.positionStack.ToString() ?? "?")));
        GameLog.Formatted($"{caster.displayName}이(가) 진형을 뒤바꿨다!", LogCategory.Skill);
        Debug.Log($"  └ [순간이동] 배치 역전: [{before}] → [{after}]");
        yield return new WaitForSeconds(DefaultSetting.RelayoutDuration + DefaultSetting.RelayoutStagger * 3f);
    }

    /// <summary>EnemyData 에 바인딩된 BattleCardView 의 BattleCardSprites 를 반환. 없으면 null.</summary>
    private BattleCardSprites FindCardSprites(EnemyData target)
    {
        if (target == null) return null;
        var views = Object.FindObjectsByType<BattleCardView>(FindObjectsSortMode.None);
        foreach (var v in views)
            if (v != null && v.Enemy == target)
                return v.GetComponent<BattleCardSprites>();
        return null;
    }
}
