// ============================================================
// UI/StatusVisual.cs
// 상태이상 표시용 아이콘/라벨/색 매핑 (2026-06-08, 2026-06-09 아이콘화)
// ============================================================
//
// 실구현 상태이상: 중독(DoT) / 압박·패닉(스트레스) / 공포경직 / 과호흡 / 중증디버프 / 도발(적).
// 표시는 아이콘 우선 — Resources/Icons/status_*.png (1-bit 픽셀, Single/Point).
// 색(*Color)은 아이콘 배경 틴트 / 아이콘 없을 때 폴백 칩 색으로 쓰인다.
//
// 확장: 새 상태 추가 시 enum + ResNameOf + ColorOf + LabelOf 에 한 줄씩 + status_* PNG 추가.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public enum StatusKind
{
    None,
    Poison,        // 중독 (DoT)
    Pressure,      // 압박 (스트레스 51~99)
    Panic,         // 패닉 (스트레스 100, 찰나)
    Frozen,        // 공포 경직 (이번 턴 행동 불가)
    OverBreathing, // 과호흡 (다음 턴 스킬 코스트 +1)
    SevereDebuff,  // 역할별 중증 디버프 (전투 종료까지 유지)
    Taunt,         // 도발 (적)
}

public static class StatusVisual
{
    // ── 색상 (아이콘 배경 틴트 / 아이콘 없을 때 폴백) ─────────────────
    public static Color PoisonColor    = new Color(0.30f, 0.78f, 0.33f, 1f); // 중독 = 초록 (지시)
    public static Color PressureColor  = new Color(0.92f, 0.60f, 0.16f, 1f); // 압박 = 주황
    public static Color PanicColor     = new Color(0.86f, 0.24f, 0.20f, 1f); // 패닉 = 빨강
    public static Color FrozenColor    = new Color(0.40f, 0.70f, 0.95f, 1f); // 공포경직 = 하늘
    public static Color OverBreathColor= new Color(0.45f, 0.82f, 0.86f, 1f); // 과호흡 = 청록
    public static Color SevereColor    = new Color(0.62f, 0.24f, 0.70f, 1f); // 중증디버프 = 자주(저주)
    public static Color TauntColor     = new Color(0.60f, 0.35f, 0.80f, 1f); // 도발 = 보라
    public static readonly Color None  = new Color(0f, 0f, 0f, 0f);

    public static Color ColorOf(StatusKind k)
    {
        switch (k)
        {
            case StatusKind.Poison:        return PoisonColor;
            case StatusKind.Pressure:      return PressureColor;
            case StatusKind.Panic:         return PanicColor;
            case StatusKind.Frozen:        return FrozenColor;
            case StatusKind.OverBreathing: return OverBreathColor;
            case StatusKind.SevereDebuff:  return SevereColor;
            case StatusKind.Taunt:         return TauntColor;
            default:                       return None;
        }
    }

    public static string LabelOf(StatusKind k)
    {
        switch (k)
        {
            case StatusKind.Poison:        return "중독";
            case StatusKind.Pressure:      return "압박";
            case StatusKind.Panic:         return "패닉";
            case StatusKind.Frozen:        return "경직";
            case StatusKind.OverBreathing: return "과호흡";
            case StatusKind.SevereDebuff:  return "중증";
            case StatusKind.Taunt:         return "도발";
            default:                       return "";
        }
    }

    // ── 아이콘 (Resources/Icons/status_*) 캐시 로더 ──────────────────
    private static readonly Dictionary<StatusKind, Sprite> _iconCache = new Dictionary<StatusKind, Sprite>();

    private static string ResNameOf(StatusKind k)
    {
        switch (k)
        {
            case StatusKind.Poison:        return "status_poison";
            case StatusKind.Pressure:      return "status_pressure";
            case StatusKind.Panic:         return "status_panic";
            case StatusKind.Frozen:        return "status_frozen";
            case StatusKind.OverBreathing: return "status_overbreath";
            case StatusKind.SevereDebuff:  return "status_severe";
            case StatusKind.Taunt:         return "status_taunt";
            default:                       return null;
        }
    }

    /// <summary>상태별 1-bit 아이콘 스프라이트(없으면 null → 폴백 색칩). 캐시.</summary>
    public static Sprite IconOf(StatusKind k)
    {
        if (k == StatusKind.None) return null;
        if (!_iconCache.TryGetValue(k, out var sp))
        {
            string rn = ResNameOf(k);
            sp = string.IsNullOrEmpty(rn) ? null : Resources.Load<Sprite>("Icons/" + rn);
            _iconCache[k] = sp;
        }
        return sp;
    }

    /// <summary>스트레스 수치 → 상태 (0~50 없음 / 51~99 압박 / 100 패닉).</summary>
    public static StatusKind FromStress(int stress)
    {
        if (stress >= 100) return StatusKind.Panic;
        if (stress >= 51)  return StatusKind.Pressure;
        return StatusKind.None;
    }

    /// <summary>상태 + 남은 턴을 한 칩 텍스트로. 예: "중독 2". (텍스트 폴백용)</summary>
    public static string ChipText(StatusKind k, int turns)
    {
        string label = LabelOf(k);
        if (string.IsNullOrEmpty(label)) return "";
        return turns > 0 ? $"{label} {turns}" : label;
    }

    /// <summary>상태이상 효과 한 줄 설명(라벨 제외).</summary>
    public static string DescOf(StatusKind k)
    {
        switch (k)
        {
            case StatusKind.Poison:        return "매 턴 도트 피해";
            case StatusKind.Pressure:      return "스킬 위력 -10% / 받는 스트레스 +10%";
            case StatusKind.Panic:         return "행동 불가 또는 코스트 증가";
            case StatusKind.Frozen:        return "이번 턴 행동 불가";
            case StatusKind.OverBreathing: return "다음 턴 스킬 코스트 +1";
            case StatusKind.SevereDebuff:  return "역할별 약화 (전투 종료까지)";
            case StatusKind.Taunt:         return "이 적을 우선 공격";
            default:                       return "";
        }
    }

    /// <summary>호버 툴팁 한 줄: "라벨 — 설명 (N턴 남음)". 턴 0이면 턴 표기 생략.</summary>
    public static LocalizedMessage TooltipText(StatusKind k, int turns)
    {
        string label = LabelOf(k);
        if (string.IsNullOrEmpty(label)) return "";
        string desc = DescOf(k);
        LocalizedMessage head = string.IsNullOrEmpty(desc) ? Loc.Message(label) : Loc.Message("<b>{0}</b> — {1}", Loc.Message(label), Loc.Message(desc));
        return turns > 0 ? Loc.Message("{0}  ({1}턴 남음)", head, turns) : head;
    }
}
