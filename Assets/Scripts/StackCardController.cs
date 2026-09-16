// ============================================================
// StackCardController.cs
// 스택 카드 UI 컨트롤러
// ============================================================
//
// [이 파일이 하는 일]
//   플레이어가 손패에서 카드를 클릭하면 확인/취소 팝업을 보여주고,
//   확인 시 GameManager 에 "카드 사용됨" 을 알립니다.
//   GameManager → PlayerRoleCost 에 스택이 반영됩니다.
//
// [카드 클릭 흐름]
//   1. 카드 클릭 → 카드 크기 확대 + 확인/취소 팝업 표시
//   2. 확인 버튼 → 카드 비활성화 + GameManager.OnCardUsed() 호출
//   3. 취소 버튼 → 원래 크기로 복구 + 팝업 숨김
//
// [어디서 쓰이나요?]
//   - GameManager.myCards[] 슬롯에 할당되어 있음
//   - GameManager.Instance.OnCardUsed(this) 를 내부에서 호출
//
// [인스펙터 설정]
//   - numberText : 카드 중앙 숫자 텍스트 (+3, -2 등)
//   - roleText   : 카드 역할 텍스트 (딜/탱/힐)
//   - descText   : 카드 설명 텍스트
//   - stackType  : 이 카드의 역할 타입 (Dealer/Tank/Support)
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 개별 스택 카드의 UI 및 클릭 처리 컨트롤러.
/// </summary>
public class StackCardController : MonoBehaviour
{
    // ----------------------------------------------------------
    // [UI 참조] — Inspector 에서 연결하세요.
    // ----------------------------------------------------------
    [Header("UI 참조 (Inspector 에서 연결)")]
    [Tooltip("카드 중앙에 표시될 숫자 텍스트 (+3, -2 등)")]
    public TextMeshProUGUI numberText;

    [Tooltip("카드 상단 역할 뱃지 텍스트 (딜/탱/힐)")]
    public TextMeshProUGUI roleText;

    [Tooltip("카드 하단 설명 텍스트")]
    public TextMeshProUGUI descText;

    // ----------------------------------------------------------
    // [카드 데이터]
    // SetupCard() 에서 설정됩니다.
    // ----------------------------------------------------------
    [Header("카드 데이터 (런타임에 자동 설정됨)")]
    [Tooltip("이 카드가 기여하는 역할 스택 타입")]
    public StackType stackType;

    [Tooltip("스택 기여량 (양수=증가, 음수=감소)")]
    public int stackDelta;

    [Tooltip("이 카드의 소유 동료 (성향 계산용)")]
    public FellowData owner;

    // ----------------------------------------------------------
    // [내부 상태]
    // ----------------------------------------------------------

    /// <summary>카드 클릭 전 원래 크기 (클릭 시 확대, 취소 시 복구)</summary>
    private Vector3 originalScale;

    /// <summary>사용한 카드(빈 슬롯)에 표시할 스프라이트</summary>
    private Sprite emptySprite;

    /// <summary>현재 카드에 표시된 숫자 값</summary>
    public int currentNumber { get; private set; }

    /// <summary>카드가 이미 사용되었는지 여부</summary>
    public bool isUsed { get; private set; }

    private Image  myImage;
    private Button myButton;

    // 드로우 애니메이션용 — 중복 호출 시 이전 트윈 정리.
    private Sequence _drawSeq;

    // 손패 한도 초과 — 턴 종료 시 랜덤 파괴 대상 마킹 (하스스톤 주문제작카드 풍 황금 tint)
    public bool IsPendingDiscard { get; private set; }
    private static readonly Color PendingDiscardColor = new Color(1f, 0.84f, 0.2f, 1f); // 황금색

    // 손패 카드 — Wave A 역할별 카드 그림 적용. 색은 스프라이트를 살리는 흰색 기준.
    private static readonly Color NormalCardColor = Color.white;
    private static readonly Color CardDescColor   = new Color(0.90f, 0.90f, 0.85f, 1f);

    /// <summary>roleText 위치에 역할 아이콘 Image 를 보장(없으면 생성, 텍스트 영역을 채움).</summary>
    private static Image EnsureRoleIcon(TextMeshProUGUI roleText)
    {
        if (roleText == null) return null;
        var existing = roleText.transform.Find("RoleIcon");
        if (existing != null) return existing.GetComponent<Image>();

        var go = new GameObject("RoleIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(roleText.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.raycastTarget  = false;
        img.preserveAspect = true;
        return img;
    }

    // ── 발라트로식 토글 선택 (2026-06-08) ──────────────────────────
    /// <summary>현재 카드가 '지정(선택)' 상태인지.</summary>
    public bool isSelected { get; private set; }
    private float _liftBaseY;                         // 선택 기준 Y (해제 시 복귀)
    private const float LiftAmount  = 50f;            // 선택 시 위로 들어올리는 양(px)
    private const float SelectScale = 1.06f;          // 선택 시 살짝 확대
    private const float SelectDur   = 0.15f;          // 트윈 시간
    private static readonly Color SelectedCardColor = new Color(1f, 0.95f, 0.70f, 1f); // 선택 강조(따뜻한 하이라이트)

    // ----------------------------------------------------------
    // 초기화
    // ----------------------------------------------------------
    void Awake()
    {
        myImage       = GetComponent<Image>();
        myButton      = GetComponent<Button>();
        originalScale = transform.localScale;
    }

    void Start()
    {
        // 버튼 클릭 시 OnCardClicked 연결
        myButton.onClick.AddListener(OnCardClicked);
    }

    // ----------------------------------------------------------
    // 카드 세팅 (GameManager.StartMyTurn() 에서 호출)
    // ----------------------------------------------------------

    /// <summary>
    /// 턴 시작 시 카드를 초기화한다.
    /// 숫자 값, 소유자, 역할 타입, UI 텍스트를 설정한다.
    /// </summary>
    /// <param name="number">스택 기여량 (+3, -2 등)</param>
    /// <param name="cardOwner">이 카드의 소유 동료</param>
    public void SetupCard(int number, FellowData cardOwner)
    {
        owner         = cardOwner;
        currentNumber = number;
        stackDelta    = number;

        // 소유자 역할 기반으로 스택 타입 설정
        stackType = (StackType)(int)cardOwner.role;

        // 어두운 가죽 배경에서도 읽히도록 숫자를 밝은 상아색으로 표시한다.
        if (numberText != null)
        {
            numberText.text      = number > 0 ? $"+{number}" : $"{number}";
            numberText.color     = StackCardArt.NumberColor;
            numberText.fontStyle = TMPro.FontStyles.Bold;
            // 카드 안에서 숫자 강조하되 아이콘/설명 공간 확보 위해 축소 + 카드 정중앙 배치. (2026-06-09 디자인 보정)
            numberText.enableAutoSizing = false;
            numberText.fontSize         = 54f;
        }

        // 역할 뱃지 — 문구(딜/탱/힐) 대신 아이콘 표시 (검=딜 / 방패=탱 / 하트=힐). (2026-06-09)
        if (roleText != null)
        {
            roleText.text = ""; // 문구 제거 — 아이콘만 표시
            var roleIcon = EnsureRoleIcon(roleText);
            if (roleIcon != null)
            {
                var sp = StackCardArt.Badge(stackType);
                roleIcon.sprite  = sp;
                roleIcon.enabled = (sp != null);
            }
        }

        // 설명 텍스트 업데이트 — 폰트 작게(숫자 강조), 색 유지. (2026-06-09)
        if (descText != null)
        {
            descText.text  = number > 0 ? Loc.Tr("스택 증가") : Loc.Tr("스택 감소");
            descText.color = CardDescColor;
            descText.enableAutoSizing = false; // 먼저 고정 해제
            descText.fontSize = 18f;           // 기준(한국어) 크기 18
            Loc.AutoFit(descText);             // 영어("Stack Down" 등)가 더 길어도 카드 안에 맞게 자동 축소
        }

        // 카드 상태 초기화
        // SetupCard 가 SetActive(true) 보다 먼저 호출되면 Awake 가 아직이라 참조가 null →
        // 지연 초기화 + null 가드로 NRE 방지. (2026-06-09)
        if (myButton == null) myButton = GetComponent<Button>();
        if (myImage  == null) myImage  = GetComponent<Image>();

        isUsed           = false;
        isSelected       = false;
        IsPendingDiscard = false;
        if (myButton != null) myButton.interactable = true;
        if (myImage  != null)
        {
            // 공격·방어·지원 역할에 맞는 Wave A 카드 배경을 적용한다.
            var cardSprite = StackCardArt.Background(stackType);
            if (cardSprite != null) myImage.sprite = cardSprite;
            myImage.type  = UnityEngine.UI.Image.Type.Simple;
            myImage.color = NormalCardColor;
        }
    }

    /// <summary>손패 한도 초과 시 호출 — 시각 마킹(황금색), 턴 종료 시 랜덤 파괴 대상.</summary>
    public void SetPendingDiscard(bool pending)
    {
        IsPendingDiscard = pending;
        if (myImage != null && !isUsed)
        {
            myImage.color = pending ? PendingDiscardColor : NormalCardColor;
        }
    }

    // ----------------------------------------------------------
    // 카드 클릭 처리
    // ----------------------------------------------------------

    /// <summary>카드 클릭 — 지정/취소 토글 (2026-06-08 발라트로식). 다중 선택 가능.</summary>
    void OnCardClicked()
    {
        if (isUsed) return;

        // PlayerCardPlay 페이즈에서만 지정/취소 가능.
        if (BattleManager.Instance != null
            && BattleManager.Instance.currentPhase != BattlePhase.PlayerCardPlay)
            return;

        if (!isSelected) Select();
        else             Deselect();
    }

    /// <summary>카드 지정 — 위로 들어올리고(발라트로) 스택에 라이브 반영. 순서는 GameManager 가 기록.</summary>
    private void Select()
    {
        isSelected = true;
        var rt = (RectTransform)transform;
        _liftBaseY = rt.anchoredPosition.y;
        rt.DOKill();
        rt.DOAnchorPosY(_liftBaseY + LiftAmount, SelectDur).SetEase(Ease.OutBack);
        transform.DOScale(originalScale * SelectScale, SelectDur);
        if (myImage != null && !IsPendingDiscard) myImage.color = SelectedCardColor;

        AudioManager.Instance?.PlaySfxById(SfxId.CardSelect);
        GameManager.Instance?.OnCardSelected(this);
    }

    /// <summary>카드 취소 — 내려놓고 스택 되돌림.</summary>
    private void Deselect()
    {
        isSelected = false;
        var rt = (RectTransform)transform;
        rt.DOKill();
        rt.DOAnchorPosY(_liftBaseY, SelectDur).SetEase(Ease.OutCubic);
        transform.DOScale(originalScale, SelectDur);
        if (myImage != null && !IsPendingDiscard) myImage.color = NormalCardColor;

        AudioManager.Instance?.PlaySfxById(SfxId.Cancel);
        GameManager.Instance?.OnCardDeselected(this);
    }

    /// <summary>턴 종료 시 GameManager 가 호출 — 지정 카드를 소비(사용됨) 처리. 스택은 이미 라이브 반영됨.</summary>
    public void MarkConsumed()
    {
        if (isUsed) return;
        isSelected = false;
        isUsed     = true;

        var rt = (RectTransform)transform;
        rt.DOKill();
        transform.localScale = originalScale;
        rt.anchoredPosition  = new Vector2(rt.anchoredPosition.x, _liftBaseY); // 내려놓기

        AudioManager.Instance?.PlaySfxById(SfxId.CardPlay);
        if (myImage  != null) myImage.color = new Color(1, 1, 1, 0.45f); // 역할 카드 유지하고 흐리게(사용됨)
        if (myButton != null) myButton.interactable = false;
    }

    // ----------------------------------------------------------
    // 드로우 애니메이션 — 덱 위치에서 손패 자리로 flip + 슬라이드.
    //   fromWorldPos : 시작점 (CardStack 의 world position)
    //   delay        : 카드별 stagger 지연
    // ----------------------------------------------------------
    public void PlayDrawAnimation(Vector3 fromWorldPos, float delay)
    {
        var rt = (RectTransform)transform;
        Vector3 targetPos   = rt.position;
        Vector3 targetScale = (originalScale != Vector3.zero) ? originalScale : rt.localScale;

        // 이전 트윈 정리 (연속 드로우 시 중복 방지)
        _drawSeq?.Kill();

        // 시작 상태: 덱 위치 + Y축 180° 회전(뒤집힌 상태) + 0.7배 스케일
        rt.position         = fromWorldPos;
        rt.localScale       = targetScale * 0.7f;
        rt.localEulerAngles = new Vector3(0f, 180f, 0f);

        _drawSeq = DOTween.Sequence().SetDelay(delay);
        _drawSeq.Append(rt.DOMove(targetPos, 0.5f).SetEase(Ease.OutCubic));
        _drawSeq.Join(rt.DOScale(targetScale, 0.5f).SetEase(Ease.OutBack));
        _drawSeq.Join(rt.DOLocalRotate(Vector3.zero, 0.5f).SetEase(Ease.OutCubic));
    }
}
