using System.Globalization;
using System.Windows.Data;

namespace PersonnalPass.App.Services;

/// <summary>Première lettre du nom, affichée dans la pastille de chaque carte.</summary>
public sealed class InitialConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = (value as string)?.Trim();
        return string.IsNullOrEmpty(text) ? "?" : char.ToUpper(text[0], culture).ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
