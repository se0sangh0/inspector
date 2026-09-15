// ============================================================
// Node/NodeSystem.cs
// 노드 맵 UI 시스템 — 자동 생성 + 타입별 분기
// ============================================================
//
// [이 파일이 하는 일]
//   로그라이크 게임의 노드 맵 화면을 관리합니다.
//   Awake 시 MapGenerator 로부터 자동 생성된 MapData 를 받아
//   인스펙터에 사전 배치된 nodeRows 의 버튼들에 RoomType 을 매핑하고,
//   클릭 시 타입별로 다른 화면(전투/화툿불/용병소/...)으로 분기합니다.
//
// [노드 클릭 → 분기 흐름]
//   1. 버튼 클릭 → OnNodeClicked(row, col)
//   2. 현재 층의 버튼이면 → 선택 + currentRowIndex++
//   3. 클릭된 노드의 RoomType 보고 분기:
//      - Combat/Elite/Boss → 전투 패널 (DisplayChanger 호출)
//      - Rest(화툿불)      → TODO 자리 (다음 사이클 E 작업)
//      - Shop(용병소)      → TODO 자리 (다음 사이클 F 작업)
//      - Event(교회)       → TODO 백로그
//   4. UpdateNodeStates() 로 버튼 색상 업데이트
//
// [인스펙터 설정]
//   - mapGenerator   : 같은 GameObject 또는 자식에 있는 MapGenerator 참조 (없으면 자동 검색)
//   - nodeRows       : 각 층의 부모 오브젝트 + 버튼들 (사전 배치 유지)
//   - nodeDisplay    : 노드 맵 화면 (전환 토글용)
//   - actionDisplay  : 전투/행동 화면 (전환 토글용)
//   - passedState / currentState / lockedState : 버튼 시각 상태
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 노드 맵 UI 시스템. 자동 생성된 RoomType 을 버튼에 매핑하고 클릭 시 타입별 분기.
/// </summary>
public class NodeSystem : MonoBehaviour
{
    // ----------------------------------------------------------
    // [NodeRow] — 한 층(Row)의 정보를 담는 내부 클래스
    // ----------------------------------------------------------
    [System.Serializable]
    public class NodeRow
    {
        [Tooltip("이 층의 부모 오브젝트를 드래그하여 연결하세요.")]
        public GameObject rowParent;

        [HideInInspector] public List<Button> buttons = new();
        [HideInInspector] public int selectedButtonIndex = -1;

        // 각 버튼에 매핑된 RoomType (MapGenerator 결과). buttons 와 인덱스 1:1.
        [HideInInspector] public List<RoomType> roomTypes = new();

        // `?`(Event) 노드가 진입 후 공개한 실제 결과. buttons 와 인덱스 1:1.
        // None = 미공개(`?` 유지). 공개되면 아이콘·색을 결과로 교체해 이력을 남긴다.
        [HideInInspector] public List<EncounterKind> encounterKinds = new();
    }

    // ----------------------------------------------------------
    // [자동 맵 생성기]
    // ----------------------------------------------------------
    [Header("자동 맵 생성 (Auto Map)")]
    [SerializeField]
    [Tooltip("MapGenerator 컴포넌트 참조. 비어있으면 같은 GameObject + 자식에서 자동 검색.")]
    private MapGenerator mapGenerator;

    private MapData generatedMap;

    // ----------------------------------------------------------
    // [노드 구조 설정]
    // ----------------------------------------------------------
    [Header("노드 구조 (Node Structure)")]
    [SerializeField]
    [Tooltip("노드 맵의 각 층. 순서대로 배치하세요. (10층 기준)")]
    private List<NodeRow> nodeRows;

    /// <summary>현재 진행 중인 층 인덱스 (0-base)</summary>
    private int currentRowIndex = 0;

    // ── 외부 노출 (EnemySpawner 의 층 기반 적 등장 결정용) ──
    public static NodeSystem Current { get; private set; }
    public int CurrentFloor => currentRowIndex;

    /// <summary>현재 노드의 RoomType — EnemySpawner 등이 노드 타입 기반 결정에 사용.</summary>
    public RoomType CurrentRoomType { get; private set; } = RoomType.Combat;

    /// <summary>가장 최근 선택한 노드의 위치 번호 — 왼쪽 1 / 중앙 2 / 오른쪽 3.
    /// 조사관 수첩 헤더 '제 N구역'에 사용 (기획자: 노드 선택 위치를 숫자로 변환).</summary>
    public int CurrentNodeNumber { get; private set; } = 1;

    // ----------------------------------------------------------
    // [선 렌더링 — 레거시 슬롯 (미사용)]
    // 연결선은 아래 [노드 연결선] 이 UI Image 로 런타임 생성한다.
    // LineRenderer 프리팹 방식은 월드 스페이스 전용이라 사용하지 않는다.
    // ----------------------------------------------------------
    [Header("선 설정 (Line Settings)")]
    [SerializeField]
    [Tooltip("(레거시 — 미사용) LineRenderer 컴포넌트가 붙은 선 프리팹")]
    private GameObject linePrefab;

    [SerializeField]
    [Tooltip("(레거시 — 미사용) 생성된 선들을 모아둘 부모 오브젝트")]
    private Transform lineParent;

    // ----------------------------------------------------------
    // [노드 연결선] — 층 사이 진행 가능 경로 시각화 (2026-08-21)
    //   진행 가능한 경우의 수가 있는 모든 노드 쌍(현재 층 → 다음 층)을 연결한다.
    //   같은 층 노드끼리는 연결하지 않는다.
    //   상태 표현은 노드 진행 규칙을 따른다:
    //     지나온 경로 = 초록(BorderPassed) × passedAlpha
    //     현재 선택 가능 = 금색(BorderCurrent) × currentAlpha
    //     나머지 = 밝은 회색 × lockedAlpha
    //   스타일: 밝은 회색 기본, 검은 테두리(Outline).
    // ----------------------------------------------------------
    [Header("노드 연결선 (Node Links)")]
    [Tooltip("연결선 굵기(px). 미관에 따라 조정.")]
    [SerializeField] private float lineThickness = 5f;

    private class NodeLink
    {
        public int fromRow, fromCol, toRow, toCol;
        public Image   img;
        public Outline outline;
    }
    private readonly List<NodeLink> _links = new();
    private RectTransform _lineLayer;

    private static readonly Color LineBaseColor    = new Color(0.80f, 0.80f, 0.82f, 1f); // 밝은 회색
    private static readonly Color LineOutlineColor = Color.black;

    // ----------------------------------------------------------
    // [버튼 시각 상태]
    // ----------------------------------------------------------
    [System.Serializable]
    public struct NodeVisualState
    {
        [Tooltip("버튼 색상")]
        public Color color;

        [Tooltip("버튼 스프라이트 (없으면 색상만 적용)")]
        public Sprite sprite;
    }

    [Header("시각 상태 (Visual Settings)")]
    [SerializeField] [Tooltip("지나간 층 — 선택한 버튼 색상 (호환 유지, 현재 미사용)")]
    private NodeVisualState passedState;

    [SerializeField] [Tooltip("현재 층 — 클릭 가능한 버튼 색상 (호환 유지, 현재 미사용)")]
    private NodeVisualState currentState;

    [SerializeField] [Tooltip("잠긴 층 — 클릭 불가 버튼 색상 (호환 유지, 현재 미사용)")]
    private NodeVisualState lockedState;

    // ── RoomType 별 색상 (배틀 흰색 / 용병소 파랑 / 화툿불 빨강 / 보스 보라) ──
    [Header("RoomType 색상")]
    [SerializeField] private Color combatColor = Color.white;
    [SerializeField] private Color eliteColor  = Color.white;
    [SerializeField] private Color shopColor   = new Color(0.24f, 0.55f, 1.00f); // 파랑
    [SerializeField] private Color restColor   = new Color(1.00f, 0.30f, 0.30f); // 빨강
    [SerializeField] private Color bossColor   = new Color(0.62f, 0.30f, 1.00f); // 보라
    [SerializeField] private Color eventColor  = new Color(0.85f, 0.85f, 0.85f); // 회색
    [SerializeField] private Color churchColor = new Color(0.93f, 0.88f, 0.66f); // 미색 — `?` 공개 후 교회 표시용

    [Header("진행 상태별 알파 (RoomType 색상에 곱해짐)")]
    [SerializeField, Range(0f, 1f)] private float currentAlpha = 1.00f;
    [SerializeField, Range(0f, 1f)] private float passedAlpha  = 0.85f;
    [SerializeField, Range(0f, 1f)] private float lockedAlpha  = 0.35f;

    // ----------------------------------------------------------
    // [화면 전환 참조]
    // ----------------------------------------------------------
    [Header("화면 전환 (Display)")]
    [SerializeField] public GameObject[] nodeDisplay;
    [SerializeField] public GameObject[] actionDisplay;

    [Header("용병소 (Shop 노드 — 선택)")]
    [Tooltip("Shop(용병소) 노드 클릭 시 열릴 메인 패널. 비어있으면 TODO 로그만 출력하고 다음 층 진행.")]
    [SerializeField] private MercenaryOfficePanel mercenaryOfficePanel;

    [Header("화툿불 (Rest 노드 — 선택)")]
    [Tooltip("Rest(화툿불) 노드 클릭 시 열릴 패널. MVP 고정맵 layer4. 비어있으면 회복 없이 다음 층 진행.")]
    [SerializeField] private RestPanel restPanel;

    [Header("교회 (Event 랜덤노드 결과 중 하나)")]
    [Tooltip("Event 노드(용병소/교회/엘리트 랜덤) 결과가 교회일 때 열릴 패널. 비어있으면 안내 로그만 출력하고 다음 층 진행.")]
    [SerializeField] private ChurchPanel churchPanel;

    // 선택지 이벤트 팝업 (기획 §06_이벤트_노드) — `?`(Event) 노드 클릭 시 런타임 생성/재사용.
    // 별도 프리팹/인스펙터 연결 없이 EventPanel.CreateUnder 로 캔버스 아래에 즉석 생성한다.
    private EventPanel _eventPanel;

    // ----------------------------------------------------------
    // 초기화
    // ----------------------------------------------------------
    void Awake()
    {
        Current = this;

        // 1) MapGenerator 자동 검색 (필요 시)
        //    Unity Object 는 fake-null 이라 ?? 연쇄가 의도대로 안 동작 → 명시적 단계 체크
        if (mapGenerator == null) mapGenerator = GetComponent<MapGenerator>();
        if (mapGenerator == null) mapGenerator = GetComponentInChildren<MapGenerator>(true);
        if (mapGenerator == null) mapGenerator = GetComponentInParent<MapGenerator>();
        if (mapGenerator == null) mapGenerator = FindFirstObjectByType<MapGenerator>(FindObjectsInactive.Include); // 씬 전체 폴백 (비활성 포함)

        // 2) 자동 맵 생성 + 버튼에 RoomType 매핑
        GenerateAndAssignRoomTypes();

        // 3) 기존 버튼 자동 등록 + 클릭 이벤트 연결
        SetupNodeData();
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Start()
    {
        // 시작(초록) 노드는 '현재 위치' 마커 — 클릭 대상이 아니다. (2026-06-07)
        // 첫 클릭 가능 층을 layer 1(전투)로 두고, 노드 화면 진입 시 위치 안내 토스트를 1회 표시.
        // (튜토리얼 맵은 layer 0 이 실제 전투이므로 제외.)
        bool isTutorial = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorial;
        if (!isTutorial && currentRowIndex == 0) currentRowIndex = 1;

        UpdateNodeStates();
        if (!isTutorial) ShowLocationToast("현재 위치는 여기입니다");
        AudioManager.Instance?.PlayBgmById(BgmId.NodeMap);
        // 튜토리얼 첫 노드맵 진입 시 인트로 모달 (1회만)
        TutorialManager.Instance?.TryShowDialogue(TutorialManager.DialogueId.NodeMapIntro);

        // 조사관 수첩 열기 버튼 — 노드맵 상시 접근 (P0-05, 16-A §5). 본편 런에서만.
        // 노드 화면(nodeDisplay)에 종속시켜 전투 화면 전환 시 함께 숨는다.
        if (!isTutorial)
        {
            Transform notebookHost = (nodeDisplay != null && nodeDisplay.Length > 0 && nodeDisplay[0] != null)
                ? nodeDisplay[0].transform : ResolveUiCanvas();
            InvestigatorNotebookController.EnsureOpenButton(notebookHost);
        }

        // 노드 연결선 — 레이아웃 그룹이 버튼 위치를 확정한 뒤 생성
        if (isActiveAndEnabled) StartCoroutine(BuildNodeLinksAfterLayout());
    }

    // ----------------------------------------------------------
    // 노드 연결선 — 생성/상태 갱신
    // ----------------------------------------------------------

    /// <summary>레이아웃 1프레임 대기 후 연결선 생성 (버튼 위치 확정 필요).</summary>
    private IEnumerator BuildNodeLinksAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        BuildNodeLinks();
        UpdateLineStates();
    }

    /// <summary>
    /// 활성 층 사이의 모든 노드 쌍을 선으로 연결한다 (같은 층 연결 없음).
    /// 선 레이어는 노드 행들의 공통 부모(ScrollRect content) 맨 뒤에 놓여 노드 아래에 그려진다.
    /// </summary>
    private void BuildNodeLinks()
    {
        if (nodeRows == null || nodeRows.Count == 0 || nodeRows[0].rowParent == null) return;
        var content = nodeRows[0].rowParent.transform.parent as RectTransform;
        if (content == null) return;

        if (_lineLayer == null)
        {
            var go = new GameObject("NodeLinkLayer", typeof(RectTransform));
            go.transform.SetParent(content, false);
            go.transform.SetAsFirstSibling(); // 노드 행들보다 먼저 렌더 = 뒤에 깔림
            go.layer = content.gameObject.layer;
            _lineLayer = (RectTransform)go.transform;
            _lineLayer.anchorMin = Vector2.zero;
            _lineLayer.anchorMax = Vector2.one;
            _lineLayer.offsetMin = Vector2.zero;
            _lineLayer.offsetMax = Vector2.zero;
            // content 에 LayoutGroup 이 있어도 선 레이어는 배치 대상에서 제외
            var le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        foreach (var l in _links)
            if (l.img != null) Destroy(l.img.gameObject);
        _links.Clear();

        for (int r = 0; r < nodeRows.Count - 1; r++)
        {
            if (!IsRowActive(r) || !IsRowActive(r + 1)) continue;
            var fromBtns = ActiveButtonsOf(r);
            var toBtns   = ActiveButtonsOf(r + 1);
            foreach (var (fromBtn, fromCol) in fromBtns)
                foreach (var (toBtn, toCol) in toBtns)
                    _links.Add(CreateLink(r, fromCol, fromBtn, r + 1, toCol, toBtn));
        }

        Debug.Log($"[NodeSystem] 노드 연결선 {_links.Count}개 생성");
    }

    private bool IsRowActive(int row)
        => row >= 0 && row < nodeRows.Count
        && nodeRows[row].rowParent != null && nodeRows[row].rowParent.activeSelf;

    /// <summary>해당 층의 활성 버튼 목록 (잉여 숨김 버튼 제외).</summary>
    private List<(Button btn, int col)> ActiveButtonsOf(int row)
    {
        var result = new List<(Button, int)>();
        var btns = nodeRows[row].buttons;
        for (int b = 0; b < btns.Count; b++)
            if (btns[b] != null && btns[b].gameObject.activeSelf)
                result.Add((btns[b], b));
        return result;
    }

    /// <summary>두 노드 버튼 중심을 잇는 UI 선 1개 생성 — 밝은 회색 + 검은 테두리.</summary>
    private NodeLink CreateLink(int fromRow, int fromCol, Button from, int toRow, int toCol, Button to)
    {
        var go = new GameObject($"Link_{fromRow}.{fromCol}-{toRow}.{toCol}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_lineLayer, false);
        go.layer = _lineLayer.gameObject.layer;

        Vector2 a   = LocalPosOf((RectTransform)from.transform);
        Vector2 b   = LocalPosOf((RectTransform)to.transform);
        Vector2 dir = b - a;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.sizeDelta        = new Vector2(dir.magnitude, lineThickness);
        rt.localRotation    = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        var img = go.GetComponent<Image>();
        img.color = LineBaseColor;
        img.raycastTarget = false; // 노드 클릭 방해 금지

        var ol = go.AddComponent<Outline>();
        ol.effectColor     = LineOutlineColor;
        ol.effectDistance  = new Vector2(1.5f, 1.5f);
        ol.useGraphicAlpha = false;

        return new NodeLink { fromRow = fromRow, fromCol = fromCol, toRow = toRow, toCol = toCol, img = img, outline = ol };
    }

    /// <summary>버튼 중심의 선 레이어 로컬 좌표. 선 레이어는 content 전체를 덮으므로 (0,0)=중앙.</summary>
    private Vector2 LocalPosOf(RectTransform target)
        => _lineLayer.InverseTransformPoint(target.TransformPoint(target.rect.center));

    /// <summary>
    /// 연결선 상태 갱신 — 노드 진행 상태 규칙(색상·알파)을 따른다.
    /// 지나온 경로(선택 노드 → 선택 노드) = 초록 / 현재 선택 가능 = 금색 / 나머지 = 회색 흐림.
    /// </summary>
    private void UpdateLineStates()
    {
        if (_links.Count == 0) return;
        bool startIsMarker = !(TutorialManager.Instance != null && TutorialManager.Instance.IsTutorial);

        foreach (var link in _links)
        {
            bool fromSelected = SelectedColOf(link.fromRow, startIsMarker) == link.fromCol;
            bool toSelected   = SelectedColOf(link.toRow, startIsMarker) == link.toCol;

            bool traveled  = link.toRow < currentRowIndex && fromSelected && toSelected;
            bool available = link.toRow == currentRowIndex && link.fromRow == currentRowIndex - 1 && fromSelected;

            Color tint;
            float alpha;
            if (traveled)       { tint = BorderPassed;  alpha = passedAlpha;  }
            else if (available) { tint = BorderCurrent; alpha = currentAlpha; }
            else                { tint = LineBaseColor; alpha = lockedAlpha;  }

            if (link.img != null)
                link.img.color = new Color(tint.r, tint.g, tint.b, alpha);
            if (link.outline != null)
                link.outline.effectColor = new Color(LineOutlineColor.r, LineOutlineColor.g, LineOutlineColor.b, alpha);
        }
    }

    /// <summary>해당 층에서 실제 선택(통과)한 노드 열. 아직 선택하지 않은 층은 -1. 시작 마커 층은 0.</summary>
    private int SelectedColOf(int row, bool startIsMarker)
    {
        if (row == 0 && startIsMarker) return 0;
        if (row < 0 || row >= nodeRows.Count) return -1;
        return row < currentRowIndex ? nodeRows[row].selectedButtonIndex : -1;
    }

    // ----------------------------------------------------------
    // 자동 맵 생성 → 버튼별 RoomType 매핑
    // ----------------------------------------------------------
    private void GenerateAndAssignRoomTypes()
    {
        if (mapGenerator == null)
        {
            Debug.LogWarning("[NodeSystem] MapGenerator 없음 — 모든 노드 RoomType=Combat 으로 폴백.");
            ApplyFallbackAllCombat();
            return;
        }

        generatedMap = mapGenerator.GenerateMap();
        if (generatedMap == null || generatedMap.nodes.Count == 0)
        {
            Debug.LogWarning("[NodeSystem] MapGenerator 결과 비어있음 — Combat 폴백.");
            ApplyFallbackAllCombat();
            return;
        }

        // layer 별 노드 그룹화
        var byLayer = new Dictionary<int, List<RoomNode>>();
        foreach (var n in generatedMap.nodes)
        {
            if (!byLayer.TryGetValue(n.layer, out var list))
            {
                list = new List<RoomNode>();
                byLayer[n.layer] = list;
            }
            list.Add(n);
        }

        // nodeRows 와 layer 매핑
        for (int r = 0; r < nodeRows.Count; r++)
        {
            // rowParent 아래 Button 들을 미리 모음 (SetupNodeData 도 같은 일을 하므로 중복 안전)
            var row = nodeRows[r];
            row.roomTypes.Clear();
            row.encounterKinds.Clear();

            if (row.rowParent == null) continue;

            var btns = row.rowParent.GetComponentsInChildren<Button>(true);

            byLayer.TryGetValue(r, out var layerNodes);
            int layerCount = layerNodes?.Count ?? 0;

            for (int b = 0; b < btns.Length; b++)
            {
                // layerNodes 가 부족하면 첫 번째 노드 타입으로 폴백 (또는 Combat)
                RoomType type;
                if (layerNodes != null && b < layerCount) type = layerNodes[b].roomType;
                else if (layerNodes != null && layerCount > 0) type = layerNodes[0].roomType;
                else type = RoomType.Combat;

                row.roomTypes.Add(type);
                row.encounterKinds.Add(EncounterKind.None); // 진입 전 미공개
            }

            // 단일 노드 층(시작/화톳불/보스)인데 씬 버튼이 더 많으면 잉여 버튼 숨김 → 선택지 1개 (2026-06-08).
            if (layerCount > 0 && btns.Length > layerCount)
            {
                for (int b = layerCount; b < btns.Length; b++)
                    if (btns[b] != null) btns[b].gameObject.SetActive(false);
            }

            // 버튼 수 < 자동 생성 노드 수 면 데이터가 잘리고, 반대면 폴백 — 경고
            if (btns.Length != layerCount)
            {
                Debug.LogWarning($"[NodeSystem] 층 {r}: 인스펙터 버튼 {btns.Length}개 vs 자동 생성 노드 {layerCount}개 — 매핑 best-effort 처리.");
            }
        }

        // 생성된 층 수(7층)를 권위로 — 초과 행(7~9)은 통째로 숨겨 시퀀스가 보스에서 끝나게 (2026-06-08).
        for (int r = generatedMap.totalLayers; r < nodeRows.Count; r++)
        {
            if (nodeRows[r] != null && nodeRows[r].rowParent != null)
                nodeRows[r].rowParent.SetActive(false);
        }

        Debug.Log($"[NodeSystem] 자동 맵 매핑 완료 — {generatedMap.nodes.Count} 노드 / {generatedMap.totalLayers} 층 (씬 {nodeRows.Count}행 중 초과분 숨김)");
    }

    /// <summary>MapGenerator 실패 시 모든 버튼 RoomType=Combat 으로 폴백.</summary>
    private void ApplyFallbackAllCombat()
    {
        for (int r = 0; r < nodeRows.Count; r++)
        {
            var row = nodeRows[r];
            row.roomTypes.Clear();
            row.encounterKinds.Clear();
            if (row.rowParent == null) continue;
            var btns = row.rowParent.GetComponentsInChildren<Button>(true);
            for (int b = 0; b < btns.Length; b++)
            {
                row.roomTypes.Add(RoomType.Combat);
                row.encounterKinds.Add(EncounterKind.None);
            }
        }
    }

    // ----------------------------------------------------------
    // 버튼 자동 등록
    // ----------------------------------------------------------
    private void SetupNodeData()
    {
        for (int r = 0; r < nodeRows.Count; r++)
        {
            if (nodeRows[r].rowParent == null) continue;

            Button[] childButtons = nodeRows[r].rowParent.GetComponentsInChildren<Button>(true);
            nodeRows[r].buttons.Clear();
            nodeRows[r].buttons.AddRange(childButtons);

            int row = r;
            for (int b = 0; b < nodeRows[r].buttons.Count; b++)
            {
                int col = b;
                nodeRows[r].buttons[b].onClick.AddListener(() => OnNodeClicked(row, col));
            }
        }
    }

    // ----------------------------------------------------------
    // 버튼 시각 상태 업데이트
    // ----------------------------------------------------------
    // 노드 상태를 '테두리 색'으로 강조 (2026-06-08). 향후 노드별 이미지가 들어오면 타입은 이미지로 구분.
    private static readonly Color BorderPassed  = new Color(0.30f, 0.85f, 0.35f, 1f); // 지나온(현재 위치 포함) = 초록
    private static readonly Color BorderCurrent = new Color(1.00f, 0.85f, 0.25f, 1f); // 현재 클릭 가능 = 금색
    private static readonly Color BorderNone    = new Color(0f, 0f, 0f, 0f);          // 잠김/미선택 = 없음
    private static readonly Color StartFrameColor = new Color(0.42f, 0.60f, 0.95f, 1f); // 시작 노드 원형 = 파랑 (추후 기지/발판 아이콘)

    public void UpdateNodeStates()
    {
        bool startIsMarker = !(TutorialManager.Instance != null && TutorialManager.Instance.IsTutorial);

        for (int r = 0; r < nodeRows.Count; r++)
        {
            for (int b = 0; b < nodeRows[r].buttons.Count; b++)
            {
                Button  btn = nodeRows[r].buttons[b];
                Image   img = btn.GetComponent<Image>();
                Outline ol  = EnsureNodeOutline(btn);

                RoomType type      = GetRoomTypeAt(r, b);
                // `?` 노드가 진입 후 공개된 경우 — 색·아이콘을 실제 결과로 교체 (기획 유지안:
                // 진입 전 비공개(03 §1-1B) + 지나온 맵에는 공개된 이력 표시)
                EncounterKind kind = GetEncounterKindAt(r, b);
                Color    baseColor = kind != EncounterKind.None ? RevealedColorFor(kind) : GetRoomColor(type);

                float alpha;
                bool  interactable;
                Color border;

                bool isStart = (r == 0 && startIsMarker);
                if (isStart)
                {
                    // 시작 노드 = '현재 위치' 마커 — 초록 테두리, 클릭 불가.
                    alpha        = currentAlpha;
                    interactable = false;
                    border       = BorderPassed;
                }
                else if (r < currentRowIndex)
                {
                    // 지나온 층 — 실제 선택해 지나온 노드만 초록 테두리, 나머지는 흐리고 테두리 없음.
                    bool isSelected = (b == nodeRows[r].selectedButtonIndex);
                    alpha        = isSelected ? passedAlpha : lockedAlpha;
                    interactable = false;
                    border       = isSelected ? BorderPassed : BorderNone;
                }
                else if (r == currentRowIndex)
                {
                    // 현재 클릭 가능 — 금색 테두리.
                    alpha        = currentAlpha;
                    interactable = true;
                    border       = BorderCurrent;
                }
                else
                {
                    // 아직 못 간 층 — 흐림, 테두리 없음.
                    alpha        = lockedAlpha;
                    interactable = false;
                    border       = BorderNone;
                }

                btn.interactable = interactable;

                // 겉 — 원형 컬러 버튼 (런타임 흰 원 + 타입색 틴트). 시작은 별도 색. (2026-06-09 교체)
                if (img != null)
                {
                    var circle = CircleFrameSprite();
                    if (circle != null)
                    {
                        img.sprite         = circle;
                        img.type           = Image.Type.Simple;
                        img.preserveAspect = true; // 버튼이 정사각이 아니어도 원형 유지
                    }
                    Color fc = isStart ? StartFrameColor : baseColor;
                    img.color = new Color(fc.r, fc.g, fc.b, alpha);
                }
                if (ol != null)
                    ol.effectColor = border;

                // 안 — 타입 아이콘 (전투=십자검/보스=해골만, 나머지는 비움). (2026-06-09 교체)
                Image icon = EnsureNodeTypeIcon(btn);
                if (icon != null)
                {
                    Sprite ic = kind != EncounterKind.None
                        ? RevealedIconFor(kind)
                        : NodeInnerIconFor(type, isStart);
                    if (ic != null)
                    {
                        icon.sprite  = ic;
                        icon.enabled = true;
                        icon.color   = new Color(1f, 1f, 1f, Mathf.Min(1f, alpha + 0.2f));
                    }
                    else icon.enabled = false; // 비움 (시작/Event/화톳불 — 추후 아이콘 소싱)
                }
            }
        }

        UpdateLineStates(); // 연결선도 노드와 같은 진행 상태 규칙으로 갱신
        FocusCurrentRow();  // 현재 층을 노드맵 화면 중앙으로 자동 스크롤 (#3)
    }

    /// <summary>노드 버튼에 상태 표시용 Outline(테두리)을 보장. 없으면 추가. (2026-06-08)</summary>
    private static Outline EnsureNodeOutline(Button btn)
    {
        if (btn == null) return null;
        var ol = btn.GetComponent<Outline>();
        if (ol == null)
        {
            ol = btn.gameObject.AddComponent<Outline>();
            ol.effectDistance  = new Vector2(4f, 4f); // 테두리 두께
            ol.useGraphicAlpha = false;               // fill 이 흐려도 테두리는 또렷하게
        }
        return ol;
    }

    // ----------------------------------------------------------
    // #3 현재 층 자동 포커싱 — NodeDisplay(ScrollRect)를 현재 노드 행으로 스크롤
    // ----------------------------------------------------------
    private ScrollRect _nodeScroll;

    private void FocusCurrentRow()
    {
        if (nodeRows == null || nodeRows.Count == 0) return;
        int idx = Mathf.Clamp(currentRowIndex, 0, nodeRows.Count - 1);
        var rowGo = nodeRows[idx].rowParent;
        if (rowGo == null) return;

        if (_nodeScroll == null) _nodeScroll = rowGo.GetComponentInParent<ScrollRect>();
        if (_nodeScroll == null) return; // 스크롤뷰 아니면 포커싱 불필요

        var target = rowGo.transform as RectTransform;
        if (target == null) return;
        if (isActiveAndEnabled) StartCoroutine(ScrollToTarget(_nodeScroll, target));
    }

    // 레이아웃 안정화 후 target 행을 viewport 중앙에 맞추도록 content 이동(끝단은 클램프).
    private IEnumerator ScrollToTarget(ScrollRect sr, RectTransform target)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        var content  = sr.content;
        var viewport = sr.viewport != null ? sr.viewport : sr.transform as RectTransform;
        if (content == null || viewport == null) yield break;

        Vector3 targetCenterW   = target.TransformPoint(target.rect.center);
        Vector3 viewportCenterW = viewport.TransformPoint(viewport.rect.center);
        Vector3 worldDelta      = viewportCenterW - targetCenterW;
        Vector2 localDelta      = (Vector2)content.parent.InverseTransformVector(worldDelta);

        var pos = content.anchoredPosition;
        if (sr.vertical)   pos.y += localDelta.y;
        if (sr.horizontal) pos.x += localDelta.x;
        content.anchoredPosition = pos;

        // 끝단 과스크롤 방지
        sr.verticalNormalizedPosition   = Mathf.Clamp01(sr.verticalNormalizedPosition);
        sr.horizontalNormalizedPosition = Mathf.Clamp01(sr.horizontalNormalizedPosition);
    }

    // ----------------------------------------------------------
    // 위치 안내 토스트 — 시작 노드 클릭 시 "현재 위치는 여기입니다" (2026-06-07)
    //   기존 전역 토스트 컴포넌트가 없어 노드 화면에 런타임 자체 생성. 몇 초 뒤 페이드아웃.
    // ----------------------------------------------------------
    private GameObject  _toastRoot;
    private TMP_Text    _toastText;
    private CanvasGroup _toastGroup;
    private Coroutine   _toastRoutine;

    [Tooltip("위치 안내 토스트가 떠 있는 시간(초). 이후 0.4초간 페이드아웃.")]
    [SerializeField] private float locationToastSeconds = 2.5f;

    private void ShowLocationToast(string message)
    {
        if (_toastRoot == null) BuildLocationToast();
        if (_toastRoot == null || _toastText == null) return;

        Loc.Set(_toastText, message);
        _toastRoot.SetActive(true);
        _toastRoot.transform.SetAsLastSibling();
        if (_toastGroup != null) _toastGroup.alpha = 1f;

        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        if (isActiveAndEnabled) _toastRoutine = StartCoroutine(HideLocationToastAfter(locationToastSeconds));
    }

    private IEnumerator HideLocationToastAfter(float visibleSeconds)
    {
        yield return new WaitForSeconds(visibleSeconds);
        const float dur = 0.4f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            if (_toastGroup != null) _toastGroup.alpha = Mathf.Lerp(1f, 0f, t / dur);
            yield return null;
        }
        if (_toastRoot != null) _toastRoot.SetActive(false);
        if (_toastGroup != null) _toastGroup.alpha = 1f; // 다음 표시를 위해 복구
        _toastRoutine = null;
    }

    /// <summary>노드 화면(ScrollRect) 상단 중앙에 토스트 UI 를 런타임 생성.</summary>
    private void BuildLocationToast()
    {
        // 호스트 = 노드 행들의 부모 ScrollRect(NodeDisplay). 없으면 상위 Canvas.
        Transform host = null;
        if (nodeRows != null && nodeRows.Count > 0 && nodeRows[0].rowParent != null)
        {
            var sr = nodeRows[0].rowParent.GetComponentInParent<ScrollRect>();
            host = sr != null ? sr.transform
                              : nodeRows[0].rowParent.GetComponentInParent<Canvas>()?.transform;
        }
        if (host == null) return;

        // 폰트 — 씬의 기존 TMP 에서 확보 (한글)
        TMP_FontAsset font = null;
        var anyTmp = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
        if (anyTmp != null) font = anyTmp.font;

        _toastRoot = new GameObject("LocationToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
        _toastRoot.transform.SetParent(host, false);
        _toastRoot.layer = host.gameObject.layer;
        _toastGroup = _toastRoot.GetComponent<CanvasGroup>();
        _toastGroup.interactable = false;
        _toastGroup.blocksRaycasts = false;

        // 하단 중앙 — 시작(상단) 노드를 가리지 않도록 (2026-06-08).
        var rt = _toastRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 40f);
        rt.sizeDelta = new Vector2(520f, 88f);

        var bg = _toastRoot.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.10f, 0.16f, 0.92f);
        bg.raycastTarget = false;

        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.transform.SetParent(_toastRoot.transform, false);
        tgo.layer = _toastRoot.layer;
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(16f, 8f); trt.offsetMax = new Vector2(-16f, -8f);

        _toastText = tgo.AddComponent<TextMeshProUGUI>();
        if (font != null) _toastText.font = font;
        _toastText.alignment = TextAlignmentOptions.Center;
        _toastText.color = new Color(0.90f, 0.94f, 1f, 1f);
        _toastText.raycastTarget = false;
        _toastText.enableWordWrapping = false;
        _toastText.enableAutoSizing = true;
        _toastText.fontSizeMin = 18f; _toastText.fontSizeMax = 34f;
    }

    private Color GetRoomColor(RoomType type) => type switch
    {
        RoomType.Combat => combatColor,
        RoomType.Elite  => eliteColor,
        RoomType.Shop   => shopColor,
        RoomType.Rest   => restColor,
        RoomType.Boss   => bossColor,
        RoomType.Event  => eventColor,
        _               => Color.white,
    };

    // ----------------------------------------------------------
    // 노드 표시 — 원형 프레임(버튼) + 안쪽 타입 아이콘. (2026-06-09 교체)
    //   겉: 원형 컬러 버튼 (런타임 생성 흰 원 + 타입색 틴트)
    //   안: 타입 아이콘 — 시작=깃발 / 전투=십자검 / 화톳불=모닥불 / 랜덤(Event)=물음표 / 보스=해골.
    //   (전부 1-bit_Pixel_Icons 팩 → Resources/Icons/node_*.png, 흰색 1-bit 실루엣)
    // ----------------------------------------------------------

    // 런타임 생성 흰 원 — 빌트인 Knob 이 Unity 6 에서 null 이라 직접 생성. 타입색으로 틴트해 원형 버튼으로 사용.
    private static Sprite _circleFrame;
    private static Sprite CircleFrameSprite()
    {
        if (_circleFrame == null)
        {
            const int S = 64; float c = (S - 1) * 0.5f; float rad = S * 0.5f - 1.5f;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(rad - d + 0.5f); // 가장자리 1px 안티에일리어싱
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            _circleFrame = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        }
        return _circleFrame;
    }

    // Resources/Icons/ 의 단독 PNG 아이콘 로드(캐시). 노드 전용 아이콘(깃발/모닥불/물음표)용.
    private static readonly Dictionary<string, Sprite> _iconCache = new();
    private static Sprite IconSprite(string resName)
    {
        if (!_iconCache.TryGetValue(resName, out var sp))
        {
            sp = Resources.Load<Sprite>("Icons/" + resName);
            _iconCache[resName] = sp;
        }
        return sp;
    }

    /// <summary>노드 안쪽 타입 아이콘. 시작=깃발 / 전투=십자검 / 화톳불=모닥불 / 랜덤(Event)=물음표 / 보스=해골.</summary>
    private static Sprite NodeInnerIconFor(RoomType type, bool isStart)
    {
        if (isStart) return IconSprite("node_start");                // 깃발 = 시작
        switch (type)
        {
            case RoomType.Combat: return IconSprite("node_combat");   // 십자검 = 전투
            case RoomType.Elite: return IconSprite("node_elite");   // 엘리트 = 드래곤
            case RoomType.Shop: return IconSprite("node_shop");     // 용병소 = 모루
            case RoomType.Boss:   return IconSprite("node_boss");     // 해골 = 보스
            case RoomType.Rest:   return IconSprite("node_rest");     // 모닥불 = 화톳불
            case RoomType.Event:  return IconSprite("node_event");    // 물음표 = 랜덤노드(엘리트/용병소/교회)
            default: return null;
        }
    }

    // ----------------------------------------------------------
    // `?` 노드 결과 공개 — 아이콘·색 이력 표시 (기획 유지안, 2026-08-21)
    //   진입 전에는 `?` 유지(03 §1-1B 비공개). 진입해 결과가 확정된 노드만
    //   실제 결과의 아이콘·색으로 교체해 지나온 맵에 이력을 남긴다.
    // ----------------------------------------------------------

    /// <summary>방금 클릭한 `?` 노드에 공개된 결과를 기록한다 (OnNodeClicked 에서 currentRowIndex++ 선행됨).</summary>
    private void RevealEventOutcome(EncounterKind kind)
    {
        int row = currentRowIndex - 1;
        if (row < 0 || row >= nodeRows.Count) return;
        var kinds = nodeRows[row].encounterKinds;
        int col = nodeRows[row].selectedButtonIndex;
        if (kinds == null || col < 0 || col >= kinds.Count) return;
        kinds[col] = kind;
    }

    /// <summary>인덱스 안전한 EncounterKind 조회. 범위 밖이면 None(미공개).</summary>
    private EncounterKind GetEncounterKindAt(int row, int col)
    {
        if (row < 0 || row >= nodeRows.Count) return EncounterKind.None;
        var kinds = nodeRows[row].encounterKinds;
        if (kinds == null || col < 0 || col >= kinds.Count) return EncounterKind.None;
        return kinds[col];
    }

    /// <summary>공개된 `?` 결과의 아이콘. 선택지 이벤트는 `?` 를 유지한다.</summary>
    private static Sprite RevealedIconFor(EncounterKind kind) => kind switch
    {
        EncounterKind.Mercenary   => IconSprite("node_shop"),    // 모루 = 용병소
        EncounterKind.Church      => IconSprite("node_church"),  // 교회
        EncounterKind.EliteBattle => IconSprite("node_elite"),   // 드래곤 = 엘리트
        EncounterKind.ChoiceEvent => IconSprite("node_event"),   // 물음표 유지
        _                         => null,
    };

    /// <summary>공개된 `?` 결과의 노드 색.</summary>
    private Color RevealedColorFor(EncounterKind kind) => kind switch
    {
        EncounterKind.Mercenary   => shopColor,
        EncounterKind.Church      => churchColor,
        EncounterKind.EliteBattle => eliteColor,
        EncounterKind.ChoiceEvent => eventColor,
        _                         => eventColor,
    };

    /// <summary>노드 버튼 중앙에 타입 마커 아이콘 Image 를 보장(없으면 생성). 클릭 방해 안 함.</summary>
    private static Image EnsureNodeTypeIcon(Button btn)
    {
        if (btn == null) return null;
        var existing = btn.transform.Find("TypeIcon");
        if (existing != null) return existing.GetComponent<Image>();

        var go = new GameObject("TypeIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(btn.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        var brt = btn.GetComponent<RectTransform>();
        float sz = (brt != null ? Mathf.Min(brt.rect.width, brt.rect.height) : 48f) * 0.6f;
        rt.sizeDelta = new Vector2(sz, sz);

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;   // 버튼 클릭 그대로 통과
        img.preserveAspect = true;
        return img;
    }

    // ----------------------------------------------------------
    // 노드 클릭 처리 — RoomType 별 분기
    // ----------------------------------------------------------
    /// <summary>
    /// 노드 버튼 클릭 시 호출된다.
    /// <summary>
    /// 🎮 치트 — 노드 1단계 전진 (F2). RoomType 분기/패널 진입 없이 currentRowIndex 만 증가.
    /// 예: 3층에서 호출 → 4층으로 점프 (전투/용병소 등 패널은 띄우지 않음).
    /// </summary>
    public void CheatAdvanceFloor()
    {
        if (nodeRows == null || nodeRows.Count == 0)
        {
            Debug.LogWarning("[NodeSystem] 🎮 F2 치트 — nodeRows 미초기화");
            return;
        }
        if (currentRowIndex >= nodeRows.Count)
        {
            Debug.Log($"[NodeSystem] 🎮 F2 치트 — 이미 마지막 층 도달 ({currentRowIndex})");
            return;
        }
        int before = currentRowIndex;
        currentRowIndex++;
        UpdateNodeStates();
        Debug.Log($"[NodeSystem] 🎮 F2 치트 — 층 {before} → {currentRowIndex}");
    }

    /// <summary>
    /// 현재 층의 버튼이면: 선택 처리 → RoomType 별 화면 분기 → 다음 층 진행.
    /// </summary>
    public void OnNodeClicked(int row, int col)
    {
        if (row != currentRowIndex) return;

        // 팝업/패널(설정·로그·파티편집 등)이 열린 상태면 노드 클릭 무시 (기획자 피드백 #10).
        // 열린 패널 위로 클릭이 새어 노드가 실행되던 문제 차단.
        if (IsAnyBlockingPanelOpen())
        {
            Debug.Log("[NodeSystem] 패널이 열려 있어 노드 클릭을 무시한다.");
            return;
        }

        // 1) 선택된 버튼 기록
        nodeRows[row].selectedButtonIndex = col;
        CurrentNodeNumber = col + 1; // 왼1 / 중2 / 오3 — 조사관 수첩 '제 N구역'
        AudioManager.Instance?.PlaySfxByIdClipped(SfxId.NodeMove, 1.2f); // 노드 클릭음 — 원본 9초라 1.2초만 재생 후 끊음

        // 2) 클릭된 노드의 RoomType 조회
        RoomType type = GetRoomTypeAt(row, col);
        CurrentRoomType = type;
        Debug.Log($"[NodeSystem] 노드 클릭 — 층 {row + 1} (col={col}) | RoomType={type}");

        // 3) 진행 + 분기
        if (currentRowIndex < nodeRows.Count)
        {
            currentRowIndex++;
            DispatchByRoomType(type);
            UpdateNodeStates();
        }
    }

    /// <summary>
    /// 모달 패널(PanelBase + CanvasGroup)이 하나라도 열려 있으면 true (기획자 피드백 #10).
    /// PanelBase 는 닫힘 상태에서 alpha=0 으로만 숨으므로 alpha 로 열림 여부를 판정한다.
    /// </summary>
    private bool IsAnyBlockingPanelOpen()
    {
        foreach (var p in FindObjectsByType<PanelBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var cg = p.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha > 0.5f) return true;
        }
        return false;
    }

    /// <summary>RoomType 에 따라 적절한 화면을 켜거나 임시 진행 처리한다.</summary>
    private void DispatchByRoomType(RoomType type)
    {
        // 노드 진입 시 게임 이벤트 로그 리셋 — 이전 노드 메시지가 누적되지 않도록.
        GameLogService.Instance?.Clear();

        // 튜토리얼 모달 — 노드 유형별 1회 안내
        var tm = TutorialManager.Instance;
        if (tm != null && tm.IsTutorial)
        {
            switch (type)
            {
                case RoomType.Combat: tm.TryShowDialogue(TutorialManager.DialogueId.CombatIntro); break;
                // Shop(고정 용병소) 노드는 폐지 — ShopIntro 는 Event 랜덤 결과가 용병소일 때 표시 (아래 case Event)
                // Boss 노드는 튜토리얼에서 제거됨 (2026-06-13 QA) — 화톳불에서 종료
                case RoomType.Rest:   tm.TryShowDialogue(TutorialManager.DialogueId.RestIntro);  break;
            }
        }

        // ============================================================
        // 2026-06-09 MVP 고정 일렬 맵 (기획 §12) + 랜덤 노드
        //   시퀀스 = 시작 > 전투 > 랜덤노드(Event) > 전투 > 화톳불(Rest) > 보스.
        //   Event = 용병소/교회/엘리트 통합 '클릭 시 3개 중 1개' 랜덤 노드.
        //     발표용으로 EventOutcomeWeights = 100/0/0 → 용병소만 등장하도록 조작.
        // ============================================================

        switch (type)
        {
            // ── 전투 계열: 기존 흐름 그대로 (DisplayChanger 가 전투 패널 토글) ──
            case RoomType.Combat:
            case RoomType.Elite:
            case RoomType.Boss:
                // P0-03 현장 관찰 예약 — 지정 인카운터(일반 전투 노드)에만 관찰을 붙인다.
                // 같은 전투 프로필의 다른 전투에 자동 부여 금지 (16-B §3) — 노드(행) 단위 바인딩.
                // OnNodeClicked 에서 currentRowIndex++ 가 선행되므로 클릭된 행 = currentRowIndex - 1.
                // 엘리트·보스·튜토리얼은 관찰 없음 (null 예약 = 이전 예약도 정리).
                if (tm == null || !tm.IsTutorial)
                {
                    string obsId = type == RoomType.Combat
                        ? FieldObservationCatalog.IdForLegacyCombatRow(currentRowIndex - 1)
                        : null;
                    RunSessionManager.Instance?.SetPendingObservation(obsId);
                }
                AudioManager.Instance?.PlaySfxByIdClipped(SfxId.NodeEnter, 1.5f); // 문 열림 원본이 길어 1.5초만 재생 (보스 진입 시 계속 울리던 문제)
                DisplayChange.Instance.DisplayChanger(nodeDisplay, actionDisplay);
                if (type == RoomType.Boss)
                    AudioManager.Instance?.PlayBgmById(BgmId.Boss, loop: false);   // 보스 브금은 진입 시 1회만 (반복 X — 사용자 요청)
                else
                    AudioManager.Instance?.PlayBgmById(BgmId.Battle);
                break;

            // ── 화툿불 (E 작업 완료 — RestPanel 호출) ──
            //   기획 §02_MVP_노드_설계 §화툿불 — HP/스트레스 -15 회복 + 파티 편집
            //   패널 미연결 시(인스펙터 빈 경우) 로그만 남기고 다음 층 진행.
            case RoomType.Rest:
                if (restPanel != null)
                {
                    restPanel.OnExit -= HandleRestExit;
                    restPanel.OnExit += HandleRestExit;
                    restPanel.OpenFromNode();
                }
                else
                {
                    Debug.Log("[NodeSystem] 화툿불 노드 — RestPanel 미연결, 회복 없이 다음 층으로 진행.");
                }
                break;

            // ── `?` 노드 (Event) — 진입 시 결과 가중 추첨 (03 §1-3 이벤트 노드 세부 결과) ──
            //   사용자 결정(2026-08-21): 용병소 40 / 교회 20 / 엘리트 10 / 선택지 이벤트 30.
            //   교회·엘리트 전용 노드 이미지는 추후 — 진입 전에는 `?` 로 통일 표시 (진입 전 비공개).
            //   단, 튜토리얼은 본편 선택지 이벤트를 생성하지 않으므로(§2) 기존 용병소 안내를 유지한다.
            case RoomType.Event:
                if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorial)
                {
                    Debug.Log("[NodeSystem] 튜토리얼 `?` 노드 → 용병소 (선택지 이벤트 제외, 기획 §2)");
                    TutorialManager.Instance?.TryShowDialogue(TutorialManager.DialogueId.ShopIntro);
                    OpenMercenaryFromNode();
                }
                else
                {
                    switch (RollEventOutcome())
                    {
                        case 0:
                            Debug.Log("[NodeSystem] `?` 노드 → 용병소");
                            RevealEventOutcome(EncounterKind.Mercenary);
                            OpenMercenaryFromNode();
                            break;
                        case 1:
                            Debug.Log("[NodeSystem] `?` 노드 → 교회");
                            RevealEventOutcome(EncounterKind.Church);
                            OpenChurchFromNode();
                            break;
                        case 2:
                            Debug.Log("[NodeSystem] `?` 노드 → 엘리트 전투");
                            RevealEventOutcome(EncounterKind.EliteBattle);
                            OpenEliteBattleFromNode();
                            break;
                        default:
                            Debug.Log("[NodeSystem] `?` 노드 → 선택지 이벤트 팝업");
                            RevealEventOutcome(EncounterKind.ChoiceEvent);
                            OpenEventFromNode();
                            break;
                    }
                }
                break;

            default:
                Debug.LogWarning($"[NodeSystem] 미지원 RoomType={type} — 폴백으로 전투 화면 호출.");
                DisplayChange.Instance.DisplayChanger(nodeDisplay, actionDisplay);
                break;
        }
    }

    // 랜덤 노드(Event) 진입 결과 가중치 [용병소, 교회, 엘리트, 선택지 이벤트].
    // 03. 노드·용병소·보상·메타 §1-1 이벤트 노드 내부 가중치(초안): 40/20/10/30.
    // 정확한 생성 비율과 런당 상한은 미결(15) — 플레이테스트 후 조정.
    private static readonly int[] EventOutcomeWeights = { 40, 20, 10, 30 };

    /// <summary>랜덤 노드 결과를 가중 랜덤으로 결정. 0=용병소 / 1=교회 / 2=엘리트 / 3=선택지 이벤트.</summary>
    private int RollEventOutcome()
    {
        int total = 0;
        foreach (var w in EventOutcomeWeights) total += w;
        if (total <= 0) return 0;
        int roll = UnityEngine.Random.Range(0, total);
        int cum = 0;
        for (int i = 0; i < EventOutcomeWeights.Length; i++)
        {
            cum += EventOutcomeWeights[i];
            if (roll < cum) return i;
        }
        return 0;
    }

    /// <summary>용병소(MercenaryOfficePanel) 진입 — Shop 노드 / 랜덤노드→용병소 공용.</summary>
    private void OpenMercenaryFromNode()
    {
        if (mercenaryOfficePanel != null)
        {
            mercenaryOfficePanel.OnExit -= HandleMercenaryExit;
            mercenaryOfficePanel.OnExit += HandleMercenaryExit;
            mercenaryOfficePanel.OpenFromNode();
            AudioManager.Instance?.PlayBgmById(BgmId.Mercenary);
        }
        else Debug.Log("[NodeSystem] 용병소 — MercenaryOfficePanel 미연결, 다음 층으로 진행.");
    }

    /// <summary>
    /// 엘리트 전투 진입 — 랜덤노드→엘리트 (03 §1-3).
    /// 액션 화면 활성화 전에 CurrentRoomType 을 Elite 로 바꿔 EnemySpawner 가
    /// 약탈자를 스폰하게 한다. 엘리트 전투에는 현장 관찰을 붙이지 않는다 (16-B §3).
    /// </summary>
    private void OpenEliteBattleFromNode()
    {
        CurrentRoomType = RoomType.Elite;
        RunSessionManager.Instance?.SetPendingObservation(null);
        AudioManager.Instance?.PlaySfxByIdClipped(SfxId.NodeEnter, 1.5f);
        DisplayChange.Instance.DisplayChanger(nodeDisplay, actionDisplay);
        AudioManager.Instance?.PlayBgmById(BgmId.Battle);
    }

    /// <summary>교회(ChurchPanel) 진입 — 랜덤노드→교회.</summary>
    private void OpenChurchFromNode()
    {
        if (churchPanel != null)
        {
            churchPanel.OnExit -= HandleChurchExit;
            churchPanel.OnExit += HandleChurchExit;
            churchPanel.OpenFromNode();
        }
        else Debug.Log("[NodeSystem] 교회 — ChurchPanel 미연결, 다음 층으로 진행.");
    }

    /// <summary>용병소 패널의 "나가기" 클릭 시 호출 — 노드맵 화면 복귀.</summary>
    private void HandleMercenaryExit()
    {
        // MercenaryService.OnLeaveNode 는 MercenaryOfficePanel 내부에서 이미 호출됨.
        // 여기서는 노드맵 UI 갱신만 — 이미 currentRowIndex++ 가 OnNodeClicked 에서 처리됨.
        if (mercenaryOfficePanel != null)
            mercenaryOfficePanel.OnExit -= HandleMercenaryExit;
        UpdateNodeStates();
        AudioManager.Instance?.PlayBgmById(BgmId.NodeMap);
    }

    /// <summary>화툿불 패널의 "다음 층" 클릭 시 호출 — 노드맵 화면 복귀.
    /// 단, 튜토리얼에서는 화톳불이 마지막 노드 → 여기서 튜토리얼 종료 후 시작 화면으로 (2026-06-13 QA: 보스 노드 제거).</summary>
    private void HandleRestExit()
    {
        if (restPanel != null)
            restPanel.OnExit -= HandleRestExit;

        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorial)
        {
            Debug.Log("[NodeSystem] 튜토리얼 화톳불 종료 — 완료 플래그 저장 후 시작 화면 복귀");
            TutorialManager.Instance.EndTutorial(markComplete: true);
            SceneTransition.Go("GameStartScene");
            return;
        }

        UpdateNodeStates();
        AudioManager.Instance?.PlayBgmById(BgmId.NodeMap);
    }

    /// <summary>교회 패널의 "다음 층" 클릭 시 호출 — 노드맵 화면 복귀.</summary>
    private void HandleChurchExit()
    {
        if (churchPanel != null)
            churchPanel.OnExit -= HandleChurchExit;
        UpdateNodeStates();
        AudioManager.Instance?.PlayBgmById(BgmId.NodeMap);
    }

    // ----------------------------------------------------------
    // `?` 노드 선택지 이벤트 (기획 §06) — 팝업 진입/복귀
    // ----------------------------------------------------------
    /// <summary>`?`(Event) 노드 — 선택지 이벤트 팝업을 런타임 생성/재사용해 무작위로 띄운다.</summary>
    private void OpenEventFromNode()
    {
        if (_eventPanel == null)
            _eventPanel = EventPanel.CreateUnder(ResolveUiCanvas());

        if (_eventPanel == null)
        {
            Debug.LogWarning("[NodeSystem] 선택지 이벤트 팝업 생성 실패 — 캔버스를 찾지 못해 다음 층으로 진행.");
            return;
        }

        _eventPanel.OnExit -= HandleEventExit;
        _eventPanel.OnExit += HandleEventExit;
        _eventPanel.OpenRandom();
    }

    /// <summary>선택지 이벤트 팝업 "다음 층" 클릭 시 호출 — 노드맵 화면 복귀.</summary>
    private void HandleEventExit()
    {
        if (_eventPanel != null) _eventPanel.OnExit -= HandleEventExit;
        UpdateNodeStates();
        AudioManager.Instance?.PlayBgmById(BgmId.NodeMap);
    }

    /// <summary>팝업을 붙일 UI 캔버스 Transform (노드 UI 의 상위 Canvas 우선, 없으면 씬 전체 검색).</summary>
    private Transform ResolveUiCanvas()
    {
        if (nodeRows != null && nodeRows.Count > 0 && nodeRows[0].rowParent != null)
        {
            var c = nodeRows[0].rowParent.GetComponentInParent<Canvas>();
            if (c != null) return c.transform;
        }
        var any = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        return any != null ? any.transform : null;
    }

    /// <summary>인덱스 안전한 RoomType 조회. 범위 밖이면 Combat 폴백.</summary>
    private RoomType GetRoomTypeAt(int row, int col)
    {
        if (row < 0 || row >= nodeRows.Count) return RoomType.Combat;
        var types = nodeRows[row].roomTypes;
        if (types == null || col < 0 || col >= types.Count) return RoomType.Combat;
        return types[col];
    }
}
