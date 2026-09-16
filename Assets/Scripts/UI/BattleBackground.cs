// ============================================================
// UI/BattleBackground.cs
// 전투 배경 — RoomType 별 월드 배경 스프라이트 적용 (2026-06-08)
// ============================================================
//
// [구조 메모]
//   전투 캐릭터(EnemyCharacter/MyCharacter)는 월드 스프라이트(InGameObjects, Main Camera 렌더)다.
//   배경을 오버레이 Canvas 에 깔면 캐릭터를 덮어버리므로, 배경은 반드시
//   캐릭터보다 뒤(낮은 sortingOrder)인 "월드 배경 SpriteRenderer"(씬 루트 Background)에 적용한다.
//
// [부착/동작]
//   전투 화면 컨테이너(GamePlayScene_RightMainArea)에 부착한다.
//   전투 화면이 활성화될 때(OnEnable) NodeSystem.Current.CurrentRoomType 을 읽어
//   보스면 보스 배경, 그 외(일반·엘리트)면 일반 배경을 월드 배경에 깐다.
//   (엘리트 전용 배경이 아직 없어 일반 배경을 사용)
//
// 일반·엘리트는 RemakeV1/Backgrounds 협곡, 보스는 기존 BackGround/Battle_Boss.
// ============================================================

using UnityEngine;

public class BattleBackground : MonoBehaviour
{
    [SerializeField] private string normalSpritePath = "RemakeV1/Backgrounds/Battle_Canyon_Floor4_v1";
    [SerializeField] private string bossSpritePath   = "BackGround/Battle_Boss";

    [Tooltip("월드 배경 SpriteRenderer (씬 루트 Background). 비우면 'Background' 이름으로 자동 검색.")]
    [SerializeField] private SpriteRenderer worldBackground;

    [Tooltip("배경 sortingOrder — 캐릭터(0)보다 뒤가 되도록 음수.")]
    [SerializeField] private int worldSortingOrder = -100;

    [Tooltip("보스 배경 명도(0~1). 기존 보스 배경의 밝기를 유지한다.")]
    [Range(0f, 1f)]
    [SerializeField] private float backgroundBrightness = 0.3f;

    [Tooltip("일반·엘리트 배경 명도(0~1). 1=원본.")]
    [Range(0f, 1f)]
    [SerializeField] private float normalBackgroundBrightness = 0.85f;

    private Sprite   _normal;
    private Sprite   _boss;
    private Material _unlit;   // 2D 라이트가 없는 씬에서 Lit 머티리얼은 검게 렌더되므로 언릿 강제
    private Sprite Normal => _normal != null ? _normal : (_normal = Resources.Load<Sprite>(normalSpritePath));
    private Sprite Boss   => _boss   != null ? _boss   : (_boss   = Resources.Load<Sprite>(bossSpritePath));

    private void OnEnable()
    {
        var room = NodeSystem.Current != null ? NodeSystem.Current.CurrentRoomType : RoomType.Combat;
        Apply(room);
    }

    /// <summary>RoomType 별 전투 배경 적용. 보스=보스 배경, 그 외(일반·엘리트)=일반 배경.</summary>
    public void Apply(RoomType room)
    {
        var sr = ResolveWorldBg();
        if (sr == null)
        {
            Debug.LogWarning("[BattleBackground] 월드 배경 SpriteRenderer 를 찾지 못함 (씬 루트 'Background').");
            return;
        }

        Sprite s = (room == RoomType.Boss) ? Boss : Normal;
        if (s == null)
        {
            Debug.LogWarning($"[BattleBackground] 배경 스프라이트 로드 실패 (room={room}) — 경로: {normalSpritePath}/{bossSpritePath}");
            return;
        }

        sr.sprite = s;
        float b   = Mathf.Clamp01(room == RoomType.Boss ? backgroundBrightness : normalBackgroundBrightness);
        sr.color  = new Color(b, b, b, 1f);  // 명도 낮춰 캐릭터가 도드라지게
        sr.sortingOrder = worldSortingOrder; // 캐릭터(0) 뒤로

        // 2D 라이트가 없는 씬에서 Lit 머티리얼은 검게 렌더됨 → 언릿(Sprites/Default) 강제.
        if (_unlit == null)
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) _unlit = new Material(sh);
        }
        if (_unlit != null) sr.sharedMaterial = _unlit;

        FitToCamera(sr, s);
    }

    private SpriteRenderer ResolveWorldBg()
    {
        if (worldBackground != null) return worldBackground;
        var go = GameObject.Find("Background");
        if (go != null) worldBackground = go.GetComponent<SpriteRenderer>();
        return worldBackground;
    }

    /// <summary>
    /// 배경을 카메라 화면 '전체'를 덮도록(cover) 균일 스케일·중앙 배치한다.
    /// 좌패널을 펼치면 패널(불투명)이 왼쪽을 가리고, 접으면 배경이 끝까지 채워진다.
    /// </summary>
    private void FitToCamera(SpriteRenderer sr, Sprite s)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic || s == null) return;

        float camH = cam.orthographicSize * 2f;
        float camW = camH * cam.aspect;
        float sw   = s.bounds.size.x;
        float sh   = s.bounds.size.y;
        if (sw <= 0f || sh <= 0f) return;

        float scale = Mathf.Max(camW / sw, camH / sh); // cover (큰 쪽 기준, 넘침 허용)
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.transform.position   = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
    }
}
