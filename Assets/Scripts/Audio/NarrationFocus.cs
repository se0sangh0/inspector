using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>경로·선택지에 초점을 주면 해당 항목을 읽는다. 클릭 확정과 분리한다.</summary>
public sealed class NarrationFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private string _source;
    private bool _hovered, _selected;

    public void SetSource(string koreanSource)
    {
        if (_source == koreanSource) return;
        NarrationPlayer.Stop(this);
        _source = koreanSource;
    }

    private bool CanRead()
    {
        if (!isActiveAndEnabled || string.IsNullOrEmpty(_source) || InvestigatorNotebookController.IsOpen) return false;
        foreach (var group in GetComponentsInParent<CanvasGroup>())
            if (group.alpha <= 0f || !group.blocksRaycasts) return false;
        return true;
    }

    private void Read()
    {
        if (CanRead()) NarrationPlayer.PlayText(_source, this);
    }

    public void OnPointerEnter(PointerEventData data) { _hovered = true; Read(); }
    public void OnPointerExit(PointerEventData data) { _hovered = false; if (!_selected) NarrationPlayer.Stop(this); }
    public void OnSelect(BaseEventData data) { _selected = true; Read(); }
    public void OnDeselect(BaseEventData data) { _selected = false; if (!_hovered) NarrationPlayer.Stop(this); }
    private void OnDisable() { _hovered = _selected = false; NarrationPlayer.Stop(this); }
}
