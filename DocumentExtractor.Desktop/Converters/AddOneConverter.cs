using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace DocumentExtractor.Desktop.Views;

/// <summary>
/// Converter that adds 1 to a number - useful for converting 0-based indices to 1-based display
/// </summary>
public class AddOneConverter : IValueConverter
{
    public static readonly AddOneConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue + 1;
        }
        
        if (value is double doubleValue)
        {
            return doubleValue + 1;
        }
        
        if (value is decimal decimalValue)
        {
            return decimalValue + 1;
        }
        
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue - 1;
        }
        
        if (value is double doubleValue)
        {
            return doubleValue - 1;
        }
        
        if (value is decimal decimalValue)
        {
            return decimalValue - 1;
        }
        
        return value;
    }
}