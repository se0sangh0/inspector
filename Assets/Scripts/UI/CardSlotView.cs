// CardSlotView.cs
// LeftPanel 의 동료 카드 한 슬롯 (Card_Base_N) 에 부착하는 뷰 컴포넌트.
//
// ── 표시 항목 ───────────────────────────────────────────────────
//   - 이름 (Name)
//   - 아이콘 (Icon_Image)
//   - HP 게이지 (Slider) + 점수(HP_score)
//   - 실드 게이지 (ShieldBarUI 가 Slider 의 Fill Area 자식으로 자동 생성)
//   - 직업 텍스트 (Job)
//   - 성향 태그 (affinityTagBg / affinityTagText: 라벨·색)
//   - 스킬 2개 (Skill1 / Skill2: 이름 + 코스트)
//
// ── HP 바 구조 ──────────────────────────────────────────────────
//   배틀 카드와 동일한 Slider 방식.
//   Bind 시 Slider 의 max/value 를 초기화하고 FellowData.OnHpChanged 를 구독한다.
//   FellowData.InitHp() 는 호출하지 않는다 — 배틀과 LeftPanel 두 곳이 같은
//   FellowData 를 공유하므로, InitHp 의 HpSlider 단일 필드를 덮어쓰지 않기 위함.
//   대신 자체 OnHpChanged 핸들러를 등록해 LeftPanel Slider 만 갱신한다.
//
// ── 바인딩 ──────────────────────────────────────────────────────
//   LeftPanelView 에서 Bind(FellowData) 로 연결한다.
//   FellowData 의 OnHpChanged / OnShieldChanged / OnStressChanged 이벤트를 구독하여
//   값 변경 시 자동 갱신된다. Unbind() 에서 모두 해제.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardSlotView : MonoBehaviour
{
    [Header("기본")]
    [SerializeField] private Image       iconImage;
    [SerializeField] private TMP_Text    nameText;

    [Header("HP / Shield")]
    [SerializeField] private Slider      hpSlider;          // 배틀과 동일한 Slider 방식
    [SerializeField] private TMP_Text    hpScoreText;       // HP_score

    [Header("스트레스 (HP 아래)")]
    [Tooltip("스트레스 게이지 (0~100). HP 바 아래에 배치. 기획자 피드백 #3.")]
    [SerializeField] private Slider      stressSlider;
    [Tooltip("스트레스 점수 텍스트 (현재 스트레스 0~100).")]
    [SerializeField] private TMP_Text    stressScoreText;

    [Header("태그")]
    [SerializeField] private TMP_Text    jobTagText;        // Job 노드 (TMP_Text)
    [SerializeField] private TMP_Text    affinityTagText;   // affinityTagBg > affinityTagText
    [SerializeField] private Image       affinityTagBg;     // 성향 색상 배경

    [Header("스킬")]
    [SerializeField] private TMP_Text    skill1NameText;
    [SerializeField] private TMP_Text    skill1CostText;
    [SerializeField] private TMP_Text    skill2NameText;
    [SerializeField] private TMP_Text    skill2CostText;


    private FellowData  _fellow;
    private ShieldBarUI _shieldUI;
    private Image       _hpFillImage;     // hpSlider.fillRect 의 Image (색상 동적 변경용)
    private Image       _stressFillImage; // stressSlider.fillRect 의 Image (상태별 색상)

    public void Bind(FellowData fellow)
    {
        Unbind();
        _fellow = fellow;
        if (_fellow == null) { gameObject.SetActive(false); return; }

        gameObject.SetActive(true);

        // 정적 정보
        if (nameText != null)      { nameText.text = !string.IsNullOrEmpty(_fellow.displayName) ? Loc.Tr(_fellow.displayName) : _fellow.id; ApplyAutoFit(nameText, 20f, 12f); }
        if (iconImage != null)     iconImage.sprite   = _fellow.portrait != null ? _fellow.portrait : _fellow.fellowSprite;
        // Job 자리에 활성 메타 패시브 표시 (기획 §16). 미배정/미해금이면 "패시브 잠금".
        // 긴 패시브명이 칸을 넘치지 않도록 폰트 자동 축소 (max=기본 20, min=11).
        if (jobTagText != null)
        {
            string pn = MetaPassiveManager.NameOf(_fellow.activePassiveId);
            jobTagText.text = string.IsNullOrEmpty(pn) ? "패시브 잠금" : pn;
            jobTagText.enableAutoSizing = true;
            jobTagText.fontSizeMax = 20f;
            jobTagText.fontSizeMin = 11f;
            jobTagText.enableWordWrapping = false;
            jobTagText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        }
        if (affinityTagText != null) { affinityTagText.text = _fellow.AffinityLabel; ApplyAutoFit(affinityTagText, 15f, 9f); }
        if (affinityTagBg != null) affinityTagBg.color   = _fellow.AffinityColor;

        // 스킬
        var skills = _fellow.GetSkills();
        SetSkill(skill1NameText, skill1CostText, skills.Count > 0 ? skills[0] : null);
        SetSkill(skill2NameText, skill2CostText, skills.Count > 1 ? skills[1] : null);

        // HP Slider 초기화
        if (hpSlider != null)
        {
            int maxHp = _fellow.maxHp > 0 ? _fellow.maxHp : 100;
            hpSlider.maxValue = maxHp;
            hpSlider.value    = _fellow.CurrentHp;

            // Fill Image 캐시 (색상 동적 변경용)
            if (_hpFillImage == null && hpSlider.fillRect != null)
                _hpFillImage = hpSlider.fillRect.GetComponent<Image>();

            // Shield UI 자동 부착 (최초 1회만)
            if (_shieldUI == null)
            {
                _shieldUI = hpSlider.GetComponent<ShieldBarUI>();
                if (_shieldUI == null) _shieldUI = hpSlider.gameObject.AddComponent<ShieldBarUI>();
                _shieldUI.Init(_fellow, hpSlider);
            }
        }

        // 스트레스 Slider 초기화 (HP 아래, #3)
        if (stressSlider != null)
        {
            stressSlider.minValue = 0;
            stressSlider.maxValue = 100;
            stressSlider.value    = _fellow.currentStress;
            if (_stressFillImage == null && stressSlider.fillRect != null)
                _stressFillImage = stressSlider.fillRect.GetComponent<Image>();
            UpdateStress(_fellow.currentStress);
        }

        // 이벤트 구독
        _fellow.OnHpChanged     += OnHpChanged;
        _fellow.OnShieldChanged += OnShieldChanged;
        _fellow.OnStressChanged += OnStressChanged;
        _fellow.OnDotChanged    += OnDotChanged; // 상태(중독) 표기 갱신
        _fellow.OnStatusChanged += OnStatusFlagsChanged; // 공포경직/과호흡/중증디버프 아이콘 갱신

        RefreshHp();
        RefreshStatusIcons();
    }

    public void Unbind()
    {
        if (_fellow == null) return;
        _fellow.OnHpChanged     -= OnHpChanged;
        _fellow.OnShieldChanged -= OnShieldChanged;
        _fellow.OnStressChanged -= OnStressChanged;
        _fellow.OnDotChanged    -= OnDotChanged;
        _fellow.OnStatusChanged -= OnStatusFlagsChanged;
        _fellow = null;
        for (int i = 0; i < _statusChips.Count; i++) if (_statusChips[i].go != null) _statusChips[i].go.SetActive(false);
    }

    private void OnDisable() => Unbind();

    private void OnHpChanged(int hp)
    {
        if (hpSlider    != null) hpSlider.value   = hp;
        if (hpScoreText != null) hpScoreText.text = BuildHpScoreText();
        UpdateHpColor(hp);
    }

    private void OnShieldChanged()
    {
        if (_fellow == null || hpScoreText == null) return;
        hpScoreText.text = BuildHpScoreText();
    }

    // 상태(중독) 부착/해제 시 — HP 숫자 + 상태 아이콘 갱신.
    private void OnDotChanged()
    {
        if (hpScoreText != null) hpScoreText.text = BuildHpScoreText();
        RefreshStatusIcons();
    }

    /// <summary>HP 스코어 텍스트(HP(+실드) 숫자만). 상태이상은 아이콘 행(RefreshStatusIcons)으로 표시.</summary>
    private string BuildHpScoreText()
    {
        if (_fellow == null) return "";
        return FormatHpScore(_fellow.CurrentHp, _fellow.shield);
    }

    // 스트레스 변경 → 게이지 갱신 (#3)
    private void OnStressChanged(int stress) => UpdateStress(stress);

    // 스트레스 바 고정 노란색 — HP 바(녹/노/빨 단계색)와 시각 구분 (사용자 요청).
    private static readonly Color StressBarColor = new Color(0.95f, 0.80f, 0.15f);

    private void UpdateStress(int stress)
    {
        if (stressSlider != null) stressSlider.value = stress;
        if (_stressFillImage != null) _stressFillImage.color = StressBarColor;
        if (stressScoreText != null) stressScoreText.text = $"{stress}/100"; // 압박/패닉은 아이콘 행으로 표시
        RefreshStatusIcons();
    }

    // ── 상태이상 아이콘 행 — 카드 우상단, 겹친 상태 모두 + 남은 턴 수. (2026-06-09) ──
    private RectTransform _statusRow;
    private readonly List<StatusChip> _statusChips = new List<StatusChip>();
    private readonly List<(StatusKind kind, int turns)> _statusCollect = new List<(StatusKind, int)>();
    private class StatusChip { public GameObject go; public Image bg; public Image icon; public TMP_Text num; public StatusTooltipTrigger trip; }

    private void OnStatusFlagsChanged() => RefreshStatusIcons();

    /// <summary>좌패널 카드의 현재 상태이상을 아이콘으로 모두 표시(겹친 상태 + 남은 턴). 없으면 숨김.</summary>
    private void RefreshStatusIcons()
    {
        EnsureStatusRow();
        if (_statusRow == null) return;

        _statusCollect.Clear();
        if (_fellow != null)
        {
            if (_fellow.isFrozen)         _statusCollect.Add((StatusKind.Frozen, 1));
            if (_fellow.isOverBreathing)  _statusCollect.Add((StatusKind.OverBreathing, 1));
            if (_fellow.dotTurnsLeft > 0) _statusCollect.Add((StatusKind.Poison, _fellow.dotTurnsLeft));
            var sKind = StatusVisual.FromStress(_fellow.currentStress);
            if (sKind != StatusKind.None) _statusCollect.Add((sKind, 0));
            if (_fellow.hasSevereDebuff)  _statusCollect.Add((StatusKind.SevereDebuff, 0));
        }

        for (int i = 0; i < _statusCollect.Count; i++)
        {
            var chip = EnsureChip(i);
            var kind = _statusCollect[i].kind; int turns = _statusCollect[i].turns;
            chip.go.SetActive(true);
            var sprite = StatusVisual.IconOf(kind);
            Color col = StatusVisual.ColorOf(kind); col.a = 0.92f;
            if (chip.bg != null) chip.bg.color = col;
            if (sprite != null) { chip.icon.enabled = true; chip.icon.sprite = sprite; chip.icon.color = Color.white; }
            else                  chip.icon.enabled = false;
            chip.num.text = turns > 0 ? turns.ToString() : "";
            chip.trip?.SetStatus(kind, turns); // 호버 툴팁 내용 갱신
        }
        for (int i = _statusCollect.Count; i < _statusChips.Count; i++) _statusChips[i].go.SetActive(false);
    }

    private void EnsureStatusRow()
    {
        if (_statusRow != null) return;
        var go = new GameObject("StatusRow", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f); // 카드 우상단
        rt.anchoredPosition = new Vector2(-6f, -6f);
        rt.sizeDelta = new Vector2(200f, 38f);
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 3f; hlg.childAlignment = TextAnchor.UpperRight;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        _statusRow = rt;
    }

    private StatusChip EnsureChip(int index)
    {
        while (_statusChips.Count <= index)
        {
            var go = new GameObject("Chip" + _statusChips.Count, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_statusRow, false);
            var bg = go.GetComponent<Image>(); bg.raycastTarget = true; // 호버 영역(툴팁)
            var le = go.AddComponent<LayoutElement>(); le.preferredWidth = 36f; le.preferredHeight = 36f;

            var igo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            igo.transform.SetParent(go.transform, false);
            var irt = (RectTransform)igo.transform; irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(3f, 3f); irt.offsetMax = new Vector2(-3f, -3f);
            var iimg = igo.GetComponent<Image>(); iimg.raycastTarget = false; iimg.preserveAspect = true; iimg.color = Color.white;

            var tgo = new GameObject("Num", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)tgo.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var num = tgo.AddComponent<TextMeshProUGUI>();
            if (hpScoreText != null && hpScoreText.font != null) num.font = hpScoreText.font;
            num.alignment = TextAlignmentOptions.BottomRight; num.fontSize = 17f;
            num.color = Color.white; num.fontStyle = FontStyles.Bold;
            num.enableWordWrapping = false; num.raycastTarget = false;

            _statusChips.Add(new StatusChip { go = go, bg = bg, icon = iimg, num = num, trip = StatusTooltipTrigger.Ensure(go) });
        }
        return _statusChips[index];
    }

    private void RefreshHp()
    {
        if (_fellow == null) return;
        if (hpSlider    != null) hpSlider.value   = _fellow.CurrentHp;
        if (hpScoreText != null) hpScoreText.text = FormatHpScore(_fellow.CurrentHp, _fellow.shield);
        UpdateHpColor(_fellow.CurrentHp);
    }

    /// <summary>실드 있으면 "HP(+S)", 없으면 "HP" 만.</summary>
    private static string FormatHpScore(int hp, int shield)
        => shield > 0 ? $"{hp}(+{shield})" : hp.ToString();

    // HP 바 색상 — 체력 비례 색 변경 제거, 빨강 통일 (2026-06-13 QA)
    private void UpdateHpColor(int hp)
    {
        if (_hpFillImage == null) return;
        _hpFillImage.color = new Color(0.85f, 0.25f, 0.25f);
    }

    /// <summary>칸보다 큰 폰트로 텍스트 밑이 잘리지 않도록 오토사이즈(축소) 적용 (#12).</summary>
    private static void ApplyAutoFit(TMP_Text label, float max, float min)
    {
        if (label == null) return;
        label.enableAutoSizing  = true;
        label.fontSizeMax       = max;
        label.fontSizeMin       = min;
        label.enableWordWrapping = false;
        label.overflowMode      = TMPro.TextOverflowModes.Ellipsis;
    }

    // 스킬 박스(Foozle 버튼 프레임) 틴트 — 빈 슬롯은 흐리게. (2026-06-09)
    private static readonly Color SkillBoxEmpty = new Color(0.5f, 0.5f, 0.5f, 0.6f);

    /// <summary>스킬 박스 왼쪽에 아이콘 슬롯(SkillIcon)을 확보. skill.sprite(1-bit 단색) 표시용.</summary>
    private static Image EnsureSkillIcon(GameObject box)
    {
        if (box == null) return null;
        var t = box.transform.Find("SkillIcon") as RectTransform;
        if (t == null)
        {
            var go = new GameObject("SkillIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            t = (RectTransform)go.transform;
            t.SetParent(box.transform, false);
            t.anchorMin = new Vector2(0f, 0.5f); t.anchorMax = new Vector2(0f, 0.5f); t.pivot = new Vector2(0f, 0.5f);
            t.anchoredPosition = new Vector2(7f, 0f);
            t.sizeDelta = new Vector2(26f, 26f);
            var im = go.GetComponent<Image>(); im.raycastTarget = false; im.preserveAspect = true;
        }
        return t.GetComponent<Image>();
    }

    /// <summary>1-bit 단색 아이콘을 스킬 종류별 색으로 틴트.</summary>
    private static Color TintForSkill(string effectType)
    {
        if (string.IsNullOrEmpty(effectType)) return Color.white;
        if (effectType.Contains("Heal"))   return new Color(0.50f, 0.95f, 0.55f); // 초록 — 힐
        if (effectType.Contains("Shield")) return new Color(0.60f, 0.82f, 1.00f); // 하늘 — 방어/실드
        if (effectType.Contains("Damage")) return new Color(1.00f, 0.55f, 0.45f); // 적 — 공격
        if (effectType.Contains("Buff") || effectType.Contains("Taunt")) return new Color(1.00f, 0.85f, 0.40f); // 금 — 버프/도발
        return Color.white;
    }

    // 카드 스킬 박스 = 프레임 + 왼쪽 아이콘(skill.sprite, 종류별 색) + 스킬명. 코스트/효과는 호버 툴팁. (2026-06-11)
    private static void SetSkill(TMP_Text nameLabel, TMP_Text costLabel, SkillData skill)
    {
        GameObject box = (nameLabel != null && nameLabel.transform.parent != null)
            ? nameLabel.transform.parent.gameObject
            : (nameLabel != null ? nameLabel.gameObject : null);
        Image boxImg = box != null ? box.GetComponent<Image>() : null;
        if (boxImg != null) boxImg.raycastTarget = true; // 호버 영역 보장

        if (costLabel != null) costLabel.gameObject.SetActive(false);
        Image icon = EnsureSkillIcon(box);

        if (skill == null)
        {
            if (boxImg != null) boxImg.color = SkillBoxEmpty;   // 프레임 유지, 흐리게
            if (icon != null) icon.enabled = false;
            if (nameLabel != null) { nameLabel.gameObject.SetActive(true); nameLabel.text = "-"; }
            if (box != null) SkillTooltipTrigger.Ensure(box).SetSkills(); // 빈 → 호버 없음
            return;
        }

        if (boxImg != null) boxImg.color = Color.white;          // 프레임 그대로 표시
        if (icon != null)
        {
            if (skill.sprite != null) { icon.enabled = true; icon.sprite = skill.sprite; icon.color = TintForSkill(skill.effectType); }
            else icon.enabled = false;
        }
        if (nameLabel != null)
        {
            nameLabel.gameObject.SetActive(true);
            nameLabel.text = skill.displayName;
            ApplyAutoFit(nameLabel, 15f, 9f); // 칸에 맞게 자동 축소
        }
        if (box != null) SkillTooltipTrigger.Ensure(box).SetSkills(skill); // 칸 전체 호버 툴팁(타입/power/범위/설명)
    }
}
