using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace PosterMaster;

// Minimal PDF 1.4 writer. Each physical-size page contains a full-page JPEG image.
// No third-party libraries and no dependence on a PDF printer.
public static class PdfExporter
{
    public static void Save(string fileName, PosterLayout layout, BitmapSource source,
        int dpi, bool trimMarks, Action<int, int>? progress = null)
    {
        string destination = Path.GetFullPath(fileName);
        string stagingPath = Path.Combine(Path.GetDirectoryName(destination)!, $".PosterMaster-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                int n = layout.Tiles.Count + 1;
                var offsets = new List<long> { 0 };
                WriteAscii(stream, "%PDF-1.4\n%Rezak\n");
                int catalogId = 1, pagesId = 2;
                int numberOfObjects = 2 + n * 3;
                long[] pageIds = Enumerable.Range(0, n).Select(i => (long)(3 + i * 3)).ToArray();
                WriteObject(stream, offsets, catalogId, $"<< /Type /Catalog /Pages {pagesId} 0 R >>");
                WriteObject(stream, offsets, pagesId, "<< /Type /Pages /Count " + n + " /Kids [" +
                    string.Join(" ", pageIds.Select(x => $"{x} 0 R")) + "] >>");

                double ptW = layout.Options.PaperWidthMm * 72 / 25.4;
                double ptH = layout.Options.PaperHeightMm * 72 / 25.4;
                string N(double val) => val.ToString("0.######", CultureInfo.InvariantCulture);

                for (int i = 0; i < n; i++)
                {
                    int pageId = 3 + i * 3, imgId = pageId + 1, contentId = pageId + 2;
                    var bitmap = i == 0
                        ? AssemblyMap.Render(layout, source)
                        : PosterRenderer.RenderSheet(layout, layout.Tiles[i - 1], source, dpi, trimMarks);
                    double imageWidth = ptW, imageHeight = ptH, left = 0, bottom = 0;
                    if (i == 0)
                    {
                        const double mapMargin = 10 * 72 / 25.4;
                        double scale = Math.Min((ptW - 2 * mapMargin) / bitmap.PixelWidth,
                            (ptH - 2 * mapMargin) / bitmap.PixelHeight);
                        imageWidth = bitmap.PixelWidth * scale;
                        imageHeight = bitmap.PixelHeight * scale;
                        left = (ptW - imageWidth) / 2;
                        bottom = (ptH - imageHeight) / 2;
                    }
                    var jpeg = PosterRenderer.EncodeJpeg(bitmap);
                    WriteObject(stream, offsets, pageId,
                        $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 {N(ptW)} {N(ptH)}] " +
                        $"/Resources << /XObject << /Im1 {imgId} 0 R >> >> /Contents {contentId} 0 R >>");
                    WriteStreamObject(stream, offsets, imgId,
                        $"/Type /XObject /Subtype /Image /Width {bitmap.PixelWidth} /Height {bitmap.PixelHeight} " +
                        "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode", jpeg);
                    byte[] content = Encoding.ASCII.GetBytes(
                        $"q\n{N(imageWidth)} 0 0 {N(imageHeight)} {N(left)} {N(bottom)} cm\n/Im1 Do\nQ\n");
                    WriteStreamObject(stream, offsets, contentId, "", content);
                    progress?.Invoke(i + 1, n);
                }

                long xrefOffset = stream.Position;
                WriteAscii(stream, $"xref\n0 {numberOfObjects + 1}\n0000000000 65535 f \n");
                for (int i = 1; i <= numberOfObjects; i++)
                    WriteAscii(stream, $"{offsets[i]:D10} 00000 n \n");
                WriteAscii(stream, $"trailer\n<< /Size {numberOfObjects + 1} /Root {catalogId} 0 R >>\n" +
                    $"startxref\n{xrefOffset}\n%%EOF\n");
            }

            File.Move(stagingPath, destination, overwrite: true);
        }
        finally
        {
            File.Delete(stagingPath);
        }
    }

    private static void WriteObject(Stream s, List<long> offsets, int id, string body)
    {
        Register(s, offsets, id);
        WriteAscii(s, $"{id} 0 obj\n{body}\nendobj\n");
    }

    private static void WriteStreamObject(Stream s, List<long> offsets, int id, string dict, byte[] bytes)
    {
        Register(s, offsets, id);
        WriteAscii(s, $"{id} 0 obj\n<< {dict} /Length {bytes.Length} >>\nstream\n");
        s.Write(bytes);
        WriteAscii(s, "\nendstream\nendobj\n");
    }

    private static void Register(Stream s, List<long> offsets, int id)
    {
        if (id != offsets.Count) throw new InvalidOperationException("Неверный порядок PDF объектов.");
        offsets.Add(s.Position);
    }

    private static void WriteAscii(Stream stream, string str)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(str);
        stream.Write(bytes);
    }
}
