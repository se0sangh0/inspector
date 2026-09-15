// ============================================================
// Localization/LocalizationManager.cs
// 언어 전환(한국어/영어) — 중앙 로컬라이제이션 (해외 전시용)
// ============================================================
//
// [이 파일이 하는 일]
//   게임 전역의 텍스트를 한국어/영어로 전환합니다.
//   - Tr(ko)          : 한국어 원문을 키로 현재 언어 문자열 반환
//   - Tr(ko, args)    : 서식 문자열(숫자 포함) 번역 (String.Format)
//   - Localize(root)  : 특정 UI 트리의 모든 TMP_Text 를 현재 언어로 교체
//   - 언어 변경/씬 로드 시 활성 씬 전체 TMP_Text 를 자동 번역
//
// [설계]
//   키 = 한국어 원문. LocalizationTable(ko→en)에서 영어를 찾는다.
//   → 기존 코드가 만든 한국어 문자열을 그대로 키로 쓸 수 있어 도입이 쉽다.
//   씬에 미리 배치된(에디터 작성) 라벨은 문자열 매칭으로 교체하므로
//   개별 오브젝트에 컴포넌트를 붙이지 않아도 번역된다.
//
// [영속] PlayerPrefs "language" (0=한국어 / 1=영어). 런 초기화로 지우지 않는다.
//
// [주의] 숫자가 섞인 동적 문자열은 문자열 매칭으로 잡히지 않으므로
//   생성/표시 코드에서 Tr(키, args) 를 사용해야 한다.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public enum Language { Korean = 0, English = 1 }

public class LocalizationManager : MonoBehaviour
{
    public const string PrefsKey = "language";

    public static LocalizationManager Instance { get; private set; }
    public static Language Current { get; private set; } = Language.Korean;

    /// <summary>언어가 바뀔 때 발생 — 열려 있는 동적 패널이 필요 시 갱신에 구독.</summary>
    public static event System.Action OnLanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        Current = PlayerPrefs.GetInt(PrefsKey, 0) == 1 ? Language.English : Language.Korean;
        var go = new GameObject("LocalizationManager");
        go.AddComponent<LocalizationManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // 최초 씬(타이틀) 안전 번역 — 영어 저장 상태로 시작해도 첫 화면이 영어로 뜨게.
        TranslateActiveScene();
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 UI 초기화가 끝난 다음 프레임에 번역 (에디터 배치 라벨 대상)
        if (isActiveAndEnabled) StartCoroutine(TranslateNextFrame());
    }

    private IEnumerator TranslateNextFrame()
    {
        yield return null;
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ResponsiveUi.Configure(canvas);
        TranslateActiveScene();
    }

    // ── 언어 설정 ────────────────────────────────────────────────
    public static void SetLanguage(Language lang)
    {
        if (lang != Language.Korean && lang != Language.English) lang = Language.Korean;
        Current = lang;
        PlayerPrefs.SetInt(PrefsKey, (int)lang);
        PlayerPrefs.Save();
        TranslateActiveScene();
        OnLanguageChanged?.Invoke();
        Debug.Log($"[Localization] 언어 변경 → {lang}");
    }

    // ── 번역 조회 ────────────────────────────────────────────────
    /// <summary>한국어 원문을 키로 현재 언어 문자열을 반환. 매핑이 없으면 원문 그대로.</summary>
    public static string Tr(string ko)
    {
        if (string.IsNullOrEmpty(ko)) return ko;
        if (Current == Language.English && LocalizationTable.Ko2En.TryGetValue(ko, out var en))
            return en;
        return ko;
    }

    /// <summary>서식 문자열 번역 — 키(한국어 서식)를 번역한 뒤 args 로 String.Format.</summary>
    public static string Tr(string koFormat, params object[] args)
    {
        string fmt = Tr(koFormat);
        try { return string.Format(fmt, args); }
        catch { return fmt; }
    }

    // ── UI 트리/씬 번역 (문자열 매칭 교체) ────────────────────────
    /// <summary>root 아래 모든 TMP_Text 를 현재 언어로 교체 (런타임 생성 패널용).</summary>
    public static void Localize(GameObject root)
    {
        if (root == null) return;
        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts) LocalizeText(t);
    }

    /// <summary>활성 씬 + DontDestroyOnLoad 의 모든 TMP_Text 를 현재 언어로 교체.</summary>
    public static void TranslateActiveScene()
    {
        var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in texts) LocalizeText(t);
    }

    /// <summary>
    /// TMP 한 개를 현재 언어로 교체. 현재 표시 문자열에서 한국어 원문(ko)을 역추적한 뒤
    /// 목표 언어로 설정한다 (KO↔EN 왕복 안전, 매핑 없으면 그대로 둔다).
    /// </summary>
    private static void LocalizeText(TMP_Text t)
    {
        if (t == null) return;
        if (t.GetComponentInParent<TMP_InputField>() != null) return;
        var binding = t.GetComponent<LocalizedLabel>();
        if (binding != null && binding.OwnsCurrentText) { binding.Refresh(); return; }
        EnsureAutoFit(t);
        string cur = t.text;
        if (string.IsNullOrEmpty(cur)) return;

        string ko = null;
        if (LocalizationTable.Ko2En.ContainsKey(cur)) ko = cur;                    // 현재 한국어 원문
        else if (LocalizationTable.En2Ko.TryGetValue(cur, out var back)) ko = back; // 현재 영어 → 원문 역추적
        if (ko == null) return; // 표에 없는(동적) 문자열 — 그대로 둔다

        Set(t, ko);
    }

    public static void Bind(TMP_Text text, System.Func<string> render)
    {
        if (text == null) return;
        var binding = text.GetComponent<LocalizedLabel>();
        if (binding == null) binding = text.gameObject.AddComponent<LocalizedLabel>();
        binding.Bind(render);
    }

    public static void Set(TMP_Text text, string key, params object[] arguments)
    {
        var message = new LocalizedMessage(key, arguments);
        Bind(text, message.Render);
    }

    /// <summary>
    /// 텍스트가 오브젝트를 벗어나지 않도록 자동 크기(auto-size)를 켠다.
    /// 한국어(원본 크기)는 유지하고, 더 긴 언어(주로 영어)만 필요 시 폰트를 줄여 맞춘다.
    /// 오브젝트를 물리적으로 키우면 이웃과 겹칠 위험이 있어, 박스 안에서 맞추는 방식을 쓴다.
    /// </summary>
    public static void EnsureAutoFit(TMP_Text t)
    {
        if (t == null) return;
        // 타이틀의 긴 영어 이름은 한글보다 작은 크기를 기준으로 맞춘다.
        // 언어를 다시 한국어로 바꾸면 씬에 지정된 120 크기로 복원한다.
        if (t.gameObject.scene.name == "GameStartScene" && t.gameObject.name == "TitleText")
        {
            float titleSize = Current == Language.English ? 64f : 120f;
            t.enableAutoSizing = true;
            t.fontSizeMax = titleSize;
            t.fontSizeMin = 12f;
            t.fontSize = titleSize;
            return;
        }
        if (t.enableAutoSizing) return; // 이미 자동 크기면 그대로
        float baseSize = t.fontSize;
        if (baseSize <= 0f) return;
        t.enableAutoSizing = true;
        t.fontSizeMax = baseSize;                          // 원본(한국어) 크기를 상한으로 유지
        t.fontSizeMin = Mathf.Min(baseSize, 12f);
    }
}

/// <summary>간편 접근용 별칭 — Loc.Tr(...) 로 호출.</summary>
public static class Loc
{
    public static void Set(TMP_Text text, string key, params object[] arguments) => LocalizationManager.Set(text, key, arguments);
    public static void Bind(TMP_Text text, System.Func<string> render) => LocalizationManager.Bind(text, render);
    public static LocalizedMessage Message(string key, params object[] arguments) => new LocalizedMessage(key, arguments);
    public static string Tr(string ko) => LocalizationManager.Tr(ko);
    public static string Tr(string koFormat, params object[] args) => LocalizationManager.Tr(koFormat, args);
    public static void Localize(GameObject root) => LocalizationManager.Localize(root);
    public static void AutoFit(TMPro.TMP_Text t) => LocalizationManager.EnsureAutoFit(t);
    public static bool IsEnglish => LocalizationManager.Current == Language.English;
}
