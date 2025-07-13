using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace DocumentExtractor.Desktop.ViewModels;

/// <summary>
/// Converter for toggle buttons - returns blue background when true, gray when false
/// </summary>
public class BoolToViewModeConverter : IValueConverter
{
    public static readonly BoolToViewModeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Brushes.DarkBlue : Brushes.Gray;
        }
        
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}