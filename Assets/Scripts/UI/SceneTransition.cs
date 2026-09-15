// SceneTransition.cs
// 가벼운 씬 전환 연출 — 검은 페이드 + 전환 사운드(Window Sliding).
//
// 사용법:  SceneTransition.Go("GamePlayScene");
//   - 씬에 미리 배치할 필요 없음. 첫 호출 시 자체적으로 GameObject + 풀스크린
//     오버레이 Canvas 를 생성하고 DontDestroyOnLoad 로 유지한다.
//   - 페이드 인(검게) → SceneManager.LoadSceneAsync → 페이드 아웃 순.
//   - 진행바 없는 경량 연출. 씬이 작아 로드가 빨라도 끊김을 부드럽게 가린다.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    private const float FadeDuration = 0.45f; // 페이드 인/아웃 각각의 시간(초)

    private CanvasGroup _group;

    /// <summary>페이드 연출과 함께 씬을 로드한다. 어디서든 한 줄로 호출.</summary>
    public static void Go(string sceneName)
    {
        EnsureInstance();
        Instance.StartCoroutine(Instance.LoadRoutine(sceneName));
    }

    private static void EnsureInstance()
    {
        if (Instance != null) return;
        var go = new GameObject("SceneTransition");
        go.AddComponent<SceneTransition>(); // Awake 에서 Instance/오버레이 셋업
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    private void BuildOverlay()
    {
        // 최상단 오버레이 Canvas (다른 모든 UI 위에 그림)
        var canvasGo = new GameObject("TransitionCanvas",
            typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        ResponsiveUi.Configure(canvas);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 항상 최상단

        _group = canvasGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        // 풀스크린 검은 패널
        var imgGo = new GameObject("Black",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgGo.transform.SetParent(canvasGo.transform, false);

        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        imgGo.GetComponent<Image>().color = Color.black;
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        _group.blocksRaycasts = true; // 전환 중 입력 차단
        AudioManager.Instance?.PlaySfxById(SfxId.SceneTransition);

        // 페이드 인 (투명 → 검정)
        yield return Fade(0f, 1f);

        // 검은 화면 뒤에서 비동기 로드
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone) yield return null;

        // 페이드 아웃 (검정 → 투명)
        yield return Fade(1f, 0f);

        _group.blocksRaycasts = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < FadeDuration)
        {
            t += Time.unscaledDeltaTime; // 타임스케일 영향 없이 일정하게
            _group.alpha = Mathf.Lerp(from, to, t / FadeDuration);
            yield return null;
        }
        _group.alpha = to;
    }
}
