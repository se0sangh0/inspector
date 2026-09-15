// ============================================================
// Mercenary/FellowCardView.cs
// 동료 카드 프리팹에 붙는 공용 뷰 컴포넌트
// ============================================================
//
// [사용 흐름]
//   1. 카드 프리팹(빈 GameObject + RectTransform + Image 배경)을 만든다.
//   2. 자식으로 배치할 UI (배지/이름/성향/별/비용/버튼들)를 만든다.
//   3. 루트에 이 FellowCardView 를 부착한다.
//   4. 인스펙터의 슬롯에 자식들을 연결한다. (모두 선택. 비어있으면 해당 항목만 표시 생략)
//   5. RecruitPanel / PartyEditPanel / GrowthPanel 이 이 프리팹을 Instantiate 해서 Bind() 호출.
//
// [모드]
//   - Recruit       : 후보 카드 — 비용 + 고용 버튼 노출
//   - Reserve       : 예비대 카드 — 정보만, 클릭 가능
//   - PartySlot     : 파티 슬롯 카드 — 정보만, 클릭 가능 (편집용)
//   - SynthesizeSlot: 합성 선택 카드 — 토글 (선택 시 외곽선 강조)
//
// [Outline 강조]
//   selectionOutline (CanvasGroup 또는 Image) 가 연결되어 있으면
//   IsSelected 상태에 따라 alpha/색상 토글된다.
// ============================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum FellowCardMode
{
    Recruit,        // 후보 — 비용 + 고용 버튼 노출
    Reserve,        // 예비대 — 정보만 + 클릭
    PartySlot,      // 파티 슬롯 — 정보만 + 클릭
    SynthesizeSlot, // 합성 슬롯 — 토글 선택
    Sell,           // 예비대 판매 — "판매 (+N)" 라벨
    Revive,         // 교회 부활 — "부활 (N)" 라벨 (성급별 비용)
}

[DisallowMultipleComponent]
public class FellowCardView : MonoBehaviour
{
    // ----------------------------------------------------------
    // 인스펙터 — 카드 안 UI 요소 연결 (모두 선택)
    // ----------------------------------------------------------
    [Header("표시 (Display) — 자식 UI 연결")]
    [Tooltip("역할(딜러/탱커/서포터) 배지 Image. role 색상에 따라 채워짐.")]
    [SerializeField] private Image     roleBadgeImage;

    [Tooltip("역할 텍스트 (선택 — 배지 안에 글자로 표시할 때)")]
    [SerializeField] private TMP_Text  roleLabel;

    [Tooltip("동료 displayName 표시")]
    [SerializeField] private TMP_Text  nameLabel;

    [Tooltip("성향 라벨 (도박사/안전주의자/기회주의자/낙천가)")]
    [SerializeField] private TMP_Text  affinityLabel;

    [Tooltip("성급 표시 (별 1~3개)")]
    [SerializeField] private TMP_Text  starLabel;

    [Tooltip("비용 표시 — Recruit 모드에서만 노출")]
    [SerializeField] private TMP_Text  costLabel;

    [Tooltip("HP 표시 (선택)")]
    [SerializeField] private TMP_Text  hpLabel;

    [Tooltip("보유 스킬 표시 — 줄바꿈으로 구분된 '스킬명 (코스트N)' 형식")]
    [SerializeField] private TMP_Text  skillsLabel;

    [Header("버튼")]
    [Tooltip("메인 액션 버튼 (Recruit:고용 / Reserve:선택 / PartySlot:선택)")]
    [SerializeField] private Button    actionButton;

    [Tooltip("메인 버튼 안 라벨 (Recruit 모드에서 \"고용 (N)\" 표시)")]
    [SerializeField] private TMP_Text  actionButtonLabel;

    [Tooltip("제거 버튼 (선택 — PartyEdit/Reserve 에서 사용 가능)")]
    [SerializeField] private Button    removeButton;

    [Header("선택 강조")]
    [Tooltip("선택 시 표시할 외곽선/배경. CanvasGroup 우선, 없으면 GameObject SetActive.")]
    [SerializeField] private GameObject selectionOutline;

    [Header("역할별 색상")]
    [SerializeField] private Color colorDealer  = new Color(0.78f, 0.20f, 0.27f);
    [SerializeField] private Color colorTanker  = new Color(0.45f, 0.45f, 0.65f);
    [SerializeField] private Color colorSupport = new Color(0.35f, 0.72f, 0.72f);

    // ----------------------------------------------------------
    // 런타임 상태
    // ----------------------------------------------------------
    public FellowData      BoundFellow { get; private set; }
    public FellowCardMode  Mode        { get; private set; }
    public bool            IsSelected  { get; private set; }

    /// <summary>메인 액션 버튼 클릭 콜백. 호출자(패널)가 등록.</summary>
    public event Action<FellowCardView> OnActionClicked;

    /// <summary>제거 버튼 클릭 콜백 (있을 때만).</summary>
    public event Action<FellowCardView> OnRemoveClicked;

    // ----------------------------------------------------------
    // 초기화 — 버튼 리스너 연결
    // ----------------------------------------------------------
    private void Awake()
    {
        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => OnActionClicked?.Invoke(this));
        }
        if (removeButton != null)
        {
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(() => OnRemoveClicked?.Invoke(this));
        }
    }

    // ----------------------------------------------------------
    // 공개 API — 바인딩
    // ----------------------------------------------------------

    /// <summary>
    /// 동료 데이터로 카드 표시를 갱신한다.
    /// fellow=null 이면 빈 슬롯 모드 (회색 외곽 / 라벨 비움).
    /// </summary>
    public void Bind(FellowData fellow, FellowCardMode mode, int? costOverride = null)
    {
        BoundFellow = fellow;
        Mode        = mode;
        IsSelected  = false;

        if (selectionOutline != null) selectionOutline.SetActive(false);

        if (fellow == null)
        {
            ShowEmpty();
            return;
        }

        // ── 초상화/스프라이트 (기획자 피드백 #6) ──
        // roleBadgeImage 를 역할 색배지 대신 동료 초상화/스프라이트로 표시.
        // 초상화 없으면(둘 다 null) 기존 역할 색 배지로 폴백.
        if (roleBadgeImage != null)
        {
            Sprite face = fellow.portrait != null ? fellow.portrait : fellow.fellowSprite;
            if (face != null)
            {
                roleBadgeImage.sprite        = face;
                roleBadgeImage.color         = Color.white;   // 스프라이트 원색
                roleBadgeImage.preserveAspect = true;
            }
            else
            {
                roleBadgeImage.sprite = null;
                roleBadgeImage.color  = ColorForRole(fellow.role);
            }
        }
        if (roleLabel != null)
            Loc.Bind(roleLabel, () => ShortRoleLabel(fellow.role));

        // ── 텍스트 ──
        if (nameLabel != null)     Loc.Set(nameLabel, !string.IsNullOrEmpty(fellow.displayName) ? fellow.displayName : fellow.id);
        if (affinityLabel != null) Loc.Set(affinityLabel, fellow.AffinityLabel);
        if (starLabel != null)     starLabel.text     = new string('★', Mathf.Clamp(fellow.starLevel, 1, 3));
        if (hpLabel != null)       hpLabel.text       = $"HP {fellow.maxHp}";
        if (skillsLabel != null)
        {
            Loc.Bind(skillsLabel, () => BuildSkillsText(fellow));
            // 스킬 호버 툴팁 (#7) — 보유 스킬 전체 정보 표시
            SkillTooltipTrigger.Ensure(skillsLabel.gameObject).SetSkills(fellow.GetSkills());
            UpdateSkillIcons(fellow.GetSkills()); // 줄별 스킬 아이콘 (1-bit, 종류별 색)
        }

        // ── 모드별 버튼/비용 표시 ──
        int costShown = costOverride ?? fellow.recruitCost;
        bool showCost   = mode == FellowCardMode.Recruit;
        bool showAction = mode != FellowCardMode.Reserve || true; // 모든 모드에서 메인 클릭 허용 — false 강제 모드 없음
        bool showRemove = mode == FellowCardMode.PartySlot
                          || mode == FellowCardMode.Reserve
                          || mode == FellowCardMode.SynthesizeSlot;

        if (costLabel != null)
        {
            costLabel.gameObject.SetActive(showCost);
            costLabel.text = showCost ? costShown.ToString() : string.Empty;
        }
        if (actionButton != null)
        {
            actionButton.gameObject.SetActive(showAction);
            if (actionButtonLabel != null)
                Loc.Bind(actionButtonLabel, () => ActionLabelForMode(mode, costShown));
        }
        if (removeButton != null)
            removeButton.gameObject.SetActive(showRemove);
    }

    /// <summary>빈 슬롯 표시 — 라벨 비우고 버튼/외곽선 끔.</summary>
    public void BindEmpty(FellowCardMode mode)
    {
        Bind(null, mode);
    }

    /// <summary>선택 토글 — SynthesizeSlot / PartySlot 등에서 호출.</summary>
    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (selectionOutline != null) selectionOutline.SetActive(selected);
        ApplySelectionLift(selected);
    }

    // 선택 시 카드 본체가 위로 솟구치는 양 (px).
    private const float SelectedLiftY = 20f;
    // 자식들의 anchoredPosition.y 에 가산된 offset 추적 — Bind 후 재선택 시 중복 가산 방지.
    private bool _liftApplied = false;

    // CardPrefab 자체엔 LayoutGroup 이 없어 자식 anchoredPosition 자유.
    // 부모(PartySlot)의 Layout 은 카드 본체 위치를 강제하므로 자식 전체를 +Y 로 이동.
    private void ApplySelectionLift(bool selected)
    {
        if (_liftApplied == selected) return;
        float delta = selected ? SelectedLiftY : -SelectedLiftY;
        var rt = transform as RectTransform;
        if (rt == null) return;
        for (int i = 0; i < rt.childCount; i++)
        {
            if (rt.GetChild(i) is RectTransform child)
            {
                var pos = child.anchoredPosition;
                pos.y += delta;
                child.anchoredPosition = pos;
            }
        }
        _liftApplied = selected;
    }

    /// <summary>인터랙티브 가능 여부 — 영혼석 부족 등 외부 조건으로 끌 때.</summary>
    public void SetInteractable(bool interactable)
    {
        if (actionButton != null) actionButton.interactable = interactable;
        // 기획 §8-3 — 비용 부족 시 비용 표기를 빨간색으로 경고
        if (costLabel != null) costLabel.color = interactable ? Color.white : new Color(0.9f, 0.25f, 0.25f, 1f);
    }

    // ----------------------------------------------------------
    // 내부 헬퍼
    // ----------------------------------------------------------
    private void ShowEmpty()
    {
        if (nameLabel != null)     nameLabel.text     = string.Empty;
        if (affinityLabel != null) affinityLabel.text = string.Empty;
        if (starLabel != null)     starLabel.text     = string.Empty;
        if (costLabel != null)     costLabel.text     = string.Empty;
        if (hpLabel != null)       hpLabel.text       = string.Empty;
        if (roleLabel != null)     roleLabel.text     = string.Empty;
        if (skillsLabel != null)
        {
            skillsLabel.text = string.Empty;
            SkillTooltipTrigger.Ensure(skillsLabel.gameObject).SetSkills(); // 빈 → 호버 표시 안 함
            HideSkillIcons();
        }
        if (roleBadgeImage != null)
        {
            roleBadgeImage.sprite = null;                       // 초상화 해제 → 빈 슬롯 회색
            roleBadgeImage.color  = new Color(1, 1, 1, 0.15f);
        }

        // 합성 슬롯(SynthesizeSlot)의 빈 카드는 클릭으로 동료 선택 팝업을 열어야 하므로 ActionButton 유지.
        // 그 외 모드(PartySlot/Reserve/RecruitCandidate)는 빈 슬롯에서 액션 불필요 → 비활성.
        bool keepAction = Mode == FellowCardMode.SynthesizeSlot;
        if (actionButton != null)
        {
            actionButton.gameObject.SetActive(keepAction);
            if (actionButtonLabel != null)
                actionButtonLabel.text = keepAction ? Loc.Tr("+ 동료 선택") : string.Empty;
        }
        if (removeButton != null) removeButton.gameObject.SetActive(false);
    }

    private Color ColorForRole(CompanionRole role) => role switch
    {
        CompanionRole.Dealer  => colorDealer,
        CompanionRole.Tanker  => colorTanker,
        CompanionRole.Support => colorSupport,
        _                     => Color.white,
    };

    private static string ShortRoleLabel(CompanionRole role) => role switch
    {
        CompanionRole.Dealer  => Loc.Tr("딜러"),
        CompanionRole.Tanker  => Loc.Tr("탱커"),
        CompanionRole.Support => Loc.Tr("서포터"),
        _                     => "?",
    };

    private static string ActionLabelForMode(FellowCardMode mode, int cost) => mode switch
    {
        FellowCardMode.Recruit        => Loc.Tr("고용 ({0})", cost),
        FellowCardMode.Reserve        => Loc.Tr("선택"),
        FellowCardMode.PartySlot      => Loc.Tr("선택"),
        FellowCardMode.SynthesizeSlot => Loc.Tr("선택"),
        FellowCardMode.Sell           => Loc.Tr("판매 (+{0})", cost),
        FellowCardMode.Revive         => Loc.Tr("부활 ({0})", cost),
        _                             => Loc.Tr("선택"),
    };

    /// <summary>보유 스킬을 "이름 (코스트N)" 형식으로 줄바꿈 결합. 스킬 없으면 빈 문자열.</summary>
    private static string BuildSkillsText(FellowData fellow)
    {
        if (fellow == null || !fellow.HasSkills) return string.Empty;
        var skills = fellow.GetSkills();
        if (skills == null || skills.Count == 0) return string.Empty;

        var lines = new System.Collections.Generic.List<string>(skills.Count);
        foreach (var s in skills)
        {
            if (s == null) continue;
            string name = !string.IsNullOrEmpty(s.displayName) ? Loc.Tr(s.displayName) : s.id;
            lines.Add($"{name} ({s.costAmount})");
        }
        return string.Join("\n", lines);
    }

    // ── 스킬 아이콘 (줄별, 1-bit 단색 → 종류별 색 틴트, 텍스트 왼쪽에 배치) ──
    private readonly System.Collections.Generic.List<UnityEngine.UI.Image> _skillIcons = new System.Collections.Generic.List<UnityEngine.UI.Image>();

    private void UpdateSkillIcons(System.Collections.Generic.List<SkillData> skills)
    {
        if (skillsLabel == null) return;
        skillsLabel.alignment = TMPro.TextAlignmentOptions.TopLeft;
        float fs = skillsLabel.fontSize > 0 ? skillsLabel.fontSize : 15f;
        float lineH = fs * 1.25f;
        float iconSize = Mathf.Clamp(lineH - 2f, 14f, 24f);
        skillsLabel.margin = new Vector4(iconSize + 6f, 0f, 0f, 0f); // 텍스트를 아이콘 폭만큼 들여쓰기
        int n = skills != null ? skills.Count : 0;
        for (int i = 0; i < 2; i++)
        {
            UnityEngine.UI.Image icon = i < _skillIcons.Count ? _skillIcons[i] : null;
            if (icon == null)
            {
                var go = new GameObject("SkillIcon" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                var it = (RectTransform)go.transform; it.SetParent(skillsLabel.transform, false);
                it.anchorMin = new Vector2(0f, 1f); it.anchorMax = new Vector2(0f, 1f); it.pivot = new Vector2(0f, 1f);
                var im = go.GetComponent<UnityEngine.UI.Image>(); im.raycastTarget = false; im.preserveAspect = true;
                icon = im; _skillIcons.Add(im);
            }
            var irt = icon.rectTransform; irt.sizeDelta = new Vector2(iconSize, iconSize);
            irt.anchoredPosition = new Vector2(1f, -i * lineH - (lineH - iconSize) * 0.5f);
            var s = (i < n) ? skills[i] : null;
            if (s != null && s.sprite != null) { icon.enabled = true; icon.sprite = s.sprite; icon.color = SkillTint(s.effectType); }
            else icon.enabled = false;
        }
    }

    private void HideSkillIcons() { foreach (var ic in _skillIcons) if (ic != null) ic.enabled = false; }

    private static Color SkillTint(string et)
    {
        if (string.IsNullOrEmpty(et)) return Color.white;
        if (et.Contains("Heal"))   return new Color(0.50f, 0.95f, 0.55f); // 초록 — 힐
        if (et.Contains("Shield")) return new Color(0.60f, 0.82f, 1.00f); // 하늘 — 방어/실드
        if (et.Contains("Damage")) return new Color(1.00f, 0.55f, 0.45f); // 적 — 공격
        if (et.Contains("Buff") || et.Contains("Taunt")) return new Color(1.00f, 0.85f, 0.40f); // 금 — 버프/도발
        return Color.white;
    }
}
