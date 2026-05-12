using System;
using System.Globalization;

namespace AetherBags.Tags;

public static class NumericTagFormatter
{
    public static string Format(object value, string format, int? precision)
    {
        var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);

        if (format.Equals("k", StringComparison.OrdinalIgnoreCase))
            return FormatScaled(number, 1_000.0, "k", precision);

        if (format.Equals("m", StringComparison.OrdinalIgnoreCase))
        {
            if (Math.Abs(number) < 1_000_000.0)
                return FormatScaled(number, 1_000.0, "k", precision);

            return FormatScaled(number, 1_000_000.0, "m", precision);
        }

        if (format.Equals("r", StringComparison.OrdinalIgnoreCase) || format.Equals("raw", StringComparison.OrdinalIgnoreCase))
            return number.ToString($"F{precision ?? 0}", CultureInfo.InvariantCulture);

        if (precision == null && int.TryParse(format, out var parsedPrecision))
            precision = Math.Clamp(parsedPrecision, 0, 6);

        if (!string.IsNullOrEmpty(format) && precision == null)
        {
            try
            {
                return number.ToString(format, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
            }
        }

        return number.ToString($"N{precision ?? 0}", CultureInfo.InvariantCulture);
    }

    private static string FormatScaled(double number, double divisor, string suffix, int? precision)
    {
        if (Math.Abs(number) < divisor)
            return number.ToString($"N{precision ?? 0}", CultureInfo.InvariantCulture);

        return (number / divisor).ToString($"F{precision ?? 0}", CultureInfo.InvariantCulture) + suffix;
    }
}
