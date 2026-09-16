using UnityEngine;

/// <summary>손패 카드와 스택 표시가 함께 사용하는 역할별 그림.</summary>
public static class StackCardArt
{
    private const string ResourceRoot = "RemakeV1/CardUI/";
    private static readonly string[] BackgroundNames =
    {
        "Card_Attack_Background_v1", "Card_Defense_Background_v1", "Card_Support_Background_v1"
    };
    private static readonly string[] BadgeNames =
    {
        "Badge_Attack_Sword_v1", "Badge_Defense_Shield_v1", "Badge_Support_Heart_v1"
    };
    private static readonly Sprite[] Backgrounds = new Sprite[3];
    private static readonly Sprite[] Badges = new Sprite[3];

    public static readonly Color NumberColor = new Color(1f, 0.95f, 0.82f, 1f);

    public static Sprite Background(StackType role) => Load(role, BackgroundNames, Backgrounds);
    public static Sprite Badge(StackType role) => Load(role, BadgeNames, Badges);

    private static Sprite Load(StackType role, string[] names, Sprite[] cache)
    {
        int index = (int)role;
        if (index < 0 || index >= names.Length) return null;
        if (cache[index] == null)
        {
            cache[index] = Resources.Load<Sprite>(ResourceRoot + names[index]);
            if (cache[index] == null)
                Debug.LogError("[StackCardArt] Missing sprite: " + ResourceRoot + names[index]);
        }
        return cache[index];
    }
}
