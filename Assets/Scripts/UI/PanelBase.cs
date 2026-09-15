// ============================================================
// UI/PanelBase.cs
// 모든 패널/팝업의 공통 베이스 — DOTween 페이드 토글
// ============================================================
//
// [부착 방법]
//   각 패널/팝업 GameObject 에 CanvasGroup 컴포넌트가 있으면 페이드 가능.
//   없으면 SetActive 만으로 동작.
//
// [사용처]
//   용병소 패널(메인/모집/성장/파티편집/픽커), 휴식 패널, 설정/로그 팝업 등.
// ============================================================

using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public abstract class PanelBase : MonoBehaviour
{
    [Header("페이드 설정")]
    [SerializeField] protected CanvasGroup canvasGroup;
    [SerializeField] protected float       fadeDuration = 0.18f;

    /// <summary>패널이 완전히 닫힌 직후 발생. 메인 ↔ 서브 전환 등에서 외부 구독.</summary>
    public event System.Action OnClosedEvent;

    // SerializedObjectNotCreatableException 방지: SetActive(false) 호출 안 함.
    // 대신 CanvasGroup alpha=0 + blocksRaycasts=false 로 가시성·입력 모두 차단.
    protected virtual void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha          = 0f;
            canvasGroup.interactable   = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            // CanvasGroup 이 없으면 어쩔 수 없이 SetActive 폴백
            gameObject.SetActive(false);
        }
    }

    public virtual void Open()
    {
        // 인스펙터 연결 실수 방어 — prefab 에셋을 직접 Open 하면 자식 Instantiate 가 실패한다.
        if (!gameObject.scene.IsValid())
        {
            Debug.LogError($"[PanelBase] '{name}' 은 prefab 에셋입니다. 씬에 인스턴스화된 GameObject 를 호출하도록 인스펙터 연결을 수정하세요.", this);
            return;
        }

        // 열리는 팝업/패널은 같은 부모 안에서 맨 앞(마지막 sibling)으로 — 좌패널 등에 가려지는 z-순서 버그 방지
        transform.SetAsLastSibling();

        if (canvasGroup != null)
        {
            // CanvasGroup 기반: SetActive 만지지 않고 alpha 페이드만으로 표시 제어
            canvasGroup.DOKill();
            canvasGroup.alpha          = 0f;
            canvasGroup.interactable   = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.DOFade(1f, fadeDuration).OnComplete(() =>
            {
                canvasGroup.interactable   = true;
                canvasGroup.blocksRaycasts = true;
            });
        }
        else
        {
            gameObject.SetActive(true);
        }

        // OnOpened 가 예외를 던져도 페이드/입력 상태는 유지 — 화면 잠금 방지
        try { OnOpened(); Loc.Localize(gameObject); }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    public virtual void Close()
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.interactable   = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.DOFade(0f, fadeDuration).OnComplete(() =>
            {
                try { OnClosed(); }
                catch (System.Exception e) { Debug.LogException(e); }
                OnClosedEvent?.Invoke();
            });
        }
        else
        {
            try { OnClosed(); }
            catch (System.Exception e) { Debug.LogException(e); }
            gameObject.SetActive(false);
            OnClosedEvent?.Invoke();
        }
    }

    /// <summary>패널이 열린 직후 호출 — 데이터 바인딩 등.</summary>
    protected virtual void OnOpened() { }

    /// <summary>패널이 닫히기 직전 호출 — 리스너 정리 등.</summary>
    protected virtual void OnClosed() { }
}
