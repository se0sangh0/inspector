// BattleCardView.cs
// 전투 카드(MyObject / EnemyObject) 의 이름·HP 점수·HP 색상·데미지 팝업 통합 뷰.
//
// ── 사용 흐름 ───────────────────────────────────────────────────
//   DefaultSetting.SpawnCard() 가 BattleCardView 를 찾아 BindFellow/BindEnemy 호출.
//   이후 FellowData/EnemyData.OnHpChanged 이벤트로 자동 갱신:
//     - HP 스코어 텍스트  "현재/최대"
//     - HP Fill 색상      ratio>0.5 녹 / >0.25 노랑 / 이하 빨강
//     - 데미지 팝업        감소분이 있을 때 DamagePopup 생성
//
// ── 인스펙터 ───────────────────────────────────────────────────
//   nameText           : 카드 위 캐릭터 이름 텍스트
//   hpScoreText        : HP 슬라이더 옆/아래 "현재/최대" 텍스트
//   damagePopupPrefab  : 데미지 숫자가 떠오르는 DamagePopup prefab
//   damagePopupAnchor  : 팝업이 생성될 위치 (보통 카드 중앙). 비어있으면 transform.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleCardView : MonoBehaviour
{
    [Header("기본 표시")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpScoreText;

    [Header("HP Slider (자동 검색 가능)")]
    [SerializeField] private Slider hpSlider;

    [Header("데미지 팝업")]
    [SerializeField] private DamagePopup damagePopupPrefab;
    [SerializeField] private Transform   damagePopupAnchor;

    private Image              _fillImage;
    private FellowData         _fellow;
    private EnemyData          _enemy;
    private int                _lastHp     = -1;
    private int                _lastShield = -1;
    private BattleCardSprites  _sprites; // 같은 GameObject 의 모션 컴포넌트 (있으면 사용)
    private TMP_Text           _countdownText; // 까마귀(소환체) 자폭까지 남은 턴 표시 — hpScoreText 복제로 동적 생성

    /// <summary>바인딩된 동료 데이터 (적 카드면 null).</summary>
    public FellowData Fellow => _fellow;
    /// <summary>바인딩된 적 데이터 (아군 카드면 null).</summary>
    public EnemyData  Enemy  => _enemy;

    // ── 외부에서 호출 ─────────────────────────────────────────────
    public void BindFellow(FellowData fellow)
    {
        Unbind();
        _fellow = fellow;
        if (nameText != null) Loc.Set(nameText, !string.IsNullOrEmpty(fellow.displayName) ? fellow.displayName : fellow.id);
        ResolveSlider();
        fellow.OnHpChanged     += OnFellowHpChanged;
        fellow.OnShieldChanged += OnShieldChanged;
        fellow.OnDamaged       += OnDamaged;
        fellow.OnSkillCast     += HandleSkillCast;
        fellow.OnDotChanged    += OnDotChanged;
        fellow.OnStressChanged += OnStressChip; // 스트레스 상태(압박/패닉) 칩 갱신
        fellow.OnStatusChanged += OnStatusFlagsChanged; // 공포경직/과호흡/중증디버프 칩 갱신
        _lastHp     = fellow.CurrentHp;
        _lastShield = fellow.shield;
        Refresh(_lastHp, fellow.maxHp);
        OnDotChanged(); // 바인딩 직후 현재 DoT 상태로 tint 동기화 (재진입 시 안전망)
    }

    public void BindEnemy(EnemyData enemy)
    {
        Unbind();
        _enemy = enemy;
        if (nameText != null) Loc.Set(nameText, !string.IsNullOrEmpty(enemy.displayName) ? enemy.displayName : enemy.name);
        ResolveSlider();
        enemy.OnHpChanged += OnEnemyHpChanged;
        enemy.OnDamaged   += OnDamaged;
        enemy.OnSkillCast += HandleSkillCast;
        enemy.OnTauntChanged += OnDotChanged; // 도발 상태 칩 갱신
        _lastHp = enemy.CurrentHp;
        Refresh(_lastHp, enemy.maxHp);
        RefreshStatusChips(); // 바인딩 직후 현재 상태로 칩 동기화

        // 까마귀 등 소환체 — HP 라인 아래에 "자폭까지 N턴" 텍스트 동적 생성
        if (enemy.summonLifeTurns > 0)
        {
            EnsureCountdownText();
            enemy.OnLifeTurnsChanged += OnCrowLifeTurnsChanged;
            UpdateCountdownText(enemy.currentLifeTurns);
        }
    }

    private void Unbind()
    {
        if (_fellow != null)
        {
            _fellow.OnHpChanged     -= OnFellowHpChanged;
            _fellow.OnShieldChanged -= OnShieldChanged;
            _fellow.OnDamaged       -= OnDamaged;
            _fellow.OnSkillCast     -= HandleSkillCast;
            _fellow.OnDotChanged    -= OnDotChanged;
            _fellow.OnStressChanged -= OnStressChip;
            _fellow.OnStatusChanged -= OnStatusFlagsChanged;
        }
        if (_enemy  != null)
        {
            _enemy.OnHpChanged -= OnEnemyHpChanged;
            _enemy.OnDamaged   -= OnDamaged;
            _enemy.OnSkillCast -= HandleSkillCast;
            _enemy.OnLifeTurnsChanged -= OnCrowLifeTurnsChanged;
            _enemy.OnTauntChanged -= OnDotChanged;
        }
        _fellow = null; _enemy = null; _lastHp = -1;
        if (_countdownText != null) _countdownText.gameObject.SetActive(false);
        for (int i = 0; i < _chips.Count; i++) if (_chips[i].go != null) _chips[i].go.SetActive(false);
        if (s_hoverOwner == this) { SkillTooltipController.Instance?.Hide(); s_hoverOwner = null; }
    }

    /// <summary>OnDamaged — 쉴드 흡수(노랑)/HP 감소(빨강) popup 분리 표시 + Hit 모션 1회.</summary>
    private void OnDamaged(int absorbed, int hpLoss)
    {
        if (absorbed <= 0 && hpLoss <= 0) return;

        // 둘 다 발생(부분 흡수)이면 노란 popup 을 살짝 위에 띄워 겹침 회피
        bool both = absorbed > 0 && hpLoss > 0;
        if (absorbed > 0) SpawnPopup(absorbed, PopupKind.ShieldAbsorb, extraYOffset: both ? 0.5f : 0f);
        if (hpLoss   > 0) SpawnPopup(hpLoss,   PopupKind.Damage);

        EnsureSprites()?.PlayHit();
    }

    private void OnShieldChanged()
    {
        if (_fellow != null)
        {
            int curShield = _fellow.shield;
            // 실드 증가량 팝업 (감소는 데미지 흡수 — HurtAlly 측 데미지 팝업과 중복 방지 위해 미표시)
            if (_lastShield >= 0 && curShield > _lastShield)
                SpawnPopup(curShield - _lastShield, PopupKind.Shield);
            _lastShield = curShield;
        }
        Refresh(_lastHp, _fellow != null ? _fellow.maxHp : 100);
    }

    private void OnDestroy() => Unbind();

    // ── 상태 칩 호버 툴팁 (전투 카드 = 캐릭터별 월드 캔버스) ──
    //   GraphicRaycaster 경로가 신 InputSystem + 에디터 고DPI 에서 포인터 좌표계와 카메라 투영이 어긋나
    //   호버를 놓치는 문제가 있어, 매 프레임 Input.mousePosition(게임뷰 픽셀 = 카메라 좌표계)로 직접
    //   칩 위 hover 를 판정한다. 좌패널(스크린 오버레이)은 기존 EventSystem 트리거로 동작. (2026-06-09)
    private static BattleCardView s_hoverOwner;
    private void Update()
    {
        if (_statusRow == null || _collect.Count == 0)
        {
            if (s_hoverOwner == this) { SkillTooltipController.Instance?.Hide(); s_hoverOwner = null; }
            return;
        }
        Camera cam = (hpScoreText != null && hpScoreText.canvas != null) ? hpScoreText.canvas.worldCamera : null;
        if (cam == null) cam = Camera.main;
        if (cam == null || Screen.width <= 0 || Screen.height <= 0) return;

        Vector3 mp = Input.mousePosition;
        // 포인터 좌표계(실제 화면)와 카메라 렌더 해상도가 다를 수 있어(고DPI/게임뷰 스케일) → 픽셀 비교는 어긋남.
        // 뷰포트(0~1)로 환산해 비교하면 해상도 불일치가 상쇄됨.
        Vector2 mvp = new Vector2(mp.x / Screen.width, mp.y / Screen.height);
        int hit = -1;
        var corners = new Vector3[4];
        for (int i = 0; i < _collect.Count && i < _chips.Count; i++)
        {
            var go = _chips[i].go;
            if (go == null || !go.activeSelf) continue;
            ((RectTransform)go.transform).GetWorldCorners(corners);
            Vector3 v0 = cam.WorldToViewportPoint(corners[0]);
            Vector3 v2 = cam.WorldToViewportPoint(corners[2]);
            float xmin = Mathf.Min(v0.x, v2.x), xmax = Mathf.Max(v0.x, v2.x);
            float ymin = Mathf.Min(v0.y, v2.y), ymax = Mathf.Max(v0.y, v2.y);
            if (mvp.x >= xmin && mvp.x <= xmax && mvp.y >= ymin && mvp.y <= ymax) { hit = i; break; }
        }
        if (hit >= 0)
        {
            SkillTooltipController.Instance?.ShowText(StatusVisual.TooltipText(_collect[hit].kind, _collect[hit].turns), mp);
            s_hoverOwner = this;
        }
        else if (s_hoverOwner == this)
        {
            SkillTooltipController.Instance?.Hide();
            s_hoverOwner = null;
        }
    }

    private void OnFellowHpChanged(int hp) => HandleHpChanged(hp, _fellow != null ? _fellow.maxHp : 100);
    private void OnEnemyHpChanged(int hp)  => HandleHpChanged(hp, _enemy  != null ? _enemy.maxHp  : 100);

    private void HandleHpChanged(int hp, int maxHp)
    {
        // 데미지(감소)는 OnDamaged 이벤트에서 일괄 처리 — 쉴드 흡수 케이스 누락 방지 + 중복 popup 방지.
        // 여기서는 회복(증가) 케이스만 처리.
        if (_lastHp >= 0 && hp > _lastHp)
            SpawnPopup(hp - _lastHp, PopupKind.Heal);
        _lastHp = hp;
        Refresh(hp, maxHp);
    }

    private BattleCardSprites EnsureSprites()
    {
        if (_sprites == null) _sprites = GetComponent<BattleCardSprites>();
        return _sprites;
    }

    // ── 상태이상 칩 행 — HP 아래 아이콘 가로 나열(겹친 상태 모두 + 남은 턴 수). (2026-06-09 아이콘화) ──
    private RectTransform _statusRow;                              // 칩 컨테이너 (HP 아래, HorizontalLayoutGroup)
    private readonly List<StatusChip> _chips = new List<StatusChip>();
    private readonly List<(StatusKind kind, int turns)> _collect = new List<(StatusKind, int)>();

    private class StatusChip { public GameObject go; public Image bg; public Image icon; public TMP_Text num; public StatusTooltipTrigger trip; }

    // DoT(아군)·도발(적)·스트레스·패닉플래그 변화 시 호출 — 상태 칩 갱신.
    private void OnDotChanged()         => RefreshStatusChips();
    private void OnStressChip(int _)    => RefreshStatusChips();
    private void OnStatusFlagsChanged() => RefreshStatusChips();

    /// <summary>유닛의 현재 상태이상을 HP 아래 아이콘 칩으로 모두 표시(겹친 상태 동시 + 남은 턴). 없으면 숨김.</summary>
    public void RefreshStatusChips()
    {
        EnsureStatusRow();
        if (_statusRow == null) return;

        // 1) 활성 상태 수집 — 표시 순서: 행동영향 큰 일시상태 → 지속상태. turns=0 이면 턴 숫자 미표기(지속).
        _collect.Clear();
        if (_fellow != null)
        {
            if (_fellow.isFrozen)               _collect.Add((StatusKind.Frozen, 1));        // 이번 턴 행동 불가
            if (_fellow.isOverBreathing)        _collect.Add((StatusKind.OverBreathing, 1)); // 다음 턴 코스트 +1
            if (_fellow.dotTurnsLeft > 0)       _collect.Add((StatusKind.Poison, _fellow.dotTurnsLeft));
            var sKind = StatusVisual.FromStress(_fellow.currentStress);
            if (sKind != StatusKind.None)       _collect.Add((sKind, 0));                     // 압박/패닉 (지속)
            if (_fellow.hasSevereDebuff)        _collect.Add((StatusKind.SevereDebuff, 0));   // 전투 종료까지 (지속)
        }
        else if (_enemy != null)
        {
            if (_enemy.tauntTurnsLeft > 0)      _collect.Add((StatusKind.Taunt, _enemy.tauntTurnsLeft));
        }

        // 2) 칩 채우기 (부족하면 생성, 남는 칩은 숨김).
        for (int i = 0; i < _collect.Count; i++)
        {
            var chip = EnsureChip(i);
            var kind = _collect[i].kind; int turns = _collect[i].turns;
            chip.go.SetActive(true);
            var sprite = StatusVisual.IconOf(kind);
            Color col = StatusVisual.ColorOf(kind); col.a = 0.92f;
            if (chip.bg != null) chip.bg.color = col;
            if (sprite != null) { chip.icon.enabled = true; chip.icon.sprite = sprite; chip.icon.color = Color.white; }
            else                  chip.icon.enabled = false; // 아이콘 로드 실패 → 색칩 폴백
            chip.num.text = turns > 0 ? turns.ToString() : "";
            chip.trip?.SetStatus(kind, turns); // 호버 툴팁 내용 갱신
        }
        for (int i = _collect.Count; i < _chips.Count; i++) _chips[i].go.SetActive(false);
    }

    private float _chipSize = 20f; // 칩 한 변 — HP 폰트 크기 기준(HP 텍스트 자식이라 동일 스케일 상속)

    private void EnsureStatusRow()
    {
        if (_statusRow != null || hpScoreText == null) return;
        float fs = hpScoreText.fontSize; if (fs < 1f) fs = 24f;
        _chipSize = fs * 1.0f;
        // 호버 툴팁용 — 캐릭터 월드 캔버스에 GraphicRaycaster 보장(없으면 추가) + 이벤트 카메라 설정.
        var cv = hpScoreText.canvas;
        if (cv != null)
        {
            var gr = cv.GetComponent<GraphicRaycaster>();
            if (gr == null) gr = cv.gameObject.AddComponent<GraphicRaycaster>();
            // ⚠️ 월드 캔버스가 카메라(전방 +Z)와 같은 방향(+Z)을 향해 'reversed'로 판정 → 기본값(true)이면 레이캐스트에서 무시되어 호버가 안 먹음. false 필수.
            gr.ignoreReversedGraphics = false;
            if (cv.renderMode != RenderMode.ScreenSpaceOverlay && cv.worldCamera == null) cv.worldCamera = Camera.main;
        }
        // 전투 카드 HP 텍스트는 월드 캔버스에서 localScale 0.01 로 축소됨 → StatusRow 를 HP 텍스트의 '자식'으로 붙여
        // 같은 스케일을 상속받게 하고, 위치/크기를 HP 폰트와 같은 로컬 단위로 다룬다. (2026-06-09 중첩 스케일 버그 수정)
        var go = new GameObject("StatusRow", typeof(RectTransform));
        go.transform.SetParent(hpScoreText.transform, false);
        go.layer = hpScoreText.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 1f); // HP 글자 중앙 기준, 위→아래 성장
        rt.anchoredPosition = new Vector2(0f, -(fs * 0.75f)); // 글자 바로 아래
        rt.sizeDelta = new Vector2(_chipSize * 5.4f, _chipSize);
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = _chipSize * 0.16f; hlg.childAlignment = TextAnchor.UpperCenter;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        _statusRow = rt;
    }

    private StatusChip EnsureChip(int index)
    {
        while (_chips.Count <= index)
        {
            float inset = _chipSize * 0.08f;
            var go = new GameObject("Chip" + _chips.Count, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_statusRow, false);
            go.layer = _statusRow.gameObject.layer;
            var bg = go.GetComponent<Image>(); bg.raycastTarget = true; // 호버 영역(툴팁)
            var le = go.AddComponent<LayoutElement>(); le.preferredWidth = _chipSize; le.preferredHeight = _chipSize;

            var igo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            igo.transform.SetParent(go.transform, false); igo.layer = go.layer;
            var irt = (RectTransform)igo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(inset, inset); irt.offsetMax = new Vector2(-inset, -inset);
            var iimg = igo.GetComponent<Image>();
            iimg.raycastTarget = false; iimg.preserveAspect = true; iimg.color = Color.white;

            var tgo = new GameObject("Num", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false); tgo.layer = go.layer;
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var num = tgo.AddComponent<TextMeshProUGUI>();
            if (hpScoreText.font != null) num.font = hpScoreText.font;
            num.alignment = TextAlignmentOptions.BottomRight;
            num.fontSize = Mathf.Max(4f, _chipSize * 0.6f);
            num.color = Color.white; num.fontStyle = FontStyles.Bold;
            num.enableWordWrapping = false; num.raycastTarget = false;

            _chips.Add(new StatusChip { go = go, bg = bg, icon = iimg, num = num, trip = StatusTooltipTrigger.Ensure(go) });
        }
        return _chips[index];
    }

    // ── 까마귀 자폭 카운트다운 ─────────────────────────────────────
    //   hpScoreText 를 복제해서 HP 라인 바로 아래에 배치. prefab 수정 없이 동적 생성.
    //   재사용 — 한번 만든 _countdownText 는 SetActive 만 토글.
    private void EnsureCountdownText()
    {
        if (_countdownText != null) { _countdownText.gameObject.SetActive(true); return; }
        if (hpScoreText == null) return;

        var go = Instantiate(hpScoreText.gameObject, hpScoreText.transform.parent);
        go.name = "CrowCountdownText";
        _countdownText = go.GetComponent<TMP_Text>();
        if (_countdownText == null) { Destroy(go); return; }

        // HP 텍스트 바로 아래로 배치. 주의: HP rect 는 스트레치 앵커라 rect.height 가 카드 전체 높이
        // (~2800 캔버스 단위) — rect 기준 오프셋을 쓰면 화면 밖 수천 px 아래로 날아간다 (실측 y=-3102).
        // 실제 렌더된 글리프 경계(textBounds, 로컬)를 기준으로 그 바로 아래에 고정 앵커로 둔다.
        var srcRt = hpScoreText.rectTransform;
        var dstRt = _countdownText.rectTransform;
        hpScoreText.ForceMeshUpdate();
        var glyphs = hpScoreText.textBounds; // hpScoreText 로컬 좌표계
        Vector3 below = srcRt.TransformPoint(new Vector3(glyphs.center.x, glyphs.min.y - hpScoreText.fontSize * 0.4f, 0f));

        dstRt.anchorMin     = dstRt.anchorMax = new Vector2(0.5f, 0.5f); // 비스트레치 — sizeDelta 가 곧 rect
        dstRt.pivot         = new Vector2(0.5f, 1f);                     // 상단 기준 → 글리프 아래로 자라남
        dstRt.localScale    = srcRt.localScale;
        dstRt.localRotation = srcRt.localRotation;
        dstRt.position      = below;
        // HP rect 는 "2/2" 에 맞게 좁음 — 그대로 물려받으면 "자폭까지 3턴" 이 truncate 되어 아예 안 보임!
        dstRt.sizeDelta = new Vector2(hpScoreText.fontSize * 14f, hpScoreText.fontSize * 2.2f);

        // 까마귀 카드는 visualScale 0.6 이라 작게 보임 — HP 보다 크게 + 볼드로 가시성 확보 (사용자: 안 보임 2026-06-11)
        _countdownText.fontSize         = hpScoreText.fontSize * 1.25f; // 1.5 는 옆 까마귀 텍스트와 겹침
        _countdownText.fontStyle        = FontStyles.Bold;
        _countdownText.color            = new Color(1f, 0.55f, 0.25f); // 위협적인 주황
        _countdownText.textWrappingMode = TextWrappingModes.NoWrap;
        _countdownText.overflowMode     = TextOverflowModes.Overflow;
        _countdownText.alignment        = TextAlignmentOptions.Center;
    }

    private void UpdateCountdownText(int lifeTurns)
    {
        if (_countdownText == null) return;
        // 내부 수명은 +1 보정값(소환 턴 끝 감소 상쇄) — 표기는 '남은 온전한 턴'으로 (기획 3턴 → 3부터 시작)
        int shown = Mathf.Max(0, lifeTurns - 1);
        // 까마귀 2마리가 나란히 서면 긴 문구는 옆 텍스트와 겹침 — 짧게
        if (shown > 0) Loc.Set(_countdownText, "자폭 {0}턴", shown);
        else Loc.Set(_countdownText, "자폭!");
        _countdownText.color = shown > 0 ? new Color(1f, 0.55f, 0.25f) : new Color(1f, 0.2f, 0.2f); // 임박 시 빨강
    }

    private void OnCrowLifeTurnsChanged(int lifeTurns) => UpdateCountdownText(lifeTurns);

    /// <summary>
    /// OnSkillCast 이벤트 핸들러. effectType + actor 의 jobClass + isRanged 로 카테고리 결정 후
    /// BattleCardSprites.PlayAttack(cat, skillIndex) 호출. skillIndex 로 Attack/Attack2 분기.
    /// isRanged=true 면 dash 시퀀스 생략, 제자리에서 모션만 재생 (적 원거리 스킬용).
    /// (적군은 jobClass 없음 — null 전달, 적은 보통 skillIndex 0)
    /// </summary>
    private void HandleSkillCast(string effectType, int skillIndex, bool isRanged)
    {
        string jobClass = _fellow != null ? _fellow.jobClass : null;
        var cat = MotionCategoryResolver.Resolve(jobClass, effectType, isRanged);
        EnsureSprites()?.PlayAttack(cat, skillIndex);
    }

    private void Refresh(int hp, int maxHp)
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHp;
            hpSlider.value    = hp;
        }
        if (hpScoreText != null)
        {
            // 아군만 쉴드 보유. 쉴드 표기를 같은 줄에 붙이면 가로로 길어져 인접 캐릭터 텍스트와 겹침 →
            // 실드는 '아랫줄'에 작게 분리해 가로폭을 HP 만큼으로 고정 (2026-06-08).
            int shield = _fellow != null ? _fellow.shield : 0;
            hpScoreText.text = shield > 0
                ? $"{hp}/{maxHp}\n<size=75%><color=#4DA6FF>(+{shield})</color></size>"
                : $"{hp}/{maxHp}";
        }
        UpdateHpColor(hp, maxHp);
    }

    private void ResolveSlider()
    {
        if (hpSlider == null) hpSlider = GetComponentInChildren<Slider>(true);
        if (_fillImage == null && hpSlider != null && hpSlider.fillRect != null)
            _fillImage = hpSlider.fillRect.GetComponent<Image>();
    }

    private void UpdateHpColor(int hp, int maxHp)
    {
        // 안전망 — 첫 호출 시 Slider.fillRect 가 미할당이었거나 캐싱 실패한 경우 재시도.
        if (_fillImage == null) ResolveSlider();
        if (_fillImage == null)
        {
            Debug.LogWarning($"[BattleCardView] '{name}' Slider.fillRect 미할당 — HP 색 변화 동작 불가. Slider 인스펙터 확인.", this);
            return;
        }
        // 체력 비례 색 변경 제거 — 빨강 통일 (2026-06-13 QA)
        _fillImage.color = new Color(0.85f, 0.25f, 0.25f);
    }

    // 카드 본체 시작 오프셋 (월드/캔버스 단위). 상태 영역(이름·HP) 보다 아래에서 출발.
    private const float DamagePopupXOffset    = 0.8f;  // 좌측 쏠림 보정 — 카드 중앙에 맞춤
    private const float DamagePopupYOffset    = 0.3f;
    // 위로 떠오를 거리 (월드 단위). 상태 영역까지만 살짝 올라가도록 작게.
    private const float DamagePopupFloatHeight = 0.8f;

    // ── AOE cascade — 같은 시점에 다수 카드에서 팝업이 동시 폭발하는 가독성 문제 완화 ──
    //   윈도우 안에 연속 스폰되면 인덱스를 누적해 startDelay 를 점차 늘려 cascade 시각화.
    //   윈도우를 벗어나면 인덱스 0 으로 리셋.
    private static float _lastPopupSpawnTime  = -10f;
    private static int   _popupStaggerIndex   = 0;
    private const  float PopupBurstWindow     = 0.15f; // 이 시간 안에 들어오면 같은 burst
    private const  float PopupStaggerStep     = 0.05f; // 인덱스당 추가 지연
    private const  float PopupStaggerMaxDelay = 0.30f; // 최대 지연 캡 (애니 0.9s 안에 끝나야 함)

    private void SpawnPopup(int amount, PopupKind kind, float extraYOffset = 0f)
    {
        if (damagePopupPrefab == null)
        {
            Debug.LogWarning($"[BattleCardView] '{name}' damagePopupPrefab 미연결 — 인스펙터 확인", this);
            return;
        }

        // anchor 우선, 없으면 자식 Canvas, 그것도 없으면 자신의 transform
        Transform parent = damagePopupAnchor;
        if (parent == null)
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            parent = canvas != null ? canvas.transform : transform;
        }

        var popup = Instantiate(damagePopupPrefab, parent);

        // WorldSpace Canvas 안의 형제 텍스트가 0.01 스케일을 쓰면 popup 도 맞춤
        // (안 맞추면 World 1 = Unity 1 스케일로 spawn 돼서 카드 100배 크기로 화면 밖)
        var siblingText = parent.GetComponentInChildren<TMP_Text>(true);
        if (siblingText != null && siblingText.transform != popup.transform)
            popup.transform.localScale = siblingText.transform.localScale;

        // 카드 머리 위 시작점 — localPosition 으로 부모(캔버스) 로컬 좌표계 사용.
        // anchoredPosition 은 WorldSpace 캔버스 스케일 1에서 단위가 어긋나서 사용 안 함.
        popup.transform.localPosition = new Vector3(DamagePopupXOffset, DamagePopupYOffset + extraYOffset, 0f);

        // AOE cascade — burst 윈도우 안이면 인덱스 누적, 아니면 리셋
        float now = Time.unscaledTime;
        if (now - _lastPopupSpawnTime > PopupBurstWindow) _popupStaggerIndex = 0;
        else                                              _popupStaggerIndex++;
        _lastPopupSpawnTime = now;
        float delay = Mathf.Min(_popupStaggerIndex * PopupStaggerStep, PopupStaggerMaxDelay);

        popup.Show(amount, kind, DamagePopupFloatHeight, delay);
    }
}
