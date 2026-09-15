// ============================================================
// MoveScene.cs
// 씬 전환 버튼 핸들러
// ============================================================
//
// [이 파일이 하는 일]
//   버튼을 누르면 지정된 씬으로 이동합니다.
//   현재는 "InGameScene" (전투 씬) 으로 이동하는 기능만 있습니다.
//
// [어디서 쓰이나요?]
//   - 메인 메뉴 씬의 "게임 시작" 버튼의 onClick 이벤트에 연결
//
// [씬 이름 확인]
//   File → Build Settings 에 씬이 등록되어 있어야 합니다.
//   씬 이름이 다르면 SceneManager.LoadScene() 이 실패합니다.
//
// [인스펙터 설정]
//   - 버튼 오브젝트의 onClick 에 InGameSceneLoaded() 를 연결하세요.
// ============================================================

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 버튼 이벤트 핸들러.
/// </summary>
public class MoveScene : MonoBehaviour
{
    // [P0-06] 필드·메서드 이름은 씬 onClick 배선 유지를 위해 보존한다 (동작만 오프닝 흐름으로 교체).
    [Header("오프닝 재열람 버튼 (옵션)")]
    [Tooltip("[오프닝 다시보기] 버튼 — 이번 실행에서 오프닝을 완료하면 노출. 인스펙터에 메인 메뉴 버튼 연결.")]
    [SerializeField] private GameObject tutorialAgainButton;

    void Start()
    {
        // GameStartScene 의 시작 BGM (제목 화면)
        AudioManager.Instance?.PlayBgmById(BgmId.Title);

        // 이번 실행에서 오프닝을 완료한 뒤 [오프닝 다시보기] 노출 (16-A §1 재열람 경로)
        if (tutorialAgainButton != null)
            tutorialAgainButton.SetActive(RunSessionManager.IsOpeningCompleted());
    }

    /// <summary>
    /// 메인 메뉴 [시작하기] onClick 핸들러 (씬 배선 이름 보존).
    /// 이번 실행 첫 시작: 오프닝(FirstRun) → 완료 시 실행 중 완료 상태 기록 후 본 런.
    /// 같은 실행에서 다시 시작: 바로 본 런 1층. 앱 재실행 시 오프닝을 다시 표시한다.
    /// </summary>
    public void InGameSceneLoaded()
    {
        if (RunSessionManager.IsOpeningCompleted())
        {
            StartMainRun();
            return;
        }

        // 이번 실행의 첫 시작 — 오프닝 완료를 메모리에 기록하고 본 런에 진입한다.
        OpeningFlowController.Show(OnboardingEntryMode.FirstRun, () =>
        {
            RunSessionManager.MarkOpeningCompleted();
            StartMainRun();
        });
    }

    /// <summary>
    /// 메인 메뉴 [오프닝 다시보기] onClick 핸들러 (씬 배선 이름 보존 — 구 StartTutorialAgain).
    /// 오프닝을 Review 모드로 다시 보여 준 뒤 타이틀로 복귀한다.
    /// 완료 플래그·런 데이터는 바꾸지 않는다 (16-A §1).
    /// </summary>
    public void StartTutorialAgain()
    {
        // Review — 완료 플래그를 쓰지 않고, 끝나면 타이틀(현재 씬)로 복귀 (오버레이만 닫힘).
        OpeningFlowController.Show(OnboardingEntryMode.Review, () =>
        {
            AudioManager.Instance?.PlayBgmById(BgmId.Title);
            Debug.Log("[MoveScene] 오프닝 재열람 종료 — 타이틀 복귀");
        });
    }

    /// <summary>
    /// 본 런 진입 — RunSessionManager.StartNewRun 단일 창구로 초기화 (16-B §4).
    /// 전체 초기화 성공 후에만 1층(GamePlayScene) 입력을 연다. 별도 교전 교육 단계 없음.
    /// </summary>
    private void StartMainRun()
    {
        if (RunSessionManager.Instance == null || !RunSessionManager.Instance.StartNewRun())
        {
            Debug.LogError("[MoveScene] 새 런 초기화 실패 — 타이틀에 머무름 (1층 입력 미개방)");
            return;
        }
        Debug.Log("[MoveScene] 본 런 진입 — 새 런 시작");
        SceneTransition.Go("GamePlayScene");
    }
}
