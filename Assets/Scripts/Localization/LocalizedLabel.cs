using System;
using TMPro;
using UnityEngine;

/// <summary>표시만 다시 계산한다. 언어 변경으로 게임 행동이나 패널 진입을 재실행하지 않는다.</summary>
[DisallowMultipleComponent]
public sealed class LocalizedLabel : MonoBehaviour
{
    private TMP_Text _text;
    private Func<string> _render;
    private string _lastRendered;

    public void Bind(Func<string> render)
    {
        _text = GetComponent<TMP_Text>();
        _render = render;
        _lastRendered = _text.text;
        Refresh();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += Refresh;
        Refresh();
    }
    private void OnDisable() => LocalizationManager.OnLanguageChanged -= Refresh;

    public bool OwnsCurrentText => _text != null && _render != null && _text.text == _lastRendered;

    public void Refresh()
    {
        if (!OwnsCurrentText) return;
        _lastRendered = _render() ?? string.Empty;
        _text.text = _lastRendered;
        LocalizationManager.EnsureAutoFit(_text);
    }
}
