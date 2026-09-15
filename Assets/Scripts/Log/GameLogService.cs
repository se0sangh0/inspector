// GameLogService.cs
// 게임 이벤트 로그 (자연어, 카테고리 색상) 와 시스템 디버그 로그 (Application.logMessageReceived) 를 분리 보관.
//
// ── 사용 ────────────────────────────────────────────────────────
//   GameLog.Event("고블린이 5의 피해를 입었다!", LogCategory.Damage)  → 게임 이벤트 채널에 기록 + UI 갱신
//   GameLogService.Instance.GetEntries()         → 게임 이벤트 엔트리 리스트 (LogPopup 이 색상 빌드용으로 사용)
//   GameLogService.Instance.OnGameEventAdded     → 새 게임 이벤트 발생 시 발화
//   GameLogService.Instance.Clear()              → 노드 진입/이탈 시 호출 — 두 채널 모두 비움

using System.Collections.Generic;
using UnityEngine;

public enum LogCategory
{
    Default,  // 흰색  (분류 안 한 일반 메시지)
    Damage,   // 빨강  (피해)
    Heal,     // 녹색  (회복)
    Shield,   // 노랑  (실드 부여 / 흡수)
    Death,    // 회색  (사망)
    Reward,   // 파랑  (영혼석/마석/아이템 획득)
    Status,   // 보라  (스트레스/패닉/탈진)
    Skill,    // 시안  (스킬 사용 — 아군/적)
}

public readonly struct GameLogEntry
{
    private readonly LocalizedMessage _message;
    public string message => _message?.Render() ?? string.Empty;
    public readonly LogCategory category;
    public GameLogEntry(LocalizedMessage message, LogCategory category)
    {
        _message = message;
        this.category = category;
    }
}

public class GameLogService : Singleton<GameLogService>
{
    [SerializeField, Tooltip("표시 보관할 게임 이벤트 최대 개수")]
    private int maxGameEvents = 30;

    [SerializeField, Tooltip("보관할 시스템 디버그 로그 최대 개수")]
    private int maxSystemLogs = 200;

    private readonly List<GameLogEntry> _gameEvents = new();
    private readonly Queue<string>      _systemLogs = new();

    /// <summary>새 게임 이벤트 발생 시 발화. LogPopup 이 구독해 라인 강조 fade 와 RefreshAll 트리거.</summary>
    public event System.Action<GameLogEntry> OnGameEventAdded;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        Application.logMessageReceived += HandleSystemLog;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Application.logMessageReceived -= HandleSystemLog;
    }

    // ── 게임 이벤트 (사용자 노출용) ───────────────────────────
    public void LogEvent(LocalizedMessage message, LogCategory category = LogCategory.Default)
    {
        if (message == null || string.IsNullOrEmpty(message.Key)) return;

        var entry = new GameLogEntry(message, category);
        _gameEvents.Add(entry);
        while (_gameEvents.Count > maxGameEvents) _gameEvents.RemoveAt(0);

        OnGameEventAdded?.Invoke(entry);
    }

    public IReadOnlyList<GameLogEntry> GetEntries() => _gameEvents;

    // ── 시스템 디버그 로그 (콘솔 전용, UI 비공개) ────────────
    private void HandleSystemLog(string condition, string stackTrace, LogType type)
    {
        string prefix = type switch
        {
            LogType.Error     => "[ERR] ",
            LogType.Warning   => "[WRN] ",
            LogType.Exception => "[EXC] ",
            LogType.Assert    => "[ASR] ",
            _                 => "",
        };
        string line = prefix + condition;

        _systemLogs.Enqueue(line);
        while (_systemLogs.Count > maxSystemLogs) _systemLogs.Dequeue();
    }

    public string GetAllSystemLogs() => string.Join("\n", _systemLogs);

    // ── 노드 진입/이탈 시 호출. 두 채널 모두 비움 + UI 갱신용 빈 이벤트 발화 ──
    public void Clear()
    {
        _gameEvents.Clear();
        _systemLogs.Clear();
        OnGameEventAdded?.Invoke(new GameLogEntry("", LogCategory.Default));
    }
}

/// <summary>전역 편의 헬퍼 — `GameLog.Event("...", LogCategory.Damage)` 한 줄로 호출.</summary>
public static class GameLog
{
    public static void Formatted(System.FormattableString message, LogCategory category = LogCategory.Default)
    {
        if (GameLogService.Instance != null)
            GameLogService.Instance.LogEvent(LocalizedMessage.FromInterpolated(message), category);
    }

    public static void Event(string message, LogCategory category = LogCategory.Default)
    {
        if (GameLogService.Instance != null) GameLogService.Instance.LogEvent(message, category);
    }
}
