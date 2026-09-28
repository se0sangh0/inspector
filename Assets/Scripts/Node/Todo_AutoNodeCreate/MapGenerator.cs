// ============================================================
// MapGenerator.cs — 맵 생성기 (MVP 고정 일렬)
// ============================================================
//
// [이 파일이 하는 일]
//   03. 노드·용병소·보상·메타 §1-1 — 10층, 일반 구간 층당 3갈래.
//
// [구조 — 2026-08-21 (P0-02 대체안: 노드맵 3갈래 방식)]
//   본편 맵 = 10층 (layer 0~9):
//     layer 0   = 시작 1노드 (RoomType.Combat — NodeSystem 이 '현재 위치' 마커로 처리, 클릭 불가)
//     layer 1~7 = 2~8층 3갈래 — 전투 70% / 이벤트(?) 30% 추첨 (03 §1-1 초안)
//     layer 8   = 화톳불 1노드 (9층 고정 — 가짜 3택 없음)
//     layer 9   = 보스 1노드 (10층 고정 — 가짜 3택 없음)
//   이벤트(?) 노드의 실제 결과(용병소/교회/엘리트/선택지 이벤트)는
//   NodeSystem 이 진입 시 가중 추첨한다 (용병소 40/교회 20/엘리트 10/이벤트 30).
//
// [튜토리얼 맵] 4노드 일렬 (Combat→Event(랜덤: 가중치 조작으로 용병소)→Rest→Boss) — 별도 유지.
//
// [호출자]
//   NodeSystem.Awake() → mapGen.GenerateMap() → 결과를 nodeRows 에 매핑
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public const int TotalFloors = 10;
    public const int RestFloor = 9;
    public const int BossFloor = 10;

    private MapData mapData;

    // ============================================================
    // 공개 API — 맵 생성
    // ============================================================
    public MapData GenerateMap()
    {
        // 튜토리얼 모드 — 기획 §15 + 2026-05-29 결정: 5노드 일렬 (Combat→Shop→Event→Elite→Boss)
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorial)
            return GenerateTutorialMap();

        return GenerateFullRunMap();
    }

    // ── 일반 구간 노드 타입 비율 (03. 노드·용병소·보상·메타 §1-1 초안) ──
    //   전투 70% / 이벤트(?) 30%. 밸런스 테스트 후 조정 — 15 에서 관리.
    private const int CombatWeight = 70;
    private const int EventWeight  = 30;

    /// <summary>
    /// 본편 맵 — 10층, 일반 구간(2~8층)은 층당 3갈래 (03 §1-1).
    /// 시작(1층) > 3갈래 ×7 (2~8층) > 화톳불(9층 고정) > 보스(10층 고정).
    /// 3갈래 노드는 전투 70 / 이벤트 30 으로 추첨. 이벤트(?)의 실제 결과
    /// (용병소/교회/엘리트/선택지 이벤트)는 NodeSystem 이 진입 시 추첨한다.
    /// 9·10층은 단일 노드 — 가짜 3택을 제시하지 않는다 (03 §1-1).
    /// </summary>
    public MapData GenerateFullRunMap()
    {
        var layers = new List<RoomType[]> { new[] { RoomType.Combat } };
        for (int floor = 2; floor <= 8; floor++) layers.Add(RollChoiceLayer());
        layers.Add(new[] { RoomType.Rest }); // 9층
        layers.Add(new[] { RoomType.Boss }); // 10층

        mapData = BuildLayeredMap(layers);
        Debug.Log("[MapGenerator] 본편 맵 생성 — 10층 (시작 > 3갈래×7 > 화톳불 > 보스)");
        return mapData;
    }

    /// <summary>
    /// 일반 구간 한 층의 3갈래를 추첨한다 (전투 70 / 이벤트 30).
    /// 한 층이 전부 같은 타입이면 1개를 다른 타입으로 교체 —
    /// 03 §1-1 "같은 타입 3회 이상 연속 시 재시도" 규칙의 층 단위 적용.
    /// </summary>
    private static RoomType[] RollChoiceLayer()
    {
        var nodes = new RoomType[3];
        for (int i = 0; i < nodes.Length; i++)
            nodes[i] = Random.Range(0, CombatWeight + EventWeight) < CombatWeight
                ? RoomType.Combat
                : RoomType.Event;

        if (nodes[0] == nodes[1] && nodes[1] == nodes[2])
        {
            int flip = Random.Range(0, nodes.Length);
            nodes[flip] = nodes[flip] == RoomType.Combat ? RoomType.Event : RoomType.Combat;
        }
        return nodes;
    }

    /// <summary>
    /// 튜토리얼 전용 맵 — 3개 노드 일렬 (분기 없음). 사용자가 게임 전체 흐름 체험.
    /// 1층 Combat(고블린) → 2층 Event(랜덤 노드 — 본편과 동일, 가중치 조작으로 용병소 100%) → 3층 Rest(화톳불) → 화톳불 "다음 층"에서 튜토리얼 종료 (2026-06-13 QA: 보스 노드 제거)
    /// </summary>
    private MapData GenerateTutorialMap()
    {
        var sequence = new[]
        {
            RoomType.Combat, RoomType.Event, RoomType.Rest,
        };

        mapData = BuildLinearMap(sequence);
        Debug.Log("[MapGenerator] 튜토리얼 맵 생성 — 3노드 일렬 (Combat→Event(랜덤)→Rest, 화톳불에서 종료)");
        return mapData;
    }

    // ============================================================
    // 층별 다중 노드 맵 빌더 — 층마다 노드 배열, 다음 층 전체와 연결.
    // (모든 선택은 다음 층에서 다시 합류 — 분기별 장기 맵 없음)
    // ============================================================
    private MapData BuildLayeredMap(List<RoomType[]> layers)
    {
        var data = new MapData
        {
            totalLayers = layers.Count,
            maxNodesPerLayer = 1,
        };

        int nextId = 0;
        var perLayer = new List<List<RoomNode>>();
        for (int layer = 0; layer < layers.Count; layer++)
        {
            int count = layers[layer].Length;
            data.maxNodesPerLayer = Mathf.Max(data.maxNodesPerLayer, count);

            var list = new List<RoomNode>();
            for (int i = 0; i < count; i++)
            {
                var node = new RoomNode
                {
                    id          = nextId++,
                    layer       = layer,
                    roomType    = layers[layer][i],
                    nextNodeIds = new List<int>(),
                    position    = new Vector2(
                        count > 1 ? (float)i / (count - 1) : 0.5f,
                        layers.Count > 1 ? (float)layer / (layers.Count - 1) : 0.5f),
                };
                list.Add(node);
                data.nodes.Add(node);
            }
            perLayer.Add(list);
        }

        // 연결: 각 노드 → 다음 층의 모든 노드 (선 렌더링은 현재 비활성)
        for (int layer = 0; layer < perLayer.Count - 1; layer++)
            foreach (var n in perLayer[layer])
                foreach (var next in perLayer[layer + 1])
                    n.nextNodeIds.Add(next.id);

        if (data.nodes.Count > 0)
            data.nodes[0].isAccessible = true;

        return data;
    }

    // ============================================================
    // 고정 일렬 맵 빌더 — 시퀀스 길이만큼 노드 1개씩, 직선 연결. (튜토리얼용)
    // ============================================================
    private MapData BuildLinearMap(RoomType[] sequence)
    {
        int layers = sequence.Length;
        var data = new MapData
        {
            totalLayers = layers,
            maxNodesPerLayer = 1,
        };

        var nodes = new List<RoomNode>();
        for (int layer = 0; layer < layers; layer++)
        {
            var node = new RoomNode
            {
                id          = layer,
                layer       = layer,
                nextNodeIds = new List<int>(),
                roomType    = sequence[layer],
                position    = new Vector2(0.5f, layers > 1 ? (float)layer / (layers - 1) : 0.5f),
            };
            nodes.Add(node);
            data.nodes.Add(node);
        }

        // 일렬 연결 (각 노드 → 다음 노드 하나)
        for (int i = 0; i < nodes.Count - 1; i++)
            nodes[i].nextNodeIds.Add(nodes[i + 1].id);

        if (nodes.Count > 0)
            nodes[0].isAccessible = true;

        return data;
    }
}
