using System.Globalization;

namespace Dragonfly.Services;

public static class Fmt
{
    public static string Money(decimal v) =>
        v < 0 ? "-$" + Math.Abs(v).ToString("N2", CultureInfo.InvariantCulture)
              : "$" + v.ToString("N2", CultureInfo.InvariantCulture);

    public static string MoneySigned(decimal v) =>
        (v > 0 ? "+" : "") + Money(v);

    public static string Cls(decimal v) => v < 0 ? "money-neg" : v > 0 ? "money-pos" : "";

    /// <summary>A day of the month as "1st", "22nd", "13th" — 11/12/13 are the usual exceptions.</summary>
    public static string Ordinal(int day)
    {
        string suffix = (day % 100) is >= 11 and <= 13 ? "th"
            : (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return day + suffix;
    }
}
