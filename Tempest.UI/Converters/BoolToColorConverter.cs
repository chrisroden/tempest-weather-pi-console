using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Tempest.UI.Converters;

public class BoolToColorConverter : IValueConverter
{
    private static readonly IBrush ConnectedBrush = new SolidColorBrush(Color.Parse("#00C853"));
    private static readonly IBrush DisconnectedBrush = new SolidColorBrush(Color.Parse("#DC3545"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isConnected)
        {
            return isConnected
                ? ConnectedBrush
                : DisconnectedBrush;
        }

        return DisconnectedBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
