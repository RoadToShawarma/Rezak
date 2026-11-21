using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace PosterMaster;

public partial class MainWindow : Window
{
    private BitmapSource? _image;
    private string? _imagePath;
    private PosterLayout? _layout;
    private int? _selectedTileIndex;
    private bool _ready;
    private bool _updatingControls;
    private bool _useCentimeters = true;
    private UserSettings? _lastValidSettings;
    private UserSettings? _savedSettings;

    private double MillimetersPerUnit => _useCentimeters ? 10 : 1;
    private string SizeUnit => _useCentimeters ? "см" : "мм";

    private string FormatSize(double mm) => (mm / MillimetersPerUnit).ToString("0.###");

    public MainWindow()
    {
        InitializeComponent();
        _ready = true;
        string? loadError = null;
        try
        {
            _savedSettings = UserSettings.LoadSaved();
            if (_savedSettings != null)
                ApplySettings(_savedSettings);
        }
        catch (Exception ex)
        {
            loadError = $"Не удалось загрузить настройки. {ex.Message}";
        }
        RefreshLayout();
        if (loadError != null)
        {
            _savedSettings = _lastValidSettings;
            StatusText.Text = loadError;
            StatusText.ToolTip = loadError;
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.webp|Все файлы|*.*",
            Title = "Выбрать изображение"
        };
        if (dialog.ShowDialog(this) == true)
            LoadImage(dialog.FileName);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_image == null) return;

        var confirmation = new ClearImageWindow { Owner = this };
        if (confirmation.ShowDialog() != true) return;

        _image = null;
        _imagePath = null;
        _selectedTileIndex = null;
        SourceInfo.Text = "Изображение не загружено";
        RefreshLayout();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow(ReadSettings, ImportSettings) { Owner = this }.ShowDialog();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _lastValidSettings == null || _lastValidSettings == _savedSettings) return;
        try
        {
            SaveSettings(_lastValidSettings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось сохранить настройки",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveSettings(UserSettings settings)
    {
        settings.SaveLocal();
        _savedSettings = settings;
    }

    private void ImportSettings(UserSettings settings)
    {
        ApplySettings(settings);
        RefreshLayout();
    }

    private void ApplySettings(UserSettings settings)
    {
        _updatingControls = true;
        try
        {
            _useCentimeters = settings.UseCentimeters;
            UnitsBox.SelectedIndex = settings.UseCentimeters ? 0 : 1;
            PaperBox.SelectedIndex = settings.UseA3 ? 1 : 0;
            if (settings.Landscape)
                LandscapeButton.IsChecked = true;
            else
                PortraitButton.IsChecked = true;
            PosterWidthBox.Text = FormatInput(settings.PosterWidthMm / MillimetersPerUnit);
            PosterHeightBox.Text = FormatInput(settings.PosterHeightMm / MillimetersPerUnit);
            MarginBox.Text = FormatInput(settings.MarginMm / MillimetersPerUnit);
            OverlapBox.Text = FormatInput(settings.OverlapMm / MillimetersPerUnit);
            DpiBox.SelectedIndex = settings.Dpi switch { 150 => 0, 200 => 1, _ => 2 };
            TrimMarksBox.IsChecked = settings.TrimMarks;
        }
        finally
        {
            _updatingControls = false;
        }
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false)) return;
        bool canCopyImage = (e.AllowedEffects & DragDropEffects.Copy) != 0 && GetDroppedImagePath(e.Data) != null;
        ImageDropHint.Visibility = canCopyImage ? Visibility.Visible : Visibility.Collapsed;
        e.Effects = canCopyImage && IsOverImageArea(e)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_PreviewDragLeave(object sender, DragEventArgs e)
        => ImageDropHint.Visibility = Visibility.Collapsed;

    private void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        ImageDropHint.Visibility = Visibility.Collapsed;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false)) return;
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        if (IsOverImageArea(e) && (e.AllowedEffects & DragDropEffects.Copy) != 0
            && GetDroppedImagePath(e.Data) is string path && LoadImage(path))
            e.Effects = DragDropEffects.Copy;
    }

    private bool IsOverImageArea(DragEventArgs e)
        => new Rect(ImageArea.RenderSize).Contains(e.GetPosition(ImageArea));

    private static string? GetDroppedImagePath(IDataObject data)
        => data.GetData(DataFormats.FileDrop, autoConvert: false) is string[] { Length: 1 } paths
            && !Directory.Exists(paths[0]) ? paths[0] : null;

    private bool LoadImage(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            _image = bmp;
            _imagePath = path;
            _selectedTileIndex = null;
            SourceInfo.Text = $"{Path.GetFileName(path)} • {bmp.PixelWidth} × {bmp.PixelHeight} пикс.";
            RefreshLayout();
            return true;
        }
        catch (Exception ex)
        {
            ShowError("Не удалось открыть изображение", ex);
            return false;
        }
    }

    private void Settings_Changed(object sender, RoutedEventArgs e) => RefreshLayout();

    private void Units_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _updatingControls) return;
        bool useCentimeters = UnitsBox.SelectedIndex == 0;
        if (useCentimeters == _useCentimeters) return;

        double factor = useCentimeters ? 0.1 : 10;
        _updatingControls = true;
        try
        {
            foreach (TextBox box in new[] { PosterWidthBox, PosterHeightBox, MarginBox, OverlapBox })
            {
                if (TryReadNumber(box, out double value) && double.IsFinite(value * factor))
                    box.Text = FormatInput(value * factor);
            }
            _useCentimeters = useCentimeters;
        }
        finally
        {
            _updatingControls = false;
        }
        RefreshLayout();
    }

    private static bool TryReadNumber(TextBox box, out double value)
        => double.TryParse(box.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string FormatInput(double value) => value.ToString("G15", CultureInfo.CurrentCulture);

    private void Spin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RepeatButton { Tag: TextBox box } button) return;

        double step = 1 / MillimetersPerUnit;
        double value = TryReadNumber(box, out double current) && double.IsFinite(current) ? current : 0;
        bool increase = Equals(button.Content, "▲");
        double next = Math.Max(0, value + (increase ? step : -step));
        if (increase)
        {
            double shortSideMm = PaperBox.SelectedIndex == 1 ? 297 : 210;
            bool editingMargin = box == MarginBox;
            var otherBox = editingMargin ? OverlapBox : MarginBox;
            double other = TryReadNumber(otherBox, out double parsed) && double.IsFinite(parsed)
                ? Math.Max(0, parsed) : 0;
            double usedMm = (editingMargin ? 2 * next + other : 2 * other + next) * MillimetersPerUnit;
            if (usedMm >= shortSideMm) return;
        }

        box.Text = FormatInput(next);
    }

    private static double ReadNumber(TextBox box)
    {
        if (!TryReadNumber(box, out double value))
            throw new ArgumentException("Проверьте числовые поля — используйте сантиметры и миллиметры.");
        return value;
    }

    private UserSettings ReadSettings() => new()
    {
        PosterWidthMm = ReadNumber(PosterWidthBox) * MillimetersPerUnit,
        PosterHeightMm = ReadNumber(PosterHeightBox) * MillimetersPerUnit,
        MarginMm = ReadNumber(MarginBox) * MillimetersPerUnit,
        OverlapMm = ReadNumber(OverlapBox) * MillimetersPerUnit,
        UseCentimeters = _useCentimeters,
        UseA3 = PaperBox.SelectedIndex == 1,
        Landscape = LandscapeButton.IsChecked == true,
        Dpi = GetDpi(),
        TrimMarks = TrimMarksBox.IsChecked == true
    };

    private void RefreshLayout()
    {
        if (!_ready || _updatingControls) return;
        StatusText.ToolTip = null;
        try
        {
            var settings = ReadSettings();
            _layout = settings.CreateLayout();
            _lastValidSettings = settings;
            if (_image == null)
            {
                SummaryText.Text = "Откройте изображение или перетащите его в область ниже.";
                StatusText.Text = "Готово к работе";
            }
            else
            {
                var o = _layout.Options;
                double effectiveDpi = Math.Min(_image.PixelWidth / (o.PosterWidthMm / 25.4),
                    _image.PixelHeight / (o.PosterHeightMm / 25.4));
                SummaryText.Text = $"Плакат {FormatSize(o.PosterWidthMm)} × {FormatSize(o.PosterHeightMm)} {SizeUnit}  •  " +
                    $"{_layout.Columns} столбц. × {_layout.Rows} ряд.  •  " +
                    $"Печатная область: {FormatSize(_layout.AvailableWidthMm)} × {FormatSize(_layout.AvailableHeightMm)} {SizeUnit}  •  " +
                    $"Исходное качество: ~{effectiveDpi:0} DPI";
                StatusText.Text = "Готово";
            }
            if (_selectedTileIndex is int index && index >= _layout.Tiles.Count)
                _selectedTileIndex = null;
        }
        catch (ArgumentException ex)
        {
            _layout = null;
            SummaryText.Text = ex.Message;
            StatusText.Text = "Исправьте параметры раскладки";
        }
        SheetCountText.Text = _image == null
            ? "0"
            : _layout?.Tiles.Count.ToString(CultureInfo.CurrentCulture) ?? "—";
        UpdatePreviews();
    }

    private void UpdatePreviews()
    {
        bool hasImage = _image != null;
        AddImagePanel.Visibility = hasImage ? Visibility.Collapsed : Visibility.Visible;
        ImagePreviews.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
        OriginalImagePreview.Source = _image;
        ClearButton.IsEnabled = hasImage;
        OutputButtons.IsEnabled = hasImage && _layout != null;

        PosterTile? selected = hasImage && _layout != null && _selectedTileIndex is int index
            ? _layout.Tiles[index] : null;
        WholePreview.Layout = _layout;
        WholePreview.SourceImage = _image;
        WholePreview.SelectedTile = selected;
        WholePreview.Cursor = OutputButtons.IsEnabled ? Cursors.Cross : Cursors.Arrow;
        WholePreview.InvalidateVisual();
        SelectionInfo.Text = selected is PosterTile tile
            ? $"Лист {tile.Name} · {FormatSize(tile.ContentWidthMm)} × {FormatSize(tile.ContentHeightMm)} {SizeUnit}"
            : hasImage && _layout != null ? "Щёлкните по листу, чтобы выделить его" : "";
        SelectionInfo.Visibility = _layout != null && hasImage ? Visibility.Visible : Visibility.Collapsed;
    }

    private void WholePreview_Click(object sender, MouseButtonEventArgs e)
    {
        var tile = WholePreview.GetTileAt(e.GetPosition(WholePreview));
        _selectedTileIndex = tile.HasValue ? tile.Value.Number - 1 : null;
        UpdatePreviews();
    }

    private (PosterLayout Layout, BitmapSource Source)? ValidateReady()
    {
        if (_layout == null || _image == null)
        {
            MessageBox.Show(this, "Откройте изображение и задайте корректные размеры.", "Резак",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }
        return (_layout, _image);
    }

    private int GetDpi()
        => int.Parse((DpiBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "300",
            CultureInfo.InvariantCulture);

    private async void ExportFolder_Click(object sender, RoutedEventArgs e)
    {
        var data = ValidateReady();
        if (data == null) return;
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку для PNG-фрагментов и схемы сборки"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            string imageName = Path.GetFileName(_imagePath)!;
            string folderName = Path.GetFileNameWithoutExtension(imageName).TrimEnd(' ', '.');
            if (folderName.Length == 0)
                folderName = imageName;

            string destination = Path.Combine(dialog.FolderName, folderName);
            bool replaceExisting = PngExportFolder.HasExistingExport(destination);
            if (replaceExisting && new ReplacePngExportWindow(destination) { Owner = this }.ShowDialog() != true)
                return;

            var (layout, source) = data.Value;
            int dpi = GetDpi();
            int count = layout.Tiles.Count;
            string instructions = CreateInstructions(layout);
            await RunExportAsync($"PNG: сохранено 0/{count}", count + 1, progress =>
            {
                using var export = new PngExportFolder(destination);
                foreach (var tile in layout.Tiles)
                {
                    var bitmap = PosterRenderer.RenderSheet(layout, tile, source, dpi, trimMarks: false, pageLabels: false);
                    PosterRenderer.SavePng(bitmap, Path.Combine(export.PagesPath, $"{tile.FileName}.png"));
                    progress.Report((tile.Number, $"PNG: сохранено {tile.Number}/{count}"));
                }
                progress.Report((count, $"PNG: сохранено {count}/{count}. Сохранение схемы сборки..."));
                PosterRenderer.SavePng(AssemblyMap.Render(layout, source), Path.Combine(export.StagingPath, "Assembly_Map.png"));
                File.WriteAllText(Path.Combine(export.StagingPath, "Assembly_Readme.txt"),
                    instructions, Encoding.UTF8);
                export.Publish(replaceExisting);
                progress.Report((count + 1, $"Сохранено фрагментов: {count}"));
            });
            StatusText.Text = $"Сохранено фрагментов: {count}";
            new ExportCompletedWindow("PNG сохранены",
                "Фрагменты и схема сборки сохранены в папке:", destination, folderPath: destination)
                { Owner = this }.ShowDialog();
        }
        catch (Exception ex) { ShowError("Ошибка экспорта PNG", ex); }
    }

    private string CreateInstructions(PosterLayout layout)
    {
        var o = layout.Options;
        return $"РЕЗАК — СХЕМА СБОРКИ\r\n\r\n" +
            $"Источник: {_imagePath}\r\n" +
            $"Размер готового плаката: {o.PosterWidthMm / 10:0.##} × {o.PosterHeightMm / 10:0.##} см\r\n" +
            $"Формат листа: {o.PaperWidthMm} × {o.PaperHeightMm} мм\r\n" +
            $"Поля: {o.MarginMm} мм с каждой стороны\r\n" +
            $"Перекрытие: {o.OverlapMm} мм\r\n" +
            $"Сетка: {layout.Columns} столбцов × {layout.Rows} рядов, всего {layout.Tiles.Count} листов\r\n\r\n" +
            "ПЕЧАТЬ: размер 100% / Actual size. Отключить Fit to page / Масштабировать. " +
            "Каждый PNG соответствует полному листу бумаги с полями. " +
            "На крайних листах часть бумаги может оставаться белой.\r\n" +
            "СБОРКА: номера в Assembly_Map.png соответствуют именам файлов в папке Pages.\r\n" +
            "Если перекрытие > 0: наложить общие участки соседних страниц друг на друга. " +
            "Не соединять их встык.\r\n";
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var data = ValidateReady();
        if (data == null) return;
        var dialog = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf", DefaultExt = ".pdf",
            FileName = $"{Path.GetFileNameWithoutExtension(_imagePath)}.pdf",
            Title = "Сохранить PDF для печати"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var (layout, source) = data.Value;
            string path = dialog.FileName;
            int dpi = GetDpi();
            bool trimMarks = TrimMarksBox.IsChecked == true;
            int pageCount = layout.Tiles.Count + 1;
            await RunExportAsync("PDF: сохранение схемы раскроя...", pageCount, progress =>
                PdfExporter.Save(path, layout, source, dpi, trimMarks,
                    (current, all) => progress.Report((current, $"PDF: сохранено {current}/{all}"))));
            StatusText.Text = "PDF сохранён";
            new ExportCompletedWindow("PDF сохранён",
                "Схема раскроя на первой странице.\nДля сохранения точных размеров печатайте фрагменты в масштабе 100%.",
                path, folderPath: Path.GetDirectoryName(path)!) { Owner = this }.ShowDialog();
        }
        catch (Exception ex) { ShowError("Не удалось сохранить PDF", ex); }
    }

    private async Task RunExportAsync(string initialStatus, int totalSteps,
        Action<IProgress<(int Completed, string Status)>> export)
    {
        IsEnabled = false;
        StatusText.Text = initialStatus;
        ExportProgress.Maximum = totalSteps;
        ExportProgress.Value = 0;
        ExportProgress.Visibility = Visibility.Visible;
        bool running = true;
        IProgress<(int Completed, string Status)> progress = new Progress<(int Completed, string Status)>(update =>
        {
            if (!running) return;
            ExportProgress.Value = update.Completed;
            StatusText.Text = update.Status;
        });
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // WPF renders and encodes the sheets on a dedicated STA thread.
        var worker = new Thread(() =>
        {
            try
            {
                export(progress);
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        try
        {
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            await completion.Task;
        }
        finally
        {
            running = false;
            ExportProgress.Visibility = Visibility.Collapsed;
            IsEnabled = true;
        }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var data = ValidateReady();
        if (data == null) return;
        try
        {
            var opt = data.Value.Layout.Options;
            var dlg = new System.Windows.Controls.PrintDialog();
            var ticket = dlg.PrintTicket;
            var orientation = opt.PaperWidthMm > opt.PaperHeightMm
                ? PageOrientation.Landscape : PageOrientation.Portrait;
            double shortSide = Math.Min(opt.PaperWidthMm, opt.PaperHeightMm);
            double longSide = Math.Max(opt.PaperWidthMm, opt.PaperHeightMm);
            var paperName = longSide == 420 ? PageMediaSizeName.ISOA3 : PageMediaSizeName.ISOA4;
            ticket.PageOrientation = orientation;
            ticket.PageMediaSize = new PageMediaSize(paperName);
            dlg.PrintTicket = ticket;
            if (dlg.ShowDialog() != true) return;

            if (!MatchesPrintSettings(dlg.PrintTicket, orientation, paperName, shortSide, longSide))
            {
                var response = MessageBox.Show(this,
                    "Проверьте формат и ориентацию бумаги в настройках принтера. " +
                    "Они должны соответствовать параметрам плаката. Продолжить?",
                    "Проверка печати", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (response != MessageBoxResult.Yes) return;
            }
            dlg.PrintDocument(new PosterPaginator(data.Value.Layout, data.Value.Source,
                TrimMarksBox.IsChecked == true), "Резак — плакат");
            StatusText.Text = "Задание отправлено на печать";
        }
        catch (Exception ex) { ShowError("Ошибка печати", ex); }
    }

    private static bool MatchesPrintSettings(PrintTicket ticket, PageOrientation orientation,
        PageMediaSizeName paperName, double shortSideMm, double longSideMm)
    {
        if (ticket.PageOrientation != orientation || ticket.PageMediaSize is not { } media)
            return false;

        if (media.PageMediaSizeName is { } actualName && actualName != paperName)
            return false;

        if (media.Width is double width && media.Height is double height)
        {
            const double tolerance = 4;
            return Math.Abs(Math.Min(width, height) - PosterRenderer.MmToDip(shortSideMm)) <= tolerance
                && Math.Abs(Math.Max(width, height) - PosterRenderer.MmToDip(longSideMm)) <= tolerance;
        }

        return media.PageMediaSizeName == paperName;
    }

    private void ShowError(string title, Exception ex)
    {
        StatusText.Text = title;
        MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
