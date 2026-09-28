using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class NodeSystem
{
    public const string RouteGuideCompletedKey = "route_guide_completed";
    private RouteSelectionPanel _routeView;
    private readonly Dictionary<int, PreparedRoutes> _preparedRoutes = new();
    private readonly Dictionary<int, RouteCardData> _visitedRoutes = new();
    private readonly List<RouteMapStop> _routeHistory = new();

    private sealed class PreparedRoutes
    {
        public RoomType[] types = new RoomType[3];
        public int[] eventOutcomes = new int[3];
        public RouteCardData[] cards = new RouteCardData[3];
    }

    private bool UsesRouteCards => !(TutorialManager.Instance != null && TutorialManager.Instance.IsTutorial);
    public bool CanSelectRoute => UsesRouteCards && currentRowIndex >= 1
        && currentRowIndex < MapGenerator.RestFloor - 1
        && !_visitInProgress && !_automaticTravelInProgress;

    private void BuildRouteInterface()
    {
        if (nodeDisplay == null || nodeDisplay.Length == 0 || nodeDisplay[0] == null) return;
        var host = nodeDisplay[0].transform;
        // 원본 씬을 재저장하지 않고 기존 연결선/노드 입력만 숨긴다.
        foreach (var button in host.GetComponentsInChildren<Button>(true)) button.interactable = false;
        foreach (var scroll in host.GetComponentsInChildren<ScrollRect>(true)) scroll.enabled = false;
        for (int i = 0; i < host.childCount; i++)
        {
            var child = host.GetChild(i);
            if (child != transform && !transform.IsChildOf(child)) child.gameObject.SetActive(false);
        }
        _routeView = RouteSelectionPanel.Create(host, this);
        _routeHistory.Add(new RouteMapStop(1, "탐사 시작", "야생림", 1));
    }

    private PreparedRoutes PrepareRoutes(int row)
    {
        if (_preparedRoutes.TryGetValue(row, out var prepared))
        {
            bool unchanged = true;
            for (int c = 0; c < 3; c++) unchanged &= prepared.types[c] == GetRoomTypeAt(row, c);
            if (unchanged) return prepared;
        }
        prepared = new PreparedRoutes();
        int offset = UnityEngine.Random.Range(0, 3);
        for (int c = 0; c < 3; c++)
        {
            var type = GetRoomTypeAt(row, c);
            prepared.types[c] = type;
            // 결과는 카드 생성 시 1회 준비한다. 언어 전환/맵 열람으로 다시 추첨하지 않는다.
            prepared.eventOutcomes[c] = type == RoomType.Event ? RollEventOutcome() : -1;
            bool strongWarning = type == RoomType.Elite
                || (type == RoomType.Event && prepared.eventOutcomes[c] == 2)
                || (type == RoomType.Combat && row + 1 >= 5);
            prepared.cards[c] = RouteCatalog.Create(row + 1, c, offset, strongWarning);
        }
        _preparedRoutes[row] = prepared;
        return prepared;
    }

    private void RefreshRouteInterface()
    {
        var choices = CanSelectRoute ? PrepareRoutes(currentRowIndex).cards : null;
        _routeView.Present(currentRowIndex, choices, _routeHistory,
            PlayerPrefs.GetInt(RouteGuideCompletedKey, 0) == 0);
    }

    /// <summary>카드만 선택 입력을 제공한다. 진행도 맵은 읽기 전용이다.</summary>
    public void SelectRoute(int row, int column)
    {
        if (!CanSelectRoute || InvestigatorNotebookController.IsOpen) return;
        OnNodeClicked(row, column);
    }

    private bool RecordSelectedRoute(int row, int column, RoomType type)
    {
        if (!UsesRouteCards) return false;
        var session = RunSessionManager.Instance;
        bool notice = false;
        if (row >= 1 && row < MapGenerator.RestFloor - 1)
        {
            var card = PrepareRoutes(row).cards[column];
            _visitedRoutes[row] = card;
            bool recorded = session != null && session.Records.RecordRouteSelected(
                row + 1, column + 1, card.title, card.environment, card.omen);
            notice = recorded && PlayerPrefs.GetInt(RouteGuideCompletedKey, 0) == 0;
            if (notice)
            {
                PlayerPrefs.SetInt(RouteGuideCompletedKey, 1);
                PlayerPrefs.Save();
                _routeView?.ShowRecordAdded(column);
            }
        }
        if (type != RoomType.Event)
            RecordRevealedRouteLocation(row, column, EncounterKind.None);
        return notice;
    }

    private IEnumerator EnterAfterRouteNotice(RoomType type)
    {
        // 최초 선택 뒤 비차단 기록 안내를 잠깐 보여 주고 실제 장소로 이어진다.
        yield return new WaitForSecondsRealtime(1.1f);
        while (InvestigatorNotebookController.IsOpen || IsAnyBlockingPanelOpen()) yield return null;
        DispatchByRoomType(type);
        UpdateNodeStates();
    }

    private int PreparedEventOutcome(int row, int column)
    {
        if (!UsesRouteCards || row < 1 || row >= MapGenerator.RestFloor - 1) return RollEventOutcome();
        return PrepareRoutes(row).eventOutcomes[column];
    }

    private void RecordRevealedRouteLocation(int row, int column, EncounterKind kind)
    {
        if (!UsesRouteCards) return;
        string place = kind switch
        {
            EncounterKind.Mercenary => "용병소",
            EncounterKind.Church => "교회",
            EncounterKind.EliteBattle => "엘리트 전투",
            EncounterKind.ChoiceEvent => "사건",
            _ => GetRoomTypeAt(row, column) switch
            {
                RoomType.Rest => "화톳불",
                RoomType.Boss => "성소",
                RoomType.Elite => "엘리트 전투",
                RoomType.Shop => "용병소",
                _ => "전투"
            }
        };
        var records = RunSessionManager.Instance != null ? RunSessionManager.Instance.Records : null;
        bool recorded = records != null && records.RecordLocationRevealed(row + 1, column + 1, Loc.Message(place));
        if (recorded || !_routeHistory.Exists(s => s.floor == row + 1))
        {
            _visitedRoutes.TryGetValue(row, out var route);
            _routeHistory.Add(new RouteMapStop(row + 1, route != null ? route.title : place, place, column + 1));
        }
    }
}

public sealed class RouteMapStop
{
    public readonly int floor, column;
    public readonly string title, place;
    public RouteMapStop(int floor, string title, string place, int column)
    {
        this.floor = floor; this.title = title; this.place = place; this.column = column;
    }
}
