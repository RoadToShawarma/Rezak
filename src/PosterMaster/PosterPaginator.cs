using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PosterMaster;

public sealed class PosterPaginator : DocumentPaginator
{
    private readonly PosterLayout _layout;
    private readonly BitmapSource _source;
    private readonly bool _trimMarks;

    public PosterPaginator(PosterLayout layout, BitmapSource source, bool trimMarks)
    {
        _layout = layout;
        _source = source;
        _trimMarks = trimMarks;
        PageSize = new Size(PosterRenderer.MmToDip(layout.Options.PaperWidthMm),
            PosterRenderer.MmToDip(layout.Options.PaperHeightMm));
    }

    public override DocumentPage GetPage(int pageNumber)
    {
        if (pageNumber < 0 || pageNumber >= PageCount) return DocumentPage.Missing;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            PosterRenderer.DrawSheet(dc, _layout, _layout.Tiles[pageNumber], _source, _trimMarks);
        return new DocumentPage(visual, PageSize, new Rect(PageSize), new Rect(PageSize));
    }

    public override bool IsPageCountValid => true;
    public override int PageCount => _layout.Tiles.Count;
    public override IDocumentPaginatorSource? Source => null;
    public override Size PageSize { get; set; }
}
