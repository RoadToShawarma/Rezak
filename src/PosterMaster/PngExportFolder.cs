using System.Globalization;
using System.IO;

namespace PosterMaster;

internal sealed class PngExportFolder : IDisposable
{
    private const string Pages = "Pages";
    private static readonly string[] AssemblyFiles = ["Assembly_Map.png", "Assembly_Readme.txt"];
    private readonly string _destination;
    private readonly string _workPath;
    private bool _keepBackup;

    public PngExportFolder(string destination)
    {
        _destination = destination;
        _workPath = Path.Combine(destination, $".PosterMaster-{Guid.NewGuid():N}");
        StagingPath = Path.Combine(_workPath, "new");
        Directory.CreateDirectory(PagesPath);
    }

    public string StagingPath { get; }
    public string PagesPath => Path.Combine(StagingPath, Pages);

    public static bool HasExistingExport(string destination)
        => ExistingFiles(destination).Any();

    private static IEnumerable<string> ExistingFiles(string destination)
    {
        string pagesPath = Path.Combine(destination, Pages);
        if (Directory.Exists(pagesPath))
        {
            foreach (string page in Directory.EnumerateFiles(pagesPath, "*.png"))
                if (IsExportedPage(Path.GetFileName(page)))
                    yield return page;
        }

        foreach (string name in AssemblyFiles)
        {
            string path = Path.Combine(destination, name);
            if (File.Exists(path))
                yield return path;
        }
    }

    public void Publish(bool replaceExisting)
    {
        using var exportLock = new FileStream(Path.Combine(_destination, ".PosterMaster-export.lock"),
            FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);

        string destinationPages = Path.Combine(_destination, Pages);
        string backupPath = Path.Combine(_workPath, "previous");
        string[] existingFiles = ExistingFiles(_destination).ToArray();
        if (!replaceExisting && existingFiles.Length > 0)
            throw new IOException("В папке появились файлы другого экспорта. Запустите экспорт снова, чтобы подтвердить замену.");

        Directory.CreateDirectory(destinationPages);
        Directory.CreateDirectory(Path.Combine(backupPath, Pages));
        var completedMoves = new List<(string From, string To)>();

        void Move(string from, string to)
        {
            File.Move(from, to);
            completedMoves.Add((from, to));
        }

        try
        {
            foreach (string oldFile in existingFiles)
            {
                Move(oldFile, Path.Combine(backupPath, Path.GetRelativePath(_destination, oldFile)));
            }

            foreach (string newPage in Directory.EnumerateFiles(PagesPath, "*.png"))
                Move(newPage, Path.Combine(destinationPages, Path.GetFileName(newPage)));

            foreach (string name in AssemblyFiles)
                Move(Path.Combine(StagingPath, name), Path.Combine(_destination, name));
        }
        catch (Exception publishError)
        {
            try
            {
                for (int i = completedMoves.Count - 1; i >= 0; i--)
                {
                    var (from, to) = completedMoves[i];
                    File.Move(to, from);
                }
            }
            catch (Exception restoreError)
            {
                _keepBackup = true;
                throw new IOException($"Не удалось восстановить предыдущий экспорт. Временная папка: {_workPath}",
                    new AggregateException(publishError, restoreError));
            }
            throw;
        }
    }

    private static bool IsExportedPage(string name)
    {
        string stem = Path.GetFileNameWithoutExtension(name);
        if (int.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            && number is >= 1 and <= PosterLayout.MaxPages
            && stem == number.ToString(CultureInfo.InvariantCulture))
            return true;

        // Recognize exports created before pages were numbered sequentially.
        string[] parts = name.Split('_', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out int row) ||
            !int.TryParse(parts[1], out int column) ||
            row is < 1 or > PosterLayout.MaxPages || column is < 1 or > PosterLayout.MaxPages)
            return false;

        int rowIndex = row - 1;
        char letter = (char)('A' + rowIndex % 26);
        string suffix = rowIndex >= 26 ? (rowIndex / 26).ToString(CultureInfo.InvariantCulture) : "";
        string legacyName = FormattableString.Invariant($"{row:D2}_{column:D2}_{letter}{suffix}-{column}.png");
        return string.Equals(name, legacyName, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (!_keepBackup)
            Directory.Delete(_workPath, recursive: true);
    }
}
