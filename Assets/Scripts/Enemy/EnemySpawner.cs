using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Tooltip("이 노드(전투)에 등장할 적 ID 목록. 비어있으면 tier 기반 랜덤.")]
    [SerializeField] private List<string> enemyIds = new();

    [Tooltip("랜덤 모드용 — tier")]
    [SerializeField] private EnemyTier randomTier = EnemyTier.Normal;

    [Tooltip("랜덤 모드용 — 등장 마릿수")]
    [SerializeField, Range(1, 5)] private int randomCount = 3;

    private void OnEnable()
    {
        // BattleManager 의 OnEnable 보다 먼저 실행돼야 함 (Script Execution Order 설정 권장)
        SpawnIntoBattleManager();
    }

    private void SpawnIntoBattleManager()
    {
        var bm = BattleManager.Instance;
        // 비활성 포함 검색 (FindObjectOfType<T>(bool) 은 Unity 2023+ 에서 deprecated)
        if (bm == null) bm = FindFirstObjectByType<BattleManager>(FindObjectsInactive.Include);
        if (bm == null) {
            Debug.LogError("[EnemySpawner] 씬에 BattleManager 없음.");
            return;
        }
        if (EnemyDatabase.Instance == null) {
            Debug.LogError("[EnemySpawner] EnemyDatabase 없음.");
            return;
        }

        // 튜토리얼 모드 — RoomType 별 적 1마리 고정 (기획 §15)
        //   · Elite → 약탈자 (엘리트답게 고블린보다 강한 단일 적)
        //   · 그 외(Combat 등) → 고블린 (기획 §3-2 학습용 기본 적)
        //   (2026-06-13 QA: 튜토리얼 보스 노드 제거 — Boss 분기 삭제. 본편 보스 스폰은 아래 일반 흐름에서 유지)
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorial)
        {
            string tutId = "enemy_goblin_01";
            RoomType tutRoom = RoomType.Combat;
            if (NodeSystem.Current != null)
            {
                tutRoom = NodeSystem.Current.CurrentRoomType;
                switch (tutRoom)
                {
                    case RoomType.Elite: tutId = "enemy_raider_01";   break;
                }
            }

            var def = EnemyDatabase.Instance.GetEnemy(tutId);
            if (def != null)
            {
                bm.enemies = new List<EnemyData> { EnemyDatabase.CreateRuntimeEnemy(def) };
                Debug.Log($"[EnemySpawner] 튜토리얼 모드 — {tutId} 1마리 고정 스폰 (RoomType={tutRoom})");
                return;
            }
            Debug.LogWarning($"[EnemySpawner] 튜토리얼이지만 {tutId} def 못 찾음 — 일반 흐름 진행");
        }

        // ── 층 + RoomType 기반 적 결정 (2026-05-29 갱신) ─────────
        // 우선순위:
        //   1) Inspector enemyIds 채워져있으면 → 그것 우선 (수동 강제)
        //   2) NodeSystem.CurrentRoomType 보고 분기:
        //      · Boss   → enemy_reaper_boss × 1
        //      · Elite  → 약탈자 풀 + RollCount 마릿수 (후반일수록 4 가중)
        //      · Combat → FloorTierResolver 풀 + RollCount 마릿수
        //   3) NodeSystem 없으면 → tier 기반 랜덤 폴백 (인스펙터 randomCount)
        EnemyTier tierToUse = randomTier;
        string[] enemyPool  = null;   // 적 ID 풀 (Boss 만 단일, Elite/Combat 은 풀)
        bool encounterIsResolved = false;
        int      spawnCount = 0;      // RollCount 결과 (Boss 만 1 고정)
        RoomType currentRoom = RoomType.Combat;

        if (NodeSystem.Current != null)
        {
            int floor    = NodeSystem.Current.CurrentFloor;
            currentRoom  = NodeSystem.Current.CurrentRoomType;

            switch (currentRoom)
            {
                case RoomType.Boss:
                    enemyPool  = new[] { "enemy_reaper_boss" };
                    spawnCount = 1;
                    tierToUse  = EnemyTier.Boss;
                    break;

                case RoomType.Elite:
                    // 엘리트 풀: 현재 약탈자 1종 (기획 §10 §엘리트). 마릿수는 일반 전투와 같은 RollCount 공식.
                    enemyPool  = new[] { "enemy_raider_01" };
                    spawnCount = FloorTierResolver.RollCount(floor);
                    tierToUse  = EnemyTier.Normal;
                    break;

                case RoomType.Combat:
                default:
                    enemyPool  = FloorTierResolver.RollEncounter(floor);
                    spawnCount = enemyPool != null ? enemyPool.Length : 0;
                    encounterIsResolved = enemyPool != null && enemyPool.Length > 0;
                    tierToUse  = FloorTierResolver.ResolveTier(floor);
                    break;
            }

            Debug.Log($"[EnemySpawner] 층 {floor} / RoomType={currentRoom} | 풀=[{(enemyPool == null ? "null" : string.Join(",", enemyPool))}] | count={spawnCount} | tier 폴백={tierToUse}");
        }

        // 우선순위: 1)Inspector → 2)RoomType/층 풀+RollCount → 3)tier 랜덤
        var spawned = new List<EnemyData>();
        if (enemyIds != null && enemyIds.Count > 0)
        {
            // 수동 강제 — 인스펙터 순서 그대로
            foreach (var id in enemyIds)
            {
                var def = EnemyDatabase.Instance.GetEnemy(id);
                if (def != null) spawned.Add(EnemyDatabase.CreateRuntimeEnemy(def));
            }
        }
        else if (encounterIsResolved)
        {
            // 일반 전투는 Resolver가 고른 조합을 그대로 사용한다.
            foreach (string id in enemyPool)
            {
                var def = EnemyDatabase.Instance.GetEnemy(id);
                if (def != null) spawned.Add(EnemyDatabase.CreateRuntimeEnemy(def));
            }
        }
        else if (enemyPool != null && enemyPool.Length > 0 && spawnCount > 0)
        {
            // 풀에서 spawnCount 만큼 랜덤 선택 — 같은 ID 중복 허용 (예: 고블린×3)
            for (int i = 0; i < spawnCount; i++)
            {
                string id  = enemyPool[Random.Range(0, enemyPool.Length)];
                var    def = EnemyDatabase.Instance.GetEnemy(id);
                if (def != null) spawned.Add(EnemyDatabase.CreateRuntimeEnemy(def));
            }
        }
        else
        {
            // tier 기반 랜덤 폴백 (NodeSystem 없을 때 등)
            for (int i = 0; i < randomCount; i++)
            {
                var def = EnemyDatabase.Instance.GetRandomEnemy(tierToUse);
                if (def != null) spawned.Add(EnemyDatabase.CreateRuntimeEnemy(def));
            }
        }
        bm.enemies = spawned;
        Debug.Log($"[EnemySpawner] 적 {spawned.Count}마리 주입 완료 (기존 목록 덮어씀).");
    }
}
