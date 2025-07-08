using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia;
using CommunityToolkit.Mvvm.Input;

namespace DocumentExtractor.Desktop.ViewModels;

/// <summary>
/// Converter to change button text based on mapping mode
/// </summary>
public class BoolToTextConverter : IValueConverter
{
    public static readonly BoolToTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isMappingMode)
        {
            return isMappingMode ? "🛑 Exit Mapping" : "🎯 Start Mapping";
        }
        return "🎯 Start Mapping";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter to change button command based on mapping mode
/// </summary>
public class BoolToCommandConverter : IValueConverter
{
    public static readonly BoolToCommandConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // This would need to be bound to the actual commands in the ViewModel
        // For now, return a placeholder
        return new RelayCommand(() => { });
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter to change button color based on mapping mode
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isMappingMode)
        {
            return isMappingMode ? Brushes.Red : Brushes.Orange;
        }
        return Brushes.Orange;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for mapping mode indicator background color
/// </summary>
public class BoolToMappingColorConverter : IValueConverter
{
    public static readonly BoolToMappingColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isMappingMode)
        {
            return isMappingMode ? Brushes.Orange : Brushes.LightGray;
        }
        return Brushes.LightGray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for mapping mode indicator text
/// </summary>
public class BoolToMappingTextConverter : IValueConverter
{
    public static readonly BoolToMappingTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isMappingMode)
        {
            return isMappingMode ? "🎯 MAPPING MODE ACTIVE - Click template to map fields" : "Mapping mode disabled";
        }
        return "Mapping mode disabled";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for status indicator color
/// </summary>
public class BoolToStatusColorConverter : IValueConverter
{
    public static readonly BoolToStatusColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isTemplateLoaded)
        {
            return isTemplateLoaded ? Brushes.Green : Brushes.Red;
        }
        return Brushes.Red;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for chat message background color
/// </summary>
public class BoolToBackgroundConverter : IValueConverter
{
    public static readonly BoolToBackgroundConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isFromUser)
        {
            return isFromUser ? Brushes.LightBlue : Brushes.White;
        }
        return Brushes.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for chat message alignment
/// </summary>
public class BoolToAlignmentConverter : IValueConverter
{
    public static readonly BoolToAlignmentConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isFromUser)
        {
            return isFromUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        return HorizontalAlignment.Left;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}