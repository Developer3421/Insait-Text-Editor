using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace InsaitTextEditor.Scripts.Converters
{
    /// <summary>
    /// Converter that cleans text from service tokens, e.g.: </s>.
    /// The Tokens property accepts a comma-separated list. Default is </s>.
    /// </summary>
    public class RemoveSpecialTokensConverter : IValueConverter
    {
        public string? Tokens { get; set; } = "</s>";

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string s || string.IsNullOrEmpty(s))
                return value;

            var tokens = (Tokens ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                if (string.IsNullOrEmpty(token)) continue;
                // Remove the token and common variants with newlines
                s = s.Replace(token, string.Empty, StringComparison.Ordinal);
                s = s.Replace("\r\n" + token, string.Empty, StringComparison.Ordinal)
                     .Replace("\n" + token, string.Empty, StringComparison.Ordinal)
                     .Replace(token + "\r\n", string.Empty, StringComparison.Ordinal)
                     .Replace(token + "\n", string.Empty, StringComparison.Ordinal);
            }

            // Normalize double line breaks and trim extra whitespace at edges
            while (s.Contains("\r\n\r\n", StringComparison.Ordinal))
                s = s.Replace("\r\n\r\n", "\r\n", StringComparison.Ordinal);
            while (s.Contains("\n\n", StringComparison.Ordinal))
                s = s.Replace("\n\n", "\n", StringComparison.Ordinal);

            return s.Trim();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Reverse conversion is not needed, return as-is
            return value;
        }
    }
}
