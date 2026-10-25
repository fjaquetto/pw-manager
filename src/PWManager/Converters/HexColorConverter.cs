using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PWManager.Domain.Appearance;

namespace PWManager.Converters;

public sealed class HexColorConverter : IValueConverter
{
    public static HexColorConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && RgbColor.TryParse(hex, out var color))
        {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            return new RgbColor(color.R, color.G, color.B, color.A).ToHex();
        }

        return BindingOperations.DoNothing;
    }
}
