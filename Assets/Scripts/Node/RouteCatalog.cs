using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>03 §1-1B의 경로 표면 정보. 숨은 장소 타입과 별도로 보관한다.</summary>
[Serializable]
public sealed class RouteCardData
{
    public string id, title, environment, omen, imagePath, area;
}

public static class RouteCatalog
{
    [Serializable] private sealed class Data
    {
        public string strongWarning;
        public List<RouteCardData> routes = new();
    }
    private static Data _data;
    private static Data Current
    {
        get
        {
            if (_data != null) return _data;
            var asset = Resources.Load<TextAsset>("Routes/RouteCatalog");
            _data = asset != null ? JsonUtility.FromJson<Data>(asset.text) : new Data();
            return _data;
        }
    }

    public static RouteCardData Create(int floor, int column, int offset, bool strongWarning)
    {
        string area = AreaFor(floor, column);
        var pool = Current.routes.FindAll(r => r.area == area);
        if (pool.Count == 0) throw new InvalidOperationException("Missing route content for " + area);
        var source = pool[(column + offset) % pool.Count];
        return new RouteCardData
        {
            id = source.id, title = source.title, environment = source.environment,
            omen = strongWarning ? Current.strongWarning : source.omen,
            imagePath = source.imagePath, area = source.area
        };
    }

    private static string AreaFor(int floor, int column)
    {
        if (floor <= 2 || (floor == 3 && column == 0)) return "forest";
        if (floor <= 5 || (floor == 6 && column == 0)) return "canyon";
        return "farmland";
    }

    public static string AreaLabel(int floor)
    {
        if (floor <= 2) return Loc.Tr("야생림");
        if (floor == 3) return Loc.Tr("야생림") + " / " + Loc.Tr("협곡");
        if (floor <= 5) return Loc.Tr("협곡");
        if (floor == 6) return Loc.Tr("협곡") + " / " + Loc.Tr("경작지");
        if (floor <= 8) return Loc.Tr("경작지");
        return Loc.Tr(floor == 9 ? "화톳불" : "성소");
    }
}
