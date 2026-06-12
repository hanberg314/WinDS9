# WinDS9

WinDS9 now contains two separate tracks:

- `WinDS9.WinUI`: the Fluent/WinUI 3 native rewrite front end. It reads FITS/event files through `WinDS9.Engine` and renders without launching DS9.
- `WinDS9.Viewer`: the older WPF validation shell for the native engine. Keep it as a temporary harness while the WinUI front end grows.
- `WinDS9.App`: the earlier compatibility launcher. It wraps an existing `ds9.exe`; keep it only as a fallback while the rewrite grows.

## Layout

- `src/WinDS9.Engine`: native FITS/HDU parser, DS9-derived EVENTS table binning, raster stats, stretch renderer.
- `src/WinDS9.WinUI`: Windows App SDK / WinUI 3 Fluent front end.
- `src/WinDS9.Viewer`: native WPF viewer using `WinDS9.Engine`.
- `tests/WinDS9.Engine.Tests`: sample-backed tests for native FITS/event loading and rendering.
- `src/WinDS9.Core`: path detection, settings, command construction, process launch, recent files.
- `src/WinDS9.App`: WPF shell.
- `tests/WinDS9.Core.Tests`: dependency-free console test runner.
- `vendor/win ver/ds9.exe`: current detected DS9 binary location.
- `samples`: local test FITS/event files.

## Build And Test

The repository is pinned to .NET SDK `10.0.301` via `global.json`. `WinDS9.WinUI` uses Windows App SDK `2.2.0`; the earlier 1.8 XAML compiler failed under `net10.0-windows`.

Restore once:

```powershell
dotnet restore WinDS9.sln --configfile NuGet.Config
```

Build reliably in this workspace:

```powershell
dotnet build WinDS9.sln --no-restore -m:1
```

If old WPF `obj/bin` files are locked or ACL-restricted, build into an isolated artifacts directory:

```powershell
dotnet build WinDS9.sln -m:1 --artifacts-path E:\WinDS9\.artifacts
```

Run tests:

```powershell
dotnet run --project tests\WinDS9.Core.Tests\WinDS9.Core.Tests.csproj --no-build
dotnet run --project tests\WinDS9.Engine.Tests\WinDS9.Engine.Tests.csproj --no-build
```

Run the Fluent WinUI native viewer:

```powershell
dotnet build src\WinDS9.WinUI\WinDS9.WinUI.csproj -p:RuntimeIdentifier=win-x64 -m:1
src\WinDS9.WinUI\bin\Debug\net10.0-windows10.0.26100.0\win-x64\WinDS9.WinUI.exe
```

For a fixed double-click folder, copy the complete build output:

```powershell
$src = 'E:\WinDS9\src\WinDS9.WinUI\bin\Debug\net10.0-windows10.0.26100.0\win-x64'
$dst = 'E:\WinDS9\dist\WinDS9.WinUI'
New-Item -ItemType Directory -Path $dst -Force | Out-Null
Copy-Item -Path (Join-Path $src '*') -Destination $dst -Recurse -Force
E:\WinDS9\dist\WinDS9.WinUI\WinDS9.WinUI.exe
```

Open a file at launch:

```powershell
E:\WinDS9\dist\WinDS9.WinUI\WinDS9.WinUI.exe E:\WinDS9\samples\fits\evt_fits\fullfield.target.soft.evt
```

The `Sample` button searches `samples` recursively and prefers Hubble `.fits` examples when present.

Do not use `.artifacts\bin\WinDS9.WinUI\debug_win-x86\WinDS9.WinUI.exe` as the double-click target; that earlier artifacts output can fail before the UI appears if the Windows App SDK runtime is not registered.

Run the temporary WPF viewer:

```powershell
dotnet run --project src\WinDS9.Viewer\WinDS9.Viewer.csproj --no-build
```

Open a sample in the native viewer:

```powershell
dotnet run --project src\WinDS9.Viewer\WinDS9.Viewer.csproj --no-build -- samples\fullfield.target.soft.evt
```

Run the compatibility launcher:

```powershell
dotnet run --project src\WinDS9.App\WinDS9.App.csproj --no-build
```

Open a file through the app entry point:

```powershell
dotnet run --project src\WinDS9.App\WinDS9.App.csproj --no-build -- samples\fullfield.target.evt
```

## Native Viewer Status

Implemented in `WinDS9.WinUI` / `WinDS9.Engine`:

- FITS header and HDU discovery.
- FITS image HDU raster loading for common `BITPIX` values.
- DS9 relax-image event HDU detection for `STDEVT`, `EVENTS`, and `RAYEVENT` binary tables, matching `vendor/SAOImageDS9/fitsy/map.C`.
- DS9/funtools-style event bin column selection from `CPREF/PREFX`, exact `X,Y`, then fuzzy column names, matching `vendor/SAOImageDS9/funtools/funopenp.c`.
- `TLMIN/TLMAX/TDBIN` table-coordinate mapping and block/sum binning, matching the first usable slice of `funtools/filter/tl.c` and `funtools/funim.c`.
- Linear, log, and sqrt stretch rendering to BGRA.
- Fluent WinUI shell with Mica title bar, hamburger NavigationView, DS9-style top workbar, Open/Sample/Recent, bin status, fit, 1:1, zoom, region/catalog, WCS status, frame metadata, and collapsed timing log.
- WinUI command-line file opening for `.fits`, `.fit`, `.fts`, and `.evt`.
- WinUI drag/drop file opening and recent-file list backed by the existing settings/recent-file services.
- Basic FITS WCS metadata extraction from `CTYPE/CRPIX/CRVAL/CD/CDELT/PC` cards, displayed in the frame metadata panel.
- TAN WCS forward/reverse projection for common celestial images, RA/Dec-formatted pointer readout, and FK5/ICRS/Galactic world-to-image projection for overlays.
- Multi-frame WinUI state: each opened displayable image/event HDU becomes a selectable tab and also appears in the left hamburger pane's Frames list.
- Cube plane controls for image HDUs with `NAXIS > 2`; the selected frame can reload previous/next planes without opening a new tab.
- Basic DS9 region overlay for image/physical/world `circle`, `box`, `ellipse`, `point`, `line`, `polygon`, `annulus`, `text`, `ruler`, `vector`, `segment`, and `projection` shapes.
- First interactive region workflow: the left inspector has region tools for select/move, point, circle, box, line, text; selected image-coordinate regions can be dragged and saved back to a `.reg` file.
- Region property editing for the selected region: label/value editing, duplicate, and delete.
- Catalog overlay from CSV/TSV/CAT/TXT files with image `x,y`, FK5/ICRS `ra,dec`, or Galactic `glon,glat` columns.
- CPU contour overlay generation and a basic analysis summary with count/min/max/mean/median/sigma/histogram data.
- WCS grid overlay with labels for supported TAN celestial frames.
- Header viewer for the current HDU with search and copy.
- Blink mode to cycle through open frames.
- Stable viewer coordinate model: image raster and overlay canvas now stay in the same unscaled coordinate space; only the outer viewport is scaled for zoom/Fit. This keeps frame FOV and region/catalog positions consistent across render, zoom, and resize.
- Pixelated zoom display: WinUI now presents the CPU-rendered BGRA raster through a composition surface brush using nearest-neighbor sampling, so zoomed FITS pixels stay sharp instead of being smoothed by the default XAML image scaler.
- Pointer readout panel for binned pixel value, image coordinates, physical coordinates, display coordinates, and linear WCS coordinates when available.
- Left-side scale/color controls: stretch and color map now live below Current/WCS, with draggable low/high cut sliders for interactive min/max adjustment.
- Approximate DS9/IRAF-style zscale cuts plus additional stretch and color-map options.
- Internal DS9-style command entry for the first command subset: open/fits, frame, scale, cmap/color, zoom, pan parsing, region load/clear, catalog, contour.
- Save current rendered raster as PNG. Overlay export is not yet composited into the PNG.
- Semi-transparent glass-style chrome for the command bar, status bar, navigation pane, and side inspector; the frame tabs and image workspace remain visually separate.

Demo overlay files:

```powershell
samples\demo.reg
samples\demo_catalog.csv
```

Not implemented yet:

- Full DS9 region/catalog/WCS/analysis command compatibility.
- WCSLIB-level projection coverage, distortion terms, coordinate grids, WCS lock/match, and full sky-frame conversion parity.
- Full interactive region resize/rotate handles, region property panels, grouping, masks/filters, and all DS9 save/export variants.
- Funtools region filters and value-column bin operators.
- Direct2D/Win2D/Direct3D GPU raster pipeline; current path still CPU-renders BGRA and lets WinUI compose it with nearest-neighbor sampling.
- Tiled/blink frame layout, RGB/3D frames, panner/magnifier/profile graph, online catalogs, and SAMP/XPA integration.
- Exact DS9 zscale and full colormap/LUT/colorbar tag parity.

## DS9 Binary Notes

The app looks for `ds9.exe` in this order:

1. A saved custom path from the UI.
2. `vendor\ds9\ds9.exe`
3. `vendor\win ver\ds9.exe`
4. `win ver\ds9.exe`
5. Source-tree diagnostic locations under `vendor\SAOImageDS9`

If `xpaset.exe` is present next to `ds9.exe`, WinDS9 will try to reuse a running DS9 instance. The current `vendor\win ver` folder has `xpans.exe` but not `xpaset.exe`, so v1 falls back to starting a new DS9 process.

## Optional File Association

`scripts\Register-FileAssociation.ps1` can register `.fits`, `.fit`, `.fts`, and `.evt` for the current user after the app has been built. It is not run automatically.
