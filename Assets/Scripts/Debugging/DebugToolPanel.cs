// ============================================================
// Debugging/DebugToolPanel.cs  (#14, 2026-06-11)
// 게임 검증용 디버그 툴 — F1 토글 팝업 (에디터/개발 빌드 전용)
// ============================================================
//
// [열기/닫기]  F1
// [단축키]     전투 중: 1·2=아군 전원 스킬1·2 / 3·4=적 전원 스킬1·2 / 5·6=적(보스) 스킬3·4 — 전부 모션+효과 실제 적용
// [섹션]
//   재화  : 영혼석 +100/-100/0 · 마석 +50/-50/0
//   전투  : 즉시 승리 / 즉시 패배 / 풀힐·스트레스0 / 스택 999
//   진행  : 층 전진(구 F2) / 새 런 시작
//   스킬  : 단축키 1~4 와 동일한 버튼
//
// 씬 배치 불필요 — RuntimeInitializeOnLoad 로 자가 생성(DontDestroyOnLoad).
// 구 CheatInput(F1 즉발 치트 / F2 층 전진)을 대체한다.
// ============================================================
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class DebugToolPanel : MonoBehaviour
{
    /// <summary>패널 빈 영역을 잡고 끌면 창 이동 (버튼 위에서는 버튼 클릭 우선).</summary>
    private class DragMove : MonoBehaviour, IDragHandler
    {
        public RectTransform target;
        public void OnDrag(PointerEventData e)
        {
            if (target != null) target.anchoredPosition += e.delta;
        }
    }

    private GameObject _root;
    private bool _built;

    // 확인 모달 (리셋 오클릭 방지) — Build 에서 1회 생성 후 재사용
    private GameObject _confirmRoot;
    private TMP_Text   _confirmText;
    private Button     _confirmYesButton;

    private static readonly Color Gold = new Color(1f, 0.84f, 0.4f, 1f);
    // 리셋 계열 — 붉은 계열로 시각적 주의 (디버깅 위험 강조)
    private static readonly Color DangerRed   = new Color(0.72f, 0.15f, 0.15f, 1f); // 버튼 바탕
    private static readonly Color DangerHi    = new Color(0.95f, 0.30f, 0.30f, 1f); // 라벨/테두리 강조
    private static readonly Color DangerDim   = new Color(0.30f, 0.06f, 0.06f, 0.97f); // 확인 박스 바탕
    private static readonly Color WhiteInk    = new Color(0.97f, 0.95f, 0.93f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<DebugToolPanel>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("DebugToolPanel");
        DontDestroyOnLoad(go);
        go.AddComponent<DebugToolPanel>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.f1Key.wasPressedThisFrame)
            Toggle();

        // 전투 중 스킬/연출 테스트 단축키 — 1·2 아군 / 3·4 적 스킬1·2 / 5·6 적(보스) 스킬3·4
        var bm = BattleManager.Instance;
        if (bm == null || !bm.DebugInBattle) return;
        if (Keyboard.current.digit1Key.wasPressedThisFrame) bm.DebugCastAllAllySkills(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) bm.DebugCastAllAllySkills(1);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) bm.DebugCastAllEnemySkills(0);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) bm.DebugCastAllEnemySkills(1);
        if (Keyboard.current.digit5Key.wasPressedThisFrame) bm.DebugCastAllEnemySkills(2);
        if (Keyboard.current.digit6Key.wasPressedThisFrame) bm.DebugCastAllEnemySkills(3);
    }

    public void Toggle()
    {
        if (!_built) Build();
        _root.SetActive(!_root.activeSelf);
    }

    // ── 버튼 동작 ───────────────────────────────────────────────
    private static void Soul(int delta)
    {
        var m = SoulstoneManager.Instance; if (m == null) return;
        m.SetAmount(Mathf.Max(0, m.Amount + delta));
    }
    private static void SoulZero() => SoulstoneManager.Instance?.SetAmount(0);
    private static void Mana(int delta)
    {
        var m = ManastoneManager.Instance; if (m == null) return;
        m.SetAmount(Mathf.Max(0, m.Amount + delta));
    }
    private static void ManaZero() => ManastoneManager.Instance?.SetAmount(0);

    private static void StackMax()
    {
        if (PlayerRoleCost.Instance == null) { Debug.LogWarning("[디버그] PlayerRoleCost 없음"); return; }
        foreach (StackType role in System.Enum.GetValues(typeof(StackType)))
            PlayerRoleCost.Instance.SetAmount(role, 999);
        Debug.Log("[디버그] 모든 스택 999");
    }

    private static bool InBattle(out BattleManager bm)
    {
        bm = BattleManager.Instance;
        if (bm != null && bm.DebugInBattle) return true;
        Debug.LogWarning("[디버그] 전투 중이 아님 — 전투 치트 무시");
        return false;
    }

    private static void NewRun()
    {
        // 새 런 초기화는 RunSessionManager 단일 창구 (16-B §4).
        // 진행 중이던 런은 포기 처리되며 완료 런 수는 증가하지 않는다.
        if (RunSessionManager.Instance == null || !RunSessionManager.Instance.StartNewRun())
        {
            Debug.LogError("[디버그] 새 런 초기화 실패 — 씬 이동 취소");
            return;
        }
        SceneManager.LoadScene("GamePlayScene");
    }

    // ── 리셋 동작 (기능별) — 확인 모달을 거쳐 호출됨 ──────────────
    private const string ManaKey = "ManaStone"; // ManastoneManager.SaveKey 와 동일

    /// <summary>온보딩 리셋 — 실행 중 오프닝·가이드 상태 초기화 및 이전 저장 플래그 삭제 (P0-06 재확인용).</summary>
    private static void ResetOnboarding()
    {
        RunSessionManager.ResetOnboardingForApplication();
        PlayerPrefs.DeleteKey(RunSessionManager.OpeningCompletedKey);
        PlayerPrefs.DeleteKey(RunSessionManager.CombatGuideCompletedKey);
        PlayerPrefs.DeleteKey(TutorialManager.PrefsKey);
        PlayerPrefs.Save();
        Debug.Log("[디버그·리셋] 온보딩 — opening/combat_guide/tutorial 플래그 삭제 (다음 [시작하기]에서 오프닝·가이드 재생)");
    }

    /// <summary>재화 리셋 — 영혼석·마석을 시작값으로.</summary>
    private static void ResetCurrency()
    {
        PlayerPrefs.DeleteKey(SoulstoneManager.PrefsKey);
        PlayerPrefs.DeleteKey(ManaKey);
        PlayerPrefs.Save();
        SoulstoneManager.Instance?.ResetCurrency(); // 라이브 UI 도 시작값으로 갱신
        ManastoneManager.Instance?.ResetCurrency();
        Debug.Log("[디버그·리셋] 재화 — 영혼석/마석 시작값 복귀");
    }

    /// <summary>메타 성장 리셋 — 마석 강화(패시브·스킬) 해금 전부 초기화.</summary>
    private static void ResetMeta()
    {
        MetaPassiveManager.ResetAll();
        Debug.Log("[디버그·리셋] 메타 성장 — 해금 전체 초기화 (마석 잔액은 재화 리셋에서 별도 처리)");
    }

    /// <summary>런 기록 리셋 — 완료 런 수 삭제 (탐사 보고서 '제 N차'가 1로 복귀).</summary>
    private static void ResetRunHistory()
    {
        PlayerPrefs.DeleteKey(RunSessionManager.RunCompletedCountKey);
        PlayerPrefs.Save();
        Debug.Log("[디버그·리셋] 런 기록 — 완료 런 수 삭제 (제 1차부터 다시)");
    }

    /// <summary>전체 초기화 — 모든 PlayerPrefs 삭제 (설정·재화·진행 전부). 최후 수단.</summary>
    private static void ResetAllPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[디버그/리셋] 전체 PlayerPrefs 삭제");
    }

    // ── UI 생성 ────────────────────────────────────────────────
    private void Build()
    {
        _built = true;
        var font = TMP_Settings.defaultFontAsset;
        var panelSprite = Resources.Load<Sprite>("UI/panel_1");
        var btnSprite   = Resources.Load<Sprite>("Button/default_button");

        var canvasGo = new GameObject("DebugCanvas", typeof(Canvas), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 11000;
        _root = canvasGo;

        var box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(canvasGo.transform, false);
        var brt = (RectTransform)box.transform;
        // 상단-우측 고정 + 내용 높이 자동 맞춤 (리셋 섹션 추가로 세로가 길어져도 넘치지 않게)
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(1f, 1f);
        brt.sizeDelta = new Vector2(560, 0); brt.anchoredPosition = new Vector2(-24, -24);
        var bimg = box.GetComponent<Image>();
        if (panelSprite != null) { bimg.sprite = panelSprite; bimg.type = Image.Type.Sliced; bimg.color = Color.white; }
        else bimg.color = new Color(0.1f, 0.1f, 0.14f, 0.97f);
        bimg.raycastTarget = true;
        box.AddComponent<DragMove>().target = brt;   // 빈 영역 드래그로 창 이동

        var vlg = box.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(36, 36, 30, 26);
        vlg.spacing = 10;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        var fitter = box.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; // 폭은 560 고정
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize; // 높이는 내용에 맞춤

        Label(box.transform, font, "디버그 툴  (F1)", 30, Gold, bold: true);

        Label(box.transform, font, "─ 재화 ─", 20, new Color(0.8f, 0.8f, 0.85f), bold: false);
        Row(box.transform, font, btnSprite,
            ("영혼석 +100", () => Soul(+100)), ("영혼석 -100", () => Soul(-100)), ("영혼석 0", SoulZero));
        Row(box.transform, font, btnSprite,
            ("마석 +50", () => Mana(+50)), ("마석 -50", () => Mana(-50)), ("마석 0", ManaZero));

        Label(box.transform, font, "─ 전투 (전투 중에만) ─", 20, new Color(0.8f, 0.8f, 0.85f), bold: false);
        Row(box.transform, font, btnSprite,
            ("즉시 승리", () => { if (InBattle(out var b)) b.DebugKillAllEnemies(); }),
            ("즉시 패배", () => { if (InBattle(out var b)) b.DebugKillAllAllies(); }));
        Row(box.transform, font, btnSprite,
            ("풀힐+스트레스0", () => { if (InBattle(out var b)) b.DebugFullHeal(); }),
            ("스택 999", StackMax));

        Label(box.transform, font, "─ 진행 ─", 20, new Color(0.8f, 0.8f, 0.85f), bold: false);
        Row(box.transform, font, btnSprite,
            ("층 전진", () => NodeSystem.Current?.CheatAdvanceFloor()),
            ("새 런 시작", NewRun));

        Label(box.transform, font, "─ 스킬/연출 테스트 (전투 중 단축키 1~6) ─", 20, new Color(0.8f, 0.8f, 0.85f), bold: false);
        Row(box.transform, font, btnSprite,
            ("아군 스킬1 [1]", () => { if (InBattle(out var b)) b.DebugCastAllAllySkills(0); }),
            ("아군 스킬2 [2]", () => { if (InBattle(out var b)) b.DebugCastAllAllySkills(1); }));
        Row(box.transform, font, btnSprite,
            ("적 스킬1 [3]", () => { if (InBattle(out var b)) b.DebugCastAllEnemySkills(0); }),
            ("적 스킬2 [4]", () => { if (InBattle(out var b)) b.DebugCastAllEnemySkills(1); }));
        Row(box.transform, font, btnSprite,
            ("적 스킬3(보스) [5]", () => { if (InBattle(out var b)) b.DebugCastAllEnemySkills(2); }),
            ("적 스킬4(보스) [6]", () => { if (InBattle(out var b)) b.DebugCastAllEnemySkills(3); }));
        Row(box.transform, font, btnSprite,
            ("적 랜덤 행동", () => { if (InBattle(out var b)) b.DebugEnemyTurnOnce(); }),
            ("피격 모션", () => { if (InBattle(out var b)) b.DebugHitMotionAll(); }));

        // ── 리셋 (저장 데이터 삭제 — 붉은 계열 주의) ──
        Label(box.transform, font, "─ 리셋 / 저장 데이터 삭제 ─", 20, DangerHi, bold: true);
        RowStyled(box.transform, font, btnSprite, DangerRed, WhiteInk,
            ("온보딩(튜토/오프닝/가이드)", () => Confirm(
                "온보딩 진행을 리셋합니다.\n오프닝, 첫 전투 가이드, 튜토리얼 완료 플래그가 삭제되어\n다음 [시작하기]에서 오프닝부터 다시 재생됩니다.", ResetOnboarding)),
            ("재화(영혼석/마석)", () => Confirm(
                "영혼석/마석을 시작값으로 되돌립니다.", ResetCurrency)));
        RowStyled(box.transform, font, btnSprite, DangerRed, WhiteInk,
            ("메타 성장(해금)", () => Confirm(
                "마석 강화(패시브/스킬) 해금을 전부 초기화합니다.", ResetMeta)),
            ("런 기록(완료 런 수)", () => Confirm(
                "완료 런 수를 삭제합니다.\n탐사 보고서의 '제 N차'가 1부터 다시 시작됩니다.", ResetRunHistory)));
        RowStyled(box.transform, font, btnSprite, new Color(0.55f, 0.08f, 0.08f, 1f), WhiteInk,
            ("전체 초기화 (PlayerPrefs 전부)", () => Confirm(
                "모든 PlayerPrefs 를 삭제합니다.\n설정/재화/진행/온보딩 전부 초기화됩니다.\n\n되돌릴 수 없습니다.", ResetAllPrefs)));

        Row(box.transform, font, btnSprite, ("닫기 (F1)", () => _root.SetActive(false)));

        BuildConfirmModal(canvasGo.transform, font, btnSprite);

        _root.SetActive(false);
    }

    // ── 확인 모달 (리셋 오클릭 방지) ────────────────────────────
    /// <summary>메시지 + [리셋 실행]/[취소] 재확인 모달을 띄운다. 실행 시에만 onYes 호출.</summary>
    private void Confirm(string message, System.Action onYes)
    {
        if (_confirmRoot == null) return;
        if (_confirmText != null) _confirmText.text = message;

        _confirmYesButton.onClick.RemoveAllListeners();
        var act = onYes;
        _confirmYesButton.onClick.AddListener(() =>
        {
            act?.Invoke();
            _confirmRoot.SetActive(false);
        });

        _confirmRoot.transform.SetAsLastSibling(); // 항상 최상단
        _confirmRoot.SetActive(true);
    }

    /// <summary>확인 모달 UI 를 1회 생성(기본 비활성). 리셋 실행/취소 버튼 포함.</summary>
    private void BuildConfirmModal(Transform canvasParent, TMP_FontAsset font, Sprite btnSprite)
    {
        _confirmRoot = new GameObject("ConfirmModal", typeof(RectTransform));
        _confirmRoot.transform.SetParent(canvasParent, false);
        var crt = (RectTransform)_confirmRoot.transform;
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

        // 전체 딤 — 뒤 버튼 입력 차단 (오클릭 방지). 딤 클릭은 취소.
        var dim = _confirmRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;
        var dimBtn = _confirmRoot.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(() => _confirmRoot.SetActive(false)); // 딤 클릭 = 취소

        // 중앙 박스 (붉은 테두리)
        var boxGo = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        boxGo.transform.SetParent(_confirmRoot.transform, false);
        var brt = (RectTransform)boxGo.transform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(560, 340);
        boxGo.GetComponent<Image>().color = DangerDim;
        boxGo.GetComponent<Image>().raycastTarget = true; // 박스 클릭이 딤(취소)으로 새지 않게
        var ol = boxGo.AddComponent<Outline>();
        ol.effectColor = DangerHi; ol.effectDistance = new Vector2(3, 3); ol.useGraphicAlpha = false;

        var vlg = boxGo.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(28, 28, 24, 24); vlg.spacing = 16;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        Label(boxGo.transform, font, "⚠ 리셋 확인", 28, DangerHi, bold: true);

        _confirmText = new GameObject("Msg", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        _confirmText.transform.SetParent(boxGo.transform, false);
        if (font != null) _confirmText.font = font;
        _confirmText.fontSize = 20; _confirmText.color = WhiteInk;
        _confirmText.alignment = TextAlignmentOptions.Center; _confirmText.raycastTarget = false;
        _confirmText.enableWordWrapping = true;
        var msgLe = _confirmText.gameObject.AddComponent<LayoutElement>();
        msgLe.preferredHeight = 150; msgLe.flexibleHeight = 1;

        // [리셋 실행] (붉은) / [취소] (회색)
        var btnRow = new GameObject("BtnRow", typeof(RectTransform));
        btnRow.transform.SetParent(boxGo.transform, false);
        var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = false;
        var rowLe = btnRow.AddComponent<LayoutElement>(); rowLe.preferredHeight = 56; rowLe.minHeight = 52;

        _confirmYesButton = MakeButton(btnRow.transform, font, btnSprite, "리셋 실행", DangerRed, WhiteInk);
        var cancel = MakeButton(btnRow.transform, font, btnSprite, "취소", new Color(0.28f, 0.28f, 0.32f, 1f), WhiteInk);
        cancel.onClick.AddListener(() => _confirmRoot.SetActive(false));

        _confirmRoot.SetActive(false);
    }

    private static void Label(Transform parent, TMP_FontAsset font, string text, float size, Color color, bool bold)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.color = color;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
    }

    /// <summary>기본 스타일 버튼 행 (흰 바탕 스프라이트 + 금색 라벨).</summary>
    private static void Row(Transform parent, TMP_FontAsset font, Sprite btnSprite, params (string label, System.Action onClick)[] buttons)
        => RowStyled(parent, font, btnSprite, Color.white, Gold, buttons);

    /// <summary>색상 지정 버튼 행 (리셋 등 주의 계열에 붉은 바탕 사용).</summary>
    private static void RowStyled(Transform parent, TMP_FontAsset font, Sprite btnSprite, Color btnColor, Color labelColor, params (string label, System.Action onClick)[] buttons)
    {
        var row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = false;

        foreach (var (label, onClick) in buttons)
        {
            var btn = MakeButton(row.transform, font, btnSprite, label, btnColor, labelColor);
            var act = onClick;
            btn.onClick.AddListener(() => act?.Invoke());
        }
    }

    /// <summary>버튼 GameObject 1개 생성 후 Button 반환 (Row·확인 모달 공용).</summary>
    private static Button MakeButton(Transform parent, TMP_FontAsset font, Sprite btnSprite, string label, Color btnColor, Color labelColor)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        if (btnSprite != null) { img.sprite = btnSprite; img.type = Image.Type.Sliced; }
        img.color = btnColor; // 스프라이트가 있으면 틴트, 없으면 단색
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 48; le.minHeight = 44;
        var btn = go.AddComponent<Button>();

        var lt = new GameObject("Label", typeof(RectTransform));
        lt.transform.SetParent(go.transform, false);
        var lrt = (RectTransform)lt.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var t = lt.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = label; t.fontSize = 20; t.color = labelColor; t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        return btn;
    }
}
#endif
