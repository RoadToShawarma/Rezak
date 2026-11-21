using System.IO;
using System.Text.Json;

namespace PosterMaster;

internal sealed record UserSettings
{
    private static readonly string LocalPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rezak", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public required double PosterWidthMm { get; init; }
    public required double PosterHeightMm { get; init; }
    public required double MarginMm { get; init; }
    public required double OverlapMm { get; init; }
    public required bool UseCentimeters { get; init; }
    public required bool UseA3 { get; init; }
    public required bool Landscape { get; init; }
    public required int Dpi { get; init; }
    public required bool TrimMarks { get; init; }

    public PosterLayout CreateLayout()
    {
        double shortSide = UseA3 ? 297 : 210;
        double longSide = UseA3 ? 420 : 297;
        return new PosterLayout(new PosterOptions(PosterWidthMm, PosterHeightMm,
            Landscape ? longSide : shortSide, Landscape ? shortSide : longSide, MarginMm, OverlapMm));
    }

    public static UserSettings Load(string path)
    {
        UserSettings settings;
        try
        {
            using var stream = File.OpenRead(path);
            settings = JsonSerializer.Deserialize<UserSettings>(stream, JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Файл не содержит полный набор настроек программы «Резак».", ex);
        }

        if (settings.Dpi is not (150 or 200 or 300))
            throw new InvalidDataException("Допустимые значения DPI — 150, 200 и 300.");
        settings.CreateLayout();
        return settings;
    }

    public static UserSettings? LoadSaved()
    {
        try
        {
            return Load(LocalPath);
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public void SaveLocal() => Save(LocalPath);

    public void Save(string path)
    {
        CreateLayout();
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write))
                JsonSerializer.Serialize(stream, this, JsonOptions);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
