// ============================================================
// Currency/SoulstoneDropFx.cs
// 적 처치 시 바닥에 떨어졌다가 UI 영혼석 아이콘으로 빨려들어가는 단일 오브젝트.
// 기획 §15 보상 시스템 명세 — 자동 수거 연출.
// ============================================================
//
// [수명 흐름]
//   Play(worldPos, target, onArrive) 호출 →
//     1) worldPos 에서 살짝 튀어오름 (0.18s)
//     2) dropDwell 대기 (기본 0.5s)
//     3) gatherDuration 동안 target world position 으로 트윈 (기본 0.4s)
//     4) onArrive() 콜백 (선택적 연출 후처리; 재화는 Pool 이 시작 전에 지급)
//     5) gameObject.SetActive(false) → 풀 반환
//
// [좌표계]
//   World Space 기반. SpriteRenderer 또는 Quad. 적 카드와 동일 좌표계.
//   target.position 을 spawn 시점에 캡처해 트윈 — 짧은 0.4s 안에 카메라/UI 이동 무시 가능.
// ============================================================

using System;
using UnityEngine;
using DG.Tweening;

public class SoulstoneDropFx : MonoBehaviour
{
    [Header("타이밍")]
    [Tooltip("드롭 후 수거 시작까지 대기 (기획 §15 약 0.5초)")]
    [SerializeField] private float dropDwell = 0.5f;
    [Tooltip("UI 로 빨려들어가는 트윈 시간")]
    [SerializeField] private float gatherDuration = 0.4f;
    [Tooltip("스폰 시 살짝 튀어오르는 시간 (0이면 생략)")]
    [SerializeField] private float popDuration = 0.18f;
    [Tooltip("스폰 시 위로 튀는 높이 (월드 단위)")]
    [SerializeField] private float popHeight = 0.3f;

    private Sequence _seq;
    private Action   _onComplete;

    /// <summary>worldPos 에서 시작 → target 위치로 빨려들어감 → onArrive() 후 풀 반환.</summary>
    public void Play(Vector3 spawnWorldPos, Transform target, Action onArrive)
    {
        transform.position = spawnWorldPos;
        _onComplete = onArrive;

        if (_seq != null && _seq.IsActive()) _seq.Kill();
        _seq = DOTween.Sequence();

        // 1) 위로 튀어오름 (살짝 X 랜덤 jitter 로 변화)
        if (popDuration > 0f && popHeight > 0f)
        {
            float jitterX = UnityEngine.Random.Range(-0.15f, 0.15f);
            Vector3 popPos = spawnWorldPos + new Vector3(jitterX, popHeight, 0f);
            _seq.Append(transform.DOMove(popPos, popDuration).SetEase(Ease.OutQuad));
        }

        // 2) 드롭 강조 대기
        if (dropDwell > 0f) _seq.AppendInterval(dropDwell);

        // 3) target 위치로 빨려들어감 (target null 이면 제자리)
        Vector3 targetPos = target != null ? target.position : spawnWorldPos;
        _seq.Append(transform.DOMove(targetPos, gatherDuration).SetEase(Ease.InCubic));

        _seq.OnComplete(() =>
        {
            _onComplete?.Invoke();
            _onComplete = null;
            gameObject.SetActive(false);
        });
    }

    private void OnDisable()
    {
        if (_seq != null && _seq.IsActive()) _seq.Kill();
        _seq = null;
        _onComplete = null;
    }
}
