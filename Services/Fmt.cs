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
}
