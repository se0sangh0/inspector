// ============================================================
// Run/FieldObservationCatalog.cs
// 전투 후 현장 관찰 데이터 카탈로그 (P0-03)
// ============================================================
//
// [이 파일이 하는 일]
//   지정 인카운터의 전투 승리 뒤 표시하는 '현장 관찰' 문안을 보관합니다.
//   화면 문안(두 문장)과 조사관 수첩 사후 관찰(한 문장)로 구성됩니다.
//
// [계약 — 16-B §2·§3 / 16-A §2 / 문안은 16-E T0 초안]
//   - 현장 관찰은 특정 경로(조우)에 연결한다. 같은 전투 프로필을 쓰는
//     다른 경로에 자동으로 붙이지 않는다.
//   - OBS_TORN_BASKET  = 찢어진 바구니 (계약상 R02-A 경로)
//   - OBS_FOREST_BARRICADE = 숲을 향한 목책 (계약상 R03-B 경로)
//   - 관찰은 표시일 뿐 — 보상·상태 변화·추가 기록을 만들지 않는다.
//
// [현재 바인딩 — P0-02(경로 카드) 도입 전 임시]
//   경로 시스템이 아직 없으므로 현행 6노드 일렬 맵의 일반 전투 노드에
//   관찰을 연결한다: 첫 일반 전투(행 1) → OBS_TORN_BASKET,
//   둘째 일반 전투(행 3) → OBS_FOREST_BARRICADE.
//   P0-02 도입 시 이 바인딩은 경로 데이터의 postBattleObservationId 로 이관한다.
// ============================================================

/// <summary>현장 관찰 한 건 — 화면 문안과 수첩 사후 관찰.</summary>
public class FieldObservation
{
    public string id;             // 예: "OBS_TORN_BASKET"
    public string title;          // 물리적 관찰 명칭 (행위자·의도 결론어 금지)
    public string screenText;     // 화면 문안 (두 문장 이내)
    public string notebookText;   // 조사관 수첩 사후 관찰 (한 문장)
    public string imageName;      // Resources 기준 경로 또는 기존 ResultImage 파일명 (확장자 제외).
}

/// <summary>현장 관찰 정적 카탈로그. 문안은 16-E T0 초안의 시연용 테스트값.</summary>
public static class FieldObservationCatalog
{
    public const string TornBasketId       = "OBS_TORN_BASKET";
    public const string ForestBarricadeId  = "OBS_FOREST_BARRICADE";

    private static readonly FieldObservation[] All =
    {
        new FieldObservation
        {
            id           = TornBasketId,
            title        = "찢어진 바구니",
            screenText   = "전투가 끝난 자리에서 찢어진 바구니가 발견된다. 안쪽에서는 평범한 약초와 열매, 땔감이 쏟아져 있다.",
            notebookText = "주변 탐색 결과 찢어진 바구니에서 약초, 열매, 땔감을 확인함.",
            imageName    = "RemakeV1/Clues/OBS_TORN_BASKET_v1",
        },
        new FieldObservation
        {
            id           = ForestBarricadeId,
            title        = "숲을 향한 목책",
            screenText   = "전투가 끝난 자리에서 숲을 향해 세워진 목책이 확인된다. 목책 위쪽 끝에는 흰 천 조각이 묶여 있다.",
            notebookText = "주변 탐색 결과 숲 방향 목책과 흰 천 표식을 확인함.",
            imageName    = "RemakeV1/Clues/OBS_FOREST_BARRICADE_v1",
        },
    };

    /// <summary>ID 로 관찰을 찾는다. 없거나 빈 ID 면 null.</summary>
    public static FieldObservation GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var o in All)
            if (o.id == id) return o;
        UnityEngine.Debug.LogWarning($"[FieldObservationCatalog] 관찰 ID '{id}' 없음");
        return null;
    }

    /// <summary>
    /// [P0-02 도입 전 임시 바인딩] 현행 일렬 맵의 노드 행 → 관찰 ID.
    /// 일반 전투(RoomType.Combat) 노드에만 사용한다. 지정이 없으면 null.
    /// </summary>
    public static string IdForLegacyCombatRow(int rowIndex) => rowIndex switch
    {
        1 => TornBasketId,       // 첫 일반 전투
        3 => ForestBarricadeId,  // 둘째 일반 전투
        _ => null,
    };
}
