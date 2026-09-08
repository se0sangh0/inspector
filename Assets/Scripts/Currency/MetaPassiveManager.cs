// ============================================================
// Currency/MetaPassiveManager.cs
// 마석 메타 성장 — 영구 해금 관리 (기획 §16, 2026-06-02 풀+랜덤 배정 재설계)
// ============================================================
//
// [카테고리 A] 동료 시그니처 패시브 (전투 관여형) — 동료당 3개 풀(총 15).
//   해금(마석)하면 그 동료의 "해금된 풀" 에 들어가고, 런 시작 시 인스턴스마다
//   해금된 풀에서 무작위 1개가 activePassiveId 로 배정된다.
// [카테고리 B] 동료 시그니처 스킬 해금 (미해금 시 스킬 풀에서 제외) — 5개.
//
// 전투 훅은 user.activePassiveId == <id> 로 발동 판정한다(전역 IsUnlocked 아님).
// ============================================================

using UnityEngine;

public static class MetaPassiveManager
{
    // ── 패시브 ID (동료당 3개) ──────────────────────────────────
    public const string CasterChain   = "pasv_caster_chain";    // 비전 연쇄
    public const string CasterAmp     = "pasv_caster_amp";      // 주문 증폭
    public const string CasterExec    = "pasv_caster_exec";     // 멸절

    public const string OffenderCombo   = "pasv_offender_combo";   // 거합 집중
    public const string OffenderExecute = "pasv_offender_execute"; // 처형인
    public const string OffenderDuel    = "pasv_offender_duel";    // 일기당천

    public const string DefenderBond    = "pasv_defender_bond";    // 수호 결속
    public const string DefenderBulwark = "pasv_defender_bulwark"; // 견고한 방벽
    public const string DefenderWall    = "pasv_defender_wall";    // 불굴의 벽

    public const string AttackerFrenzy    = "pasv_attacker_frenzy";    // 배수의 진
    public const string AttackerLifesteal = "pasv_attacker_lifesteal"; // 피의 갈망
    public const string AttackerSpirit    = "pasv_attacker_spirit";    // 투혼

    public const string PriestCleanse  = "pasv_priest_cleanse";  // 정화의 빛
    public const string PriestBlessing = "pasv_priest_blessing"; // 축복받은 손길
    public const string PriestGuard    = "pasv_priest_guard";    // 수호 기도

    // ── 스킬 해금 ID ────────────────────────────────────────────
    public const string UnlockIceStorm  = "unlock_skill_ice_storm";
    public const string UnlockSkySlash  = "unlock_skill_sky_slash";
    public const string UnlockWarShield = "unlock_skill_war_shield";
    public const string UnlockWarCry    = "unlock_skill_war_cry";
    public const string UnlockPrayer    = "unlock_skill_prayer";

    // 직업당 해금 스킬 2개 구성 (기본 2 / 해금 2) — 마석 해금. 2026-06-05 사용자 요청으로 복원.
    public const string UnlockFireball     = "unlock_skill_fireball";
    public const string UnlockMoonlight    = "unlock_skill_moonlight_slash";
    public const string UnlockBattleStance = "unlock_skill_battle_stance";
    public const string UnlockIndomitable  = "unlock_skill_indomitable";
    public const string UnlockStarlight    = "unlock_skill_starlight";

    // ── 효과 상수 (기획 §16 §3) ─────────────────────────────────
    public const float CasterAmpBonus      = 0.15f; // 주문 증폭
    public const float CasterExecBonus     = 0.50f; // 멸절 추가타 비율(최저HP 적)
    public const float OffenderComboPerStack = 0.20f;
    public const int   OffenderComboMaxStack = 3;
    public const float OffenderExecThreshold = 0.30f; // 처형인 발동 HP 비율
    public const float OffenderExecBonus     = 0.50f;
    public const int   OffenderDuelMaxEnemies = 2;    // 일기당천 발동 적 수 이하
    public const float OffenderDuelBonus      = 0.30f;
    public const float DefenderBondRatio     = 0.25f; // 수호 결속 분담
    public const float DefenderBulwarkBonus  = 0.30f; // 견고한 방벽 실드 증가
    public const float DefenderWallReduce    = 0.20f; // 불굴의 벽 받는 피해 감소
    public const float AttackerFrenzyMax     = 0.50f;
    public const float AttackerLifestealRatio= 0.15f; // 피의 갈망 회복(maxHp 비율)
    public const float AttackerSpiritReduce  = 0.10f; // 투혼 파티 받는 피해 감소
    public const float PriestCleanseRatio    = 0.50f;
    public const float PriestBlessingBonus   = 0.25f; // 축복받은 손길 힐 증가
    public const float PriestGuardRatio      = 0.30f; // 수호 기도 실드 비율

    public enum Kind { Passive, Skill }

    public struct Info
    {
        public string id;
        public Kind   kind;
        public string job;
        public string name;
        public string desc;
        public int    cost;
        public string skillId;
    }

    /// <summary>상점 노출 목록 (표시 순서: 동료별 패시브 3개 → 스킬 해금).</summary>
    public static readonly Info[] All = new Info[]
    {
        // 캐스터
        new Info{ id=CasterChain, kind=Kind.Passive, job="캐스터", name="비전 연쇄", desc="매직미사일로 적 처치 시 다른 적 1연쇄", cost=80 },
        new Info{ id=CasterAmp,   kind=Kind.Passive, job="캐스터", name="주문 증폭", desc="원소술사가 주는 모든 데미지 +15%",       cost=80 },
        new Info{ id=CasterExec,  kind=Kind.Passive, job="캐스터", name="멸절",     desc="광역 데미지 시 HP 최저 적에게 +50% 추가타", cost=90 },
        // 오펜더
        new Info{ id=OffenderCombo,   kind=Kind.Passive, job="오펜더", name="거합 집중", desc="같은 적 연속 공격 +20%/스택(최대 +60%)", cost=80 },
        new Info{ id=OffenderExecute, kind=Kind.Passive, job="오펜더", name="처형인",   desc="HP 30% 이하 적 공격 시 +50%",          cost=80 },
        new Info{ id=OffenderDuel,    kind=Kind.Passive, job="오펜더", name="일기당천", desc="적 2마리 이하일 때 단일 데미지 +30%",   cost=80 },
        // 디펜더
        new Info{ id=DefenderBond,    kind=Kind.Passive, job="디펜더", name="수호 결속",   desc="실드 보유 중 다른 아군 피해의 25% 분담", cost=100 },
        new Info{ id=DefenderBulwark, kind=Kind.Passive, job="디펜더", name="견고한 방벽", desc="봉쇄병이 부여하는 실드 +30%",          cost=80 },
        new Info{ id=DefenderWall,    kind=Kind.Passive, job="디펜더", name="불굴의 벽",   desc="봉쇄병 자신이 받는 피해 -20%",         cost=80 },
        // 어택커
        new Info{ id=AttackerFrenzy,    kind=Kind.Passive, job="어택커", name="배수의 진", desc="HP 낮을수록 주는 피해 증가(최대 +50%)", cost=90 },
        new Info{ id=AttackerLifesteal, kind=Kind.Passive, job="어택커", name="피의 갈망", desc="적 처치 시 자기 HP 15% 회복",          cost=80 },
        new Info{ id=AttackerSpirit,    kind=Kind.Passive, job="어택커", name="투혼",     desc="돌격병 생존 중 모든 아군 받는 피해 -10%", cost=90 },
        // 프리스트
        new Info{ id=PriestCleanse,  kind=Kind.Passive, job="프리스트", name="정화의 빛",   desc="힐 시 대상 스트레스를 힐량의 50% 감소", cost=80 },
        new Info{ id=PriestBlessing, kind=Kind.Passive, job="프리스트", name="축복받은 손길", desc="치유사 힐량 +25%",                cost=80 },
        new Info{ id=PriestGuard,    kind=Kind.Passive, job="프리스트", name="수호 기도",   desc="힐 대상에게 힐량의 30%만큼 실드 부여",  cost=80 },
        // 스킬 해금
        new Info{ id=UnlockIceStorm,  kind=Kind.Skill, job="캐스터",  name="아이스스톰 해금", desc="6코 광역 55 (스킬 풀에 추가)",      cost=60, skillId="skill_ice_storm" },
        new Info{ id=UnlockSkySlash,  kind=Kind.Skill, job="오펜더",  name="하늘가르기 해금", desc="10코 단일 75 (스킬 풀에 추가)",     cost=60, skillId="skill_sky_slash" },
        new Info{ id=UnlockWarShield, kind=Kind.Skill, job="디펜더",  name="전장의 방패 해금", desc="6코 데미지+전체실드 (스킬 풀에 추가)", cost=60, skillId="skill_war_shield" },
        new Info{ id=UnlockWarCry,    kind=Kind.Skill, job="어택커",  name="워크라이 해금",   desc="5코 데미지+도발 (스킬 풀에 추가)",   cost=60, skillId="skill_war_cry" },
        new Info{ id=UnlockPrayer,    kind=Kind.Skill, job="프리스트", name="기원 해금",       desc="5코 전체힐 40 (스킬 풀에 추가)",     cost=60, skillId="skill_prayer" },
        // 직업당 해금 스킬 2개째 (기본 2 / 해금 2) — 2026-06-05 복원
        new Info{ id=UnlockFireball,     kind=Kind.Skill, job="캐스터",  name="파이어볼 해금",   desc="3코 광역 35 (스킬 풀에 추가)",    cost=60, skillId="skill_fireball" },
        new Info{ id=UnlockMoonlight,    kind=Kind.Skill, job="오펜더",  name="월광베기 해금",   desc="7코 단일 60 (스킬 풀에 추가)",    cost=60, skillId="skill_moonlight_slash" },
        new Info{ id=UnlockBattleStance, kind=Kind.Skill, job="디펜더",  name="전투 태세 해금",  desc="5코 전체실드 40 (스킬 풀에 추가)", cost=60, skillId="skill_battle_stance" },
        new Info{ id=UnlockIndomitable,  kind=Kind.Skill, job="어택커",  name="불굴 해금",       desc="4코 전체힐 34 (스킬 풀에 추가)",   cost=60, skillId="skill_indomitable" },
        new Info{ id=UnlockStarlight,    kind=Kind.Skill, job="프리스트", name="별부름 해금",     desc="3코 단일힐 35 (스킬 풀에 추가)",   cost=60, skillId="skill_starlight" },
    };

    private static readonly System.Collections.Generic.Dictionary<string,string> _skillUnlockKey =
        new System.Collections.Generic.Dictionary<string,string>
    {
        { "skill_ice_storm",  UnlockIceStorm  },
        { "skill_sky_slash",  UnlockSkySlash  },
        { "skill_war_shield", UnlockWarShield },
        { "skill_war_cry",    UnlockWarCry    },
        { "skill_prayer",     UnlockPrayer    },
        { "skill_fireball",        UnlockFireball     },
        { "skill_moonlight_slash", UnlockMoonlight    },
        { "skill_battle_stance",   UnlockBattleStance },
        { "skill_indomitable",     UnlockIndomitable  },
        { "skill_starlight",       UnlockStarlight    },
    };

    // ── 해금 상태 ───────────────────────────────────────────────
    public static bool IsUnlocked(string id) => PlayerPrefs.GetInt(id, 0) == 1;

    public static void Unlock(string id)
    {
        PlayerPrefs.SetInt(id, 1);
        PlayerPrefs.Save();
        Debug.Log($"[MetaPassive] 해금: {id}");
    }

    public static int CostOf(string id)
    {
        foreach (var i in All) if (i.id == id) return i.cost;
        return 0;
    }

    // ── 누진 할증 (2026-06-13, QA) ──────────────────────────────
    // 해금할수록 남은 항목 가격이 오른다: 유효가 = 기본가 × (1 + 해금수 × 10%).
    // "활성화 갯수" = 현재 해금된 항목 전체 수(패시브+스킬 통합).
    public const float SurchargePerUnlock = 0.10f;

    /// <summary>현재까지 해금된 항목 수(패시브+스킬 전체).</summary>
    public static int UnlockedCount
    {
        get
        {
            int n = 0;
            foreach (var i in All) if (IsUnlocked(i.id)) n++;
            return n;
        }
    }

    /// <summary>누진 할증이 반영된 유효 해금가 = 기본가 × (1 + 해금수 × 10%).</summary>
    public static int EffectiveCostOf(string id)
    {
        return Mathf.RoundToInt(CostOf(id) * (1f + UnlockedCount * SurchargePerUnlock));
    }

    public static bool TryUnlock(string id)
    {
        // 해금 비용 = 마석(ManaStone) — 영구 메타 재화. 기획 §16. (2026-06-08: 영혼석→마석 정정)
        // 마석 상점 패널이 마석 잔액을 표시/검사하므로 차감도 마석으로 일치시킨다.
        // 2026-06-13(QA): 정액 CostOf → 누진 EffectiveCostOf 로 차감(구매할수록 비싸짐).
        if (IsUnlocked(id)) return false;
        int cost = EffectiveCostOf(id);
        if (ManastoneManager.Instance == null || ManastoneManager.Instance.Amount < cost) return false;
        ManastoneManager.Instance.Use(cost);
        Unlock(id);
        return true;
    }

    public static void ResetAll()
    {
        foreach (var i in All) PlayerPrefs.DeleteKey(i.id);
        PlayerPrefs.Save();
        Debug.Log("[MetaPassive] 전체 해금 초기화");
    }

    /// <summary>스킬 사용 가능 여부 — 시그니처 스킬은 해금돼야 true, 일반 스킬은 항상 true.</summary>
    public static bool IsSkillUnlocked(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return true;
        if (_skillUnlockKey.TryGetValue(skillId, out var key)) return IsUnlocked(key);
        return true;
    }

    /// <summary>이름 조회 (카드 표시용). 없으면 빈 문자열.</summary>
    public static string NameOf(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        foreach (var i in All) if (i.id == id) return i.name;
        return "";
    }

    /// <summary>
    /// 런 시작 패시브 배정 — 해당 직업의 해금된 패시브 풀에서 무작위 1개. 없으면 null.
    /// </summary>
    public static string RollPassive(string jobClass)
    {
        var pool = new System.Collections.Generic.List<string>();
        foreach (var i in All)
            if (i.kind == Kind.Passive && i.job == jobClass && IsUnlocked(i.id))
                pool.Add(i.id);
        if (pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }
}
