using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PosterMaster;

internal static class AssemblyMap
{
    public static BitmapSource Render(PosterLayout layout, BitmapSource source)
    {
        const int padding = 20;
        const int minCellWidth = 60;
        const int minCellHeight = 30;
        var options = layout.Options;
        double scale = 1760 / Math.Max(options.PosterWidthMm, options.PosterHeightMm);
        double[] columns = AssemblyGridEdges(layout.Columns, layout.StepWidthMm,
            options.PosterWidthMm, scale, minCellWidth);
        double[] rows = AssemblyGridEdges(layout.Rows, layout.StepHeightMm,
            options.PosterHeightMm, scale, minCellHeight);
        int width = (int)Math.Ceiling(columns[^1]) + 2 * padding;
        int height = (int)Math.Ceiling(rows[^1]) + 2 * padding;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            var line = new Pen(Brushes.DarkBlue, 1);
            var labelBackground = new SolidColorBrush(Color.FromArgb(235, 12, 43, 94));
            foreach (var tile in layout.Tiles)
            {
                var cell = new Rect(padding + columns[tile.Column], padding + rows[tile.Row],
                    columns[tile.Column + 1] - columns[tile.Column],
                    rows[tile.Row + 1] - rows[tile.Row]);
                double right = tile.Column + 1 == layout.Columns
                    ? options.PosterWidthMm : tile.Xmm + layout.StepWidthMm;
                double bottom = tile.Row + 1 == layout.Rows
                    ? options.PosterHeightMm : tile.Ymm + layout.StepHeightMm;
                double imageWidth = cell.Width * (options.PosterWidthMm / (right - tile.Xmm));
                double imageHeight = cell.Height * (options.PosterHeightMm / (bottom - tile.Ymm));

                // Expand narrow cells so every label fits, preserving their source boundaries.
                dc.PushClip(new RectangleGeometry(cell));
                dc.DrawImage(source, new Rect(cell.X - imageWidth * (tile.Xmm / options.PosterWidthMm),
                    cell.Y - imageHeight * (tile.Ymm / options.PosterHeightMm), imageWidth, imageHeight));
                dc.Pop();
                dc.DrawRectangle(null, line, cell);

                var text = new FormattedText(tile.Name, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI Bold"), 14, Brushes.White, 1.0);
                double x = cell.X + (cell.Width - text.Width) / 2;
                double y = cell.Y + (cell.Height - text.Height) / 2;
                dc.DrawRoundedRectangle(labelBackground, null,
                    new Rect(x - 4, y - 2, text.Width + 8, text.Height + 4), 3, 3);
                dc.DrawText(text, new Point(x, y));
            }
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static double[] AssemblyGridEdges(int count, double step, double extent, double scale, int minCellSize)
    {
        var edges = new double[count + 1];
        for (int i = 0; i < count; i++)
        {
            double span = i + 1 == count ? extent - i * step : step;
            edges[i + 1] = edges[i] + Math.Max(minCellSize, span * scale);
        }
        return edges;
    }
}
