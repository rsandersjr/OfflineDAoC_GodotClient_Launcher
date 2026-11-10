using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OfflineDAoCLauncher.Controls;

/// <summary>
/// The "Offline DAoC 1.65" title mark in the top-left corner, drawn as outlined text so it
/// needs no image file and looks the same on every platform.
/// </summary>
public sealed class LogoControl : Control
{
    private static readonly FontFamily Serif =
        new("Palatino Linotype, Book Antiqua, Georgia, DejaVu Serif, Times New Roman, serif");

    private static readonly IBrush Gold = Vertical("#FFF1B8", "#E0B24A", "#8A5F14");
    private static readonly IBrush Ember = Vertical("#FFB14A", "#E8401A", "#8E0C0C");
    private static readonly IPen GoldEdge = new Pen(new SolidColorBrush(Color.Parse("#F3D27A")), 1.1);
    private static readonly IPen DarkEdge = new Pen(new SolidColorBrush(Color.Parse("#3A2606")), 0.8);
    private static readonly IBrush Shadow = new SolidColorBrush(Color.Parse("#B0000000"));

    public override void Render(DrawingContext context)
    {
        DrawWord(context, "Offline", 24, FontStyle.Italic, new Point(26, 0), Gold, DarkEdge);
        DrawWord(context, "DAoC", 48, FontStyle.Normal, new Point(0, 20), Ember, GoldEdge);
        DrawWord(context, "1.65", 16, FontStyle.Italic, new Point(147, 49), Gold, DarkEdge);
    }

    private static void DrawWord(
        DrawingContext context, string text, double size, FontStyle style, Point origin, IBrush fill, IPen edge)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(Serif, style, FontWeight.Bold),
            size,
            fill);

        if (formatted.BuildGeometry(origin + new Vector(2, 2)) is { } shadow)
            context.DrawGeometry(Shadow, null, shadow);
        if (formatted.BuildGeometry(origin) is { } outline)
            context.DrawGeometry(fill, edge, outline);
    }

    private static IBrush Vertical(string top, string middle, string bottom) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.Parse(top), 0),
            new GradientStop(Color.Parse(middle), 0.5),
            new GradientStop(Color.Parse(bottom), 1),
        },
    };
}
