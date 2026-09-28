// ============================================================
// Node/FloorTierResolver.cs
// 일반 전투 층별 적 조합과 tier 폴백
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public static class FloorTierResolver
{
    private sealed class EncounterOption
    {
        public readonly string[] ids;
        public readonly int weight;
        public EncounterOption(int weight, params string[] ids) { this.weight = weight; this.ids = ids; }
        public string Key => string.Join("+", ids);
    }

    private static string _lastEncounterKey;

    public static void ResetRun() => _lastEncounterKey = null;

    /// <summary>1-base 일반 전투 층의 적 조합을 가중 추첨한다.</summary>
    public static string[] RollEncounter(int floor)
    {
        var options = BuildOptions(floor);
        if (options == null || options.Count == 0) return null;

        var candidates = new List<EncounterOption>(options.Count);
        foreach (var option in options)
            if (options.Count == 1 || option.Key != _lastEncounterKey)
                candidates.Add(option);

        int total = 0;
        foreach (var option in candidates) total += option.weight;
        if (total <= 0) return null;

        int roll = Random.Range(0, total);
        EncounterOption picked = candidates[candidates.Count - 1];
        int cursor = 0;
        foreach (var option in candidates)
        {
            cursor += option.weight;
            if (roll < cursor) { picked = option; break; }
        }

        _lastEncounterKey = picked.Key;
        return (string[])picked.ids.Clone();
    }

    public static EnemyTier ResolveTier(int floor) => floor >= 5 ? EnemyTier.Normal : EnemyTier.Weak;

    /// <summary>기존 호출부 호환용 ID 풀. 실제 일반 전투는 RollEncounter를 사용한다.</summary>
    public static string[] GetEnemyPool(int floor)
    {
        var options = BuildOptions(floor);
        if (options == null) return null;
        var ids = new List<string>();
        foreach (var option in options)
            foreach (var id in option.ids)
                if (!ids.Contains(id)) ids.Add(id);
        return ids.ToArray();
    }

    public static int RollCount(int floor)
    {
        // 일반 전투는 RollEncounter의 조합을 사용한다. 이 경로는 기존 엘리트용이다.
        if (floor <= 3) return 2;
        return Random.Range(3, 5);
    }

    private static List<EncounterOption> BuildOptions(int floor)
    {
        if (floor >= 2 && floor <= 4)
            return new List<EncounterOption>
            {
                new EncounterOption(40, "enemy_goblin_01", "enemy_goblin_01"),
                new EncounterOption(30, "enemy_goblin_01", "enemy_goblin_01", "enemy_goblin_01"),
                new EncounterOption(30, "enemy_goblin_01", "enemy_raider_01"),
            };
        if (floor >= 5 && floor <= 8)
            return new List<EncounterOption>
            {
                new EncounterOption(35, "enemy_goblin_01", "enemy_goblin_01", "enemy_raider_01"),
                new EncounterOption(25, "enemy_raider_01", "enemy_raider_01"),
                new EncounterOption(25, "enemy_goblin_01", "enemy_goblin_01", "enemy_goblin_01", "enemy_raider_01"),
                new EncounterOption(15, "enemy_raider_01", "enemy_raider_01", "enemy_goblin_01"),
            };
        return null;
    }
}
