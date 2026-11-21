using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PosterMaster;

public sealed class PosterPreview : FrameworkElement
{
    public PosterLayout? Layout { get; set; }
    public BitmapSource? SourceImage { get; set; }
    public PosterTile? SelectedTile { get; set; }
    private Rect GetPosterBounds(PosterLayout layout, out double scale)
    {
        double width = layout.Options.PosterWidthMm;
        double height = layout.Options.PosterHeightMm;
        scale = Math.Min((ActualWidth - 35) / width, (ActualHeight - 35) / height);
        if (!double.IsFinite(scale) || scale <= 0) return Rect.Empty;

        double scaledWidth = width * scale;
        double scaledHeight = height * scale;
        return new Rect((ActualWidth - scaledWidth) / 2, (ActualHeight - scaledHeight) / 2,
            scaledWidth, scaledHeight);
    }

    public PosterTile? GetTileAt(Point point)
    {
        if (Layout is not { } layout || SourceImage == null) return null;

        var bounds = GetPosterBounds(layout, out double scale);
        if (!bounds.Contains(point)) return null;

        // The final cell covers the rest of the poster, which can be wider than one grid step.
        int column = (int)Math.Min(layout.Columns - 1, (point.X - bounds.X) / (scale * layout.StepWidthMm));
        int row = (int)Math.Min(layout.Rows - 1, (point.Y - bounds.Y) / (scale * layout.StepHeightMm));
        return layout.Tiles[row * layout.Columns + column];
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(235, 238, 243)), null,
            new Rect(0, 0, ActualWidth, ActualHeight), 10, 10);
        if (Layout is not { } layout || SourceImage == null) return;
        var dest = GetPosterBounds(layout, out double scale);
        if (dest.IsEmpty) return;
        dc.DrawRectangle(Brushes.White, null, dest);
        dc.DrawImage(SourceImage, dest);

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), 2);
        var edgePen = new Pen(new SolidColorBrush(Color.FromRgb(30, 77, 160)), 1.2);
        for (int c = 1; c < layout.Columns; c++)
        {
            double x = dest.X + c * layout.StepWidthMm * scale;
            dc.DrawLine(gridPen, new Point(x, dest.Top), new Point(x, dest.Bottom));
            dc.DrawLine(edgePen, new Point(x, dest.Top), new Point(x, dest.Bottom));
        }
        for (int r = 1; r < layout.Rows; r++)
        {
            double y = dest.Y + r * layout.StepHeightMm * scale;
            dc.DrawLine(gridPen, new Point(dest.Left, y), new Point(dest.Right, y));
            dc.DrawLine(edgePen, new Point(dest.Left, y), new Point(dest.Right, y));
        }

        if (SelectedTile is PosterTile selected)
        {
            var rect = new Rect(dest.X + selected.Xmm * scale, dest.Y + selected.Ymm * scale,
                selected.ContentWidthMm * scale, selected.ContentHeightMm * scale);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(45, 37, 99, 235)),
                new Pen(Brushes.DodgerBlue, 3), rect);
        }

        // Number grid cells, rather than full overlapping sheets, to avoid duplicate labels.
        if (scale * Math.Min(layout.StepWidthMm, layout.StepHeightMm) > 25)
        foreach (var tile in layout.Tiles)
        {
            var text = new FormattedText(tile.Name, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"),
                12, Brushes.White, 1);
            double tx = dest.X + tile.Xmm * scale + 6;
            double ty = dest.Y + tile.Ymm * scale + 5;
            var back = new Rect(tx - 3, ty - 2, text.Width + 6, text.Height + 4);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(205, 14, 45, 95)), null, back, 4, 4);
            dc.DrawText(text, new Point(tx, ty));
        }
        dc.DrawRectangle(null, edgePen, dest);
    }
}
