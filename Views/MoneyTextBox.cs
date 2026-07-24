using System.Globalization;
using System.Text;
using System.Windows.Controls;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>
/// A text box that formats what you type as currency in real time — adds the leading $ and
/// thousands separators while you enter the number, keeping the caret in a sensible spot. Allows an
/// optional leading minus (for money-out amounts). <see cref="Value"/> reads the parsed decimal.
/// </summary>
public class MoneyTextBox : TextBox
{
    private bool _updating;
    private readonly bool _allowNegative;

    public MoneyTextBox(decimal initial = 0m, bool allowNegative = false)
    {
        _allowNegative = allowNegative;
        Style = St("Input");
        TextAlignment = System.Windows.TextAlignment.Right;
        SetValue(initial);
        TextChanged += OnTextChanged;
    }

    /// <summary>Parsed numeric value of the current text.</summary>
    public decimal Value
    {
        get
        {
            var t = Sanitize(Text, _allowNegative, out _, out _);
            return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
        }
    }

    public void SetValue(decimal v)
    {
        _updating = true;
        Text = v == 0m ? "" : Format(v.ToString("0.##", CultureInfo.InvariantCulture), _allowNegative);
        CaretIndex = Text.Length;
        _updating = false;
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updating) return;

        int caret = CaretIndex;
        int sigBefore = CountSignificant(Text, caret);

        var formatted = Format(Text, _allowNegative);

        _updating = true;
        Text = formatted;
        CaretIndex = CaretForSignificant(formatted, sigBefore);
        _updating = false;
    }

    // '-', digits and '.' are "significant"; '$', ',' and spaces are decoration.
    private static bool IsSignificant(char c) => char.IsDigit(c) || c == '.' || c == '-';

    private static int CountSignificant(string s, int upTo)
    {
        int n = 0;
        for (int i = 0; i < upTo && i < s.Length; i++)
            if (IsSignificant(s[i])) n++;
        return n;
    }

    private static int CaretForSignificant(string s, int sig)
    {
        if (sig <= 0) return FirstDigitOrEnd(s);
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (IsSignificant(s[i]))
            {
                n++;
                if (n == sig) return i + 1;
            }
        }
        return s.Length;
    }

    private static int FirstDigitOrEnd(string s)
    {
        for (int i = 0; i < s.Length; i++)
            if (char.IsDigit(s[i])) return i;
        return s.Length;
    }

    /// <summary>Strip decoration and keep a valid number: optional leading '-', digits, one '.', ≤2 decimals.</summary>
    private static string Sanitize(string raw, bool allowNeg, out string intPart, out string decPart)
    {
        bool neg = allowNeg && raw.TrimStart().StartsWith('-');
        var sb = new StringBuilder();
        bool seenDot = false;
        int decimals = 0;
        foreach (var c in raw)
        {
            if (char.IsDigit(c))
            {
                if (seenDot) { if (decimals >= 2) continue; decimals++; }
                sb.Append(c);
            }
            else if (c == '.' && !seenDot)
            {
                seenDot = true;
                sb.Append('.');
            }
        }

        var digits = sb.ToString();
        int dot = digits.IndexOf('.');
        if (dot < 0) { intPart = digits; decPart = ""; }
        else { intPart = digits[..dot]; decPart = digits[(dot + 1)..]; }

        var full = (neg ? "-" : "") + (string.IsNullOrEmpty(intPart) ? "0" : intPart) + (seenDot ? "." + decPart : "");
        return full;
    }

    private static string Format(string raw, bool allowNeg)
    {
        var clean = Sanitize(raw, allowNeg, out string intPart, out string decPart);
        if (clean is "0" or "-0" && !raw.Contains('.')) return "";

        bool neg = clean.StartsWith('-');
        bool hasDot = raw.Contains('.');

        var grouped = GroupThousands(string.IsNullOrEmpty(intPart) ? "0" : intPart);
        var sb = new StringBuilder();
        if (neg) sb.Append('-');
        sb.Append('$').Append(grouped);
        if (hasDot) sb.Append('.').Append(decPart);
        return sb.ToString();
    }

    private static string GroupThousands(string digits)
    {
        digits = digits.TrimStart('0');
        if (digits.Length == 0) digits = "0";
        return long.TryParse(digits, out var n) ? n.ToString("N0", CultureInfo.InvariantCulture) : digits;
    }
}
