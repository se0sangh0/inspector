// ============================================================
// Currency/SoulstoneDropPool.cs
// 영혼석 드롭 오브젝트(SoulstoneDropFx) 풀 관리 싱글톤.
// 기획 §15 — Object Pooling 적용 권장.
// ============================================================
//
// [공개 API]
//   SoulstoneDropPool.Instance?.SpawnAt(worldPos, amount)
//     - 풀에서 비활성 인스턴스를 꺼내(없으면 신규 생성) 연출 시작.
//     - 영혼석을 먼저 지급하고 수거 연출을 시작한다.
//
// [폴백 안전망]
//   prefab 또는 target 가 미연결이면 즉시 SoulstoneManager.Add 만 호출 — 게임 안 깨짐.
//
// [씬 배치]
//   GamePlayScene 에 빈 GameObject "SoulstoneDropPool" 만들고 컴포넌트 부착.
//   인스펙터에서 prefab 과 target(LeftPanel 의 영혼석 아이콘 RectTransform) 연결.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public class SoulstoneDropPool : MonoBehaviour
{
    public static SoulstoneDropPool Instance { get; private set; }

    [Header("연출 prefab — SoulstoneDropFx 컴포넌트 부착 필요")]
    [SerializeField] private SoulstoneDropFx prefab;

    [Header("도착 지점 — UI 영혼석 아이콘 RectTransform")]
    [Tooltip("LeftPanel 의 Item_SoulStone 위치. 비어있으면 즉시 Add 폴백.")]
    [SerializeField] private Transform target;

    [Header("풀 옵션")]
    [Tooltip("초기 prewarm 수 (0 이면 lazy)")]
    [SerializeField] private int prewarmCount = 4;

    private readonly List<SoulstoneDropFx> _pool = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < prewarmCount; i++) CreateInstance();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>외부에서 target 을 동적으로 설정. 씬 재구성 시 사용.</summary>
    public void SetTarget(Transform t) => target = t;

    /// <summary>
    /// 풀에서 인스턴스를 꺼내 worldPos 에서 시작해 target 으로 빨려들어가게 한다.
    /// amount 는 연출 시작 전에 SoulstoneManager 에 가산된다 (16-B §3).
    /// prefab 또는 target 가 null 이면 즉시 Add 만 수행 (폴백).
    /// </summary>
    public void SpawnAt(Vector3 worldPos, int amount)
    {
        if (amount <= 0) return;
        // 기록·보고서·씬 전환은 수거 연출의 완료를 기다리지 않는다.
        SoulstoneManager.Instance?.Add(amount);
        if (prefab == null || target == null) return;

        var fx = GetFromPool();
        fx.gameObject.SetActive(true);
        fx.Play(worldPos, target, null);
    }

    private SoulstoneDropFx GetFromPool()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            var fx = _pool[i];
            if (fx != null && !fx.gameObject.activeInHierarchy) return fx;
        }
        return CreateInstance();
    }

    private SoulstoneDropFx CreateInstance()
    {
        if (prefab == null) return null;
        var fx = Instantiate(prefab, transform);
        fx.gameObject.SetActive(false);
        _pool.Add(fx);
        return fx;
    }
}
