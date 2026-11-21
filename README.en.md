# Rezak

[Русский](README.md)

Rezak is a Windows app that splits an image across A4 or A3 sheets so you can print a poster at a specified physical size. Enter the finished width and height and Rezak calculates how many sheets you need. Unused space in the last row and column stays white. The app interface is in Russian.

## Running Rezak

If you have a prebuilt copy for Windows 10/11 x64, open `Rezak.exe` next to the `Data/` folder. The build includes the .NET runtime. To move it to another PC, copy the entire application folder.

If you downloaded the source code, [build the app](#building-from-source) first. Then open `dist\Rezak.exe` or run `Start_Windows.bat` from the repository root.

## Printing a poster

1. Open a PNG, JPEG, BMP, TIFF, or WebP image, or drag a single file into the window. WebP support depends on the codecs installed in Windows.
2. Enter the finished poster width and height in centimeters or millimeters. Switching units converts the dimensions, margins, and overlap already entered.
3. Choose the paper size (A4 or A3), orientation, margins, and overlap. Check the layout and sheet count in the preview.
4. Export to PDF or PNG, or print from the app. The export resolutions are 150, 200, and 300 DPI.
5. When printing, match the paper size and orientation, choose **100% / Actual size**, and turn off **Fit to page**. Set margins at least as wide as the printer's unprintable area. You can measure a test sheet before printing the whole poster.
6. Assemble the poster using the assembly map. If you set an overlap, line up the repeated strips on neighboring sheets. With zero overlap, trim the margins at the seams and join the pieces edge to edge.

The finished poster dimensions refer to the image area and exclude margins and duplicated strips. Sharpness on a large poster depends on the source image resolution. Layout and export settings are saved when you close the app. Use the Settings window to export or import them as JSON.

## Export files

- **PNG** saves full sheets with margins as `Pages/1.png`, `Pages/2.png`, and so on. Sheets are numbered from left to right and top to bottom. The sheets have no trim marks or labels. The export also includes `Assembly_Map.png` with sheet numbers and `Assembly_Readme.txt` with layout settings and assembly instructions.
- **PDF** contains the assembly map on the first page and the poster tiles from page 2 onward. Print from page 2 to assemble the poster. Optional trim marks are available for PDF and direct printing when the margins are at least 3 mm.

## How the layout is calculated

A4 is 210 × 297 mm and A3 is 297 × 420 mm. On each axis, the usable area is the sheet dimension minus both margins. The grid step is the usable area minus the overlap. The preview lines show the grid steps. With overlap, adjacent sheets share a strip of the image.

For example, a **100 × 50 cm** poster on landscape A4 sheets with 8 mm margins and no overlap has a usable area of **281 × 194 mm** per sheet. The layout uses 4 columns and 3 rows, for **12 sheets**. The image is 157 mm wide in the rightmost column and 112 mm high in the bottom row.

## Building from source

To build on Windows, install the **.NET 8 SDK** or a newer .NET SDK. To build the self-contained Windows x64 package, also install Visual Studio or Visual Studio Build Tools with the **Desktop development with C++** workload. The project has no third-party NuGet dependencies.

Run [`Build_EXE_Windows.bat`](Build_EXE_Windows.bat) from the repository root. After a successful build, the script replaces `dist/` with the `Rezak.exe` launcher and the app files in `Data/`. Intermediate build files go in `artifacts/`.

For development, open [`PosterMaster.sln`](PosterMaster.sln) in Rider or Visual Studio, or run this command from the repository root.

```powershell
dotnet run --project src/PosterMaster/PosterMaster.csproj
```

The C# and WPF app is in [`src/PosterMaster/`](src/PosterMaster/). The C launcher is in [`src/PosterMaster.Launcher/`](src/PosterMaster.Launcher/).
