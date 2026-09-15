// ============================================================
// BattleManager.Phases.cs
// 전투 페이즈 핸들러 (파셜 클래스 분리 파일)
// ============================================================
//
// [이 파일이 하는 일]
//   전투의 각 단계(페이즈)를 처리하는 함수들이 담겨 있습니다.
//   BattleManager.cs 의 ExecutePhase() 에서 호출됩니다.
//
// [페이즈 흐름]
//   1. HandleDrawPhase      → 카드를 손에 뽑음
//   2. HandlePlayerCardPlay → 플레이어 입력 대기
//   3. HandleInitiativeCheck → 선공/후공 결정
//   4. HandleActionPhase    → 선공 팀 행동
//   5. HandleActionPhase    → 후공 팀 행동
//   6. HandleResultProcessing → 결과 처리 및 다음 턴 준비
//
// [이 파일에는 없는 것]
//   필드 선언, 초기화, 공개 API → BattleManager.cs
//   전투 로직 (선공 판정, 데미지, 사망 처리) → BattleManager.Combat.cs
// ============================================================

using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// partial 키워드: 이 파일이 BattleManager 클래스의 일부임을 선언
public partial class BattleManager
{
    // ===========================================================
    // 페이즈 핸들러 모음
    // ===========================================================

    // ----------------------------------------------------------
    // 1. 드로우 페이즈
    // 카드를 화면에 뽑아 슬롯에 배치합니다.
    // ----------------------------------------------------------
    private IEnumerator HandleDrawPhase()
    {
        AdvanceTurnCounter();
        Debug.Log($"--- [1] 드로우 페이즈 (턴 {CurrentTurn}) ---");
        GameManager.Instance?.StartMyTurn();
        // 튜토리얼 1단계 — 카드 드로우 (첫 드로우 페이즈에만 트리거)
        TutorialManager.Instance?.TryAdvanceTo(0);
        currentPhase = BattlePhase.PlayerCardPlay;
        yield return null;
    }

    // ----------------------------------------------------------
    // 2. 플레이어 카드 플레이 페이즈
    // 플레이어가 카드를 선택하고 턴 종료를 누를 때까지 대기합니다.
    // 스페이스바로도 턴을 종료할 수 있습니다 (테스트용).
    // ----------------------------------------------------------
    private IEnumerator HandlePlayerCardPlay()
    {
        Debug.Log("--- [2] 플레이어 카드 플레이 (입력 대기 중) ---");
        isPlayerTurnFinishing = false;

        // 손패에 낼 카드가 없고 덱도 0장이면 — 할 게 없으므로 [턴 종료] 없이 자동 진행 (2026-06-13 QA)
        var gm = GameManager.Instance;
        if (gm != null && gm.RemainingDeckCount == 0 && !gm.HasPlayableCards())
        {
            Debug.Log("[BattleManager] 손패/덱 모두 0 — 자동 턴 진행");
            yield return new WaitForSeconds(0.6f); // 짧은 안내 텀
            currentPhase = BattlePhase.InitiativeCheck;
            yield break;
        }

        // 플레이어가 턴 종료를 누르거나 스페이스바를 누를 때까지 대기
        yield return new WaitUntil(() =>
            isPlayerTurnFinishing ||
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        );

        // 지정(발라트로식 토글)한 카드 소비 확정 — 버튼/스페이스바 등 모든 턴종료 경로 공통. 2026-06-08
        GameManager.Instance?.ConsumeSelectedCards();

        currentPhase = BattlePhase.InitiativeCheck;
    }

    // ----------------------------------------------------------
    // 3. 선공 판정 페이즈
    // 전투 1회에 한해 선공을 결정한다. 이미 결정된 경우 유지.
    // ----------------------------------------------------------
    private void HandleInitiativeCheck()
    {
        Debug.Log("--- [3] 선공 판정 ---");
        if (!_initiativeDecided)
        {
            DecideInitiative();
            _initiativeDecided = true;
        }
        else
        {
            Debug.Log($"[선공 판정] 이미 결정됨 — {(isAllyFirstAttacker ? "아군" : "적")} 선공 유지");
        }
        currentPhase = BattlePhase.FirstAction;
    }

    // ----------------------------------------------------------
    // 4~5. 행동 페이즈
    // isAllyTurn=true 이면 아군 행동, false 이면 적군 행동
    // ----------------------------------------------------------
    private IEnumerator HandleActionPhase(bool isAllyTurn, BattlePhase nextPhase)
    {
        string faction = isAllyTurn ? "아군" : "적군";
        Debug.Log($"--- [행동] {faction} 행동 페이즈 ---");
        // 튜토리얼 모달 — 적 행동 페이즈 첫 진입 시
        if (!isAllyTurn) TutorialManager.Instance?.TryShowDialogue(TutorialManager.DialogueId.EnemyTurnIntro);
        yield return StartCoroutine(ExecuteAction(isAllyTurn));

        // 한 팀 행동 직후 전투 종료 조건(적/아군 전멸) 충족 시 상대 팀 행동을 건너뛰고
        // 즉시 결과 처리(→ 전투 종료)로 진입한다 (사용자 요청 — 적 전멸 시 바로 종료).
        currentPhase = CheckBattleEndCondition() ? BattlePhase.ResultProcessing : nextPhase;
    }

    // ----------------------------------------------------------
    // 6. 결과 처리 페이즈
    // 사망 처리, 스택 초기화, 전투 종료 여부 판정 (+ 튜토리얼 4단계 트리거)
    // ----------------------------------------------------------
    private IEnumerator HandleResultProcessing()
    {
        Debug.Log("--- [6] 결과 처리 ---");
        // 튜토리얼 모달 — 첫 결과 처리 진입 시 (미행동 보상 안내)
        TutorialManager.Instance?.TryShowDialogue(TutorialManager.DialogueId.ResultIntro);
        ProcessDeathAndStress();

        // 매 턴 끝: 잔여 스택은 유지, 이월 보너스(+1)만 더해줌
		if (PlayerRoleCost.Instance != null)
		{
    		foreach (var kv in _carryoverBonus)
    		{
        		PlayerRoleCost.Instance.Add(kv.Key, kv.Value);  // SET이 아니라 ADD
    		}
    		_carryoverBonus.Clear();
    		Debug.Log("[결과 처리] 미행동 보너스 반영 (스택 누적 유지)");
		}

        // 미행동자 진형 재정렬은 ExecuteAction(아군 턴) 종료 시 즉시 처리됨
        //   (allies 맨 앞으로 이동 + battleSlotIndex 갱신 + RelayoutNow). 2026-06-05 복원.

        // 손패 한도 초과 — 사망 후 손패에 사망 동료 카드가 없던 경우 누적된 pending count 만큼 랜덤 파괴.
        // 기획 §02 §동료 사망 처리: "사용자가 해당 턴에 사용하지 않으면 결과 처리 단계에서 N개를 랜덤 파괴한다."
        GameManager.Instance?.ProcessPendingDiscard();

        // 탈진 페널티 (기획 §02 §1) Hand Empty / §03 §탈진 — 손패 0 + 덱 0 시 스트레스 페널티)
        ApplyExhaustionPenaltyIfNeeded();

        // 적 스킬 쿨다운 -1 (까마귀 부름 등 cooldownTurns 가진 스킬용).
        foreach (var e in enemies)
        {
            if (e != null) e.TickSkillCooldowns();
        }

        // 도발 카운터 -1 — 워크라이로 걸린 도발 시간 흐름 (2026-05-29 추가)
        foreach (var e in enemies)
        {
            if (e == null || e.tauntTurnsLeft <= 0) continue;
            e.tauntTurnsLeft--;
            if (e.tauntTurnsLeft <= 0)
            {
                e.taunter = null;
                Debug.Log($"[도발 해제] {e.displayName}");
            }
            e.OnTauntChanged?.Invoke(); // 상태 칩 갱신 (감소/해제)
        }

        // DoT 처리 — 매 턴 끝 아군 dot 누적 적용 후 카운터 -1 (기획 §11 §독침 도트)
        foreach (var a in allies)
        {
            if (a == null || a.isDead || a.dotTurnsLeft <= 0) continue;
            int dmg = a.dotPerTurn;
            ApplyDamageToAlly(a, dmg);
            a.dotTurnsLeft--;
            if (a.dotTurnsLeft <= 0)
            {
                a.dotPerTurn = 0;
                a.OnDotChanged?.Invoke(); // UI 초록 tint 해제
                Debug.Log($"[DoT 해제] {a.displayName}");
            }
        }

        // 까마귀 등 소환체 수명 카운터 -1 + 만료 처리 (기획 §11 §3 보스 까마귀)
        ProcessSummonExpiration();

        // 전투 종료 여부 판정
        if (CheckBattleEndCondition())
        {
            currentPhase = BattlePhase.BattleEnd;
            yield return StartCoroutine(HandleBattleEnd());
        }
        else
        {
            // 다음 턴까지 잠깐 대기 후 드로우 페이즈로 돌아감
            yield return new WaitForSeconds(turnTransitionDelay);
            currentPhase = BattlePhase.DrawPhase;
        }
    }

    // ----------------------------------------------------------
    // 7. 전투 종료 페이즈
    // 아군 전멸 → 게임 오버 씬으로 이동
    // 적군 전멸 → 승리 처리
    // ----------------------------------------------------------
    private IEnumerator HandleBattleEnd()
    {
        // 전투 한정 일시 상태(실드) 정리 — 종료 시 비우지 않으면 노드맵 좌패널 게이지에 남아 보인다 (QA ②)
        foreach (var ally in allies)
            if (ally != null && !ally.isDead) ally.ClearShield();

        bool allEnemiesDead = enemies.Count > 0 && enemies.All(a => a.isDead);
        // 튜토리얼 모드 분기 — 기획 §15 + 2026-05-29 5노드 시퀀스
        bool isTutorial = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorial;

        // 승리는 튜토/본편 공통으로 새 결과 화면(BattleResultScreen.ShowVictory) 사용 (2026-06-12).
        // 튜토리얼 패배 계열만 기존 Lose 팝업 연출(1.5s → 표시 → 1.5s) 유지 —
        // 튜토 패배=자동 재시작 / 보스 전멸=완료 종료라 본편 ShowDefeat(타이틀행)와 흐름이 다르다.
        if (isTutorial)
        {
            if (allEnemiesDead)
            {
                // 일반 적 전투 승리 — 본편과 동일한 승리 팝업, [다음으로] 클릭 시 노드맵 복귀
                GameLog.Event("전투 승리!", LogCategory.Reward);
                Debug.Log("[BattleManager] 튜토리얼 일반 전투 승리 — 승리 팝업 후 다음 노드");
                int tutorialSoulGained = enemies.Where(e => e != null).Sum(e => e.soulstoneDrop);
                yield return new WaitForSeconds(resultPopupDelay);
                BattleResultScreen.ShowVictory(tutorialSoulGained, () =>
                {
                    DisplayChange.Instance.ToggleDisplay();
                    AudioManager.Instance?.PlayBgmById(BgmId.NodeMap);
                    // 첫 전투 승리 직후 — 다음 노드 클릭 안내 (1회)
                    TutorialManager.Instance?.TryShowDialogue(TutorialManager.DialogueId.CombatVictory);
                });
                yield break;
            }

            // 패배 계열 — 기존 Lose 팝업 연출 → 자동 재시작 (튜토리얼 처음부터)
            // (2026-06-13 QA: 보스 노드 제거 — 튜토리얼 종료는 화톳불 NodeSystem.HandleRestExit 에서 처리)
            yield return new WaitForSeconds(gameOverDelay);
            DisplayChange.Instance.ToggleResultDisplay(false);
            yield return new WaitForSeconds(gameOverDelay);

            GameLog.Event("다시 도전!", LogCategory.Status);
            Debug.Log("[BattleManager] 튜토리얼 노드 패배 — 파티 재생성 후 같은 씬 리로드");
            yield return new WaitForSeconds(gameOverDelay);
            PartyManager.Instance?.ForceReinitParty();
            SceneTransition.Go("GamePlayScene");
            yield break;
        }

        // 비-튜토리얼 — 전투 종료 상태: 공용 스택 초기화 (실드는 위에서 정리 — 16-A §2 전투 종료 상태)
        if (PlayerRoleCost.Instance != null)
            foreach (StackType role in System.Enum.GetValues(typeof(StackType)))
                PlayerRoleCost.Instance.SetAmount(role, 0);

        // 동시 전멸(양측 전멸) 시 아군 전멸(패배)을 우선한다 (16-A §2 결과 우선순위 — 프로토타입 임시 계약).
        bool allAlliesDead = allies.Count > 0 && allies.All(a => a.isDead);
        bool isVictory     = allEnemiesDead && !allAlliesDead;

        // 마지막 처치 연출 호흡(resultPopupDelay)만 두고 결과 화면 표시.
        yield return new WaitForSeconds(resultPopupDelay);
        if (isVictory)
        {
            GameLog.Event("전투에서 승리했다!", LogCategory.Reward);
            Debug.Log("[BattleManager] 전투 승리!");
            // 보상 — 영혼석만. 처치 시 즉시 누적되어 이미 반영된 상태 (영혼석 먼저 → 기록 — 16-B §3). 팝업엔 합계 표시.
            int soulGained = enemies.Where(e => e != null).Sum(e => e.soulstoneDrop);
            GrantStressRecovery();

            // 현장 관찰 — 지정 인카운터에 예약된 관찰을 1회 소비 (연타·재표시 중복 방지).
            var observation = RunSessionManager.Instance?.ConsumePendingObservation();

            // 전투 사건 — 전투 한 번당 BattleResolved 정확히 1건 (P0-03, 16-B §3).
            // 관찰 문안은 표시 전에 사후 관찰 필드로 기록에 포함한다 (16-A §2).
            int battleFloor = NodeSystem.Current != null ? NodeSystem.Current.CurrentFloor : 0;
            RunSessionManager.Instance?.RecordBattleResolved(
                battleFloor, BuildEnemySummary(), victory: true,
                soulstoneGained: soulGained, observationNotebookText: observation?.notebookText);

            // 보스 클리어 판정 — 보스 tier 적 + RoomType.Boss 노드 둘 다 만족 시 엔딩.
            bool bossWasInBattle = enemies.Any(e => e != null && e.tier == EnemyTier.Boss);
            bool isBossRoom      = NodeSystem.Current != null && NodeSystem.Current.CurrentRoomType == RoomType.Boss;
            if (bossWasInBattle && isBossRoom)
            {
                GameLog.Event("보스를 쓰러트렸다!", LogCategory.Reward);
                Debug.Log("[BattleManager] 🎉 보스 클리어 — 엔딩 진입");
                RunSessionManager.Instance?.RecordRunResolved(victory: true, reachedFloor: battleFloor); // 클리어 기록 (보고서 자료 — P0-05)
                ShowEndingPanel("보스 처치\n\n엔딩");
                yield return new WaitForSeconds(endingDisplayDuration);

                // 클리어 탐사 보고서 (P0-05, 16-A §5) — 후면 딤 + 이번 런 요약.
                // [확인] 시 FinalizeRun 정확히 1회 → 다음 탐사·타이틀 선택.
                RunReportPanel.Show(RunResult.Victory,
                    onConfirmed:       () => RunSessionManager.Instance?.FinalizeRun(RunResult.Victory),
                    onNextExploration: StartNextRun,
                    onTitle:           () => SceneTransition.Go("GameStartScene"));
            }
            else
            {
                // 승리 결과 팝업 — '다음으로' 클릭 시:
                //   영혼석(반영 완료) → BattleResolved(기록 완료) → 지정 인카운터면 현장 관찰 → 다음 이동 (16-A §2)
                BattleResultScreen.ShowVictory(soulGained, () =>
                {
                    System.Action returnToMap = () =>
                    {
                        DisplayChange.Instance.ToggleDisplay();
                        AudioManager.Instance?.PlayBgmById(BgmId.NodeMap);
                    };
                    if (observation != null)
                        PostBattleObservationPanel.Show(observation.title, observation.screenText, observation.imageName, returnToMap);
                    else
                        returnToMap();
                });
            }
        }
        else
        {
            GameLog.Event("전원 쓰러졌다…", LogCategory.Death);
            Debug.Log("[BattleManager] 아군 전멸 — 게임오버 화면 후 타이틀 복귀");
            // 전멸 기록 — BattleResolved(전멸) + RunResolved(전멸·최종 도달 층) 각 1건 (P0-03).
            int wipeFloor = NodeSystem.Current != null ? NodeSystem.Current.CurrentFloor : 0;
            RunSessionManager.Instance?.RecordBattleResolved(
                wipeFloor, BuildEnemySummary(), victory: false, soulstoneGained: 0, observationNotebookText: null);
            RunSessionManager.Instance?.RecordRunResolved(victory: false, reachedFloor: wipeFloor);
            // 전멸 탐사 보고서 (P0-05, 16-A §2·§5) — 임시 게임오버 화면을 실제 보고서로 교체.
            // 후면 딤 + 이번 런 요약. [확인] 시 FinalizeRun 정확히 1회 → 다음 탐사·타이틀 선택.
            RunReportPanel.Show(RunResult.Defeat,
                onConfirmed:       () => RunSessionManager.Instance?.FinalizeRun(RunResult.Defeat),
                onNextExploration: StartNextRun,
                onTitle:           () => SceneTransition.Go("GameStartScene"));
        }
    }

    /// <summary>전투 기록용 조우 대상 요약 — 예: "고블린 2체"/"2x Goblin". 수치 로그는 포함하지 않는다 (16-A §5).</summary>
    private LocalizedMessage BuildEnemySummary()
    {
        var groups = enemies.Where(e => e != null)
            .GroupBy(e => string.IsNullOrEmpty(e.displayName) ? "괴생물체" : e.displayName)
            .Select(g => Loc.Message("{0} {1}체", Loc.Message(g.Key), g.Count()));
        return LocalizedMessage.Join(", ", groups);
    }

    /// <summary>
    /// 엔딩/결과 팝업 표시 (보스 클리어·전멸 공통). 부모 트리가 비활성이어도 보이도록 상위까지 활성화.
    /// message 로 승리/패배 문구를 분기한다 (기획자 피드백 #13 — 패배 시 "보스 처치 엔딩" 오표시 수정).
    /// </summary>
    private void ShowEndingPanel(string message)
    {
        if (endingPanel == null)
        {
            Debug.LogWarning("[BattleManager] endingPanel 미할당 — 엔딩 UI 표시 생략");
            return;
        }
        var t = endingPanel.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }
        endingPanel.SetActive(true);

        // EndingText(단일 TMP) 에 승리/패배 문구 반영
        if (!string.IsNullOrEmpty(message))
        {
            var label = endingPanel.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null) label.text = message;
        }
    }

    /// <summary>
    /// 보스 클리어 뒤 다음 탐사 진입 (FinalizeRun 이후에만 호출).
    /// 초기화는 RunSessionManager.StartNewRun 이 단일 창구로 수행하며 (16-B §4),
    /// 성공했을 때만 GamePlayScene 을 로드한다. 실패 시 타이틀로 복귀.
    /// </summary>
    private void StartNextRun()
    {
        bool ok = RunSessionManager.Instance != null && RunSessionManager.Instance.StartNewRun();
        if (ok)
        {
            SceneManager.LoadScene("GamePlayScene");      // 새 런 (노드맵 재생성)
        }
        else
        {
            Debug.LogError("[BattleManager] 새 런 초기화 실패 — 1층 입력을 열지 않고 타이틀로 복귀");
            SceneTransition.Go("GameStartScene");
        }
    }

    // 기획 §스트레스 §기본 회복 — 전투 승리: -10
    private void GrantStressRecovery()
    {
        foreach (var ally in allies.Where(a => !a.isDead))
            ally.currentStress = Mathf.Max(0, ally.currentStress - 10);
        GameLog.Event("생존한 동료들의 스트레스가 회복되었다 (-10).", LogCategory.Heal);
        Debug.Log("[BattleManager] 전투 승리 보상 — 생존 동료 스트레스 -10");
    }
}
