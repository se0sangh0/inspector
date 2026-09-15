// ============================================================
// UI/SkillTooltip.cs
// 공용 스킬 호버 툴팁 (기획자 피드백 #2·#7)
// ============================================================
//
// [구성]
//   SkillTooltipController : 씬에 1개 존재하는 싱글톤. 단일 툴팁 패널을 보유하고
//                            Show/Hide 로 표시한다. 본문은 리치텍스트 1개에 그린다.
//   SkillTooltipTrigger    : 스킬 라벨 GameObject 에 부착(런타임)되어 마우스 호버 시
//                            보유 스킬 정보를 컨트롤러에 넘긴다.
//
// [사용]
//   - CardSlotView (좌측 파티 카드, #2) : 스킬1/스킬2 라벨에 Attach(label, skill).
//   - FellowCardView (용병소/파티편집, #7) : skillsLabel 에 Attach(label, skills[]).
//
// [요구]
//   - 스킬 라벨 TMP 의 raycastTarget = true (기본값) + 상위 Canvas 에 GraphicRaycaster.
//   - 씬에 SkillTooltipController 가 부착된 패널 1개.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class SkillTooltipController : MonoBehaviour
{
    public static SkillTooltipController Instance { get; private set; }

    [Header("툴팁 패널 (이 컴포넌트가 붙은 오브젝트 또는 자식)")]
    [SerializeField] private RectTransform panel;     // 표시/숨김 대상
    [SerializeField] private TMP_Text      bodyText;  // 명/코스트/효과/설명을 모은 본문

    [Tooltip("커서로부터의 오프셋(px). 패널이 커서를 가리지 않도록.")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -18f);

    // 구조화 엔트리 (이미지1 사양: 아이콘 + 타입 + 위력 + 사거리 + 설명) — 코드 생성, 최대 2개
    private class Entry
    {
        public GameObject root;
        public UnityEngine.UI.Image icon;
        public TMP_Text nameLine;   // 이름 + [타입 · 대상] (리치텍스트)
        //public TMP_Text statsLine;  // 위력 · 사거리 · 코스트
        public TMP_Text desc;
        public GameObject divider;  // 두 번째 스킬 위 구분선
    }
    private readonly List<Entry> _entries = new();
    private const int MaxEntries = 2;

    private void Awake()
    {
        Instance = this;
        BuildEntries();
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>스킬 목록을 받아 구조화 엔트리(아이콘/타입/위력/사거리/설명)로 표시.</summary>
    public void Show(IList<SkillData> skills, Vector2 screenPos)
    {
        if (panel == null || skills == null || skills.Count == 0) return;

        if (bodyText != null) bodyText.gameObject.SetActive(false);

        int shown = 0;
        for (int i = 0; i < skills.Count && shown < _entries.Count; i++)
        {
            if (skills[i] == null) continue;
            FillEntry(_entries[shown], skills[i], isFirst: shown == 0);
            shown++;
        }
        for (int i = shown; i < _entries.Count; i++) _entries[i].root.SetActive(false);
        if (shown == 0) return;

        panel.gameObject.SetActive(true);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        panel.position = screenPos + cursorOffset;
        ClampToScreen();
    }

    /// <summary>임의 텍스트 본문을 커서 근처에 표시(상태이상 툴팁 등 범용).</summary>
    public void ShowText(LocalizedMessage body, Vector2 screenPos)
    {
        if (panel == null || body == null || string.IsNullOrEmpty(body.Key)) return;
        foreach (var e in _entries) e.root.SetActive(false);
        if (bodyText != null) { bodyText.gameObject.SetActive(true); Loc.Bind(bodyText, body.Render); }
        panel.gameObject.SetActive(true);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        panel.position = screenPos + cursorOffset;
        ClampToScreen();
    }

    public void Hide()
    {
        if (panel != null) panel.gameObject.SetActive(false);
    }

    // ----------------------------------------------------------
    // 엔트리 생성/채움 (이미지1 사양)
    // ----------------------------------------------------------
    private void BuildEntries()
    {
        if (panel == null || _entries.Count > 0) return;
        var font = bodyText != null ? bodyText.font : TMP_Settings.defaultFontAsset;

        for (int i = 0; i < MaxEntries; i++)
        {
            var e = new Entry();

            e.root = new GameObject("SkillEntry_" + i, typeof(RectTransform));
            e.root.transform.SetParent(panel, false);
            var rootVlg = e.root.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            rootVlg.spacing = 4; rootVlg.childControlWidth = true; rootVlg.childControlHeight = true;
            rootVlg.childForceExpandWidth = true; rootVlg.childForceExpandHeight = false;

            // 구분선 (두 번째 엔트리에만 표시)
            var div = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            div.transform.SetParent(e.root.transform, false);
            div.GetComponent<UnityEngine.UI.Image>().color = new Color(1f, 1f, 1f, 0.15f);
            var divLe = div.AddComponent<UnityEngine.UI.LayoutElement>();
            divLe.preferredHeight = 2; divLe.minHeight = 2;
            e.divider = div;

            // 헤더 행: [아이콘] [이름/스탯]
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(e.root.transform, false);
            var hlg = header.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.spacing = 10; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            iconGo.transform.SetParent(header.transform, false);
            e.icon = iconGo.GetComponent<UnityEngine.UI.Image>();
            e.icon.preserveAspect = true; e.icon.raycastTarget = false;
            var iconLe = iconGo.AddComponent<UnityEngine.UI.LayoutElement>();
            iconLe.preferredWidth = 52; iconLe.preferredHeight = 52; iconLe.minWidth = 52; iconLe.minHeight = 52;

            var textCol = new GameObject("TextCol", typeof(RectTransform));
            textCol.transform.SetParent(header.transform, false);
            var colVlg = textCol.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            colVlg.spacing = 2; colVlg.childControlWidth = true; colVlg.childControlHeight = true;
            colVlg.childForceExpandWidth = true; colVlg.childForceExpandHeight = false;
            var colLe = textCol.AddComponent<UnityEngine.UI.LayoutElement>();
            colLe.flexibleWidth = 1;

            e.nameLine  = NewTmp("NameLine",  textCol.transform, font, 21, Color.white, bold: true);
            //e.statsLine = NewTmp("StatsLine", textCol.transform, font, 16, new Color(0.85f, 0.78f, 0.55f, 1f), bold: false);

            e.desc = NewTmp("Desc", e.root.transform, font, 17, new Color(0.82f, 0.82f, 0.86f, 1f), bold: false);
            e.desc.textWrappingMode = TextWrappingModes.Normal;

            e.root.SetActive(false);
            _entries.Add(e);
        }
        // bodyText(범용 텍스트)가 항상 맨 위에 오지 않도록 — 엔트리들 뒤(아래)로 보낼 필요 없음. 표시 모드가 상호 배타라 무관.
    }

    private static TMP_Text NewTmp(string name, Transform parent, TMP_FontAsset font, float size, Color color, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = color;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private void FillEntry(Entry e, SkillData s, bool isFirst)
    {
        e.root.SetActive(true);
        e.divider.SetActive(!isFirst);

        bool hasSprite = s.sprite != null;
        e.icon.sprite  = s.sprite;
        e.icon.enabled = hasSprite;
        e.icon.color   = hasSprite ? TypeColor(s.effectType) : Color.clear;

        string name = !string.IsNullOrEmpty(s.displayName) ? s.displayName : s.id;
        Loc.Bind(e.nameLine, () => $"({s.costAmount}){Loc.Tr(name)}  <size=70%><color=#{TypeHex(s.effectType)}>[{Loc.Tr(RangeLabel(s))} | {Loc.Tr(TypeLabel(s.effectType))} | {Loc.Tr(TargetLabel(s.targeting))}]</color></size>");   // '·' 는 NanumGothic 에 없어 □ 로 깨짐 → '|' 사용

        //string stats = $"위력 {s.power}";
        //if (s.effectType == "MixedDamageShield") stats += $" <color=#7FB2FF>+ 실드 {s.shieldPower}</color>";
        //if (s.effectType == "MixedDamageTaunt")  stats += $" <color=#FFC107>+ 도발 {s.tauntTurns}턴</color>";
        //stats += $"  /  사거리 {RangeLabel(s)}  /  코스트 {s.costAmount}";
        //e.statsLine.text = stats;

        bool hasDesc = !string.IsNullOrEmpty(s.description);
        e.desc.gameObject.SetActive(hasDesc);
        if (hasDesc) Loc.Set(e.desc, s.description);
    }

    private static string TypeLabel(string effectType) => effectType switch
    {
        "Heal"              => "회복",
        "Shield"            => "실드",
        "Buff"              => "강화",
        "Debuff"            => "약화",
        "MixedDamageShield" => "공격+실드",
        "MixedDamageTaunt"  => "공격+도발",
        _                   => "공격",
    };

    private static string TypeHex(string effectType) => effectType switch
    {
        "Heal"              => "6FD37F",
        "Shield"            => "7FB2FF",
        "Buff"              => "FFC107",
        "Debuff"            => "C792EA",
        "MixedDamageShield" => "FF8A65",
        "MixedDamageTaunt"  => "FF8A65",
        _                   => "FF6B6B",
    };

    private static Color TypeColor(string effectType) => effectType switch
    {
        "Heal"              => new Color(0.45f, 0.85f, 0.5f, 1f),
        "Shield"            => new Color(0.5f, 0.7f, 1f, 1f),
        "Buff"              => new Color(1f, 0.84f, 0.4f, 1f),
        "Debuff"            => new Color(0.78f, 0.57f, 0.92f, 1f),
        _                   => new Color(1f, 0.45f, 0.45f, 1f),
    };

    /// <summary>사거리 — Damage 계열은 isRanged(원거리/근접), 그 외는 대상 기준(자신/아군 지원).</summary>
    private static string RangeLabel(SkillData s)
    {
        bool damageLike = s.effectType == "Damage" || s.effectType == "MixedDamageShield" || s.effectType == "MixedDamageTaunt"
                          || string.IsNullOrEmpty(s.effectType);
        if (damageLike) return s.isRanged ? "원거리" : "근접";
        return s.targeting == "Self" ? "자신" : "아군 지원";
    }

    private static string TargetLabel(string targeting) => targeting switch
    {
        "Self"        => "자신",
        "SingleEnemy" => "단일 적",
        "AllEnemies"  => "광역 적",
        "SingleAlly"  => "단일 아군",
        "AllAllies"   => "전체 아군",
        _             => "",
    };

    // 화면 밖으로 넘치면 안쪽으로 당김.
    private void ClampToScreen()
    {
        if (panel == null) return;
        var corners = new Vector3[4];
        panel.GetWorldCorners(corners);
        float w = corners[2].x - corners[0].x;
        float h = corners[2].y - corners[0].y;
        var p = panel.position;
        if (corners[2].x > Screen.width)  p.x -= (corners[2].x - Screen.width);
        if (corners[0].x < 0)             p.x -= corners[0].x;
        if (corners[2].y > Screen.height) p.y -= (corners[2].y - Screen.height);
        if (corners[0].y < 0)             p.y -= corners[0].y;
        panel.position = p;
    }
}

/// <summary>
/// 스킬 라벨에 런타임 부착되는 호버 트리거. 호버 시 보유 스킬 정보를 툴팁으로 표시.
/// CardSlotView / FellowCardView 의 Ensure(...) 헬퍼로 부착·갱신한다.
/// </summary>
public class SkillTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private readonly List<SkillData> _skills = new();

    /// <summary>표시할 스킬 목록을 교체.</summary>
    public void SetSkills(params SkillData[] skills)
    {
        _skills.Clear();
        if (skills != null)
            foreach (var s in skills)
                if (s != null) _skills.Add(s);
    }

    public void SetSkills(IEnumerable<SkillData> skills)
    {
        _skills.Clear();
        if (skills != null)
            foreach (var s in skills)
                if (s != null) _skills.Add(s);
    }

    public bool HasAny => _skills.Count > 0;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!HasAny) return;
        SkillTooltipController.Instance?.Show(_skills, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SkillTooltipController.Instance?.Hide();
    }

    /// <summary>대상 GameObject 에 트리거가 없으면 추가하고 반환. 영역 호버를 위해 raycast 대상도 보장.</summary>
    public static SkillTooltipTrigger Ensure(GameObject target)
    {
        if (target == null) return null;
        var t = target.GetComponent<SkillTooltipTrigger>();
        if (t == null) t = target.AddComponent<SkillTooltipTrigger>();
        // 컨테이너(그래픽 없음)에 붙는 경우, 영역 전체가 호버되도록 투명 raycast Image 추가 (#2).
        if (target.GetComponent<UnityEngine.UI.Graphic>() == null)
        {
            var img = target.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // 완전 투명
            img.raycastTarget = true;
        }
        return t;
    }
}

/// <summary>
/// 상태이상 아이콘 칩에 부착되는 호버 트리거. 호버 시 "라벨 — 설명 (N턴 남음)" 을 공용 툴팁에 표시.
/// BattleCardView / CardSlotView 의 칩 생성 시 Ensure 로 부착, 갱신 시 SetStatus 로 내용 교체. (2026-06-09)
/// </summary>
public class StatusTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private StatusKind _kind = StatusKind.None;
    private int        _turns;

    public void SetStatus(StatusKind kind, int turns) { _kind = kind; _turns = turns; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_kind == StatusKind.None) return;
        SkillTooltipController.Instance?.ShowText(StatusVisual.TooltipText(_kind, _turns), eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SkillTooltipController.Instance?.Hide();
    }

    public static StatusTooltipTrigger Ensure(GameObject target)
    {
        if (target == null) return null;
        var t = target.GetComponent<StatusTooltipTrigger>();
        if (t == null) t = target.AddComponent<StatusTooltipTrigger>();
        return t;
    }
}
