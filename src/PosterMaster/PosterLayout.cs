using System.Globalization;

namespace PosterMaster;

public readonly record struct PosterOptions(
    double PosterWidthMm, double PosterHeightMm,
    double PaperWidthMm, double PaperHeightMm,
    double MarginMm, double OverlapMm);

public readonly record struct PosterTile(
    int Row, int Column, double Xmm, double Ymm, double ContentWidthMm, double ContentHeightMm)
{
    public int Number { get; init; }
    public string Name => Number.ToString(CultureInfo.InvariantCulture);
    public string FileName => Name;
}

public sealed class PosterLayout
{
    internal const int MaxPages = 500;

    public PosterOptions Options { get; }
    public IReadOnlyList<PosterTile> Tiles { get; }
    public int Columns { get; }
    public int Rows { get; }
    public double AvailableWidthMm => Options.PaperWidthMm - 2 * Options.MarginMm;
    public double AvailableHeightMm => Options.PaperHeightMm - 2 * Options.MarginMm;
    public double StepWidthMm => AvailableWidthMm - Options.OverlapMm;
    public double StepHeightMm => AvailableHeightMm - Options.OverlapMm;

    public PosterLayout(PosterOptions options)
    {
        Options = options;
        if (!double.IsFinite(options.PosterWidthMm) || !double.IsFinite(options.PosterHeightMm)
            || options.PosterWidthMm <= 0 || options.PosterHeightMm <= 0)
            throw new ArgumentException("Размер плаката должен быть положительным.");
        if (!double.IsFinite(options.MarginMm) || options.MarginMm < 0 ||
            AvailableWidthMm <= 0 || AvailableHeightMm <= 0)
            throw new ArgumentException("Поля слишком велики для выбранного формата бумаги.");
        if (!double.IsFinite(options.OverlapMm) || options.OverlapMm < 0 ||
            options.OverlapMm >= AvailableWidthMm || options.OverlapMm >= AvailableHeightMm)
            throw new ArgumentException("Перекрытие должно быть меньше печатной области листа.");

        Columns = Count(options.PosterWidthMm, AvailableWidthMm, StepWidthMm);
        Rows = Count(options.PosterHeightMm, AvailableHeightMm, StepHeightMm);
        if ((long)Columns * Rows > MaxPages)
            throw new ArgumentException($"Слишком много листов (более {MaxPages}). Измените размеры или перекрытие.");

        var tiles = new List<PosterTile>(Columns * Rows);
        for (int row = 0; row < Rows; row++)
        for (int col = 0; col < Columns; col++)
        {
            double x = col * StepWidthMm;
            double y = row * StepHeightMm;
            tiles.Add(new PosterTile(row, col, x, y,
                Math.Min(AvailableWidthMm, options.PosterWidthMm - x),
                Math.Min(AvailableHeightMm, options.PosterHeightMm - y))
            {
                Number = tiles.Count + 1
            });
        }
        Tiles = tiles;
    }

    private static int Count(double target, double firstPage, double advance)
    {
        if (target <= firstPage + 1e-7) return 1;
        double pageCount = 1 + Math.Ceiling((target - firstPage - 1e-7) / advance);
        return (int)Math.Min(pageCount, MaxPages + 1);
    }
}
