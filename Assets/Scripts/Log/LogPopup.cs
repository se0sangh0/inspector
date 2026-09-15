// LogPopup.cs
// GameLogService.GetEntries() 의 게임 이벤트를 카테고리별 색상으로 표시.
// 새 로그 추가 시 마지막 줄을 0.4초간 굵게 강조 후 일반화.
//
// ── 인스펙터 ───────────────────────────────────────────────────
//   logText      : ScrollView 안의 TMP_Text — Rich Text 활성 필요
//   scrollRect   : ScrollRect (새 로그 추가 시 하단으로 스크롤)
//   closeButton  : 닫기 버튼
//   clearButton  : (선택) 로그 비우기 버튼

using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LogPopup : PanelBase
{
    [Header("로그 표시")]
    [SerializeField] private TMP_Text   logText;
    [SerializeField] private ScrollRect scrollRect;

    [Header("버튼")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button clearButton;

    [Header("강조 fade")]
    [SerializeField, Tooltip("새 로그 추가 시 마지막 줄을 굵게 표시할 시간(초)")]
    private float flashDuration = 0.4f;

    private bool      _flashLast;
    private Coroutine _flashCoroutine;

    protected override void OnOpened()
    {
        RefreshAll();
        if (GameLogService.Instance != null)
            GameLogService.Instance.OnGameEventAdded += OnLogAdded;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
        if (clearButton != null)
        {
            clearButton.onClick.RemoveListener(OnClearClicked);
            clearButton.onClick.AddListener(OnClearClicked);
        }
    }

    protected override void OnClosed()
    {
        if (GameLogService.Instance != null)
            GameLogService.Instance.OnGameEventAdded -= OnLogAdded;

        if (_flashCoroutine != null) { StopCoroutine(_flashCoroutine); _flashCoroutine = null; }

        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (clearButton != null) clearButton.onClick.RemoveListener(OnClearClicked);
    }

    private void OnLogAdded(GameLogEntry _)
    {
        _flashLast = true;
        RefreshAll();

        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(UnflashAfter(flashDuration));
    }

    private IEnumerator UnflashAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _flashLast = false;
        RefreshAll();
        _flashCoroutine = null;
    }

    private void RefreshAll()
    {
        if (logText == null) return;

        if (GameLogService.Instance == null)
        {
            logText.text = "";
            return;
        }

        var entries = GameLogService.Instance.GetEntries();
        Loc.Bind(logText, () => BuildLogText(entries));

        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private string BuildLogText(System.Collections.Generic.IReadOnlyList<GameLogEntry> entries)
    {
        var sb = new StringBuilder(entries.Count * 64);
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            string hex = ColorHex(e.category);
            bool   isLast = (i == entries.Count - 1);
            if (isLast && _flashLast)
                sb.Append("<b><color=#").Append(hex).Append('>').Append(e.message).Append("</color></b>");
            else
                sb.Append("<color=#").Append(hex).Append('>').Append(e.message).Append("</color>");

            if (i < entries.Count - 1) sb.Append('\n');
        }

        return sb.ToString();
    }

    private static string ColorHex(LogCategory c) => c switch
    {
        LogCategory.Damage  => "FF5252",  // 빨강 — 피해
        LogCategory.Heal    => "66BB6A",  // 녹색 — 회복
        LogCategory.Shield  => "FFC107",  // 노랑 — 실드
        LogCategory.Death   => "9E9E9E",  // 회색 — 사망
        LogCategory.Reward  => "29B6F6",  // 파랑 — 보상
        LogCategory.Status  => "BA68C8",  // 보라 — 스트레스/패닉
        LogCategory.Skill   => "4DD0E1",  // 시안 — 스킬
        _                   => "FFFFFF",
    };

    private void OnClearClicked()
    {
        if (GameLogService.Instance != null) GameLogService.Instance.Clear();
    }
}
