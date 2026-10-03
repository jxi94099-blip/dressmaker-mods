using System;
using System.Text.RegularExpressions;

public static class PatternClassifier
{
    public static bool ShouldCloseMenu(bool insideMenu, bool insideToggle) { return !insideMenu && !insideToggle; }
    public static readonly string[] Categories = { "全部", "上身", "裙身", "袖子／袖口", "领子／领口", "腰带／腰头", "装饰／辅片", "其他" };
    public static readonly string[] Directions = { "全部", "前", "后", "左侧", "右侧" };
    public static int DirectionMask(string name, int zones)
    {
        name = name ?? "";
        int mask = 0;
        if (Regex.IsMatch(name, @"\bFront\b|\bRuffle F\s*$", RegexOptions.IgnoreCase)) mask |= 1;
        if (Regex.IsMatch(name, @"\bBack\b|\bRuffle B\s*$", RegexOptions.IgnoreCase)) mask |= 2;
        if (Regex.IsMatch(name, @"\bLeft\b", RegexOptions.IgnoreCase)) mask |= 4;
        if (Regex.IsMatch(name, @"\bRight\b", RegexOptions.IgnoreCase)) mask |= 8;
        // Names take precedence: some shipped zone flags disagree with front/back or left/right names.
        if ((mask & 3) == 0) { if ((zones & 17) != 0) mask |= 1; if ((zones & 34) != 0) mask |= 2; }
        if ((mask & 12) == 0) { if ((zones & 2628) != 0) mask |= 4; if ((zones & 5256) != 0) mask |= 8; }
        return mask;
    }
    public static bool Matches(int category, int mask, int selectedCategory, int selectedDirection)
    {
        return (selectedCategory == 0 || category == selectedCategory)
            && (selectedDirection == 0 || (mask & (1 << (selectedDirection - 1))) != 0);
    }
    public static int Classify(string name, string kind)
    {
        name = (name ?? "").Trim();
        kind = (kind ?? "").Trim().ToLowerInvariant();
        if (Regex.IsMatch(name, @"\b(Belt|Waistband|Waist Band)\b", RegexOptions.IgnoreCase)) return 5;
        if (Regex.IsMatch(name, @"\b(Collar|Neck|Cowl)\b", RegexOptions.IgnoreCase) || kind == "collar") return 4;
        if (Regex.IsMatch(name, @"\b(Sleeve|Sleev|Cuff)\b", RegexOptions.IgnoreCase) || kind == "sleeve") return 3;
        if (Regex.IsMatch(name, @"\b(Bow|Ribbon|Ruffle|Frill|Placket|Trim|Strap|Lace)\b", RegexOptions.IgnoreCase)) return 6;
        if (kind == "bodice") return 1;
        if (kind == "skirt") return 2;
        return 7;
    }
}

