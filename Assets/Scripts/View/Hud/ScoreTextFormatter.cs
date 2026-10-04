using System;
using System.Globalization;

public static class ScoreTextFormatter
{
    private const long Thousand = 1000;
    private const long Million = 1000000;
    private const long Billion = 1000000000;
    private const long MaxWholeWithDecimal = 100;

    private static readonly CompactSuffixes RussianSuffixes = new CompactSuffixes("к", "млн", "млрд", ",");
    private static readonly CompactSuffixes EnglishSuffixes = new CompactSuffixes("K", "M", "B", ".");
    private static readonly CompactSuffixes TurkishSuffixes = new CompactSuffixes("B", "Mn", "Mr", ",");

    public static string FormatCompact(long score, string languageCode)
    {
        if (score < 0)
            throw new ArgumentOutOfRangeException(nameof(score));

        if (score < Thousand)
            return score.ToString(CultureInfo.InvariantCulture);

        CompactSuffixes suffixes = GetSuffixes(languageCode);

        if (score >= Billion)
            return FormatInUnits(score, Billion, suffixes.Billions, suffixes.DecimalSeparator);

        if (score >= Million)
            return FormatInUnits(score, Million, suffixes.Millions, suffixes.DecimalSeparator);

        return FormatInUnits(score, Thousand, suffixes.Thousands, suffixes.DecimalSeparator);
    }

    private static string FormatInUnits(long score, long unit, string suffix, string decimalSeparator)
    {
        long whole = score / unit;
        string wholeText = whole.ToString(CultureInfo.InvariantCulture);

        if (whole >= MaxWholeWithDecimal)
            return wholeText + suffix;

        long tenths = score / (unit / 10) % 10;

        if (tenths == 0)
            return wholeText + suffix;

        return wholeText + decimalSeparator + tenths.ToString(CultureInfo.InvariantCulture) + suffix;
    }

    private static CompactSuffixes GetSuffixes(string languageCode) => languageCode switch
    {
        "ru" => RussianSuffixes,
        "tr" => TurkishSuffixes,
        _ => EnglishSuffixes
    };

    private sealed class CompactSuffixes
    {
        public string Thousands { get; }
        public string Millions { get; }
        public string Billions { get; }
        public string DecimalSeparator { get; }

        public CompactSuffixes(string thousands, string millions, string billions, string decimalSeparator)
        {
            Thousands = thousands;
            Millions = millions;
            Billions = billions;
            DecimalSeparator = decimalSeparator;
        }
    }
}
