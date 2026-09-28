using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>경로 선택과 읽기 전용 진행도 맵. 숨은 RoomType/후보 결과는 받지 않는다.</summary>
public sealed class RouteSelectionPanel : MonoBehaviour
{
    private NodeSystem _owner;
    private int _floor, _choiceRow;
    private bool _showMap, _firstGuide, _recordNotice;
    private RouteCardData[] _choices;
    private RouteCardData[] _noticeChoices;
    private int _noticeColumn;
    private IReadOnlyList<RouteMapStop> _history;
    private TMP_Text _region, _title, _position, _guidance, _mapLabel;
    private GameObject _cardRoot, _mapRoot;
    private Image _notebookBackground;
    private readonly List<CardView> _cards = new();
    private float _noticeUntil;
    private bool _guideSpoken;

    private static readonly Color Ink = new Color(0.89f, 0.87f, 0.80f);
    private static readonly Color Muted = new Color(0.58f, 0.65f, 0.61f);
    private static readonly Color Gold = new Color(0.77f, 0.66f, 0.43f);
    private static readonly Color Panel = new Color(0.10f, 0.135f, 0.12f);
    private sealed class CardView
    {
        public GameObject root, picture;
        public Image image;
        public RouteSketchGraphic sketch;
        public TMP_Text title, environment, omen;
        public Button button;
        public NarrationFocus narration;
    }

    public static RouteSelectionPanel Create(Transform parent, NodeSystem owner)
    {
        var root = Rect("RouteSelection", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var view = root.gameObject.AddComponent<RouteSelectionPanel>();
        view._owner = owner;
        view.Build();
        LocalizationManager.OnLanguageChanged += view.RefreshTexts;
        return view;
    }

    private void OnDestroy() => LocalizationManager.OnLanguageChanged -= RefreshTexts;

    public void Present(int currentFloor, RouteCardData[] choices, IReadOnlyList<RouteMapStop> history, bool firstGuide)
    {
        _floor = currentFloor;
        _choiceRow = currentFloor;
        _choices = choices;
        _history = history;
        _firstGuide = firstGuide;
        RefreshTexts();
        if (firstGuide && !_guideSpoken)
        {
            _guideSpoken = true;
            NarrationPlayer.PlayText("이동 후보 3건이 확인되었습니다. 각 기록의 징조를 비교해 다음 경로를 선택하십시오.", this);
        }
    }

    public void ShowRecordAdded(int column)
    {
        _noticeChoices = _choices;
        _noticeColumn = column;
        _recordNotice = true;
        _noticeUntil = Time.unscaledTime + 1.1f;
        RefreshTexts();
        // This 1.1-second transient notice is visual only; a full spoken sentence would be cut by room entry.
    }

    private void Update()
    {
        if (!_recordNotice) return;
        _notebookBackground.color = Color.Lerp(Panel, Gold, 0.35f + Mathf.Sin(Time.unscaledTime * 9f) * 0.25f);
        if (Time.unscaledTime < _noticeUntil) return;
        _recordNotice = false;
        _notebookBackground.color = Panel;
        RefreshTexts();
    }

    private void Build()
    {
        ImageOf(gameObject, new Color(0.055f, 0.085f, 0.073f), true);
        var topLine = At("TopRule", transform, 38, 32, -76, 2, true);
        ImageOf(topLine.gameObject, Gold, false);
        _region = Label(At("Region", transform, 40, 49, 550, 30), 22, Muted);
        _title = Label(At("Heading", transform, 38, 83, 720, 68), 48, Ink);
        _position = Label(At("CurrentPosition", transform, -400, 52, 360, 32, right: true), 24, Muted);
        _position.alignment = TextAlignmentOptions.Right;
        var mapButton = MakeButton(At("MapToggle", transform, -580, 99, 240, 54, right: true), () =>
        {
            if (InvestigatorNotebookController.IsOpen) return;
            NarrationPlayer.StopAll();
            _showMap = !_showMap;
            RefreshTexts();
        });
        _mapLabel = mapButton.GetComponentInChildren<TMP_Text>();
        var notebook = MakeButton(At("Notebook", transform, -320, 99, 280, 54, right: true), InvestigatorNotebookController.Open);
        _notebookBackground = notebook.GetComponent<Image>();
        SystemIconArt.EnsureLeftIcon(notebook, "icon_notebook", 30f, 12f);
        Loc.Set(notebook.GetComponentInChildren<TMP_Text>(), "조사관 수첩");
        _guidance = Label(At("Guidance", transform, 40, 164, -80, 62, true), 24, Muted);
        _guidance.enableAutoSizing = true; _guidance.fontSizeMin = 20; _guidance.fontSizeMax = 24;

        _cardRoot = Rect("RouteCards", transform, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero).gameObject;
        for (int c = 0; c < 3; c++) BuildCard(c);
        _mapRoot = Rect("ProgressMap", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

        var rule = Bottom("FooterRule", transform, 94, 1);
        ImageOf(rule.gameObject, new Color(0.24f, 0.30f, 0.25f), false);
        var footer = Label(Bottom("FixedDestinations", transform, 75, 46), 22, Muted);
        Loc.Bind(footer, () => Loc.Tr("9층 · 화톳불") + "     /     " + Loc.Tr("10층 · 성소"));
        footer.alignment = TextAlignmentOptions.Right;
    }

    private void BuildCard(int column)
    {
        // 1320×1080 오른쪽 영역을 기준으로 세 장을 같은 폭으로 배치한다.
        var rt = Rect("RouteCard" + column, _cardRoot.transform,
            new Vector2(0.03f + column * 0.3233f, 0), new Vector2(0.3233f + column * 0.3233f, 1),
            new Vector2(0, 138), new Vector2(0, -238));
        var card = new CardView { root = rt.gameObject };
        var button = rt.gameObject.AddComponent<Button>();
        ImageOf(rt.gameObject, Panel, true);
        button.targetGraphic = rt.GetComponent<Image>();
        var colors = button.colors; colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.22f, 1.08f); colors.pressedColor = Gold;
        colors.selectedColor = colors.highlightedColor; button.colors = colors;
        card.button = button;
        card.narration = rt.gameObject.AddComponent<NarrationFocus>();
        int selected = column;
        button.onClick.AddListener(() =>
        {
            NarrationPlayer.StopAll();
            _owner.SelectRoute(_choiceRow, selected);
        });
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.31f, 0.37f, 0.30f); outline.effectDistance = Vector2.one;

        var imageFrame = Rect("Illustration", rt, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(10, -405), new Vector2(-10, -10));
        imageFrame.gameObject.AddComponent<RectMask2D>();
        card.picture = imageFrame.gameObject;
        var photo = Rect("Photo", imageFrame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        card.image = photo.gameObject.AddComponent<Image>(); card.image.raycastTarget = false;
        var ratio = photo.gameObject.AddComponent<AspectRatioFitter>();
        ratio.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; ratio.aspectRatio = 2f / 3f;
        var sketch = Rect("RouteSketch", imageFrame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        card.sketch = sketch.gameObject.AddComponent<RouteSketchGraphic>();
        card.sketch.raycastTarget = false; card.sketch.column = column;

        card.title = Label(At("Title", rt, 24, 429, -48, 68, true), 32, Ink);
        card.title.fontStyle = FontStyles.Bold; card.title.enableAutoSizing = true;
        card.title.fontSizeMin = 25; card.title.fontSizeMax = 32;
        card.environment = Label(At("Environment", rt, 24, 516, -48, 72, true), 25, Muted);
        card.omen = Label(At("Omen", rt, 24, 607, -48, 92, true), 25, Ink);
        card.environment.enableAutoSizing = card.omen.enableAutoSizing = true;
        card.environment.fontSizeMin = card.omen.fontSizeMin = 21;
        card.environment.fontSizeMax = card.omen.fontSizeMax = 25;
        _cards.Add(card);
    }

    private void RefreshTexts()
    {
        if (_title == null) return;
        _region.text = RouteCatalog.AreaLabel(Mathf.Min(10, _floor + (!_showMap && _choices != null ? 1 : 0)));
        _title.text = _showMap ? Loc.Tr("진행도 맵") : Loc.Tr("이동 경로");
        _position.text = Loc.Tr("현재 위치 · {0}층", Mathf.Max(1, _floor));
        _mapLabel.text = Loc.Tr(_showMap ? "이동 경로" : "진행도 맵");
        _guidance.text = _recordNotice
            ? Loc.Tr("현장 기록 갱신: 선택 경로가 수첩에 추가되었습니다.")
            : _showMap ? Loc.Tr("지나온 경로")
            : _firstGuide && _choices != null
                ? Loc.Tr("이동 후보 3건이 확인되었습니다. 각 기록의 징조를 비교해 다음 경로를 선택하십시오.")
                : _choices != null ? Loc.Tr("{0}층으로 이어지는 길", _floor + 1) : "";
        var visibleChoices = _choices ?? (_recordNotice ? _noticeChoices : null);
        _cardRoot.SetActive(!_showMap && visibleChoices != null);
        _mapRoot.SetActive(_showMap);
        for (int c = 0; c < _cards.Count; c++)
        {
            var view = _cards[c];
            view.root.SetActive(!_recordNotice || c == _noticeColumn);
            view.button.interactable = _choices != null && !_recordNotice;
            if (visibleChoices == null) continue;
            var data = visibleChoices[c];
            view.narration.SetSource(data.title + "\n" + data.environment + "\n" + data.omen);
            view.title.text = Loc.Tr(data.title);
            view.environment.text = Loc.Tr(data.environment);
            view.omen.text = Loc.Tr(data.omen);
            var sprite = string.IsNullOrEmpty(data.imagePath) ? null : Resources.Load<Sprite>(data.imagePath);
            view.image.gameObject.SetActive(sprite != null);
            view.image.sprite = sprite;
            view.sketch.column = data.id == "FARM-A" ? 0 : data.id == "FARM-C" ? 2 : 1;
            view.sketch.SetVerticesDirty();
            view.sketch.gameObject.SetActive(sprite == null);
        }
        RebuildMap();
    }

    private void RebuildMap()
    {
        foreach (Transform child in _mapRoot.transform)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        if (_history == null) return;
        int index = 0;
        Vector2? previous = null;
        foreach (var stop in _history)
        {
            var point = new Vector2(121 + (stop.column - 1) * 45, 272 + index * 68);
            if (previous.HasValue) DrawConnection(previous.Value, point);
            previous = point;
            DrawStop(index++, stop.floor, stop.column, Loc.Tr(stop.title), Loc.Tr(stop.place), stop.floor == _floor, false);
        }
        if (_floor < 9) DrawStop(index++, 9, 2, Loc.Tr("화톳불"), "", false, true);
        if (_floor < 10) DrawStop(index, 10, 2, Loc.Tr("성소"), "", false, true);
    }

    private void DrawStop(int index, int floor, int column, string title, string place, bool current, bool future)
    {
        float y = 244 + index * 68;
        var strip = At("VisitedFloor" + floor, _mapRoot.transform, 70, y, -140, 59, true);
        if (current) ImageOf(strip.gameObject, new Color(0.16f, 0.22f, 0.18f), false);
        Color color = future ? Muted : current ? Gold : Ink;
        var dot = At("PathMarker", strip, 45 + (column - 1) * 45, 22, 12, 12);
        ImageOf(dot.gameObject, color, false);
        var number = Label(At("Floor", strip, 210, 12, 80, 35), 24, color);
        number.text = floor.ToString("00");
        var label = Label(At("VisitedRoute", strip, 305, 10, -540, 38, true), 25, color);
        label.text = title;
        var location = Label(At("RevealedPlace", strip, -225, 10, 205, 38, right: true), 22, Muted);
        location.text = place;
        location.alignment = TextAlignmentOptions.Right;
    }

    private void DrawConnection(Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var line = At("VisitedConnection", _mapRoot.transform, from.x, from.y, delta.magnitude, 2);
        line.pivot = new Vector2(0, 0.5f);
        line.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        ImageOf(line.gameObject, Gold * new Color(1, 1, 1, 0.55f), false);
        line.SetAsFirstSibling();
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = low; rt.offsetMax = high;
        return rt;
    }

    private static RectTransform At(string name, Transform parent, float x, float y, float width, float height,
        bool stretch = false, bool right = false)
    {
        var anchor = new Vector2(right ? 1 : 0, 1);
        var rt = Rect(name, parent, anchor, stretch ? Vector2.one : anchor, Vector2.zero, Vector2.zero);
        rt.pivot = new Vector2(0, 1); rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(x, -y);
        return rt;
    }

    private static RectTransform Bottom(string name, Transform parent, float top, float height)
    {
        var rt = Rect(name, parent, Vector2.zero, new Vector2(1, 0), Vector2.zero, Vector2.zero);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(-80, height);
        rt.anchoredPosition = new Vector2(40, top);
        return rt;
    }

    private static TMP_Text Label(RectTransform rt, float size, Color color)
    {
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size; text.color = color; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static Image ImageOf(GameObject go, Color color, bool hit)
    {
        var image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.color = color; image.raycastTarget = hit; return image;
    }

    private static Button MakeButton(RectTransform rt, Action callback)
    {
        ImageOf(rt.gameObject, Panel, true);
        var border = rt.gameObject.AddComponent<Outline>(); border.effectColor = Gold; border.effectDistance = Vector2.one;
        var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = rt.GetComponent<Image>();
        button.onClick.AddListener(() => callback());
        var label = Label(Rect("Label", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), 24, Ink);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }
}
