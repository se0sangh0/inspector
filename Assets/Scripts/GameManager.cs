// ============================================================
// GameManager.cs
// UI 입력 수신 및 드로우 페이즈 카드 세팅 매니저
// ============================================================
//
// [이 파일이 하는 일]
//   - 플레이어가 사용할 카드 슬롯을 화면에 세팅합니다.
//   - 카드가 사용되면 BattleManager 에 스택 반영을 요청합니다.
//   - 게임 내 덱(카드 뭉치)을 보관하고 관리합니다.
//
// [스택 관리 권한]
//   스택 값 자체는 BattleManager / PlayerRoleCost 가 관리합니다.
//   GameManager 는 카드 선택 이벤트를 중계하는 역할만 합니다.
//
// [어디서 쓰이나요?]
//   - BattleManager.cs : InjectDeck(), RemoveCardsOfFellow() 호출
//   - StackCardController.cs : GameManager.Instance 참조
//
// [연결된 파일]
//   - Core/Singleton.cs : 싱글톤 기반 클래스
//   - StackCardController.cs : 카드 UI 컨트롤러
//   - BattleManager.cs : 전투 흐름 관리
// ============================================================

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// ----------------------------------------------------------
// [StackType 열거형]
// 역할별 스택 종류를 나타냅니다.
// 이 값은 게임 전체에서 공통으로 사용됩니다.
// ----------------------------------------------------------
/// <summary>
/// 역할별 스택 종류.
/// Dealer=딜러, Tank=탱커, Support=서포터
/// </summary>
public enum StackType
{
    Dealer  = 0,  // 딜 (딜러)
    Tank    = 1,  // 탱 (탱커)
    Support = 2   // 힐 (서포터)
}

/// <summary>
/// UI 입력 수신 및 드로우 페이즈 카드 세팅 싱글톤 매니저.
/// GameManager.Instance 로 전역 접근 가능.
/// </summary>
public class GameManager : Singleton<GameManager>
{
    // ----------------------------------------------------------
    // [에셋 설정]
    // ----------------------------------------------------------
    [Header("에셋 설정 (Assets)")]
    [Tooltip("숫자 스프라이트 배열. 인덱스 순서: -5~-1, +1~+5 (총 10장)")]
    public Sprite[] numberSprites;

    [Tooltip("사용한(빈) 카드 슬롯에 표시될 스프라이트")]
    public Sprite emptyCardSprite;

    // ----------------------------------------------------------
    // [UI 연결]
    // myCards : 화면에 표시될 카드 슬롯들 (Inspector 에서 연결)
    // checkButtonBox : 카드 확인/취소 팝업 박스
    // btnConfirm / btnCancel : 확인/취소 버튼
    // ----------------------------------------------------------
    [Header("UI 연결 (UI References)")]
    [Tooltip("화면에 표시될 카드 슬롯 배열. Inspector 에서 연결하세요.")]
    public StackCardController[] myCards;

    [Tooltip("덱(카드 뒷면) 시작 위치. Deck 같은 RectTransform 을 할당. null 이면 드로우 애니메이션 생략.")]
    [SerializeField] private RectTransform cardStackAnchor;

    [Tooltip("카드 영역의 HorizontalLayoutGroup. 드로우 트윈 중 임시 비활성. null 이면 자동 검색.")]
    [SerializeField] private HorizontalLayoutGroup cardAreaLayout;

    [Tooltip("손패 한도 초과 안내 팝업 텍스트 (TMP_Text). null 이면 콘솔 로그만.")]
    [SerializeField] private TMPro.TMP_Text discardNoticeText;

    // 손패 한도 초과 — 이번 턴 동안 누적된 "결과 처리 시 파괴할 카드 수"
    // 사망 동료 카드가 손패에 없었을 때만 누적됨. HandleResultProcessing 에서 처리 후 0 리셋.
    private int _pendingDiscardCount = 0;

    // 사망 처리 중복 호출 가드. BattleManager.allies 가 사망 동료를 계속 보유하기 때문에
    // 매 턴 ProcessDeathAndStress 가 같은 deadFellow 로 RemoveCardsOfFellow 를 재호출할 수 있다.
    // (1st call: handRemoved=1 정상, 2nd call: handRemoved=0 → 잘못된 pending 누적 → 다음 턴 1장 더 파괴됨)
    private readonly HashSet<FellowData> _processedDeadFellows = new();

    [Tooltip("카드 클릭 시 나타나는 확인/취소 팝업 오브젝트")]
    public GameObject checkButtonBox;

    [Tooltip("카드 사용 확인 버튼")]
    public Button btnConfirm;

    [Tooltip("카드 사용 취소 버튼")]
    public Button btnCancel;

    // ----------------------------------------------------------
    // [드로우 덱]
    // 전투 진입 시 BattleManager → DeckBuilder 가 생성하여 주입합니다.
    // 각 카드는 (CardData, 소유자 FellowData) 쌍으로 저장됩니다.
    // ----------------------------------------------------------
    private List<(CardData card, FellowData owner)> drawDeck = new();

    /// <summary>현재 드로우 위치 (몇 번째 카드까지 뽑았는지)</summary>
    private int currentDrawIndex = 0;

    /// <summary>남은 드로우 덱 장수 — 덱 더미 UI(DeckPileView) 표시용.</summary>
    public int RemainingDeckCount => drawDeck != null ? Mathf.Max(0, drawDeck.Count - currentDrawIndex) : 0;

    /// <summary>손패에 낼 수 있는 카드(살아있는 owner의 활성·미사용 카드)가 1장이라도 있는지. (2026-06-13 자동진행 판정용)</summary>
    public bool HasPlayableCards()
    {
        if (myCards == null) return false;
        for (int i = 0; i < myCards.Length; i++)
        {
            var c = myCards[i];
            if (c != null && c.gameObject.activeSelf && c.owner != null && !c.owner.isDead && !c.isUsed)
                return true;
        }
        return false;
    }

    // ── 발라트로식 토글 선택 (2026-06-08) ──────────────────────────
    // 이번 턴에 '지정'된 카드 (클릭 순서 유지 — 내부 관리).
    private readonly List<StackCardController> _selectedOrder = new();
    // 턴 시작 시 잔여 스택(이월분) — 선택 미리보기 재계산의 base (Dealer/Tank/Support).
    private readonly int[] _stackBase = new int[3];

    /// <summary>지정 카드 순서(읽기 전용). 향후 처리 순서 참조용.</summary>
    public IReadOnlyList<StackCardController> SelectedOrder => _selectedOrder;

    // ----------------------------------------------------------
    // 드로우 페이즈 — 카드 슬롯에 카드 세팅
    // drawDeck 에서 카드를 뽑아 myCards 슬롯에 배치합니다.
    // ----------------------------------------------------------

    /// <summary>
    /// 드로우 페이즈: 덱에서 카드를 뽑아 화면 슬롯에 세팅한다.
    /// BattleManager.HandleDrawPhase() 에서 호출됩니다.
    /// </summary>
    public void StartMyTurn()
    {
        AudioManager.Instance?.PlaySfxById(SfxId.CardDraw);

        // 새 턴 — 지정 목록 초기화 + 잔여 스택을 base 로 캡처 (선택 미리보기 재계산 기준). 2026-06-08
        _selectedOrder.Clear();
        if (PlayerRoleCost.Instance != null)
            for (int s = 0; s < 3; s++) _stackBase[s] = PlayerRoleCost.Instance.GetAmount((StackType)s);

        // 손패 동적 상한 — 기획 §03_카드_설계_프레임 §손패/드로우:
        //   "손패 유지량 = 배치된 동료의 수와 동일" (4인 → 4장, 3인 → 3장)
        //   살아있는 동료 수 = aliveCompanionCount.
        // ⚠️ 슬롯 인덱스(i)로 한도를 판정하면, 죽은 동료보다 뒤 인덱스의 살아있는 동료 카드가
        //    잘못 비활성화됨. 한도는 "살아있는 카드 수" 기준으로 판정한다.
        int aliveCompanionCount = PartyManager.Instance != null
            ? PartyManager.Instance.CompanionCount
            : myCards.Length;

        // 현재 살아있는 카드 수 (유지 가능한 카드)
        int liveCardCount = 0;
        for (int i = 0; i < myCards.Length; i++)
        {
            if (myCards[i] != null
                && myCards[i].gameObject.activeSelf
                && myCards[i].owner != null
                && !myCards[i].owner.isDead
                && !myCards[i].isUsed)
            {
                liveCardCount++;
            }
        }

        // 1차 패스: 슬롯 유지 / 비활성 / 새 카드 SetupCard + SetActive (트윈 호출 안 함)
        var newlyDrawnIndices = new List<int>();
        for (int i = 0; i < myCards.Length; i++)
        {
            // ① 살아있는 owner 의 활성·미사용 카드 → 유지 (인덱스 무관)
            if (myCards[i].gameObject.activeSelf
                && myCards[i].owner != null
                && !myCards[i].owner.isDead
                && !myCards[i].isUsed)
            {
                continue;
            }

            // ② 한도 초과 또는 덱 부족 → 빈 슬롯
            if (liveCardCount >= aliveCompanionCount || currentDrawIndex >= drawDeck.Count)
            {
                myCards[i].gameObject.SetActive(false);
                continue;
            }

            // ③ 새 카드 뽑기 — 스택 범위는 카드 owner 의 성향으로 결정
            var (cardData, owner) = drawDeck[currentDrawIndex];
            currentDrawIndex++;
            int stackValue = GenerateStackValue(owner.affinity);
            myCards[i].SetupCard(stackValue, owner);
            myCards[i].gameObject.SetActive(true);
            newlyDrawnIndices.Add(i);
            liveCardCount++;
        }

        // 2차 패스: 트윈 시작 — LayoutGroup 이 자식 위치 강제하므로 일시 비활성.
        if (newlyDrawnIndices.Count > 0 && cardStackAnchor != null)
        {
            // Layout 자동 검색 fallback
            if (cardAreaLayout == null && myCards[newlyDrawnIndices[0]] != null)
                cardAreaLayout = myCards[newlyDrawnIndices[0]].transform.parent?.GetComponent<HorizontalLayoutGroup>();

            if (cardAreaLayout != null)
            {
                // 모든 새 카드 SetActive 된 상태에서 layout 한 번 정확히 갱신 → rt.position 정확
                LayoutRebuilder.ForceRebuildLayoutImmediate(cardAreaLayout.transform as RectTransform);
                cardAreaLayout.enabled = false;
            }

            for (int k = 0; k < newlyDrawnIndices.Count; k++)
            {
                int idx = newlyDrawnIndices[k];
                myCards[idx].PlayDrawAnimation(cardStackAnchor.position, k * 0.08f);
            }

            if (cardAreaLayout != null)
            {
                float totalDuration = (newlyDrawnIndices.Count - 1) * 0.08f + 0.5f + 0.05f;
                StartCoroutine(ReenableLayoutAfter(totalDuration));
            }
        }
    }

    private IEnumerator ReenableLayoutAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (cardAreaLayout != null)
        {
            cardAreaLayout.enabled = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardAreaLayout.transform as RectTransform);
        }
    }

    // ----------------------------------------------------------
    // 공개 API
    // ----------------------------------------------------------

    /// <summary>
    /// 전투 진입 시 BattleManager 가 호출하여 덱을 주입한다.
    /// </summary>
    public void InjectDeck(List<(CardData card, FellowData owner)> deck)
    {
        drawDeck = deck;
        currentDrawIndex = 0;
        _processedDeadFellows.Clear();
        _pendingDiscardCount = 0;
        Debug.Log($"[GameManager] 덱 주입 완료: {drawDeck.Count}장");
    }

    /// <summary>
    /// 사망한 동료의 카드를 drawDeck 에서 전부 제거한다.
    /// BattleManager.ProcessDeathAndStress() 에서 호출됩니다.
    /// </summary>
    public void RemoveCardsOfFellow(FellowData deadFellow)
    {
        if (deadFellow == null) return;

        // 같은 deadFellow 에 대해 중복 호출 차단.
        // BattleManager.allies 에서 사망 동료가 빠지지 않아 매 턴 dyingAllies 에 재포함되는 케이스 방어.
        if (!_processedDeadFellows.Add(deadFellow))
        {
            Debug.Log($"[GameManager] {deadFellow.displayName} — 이미 사망 처리됨, 중복 호출 무시");
            return;
        }

        // 이미 뽑힌 카드 중 해당 동료 카드 수 (인덱스 보정용)
        int removedBeforeIndex = drawDeck
            .Take(currentDrawIndex)
            .Count(entry => entry.owner == deadFellow);

        // drawDeck 에서 해당 동료 카드 전부 제거
        int removedCount = drawDeck.RemoveAll(entry => entry.owner == deadFellow);

        // 이미 지나간 인덱스도 당겨지므로 보정
        currentDrawIndex = Mathf.Max(0, currentDrawIndex - removedBeforeIndex);

        // ✨ 현재 손패에서도 사망 동료 카드 비활성화 (drawDeck 정리만 하면 손패에 남아있음)
        // 이유: 손패에 죽은 동료 카드가 남아있으면
        //   1) 사용자가 클릭 시 NullReferenceException 위험 (사망한 fellow 참조)
        //   2) 자동 턴 종료 로직(AreAllActiveCardsUsed) 이 잘못 판정될 수 있음
        //   3) 시각적으로도 죽은 동료의 카드가 떠있어 혼란
        int handRemoved = 0;
        if (myCards != null)
        {
            for (int i = 0; i < myCards.Length; i++)
            {
                var card = myCards[i];
                if (card == null) continue;
                bool wasActive = card.gameObject.activeSelf;
                bool matched   = card.owner == deadFellow;
                if (matched && wasActive)
                {
                    card.gameObject.SetActive(false);
                    handRemoved++;
                    Debug.Log($"  └ [손패 비활성] 슬롯 {i} (owner={card.owner.displayName}, deadFellow={deadFellow.displayName})");
                }
                else if (wasActive && card.owner != null)
                {
                    Debug.Log($"  - [손패 유지] 슬롯 {i} (owner={card.owner.displayName})");
                }
            }
        }

        Debug.Log($"[GameManager] {deadFellow.displayName} 카드 제거 | 덱 -{removedCount}장, 손패 -{handRemoved}장 | 잔여 덱: {drawDeck.Count}장");

        // 사망 동료 카드가 손패에 없었음 → 손패 한도 초과 상태.
        // 기획 §02 §동료 사망 처리: "턴 종료 시 랜덤으로 N개의 카드가 파괴됩니다."
        if (handRemoved == 0)
        {
            _pendingDiscardCount++;
            MarkExcessHandPending();
            ShowDiscardNotice(_pendingDiscardCount);
        }
    }

    /// <summary>손패 한도 초과 마킹 — 활성·살아있는·미사용 카드 전체를 IsPendingDiscard=true 로.</summary>
    private void MarkExcessHandPending()
    {
        if (myCards == null) return;
        foreach (var card in myCards)
        {
            if (card == null) continue;
            if (card.gameObject.activeSelf
                && card.owner != null
                && !card.owner.isDead
                && !card.isUsed)
            {
                card.SetPendingDiscard(true);
            }
        }
    }

    /// <summary>결과 처리 단계에서 호출 — pending 카운트만큼 마킹된 손패 중 랜덤 파괴.</summary>
    public void ProcessPendingDiscard()
    {
        if (_pendingDiscardCount <= 0) return;
        if (myCards == null) { _pendingDiscardCount = 0; return; }

        var marked = myCards
            .Where(c => c != null && c.gameObject.activeSelf && c.IsPendingDiscard && !c.isUsed)
            .ToList();

        int discardCount = Mathf.Min(_pendingDiscardCount, marked.Count);
        for (int i = 0; i < discardCount; i++)
        {
            int idx = Random.Range(0, marked.Count);
            var card = marked[idx];
            marked.RemoveAt(idx);
            card.gameObject.SetActive(false);
            GameLog.Event("동료 사망으로 손패 카드 1장이 파괴되었다.", LogCategory.Death);
            Debug.Log($"[GameManager] 손패 한도 초과 — 랜덤 파괴: {(card.owner != null ? card.owner.displayName : "owner?")}");
        }

        // 남은 마킹은 해제 (다음 턴 정상 사용 가능)
        foreach (var card in marked)
        {
            if (card != null) card.SetPendingDiscard(false);
        }

        _pendingDiscardCount = 0;
        ClearDiscardNotice();
    }

    private void ShowDiscardNotice(int count)
    {
        string msg = $"턴 종료 시 랜덤으로 {count}개의 카드가 파괴됩니다.";
        Debug.Log($"[GameManager] {msg}");
        if (discardNoticeText != null)
        {
            Loc.Set(discardNoticeText, "턴 종료 시 랜덤으로 {0}개의 카드가 파괴됩니다.", count);
            discardNoticeText.gameObject.SetActive(true);
        }
    }

    private void ClearDiscardNotice()
    {
        if (discardNoticeText != null)
        {
            discardNoticeText.text = "";
            discardNoticeText.gameObject.SetActive(false);
        }
    }

    // ----------------------------------------------------------
    // 카드 지정/취소 (2026-06-08 발라트로식 토글) — StackCardController 가 호출.
    //   스택은 (턴 시작 잔여 base) + (지정 카드 합)을 재계산해 SetAmount 한다.
    //   (RoleCostBase.Add 는 0 클램프라 증분 토글이 깨지므로 전체 재계산 방식.)
    // ----------------------------------------------------------

    /// <summary>카드 지정 시 호출 — 순서 기록 + 스택 라이브 반영.</summary>
    public void OnCardSelected(StackCardController card)
    {
        if (card == null) return;
        if (!_selectedOrder.Contains(card)) _selectedOrder.Add(card);
        RecomputeStackPreview();
        Debug.Log($"[GameManager] 카드 지정 #{_selectedOrder.Count} | {card.stackType} {card.stackDelta:+#;-#;0}");
    }

    /// <summary>카드 지정 취소 시 호출 — 순서에서 제거 + 스택 되돌림.</summary>
    public void OnCardDeselected(StackCardController card)
    {
        if (card == null) return;
        _selectedOrder.Remove(card);
        RecomputeStackPreview();
        Debug.Log($"[GameManager] 카드 지정 취소 | {card.stackType} {card.stackDelta:+#;-#;0}");
    }

    /// <summary>스택 = base(턴 시작 잔여) + 지정 카드 합, 0 이상 클램프해 SetAmount.</summary>
    private void RecomputeStackPreview()
    {
        if (PlayerRoleCost.Instance == null) return;
        int[] sum = (int[])_stackBase.Clone();
        foreach (var c in _selectedOrder)
        {
            if (c == null) continue;
            int idx = (int)c.stackType;
            if (idx >= 0 && idx < 3) sum[idx] += c.stackDelta;
        }
        for (int i = 0; i < 3; i++)
            PlayerRoleCost.Instance.SetAmount((StackType)i, Mathf.Max(0, sum[i]));
    }

    /// <summary>드로우 덱이 끝까지 소진되었는지 여부.</summary>
    private bool IsDeckExhausted()
    {
        return currentDrawIndex >= drawDeck.Count;
    }

    /// <summary>
    /// 탈진 상태 — 덱 고갈 + 활성 손패 모두 사용 (기획 §02 §1) Hand Empty).
    /// BattleManager.HandleResultProcessing 에서 호출되어 페널티 적용 여부 판정.
    /// </summary>
    public bool IsExhausted() => IsDeckExhausted() && AreAllActiveCardsUsed();

    /// <summary>
    /// 화면에 활성화된 카드 슬롯 중 아직 사용 안 한 게 있는지 확인.
    /// 빈 슬롯(SetActive(false))은 무시.
    /// </summary>
    private bool AreAllActiveCardsUsed()
    {
        if (myCards == null || myCards.Length == 0) return false;
        foreach (var card in myCards)
        {
            if (card == null) continue;
            if (!card.gameObject.activeSelf) continue;
            if (!card.isUsed) return false;
        }
        return true;
    }

    /// <summary>
    /// 내 턴 종료. BattleManager.FinishPlayerTurn() 을 호출한다.
    /// </summary>
    /// <summary>
    /// 지정된 카드를 소비(사용됨) 처리 — 턴 종료 공통 처리. 스택은 라이브로 이미 반영됨.
    /// BattleManager.HandlePlayerCardPlay 가 턴 종료(버튼/스페이스바 등 모든 경로) 직후 호출한다.
    /// </summary>
    public void ConsumeSelectedCards()
    {
        if (_selectedOrder.Count == 0) return;
        foreach (var card in _selectedOrder)
            if (card != null) card.MarkConsumed();
        _selectedOrder.Clear();
    }

    public void EndMyTurn()
    {
        Debug.Log("[GameManager] 내 턴 종료 신호 전송.");
        BattleManager.Instance?.FinishPlayerTurn();
    }

    // ----------------------------------------------------------
    // 내부 유틸리티
    // ----------------------------------------------------------

    /// <summary>
    /// 성향 규칙에 따라 0을 제외한 스택 값을 생성한다.
    /// AffinityHelper.GenerateStack() 이 0을 반환하면 최대 20회 재시도.
    /// </summary>
    private int GenerateStackValue(CardAffinity affinity)
    {
        // None 성향은 낙천가(Optimist)와 동일하게 처리
        if (affinity == CardAffinity.None)
            affinity = CardAffinity.Optimist;

        int value;
        int maxRetry = 20;
        do
        {
            value = AffinityHelper.GenerateStack(affinity);
            maxRetry--;
        }
        while (value == 0 && maxRetry > 0);

        return value;
    }

    /// <summary>
    /// stackValue (-5~-1, +1~+5) 에 대응하는 스프라이트를 반환한다.
    /// 배열 인덱스: 0=-5, 1=-4, 2=-3, 3=-2, 4=-1, 5=+1, 6=+2, 7=+3, 8=+4, 9=+5
    /// </summary>
    private Sprite GetSpriteForValue(int value)
    {
        if (numberSprites == null || numberSprites.Length == 0) return null;

        int index = value > 0 ? value + 4 : value + 5;
        index = Mathf.Clamp(index, 0, numberSprites.Length - 1);
        return numberSprites[index];
    }

    // ----------------------------------------------------------
    // [ContextMenu] 무결성 테스트
    // ----------------------------------------------------------

    /// <summary>[에디터 테스트] 현재 덱 상태를 콘솔에 출력한다.</summary>
    [ContextMenu("TEST / 덱 상태 출력")]
    private void TestPrintDeck()
    {
        Debug.Log($"[GameManager] 덱 상태: 전체={drawDeck.Count}장 | 현재 인덱스={currentDrawIndex}");
        for (int i = currentDrawIndex; i < drawDeck.Count; i++)
            Debug.Log($"  [{i}] {drawDeck[i].owner?.displayName ?? "없음"} — {drawDeck[i].card?.id ?? "없음"}");
    }

    /// <summary>[에디터 테스트] 카드 슬롯 연결 상태를 확인한다.</summary>
    [ContextMenu("TEST / 카드 슬롯 연결 확인")]
    private void TestCheckCardSlots()
    {
        Debug.Log($"[GameManager] 카드 슬롯 확인: {myCards?.Length ?? 0}개");
        if (myCards != null)
            for (int i = 0; i < myCards.Length; i++)
                Debug.Log($"  슬롯[{i}]: {(myCards[i] == null ? "NULL ← 연결 필요!" : myCards[i].name)}");

        Debug.Log($"  checkButtonBox: {(checkButtonBox == null ? "NULL ← 연결 필요!" : checkButtonBox.name)}");
        Debug.Log($"  btnConfirm: {(btnConfirm == null ? "NULL ← 연결 필요!" : btnConfirm.name)}");
        Debug.Log($"  btnCancel: {(btnCancel == null ? "NULL ← 연결 필요!" : btnCancel.name)}");
    }
}
