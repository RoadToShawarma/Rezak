using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PosterMaster;

public static class PosterRenderer
{
    public static double MmToDip(double mm) => mm * 96.0 / 25.4;

    public static void DrawSheet(DrawingContext dc, PosterLayout layout, PosterTile tile,
        BitmapSource image, bool trimMarks = true, bool pageLabels = true)
    {
        var opt = layout.Options;
        double sheetW = MmToDip(opt.PaperWidthMm);
        double sheetH = MmToDip(opt.PaperHeightMm);
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, sheetW, sheetH));

        double mx = MmToDip(opt.MarginMm);
        double my = mx;
        double left = mx;
        double top = my;
        double w = MmToDip(tile.ContentWidthMm);
        double h = MmToDip(tile.ContentHeightMm);
        double posterW = MmToDip(opt.PosterWidthMm);
        double posterH = MmToDip(opt.PosterHeightMm);

        // Clip the source image to the *actual* fragment size, not to the sheet.
        // This is what leaves unused white space on the last row/column.
        dc.PushClip(new RectangleGeometry(new Rect(left, top, w, h)));
        dc.DrawImage(image, new Rect(left - MmToDip(tile.Xmm),
            top - MmToDip(tile.Ymm), posterW, posterH));
        dc.Pop();

        if (trimMarks && opt.MarginMm >= 3)
        {
            var pen = new Pen(Brushes.Black, 0.45);
            double tick = MmToDip(Math.Min(3.0, opt.MarginMm - 1.0));
            if (tick > 0)
            {
                foreach (double x in new[] { left, left + w })
                {
                    dc.DrawLine(pen, new Point(x, top - tick), new Point(x, top - 1));
                    dc.DrawLine(pen, new Point(x, top + h + 1), new Point(x, top + h + tick));
                }
                foreach (double y in new[] { top, top + h })
                {
                    dc.DrawLine(pen, new Point(left - tick, y), new Point(left - 1, y));
                    dc.DrawLine(pen, new Point(left + w + 1, y), new Point(left + w + tick, y));
                }
            }
        }

        if (pageLabels && opt.MarginMm >= 7)
        {
            var text = new FormattedText($"{tile.Name}  •  {tile.Row + 1}/{layout.Rows} × {tile.Column + 1}/{layout.Columns}",
                System.Globalization.CultureInfo.GetCultureInfo("ru-RU"),
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 8, Brushes.Black, 1.0);
            double labelY = sheetH - Math.Max(MmToDip(opt.MarginMm) - 3, 11);
            dc.DrawText(text, new Point(left, labelY));
        }
    }

    public static RenderTargetBitmap RenderSheet(PosterLayout layout, PosterTile tile,
        BitmapSource image, int dpi, bool trimMarks, bool pageLabels = true)
    {
        double dipW = MmToDip(layout.Options.PaperWidthMm);
        double dipH = MmToDip(layout.Options.PaperHeightMm);
        int pixelW = (int)Math.Round(dipW * dpi / 96.0);
        int pixelH = (int)Math.Round(dipH * dpi / 96.0);
        if ((long)pixelW * pixelH > 90_000_000)
            throw new InvalidOperationException("Слишком высокое разрешение для одного листа.");
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            DrawSheet(dc, layout, tile, image, trimMarks, pageLabels);
        var bitmap = new RenderTargetBitmap(pixelW, pixelH, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public static void SavePng(BitmapSource bitmap, string fileName)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(fileName);
        encoder.Save(stream);
    }

    public static byte[] EncodeJpeg(BitmapSource bitmap)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 94 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
