using System.Diagnostics;

namespace WinDS9.Engine;

public sealed class Ds9EventBinner
{
    private const int MaxRowsPerChunk = 10240;

    public EventBinningPlan CreatePlan(FitsHduInfo hdu, EventBinningOptions options)
    {
        if (!hdu.IsBinaryTable)
        {
            throw new InvalidOperationException("DS9 event binning requires a FITS binary table HDU.");
        }

        var (xColumn, yColumn) = SelectBinColumns(hdu, options);
        var xType = FitsTableFormat.GetScalarCode(xColumn);
        var yType = FitsTableFormat.GetScalarCode(yColumn);
        if (xType == '\0' || yType == '\0')
        {
            throw new InvalidOperationException("WinDS9 currently supports scalar event bin columns only.");
        }

        var xMin = xColumn.Min ?? 1.0;
        var xMax = xColumn.Max ?? throw new InvalidOperationException($"Column '{xColumn.TrimmedName}' is missing TLMAX.");
        var yMin = yColumn.Min ?? 1.0;
        var yMax = yColumn.Max ?? throw new InvalidOperationException($"Column '{yColumn.TrimmedName}' is missing TLMAX.");
        var xBinSize = xColumn.BinSize <= 0 ? 1.0 : xColumn.BinSize;
        var yBinSize = yColumn.BinSize <= 0 ? 1.0 : yColumn.BinSize;
        var xLower = Ds9TableCoordinateMath.LowerEdge(xMin, xType);
        var xUpper = Ds9TableCoordinateMath.UpperEdge(xMax, xType);
        var yLower = Ds9TableCoordinateMath.LowerEdge(yMin, yType);
        var yUpper = Ds9TableCoordinateMath.UpperEdge(yMax, yType);
        var sourceWidth = Ds9TableCoordinateMath.Dimension(xMin, xMax, xBinSize, xType);
        var sourceHeight = Ds9TableCoordinateMath.Dimension(yMin, yMax, yBinSize, yType);
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new InvalidOperationException("DS9 event binning produced an empty image extent.");
        }

        var requestedBlock = Math.Max(
            1,
            options.BlockFactor ?? (options.Mode == EventBinningMode.FullExtent
                ? ChoosePreviewBlock(sourceWidth, sourceHeight, options.MaxRasterSide)
                : 1));
        var outputWidth = Math.Max(1, (int)Math.Ceiling(sourceWidth / (double)requestedBlock));
        var outputHeight = Math.Max(1, (int)Math.Ceiling(sourceHeight / (double)requestedBlock));
        var centerX = xLower;
        var centerY = yLower;
        var outputCenterX = 0.0;
        var outputCenterY = 0.0;

        if (options.Mode == EventBinningMode.Ds9Buffer)
        {
            var bufferSize = options.BufferSize > 0
                ? options.BufferSize
                : Math.Max(1, options.MaxRasterSide);
            outputWidth = Math.Max(1, Math.Min(bufferSize, outputWidth));
            outputHeight = Math.Max(1, Math.Min(bufferSize, outputHeight));
            centerX = AlignCursorToDs9BinBoundary(options.CenterX ?? (xLower + (xUpper - xLower) / 2.0), xLower, requestedBlock);
            centerY = AlignCursorToDs9BinBoundary(options.CenterY ?? (yLower + (yUpper - yLower) / 2.0), yLower, requestedBlock);
            outputCenterX = Math.Ceiling(outputWidth / 2.0);
            outputCenterY = Math.Ceiling(outputHeight / 2.0);
        }

        return new EventBinningPlan(
            xColumn,
            yColumn,
            xMin,
            xMax,
            yMin,
            yMax,
            xBinSize,
            yBinSize,
            xLower,
            xUpper,
            yLower,
            yUpper,
            centerX,
            centerY,
            outputCenterX,
            outputCenterY,
            sourceWidth,
            sourceHeight,
            1,
            sourceWidth,
            1,
            sourceHeight,
            requestedBlock,
            outputWidth,
            outputHeight,
            options.Mode,
            options.Function);
    }

    public LoadedImage Bin(string path, FitsHduInfo hdu, EventBinningOptions options, long startedAt)
    {
        var plan = CreatePlan(hdu, options);
        var pixels = new float[plan.OutputWidth * plan.OutputHeight];
        var binnedRows = 0L;
        var rowsRemaining = hdu.RowCount;

        using var stream = File.OpenRead(path);
        stream.Position = hdu.DataOffset;

        var rowsPerChunk = Math.Max(1, Math.Min(MaxRowsPerChunk, hdu.RowCount));
        var chunk = new byte[checked(hdu.RowByteCount * rowsPerChunk)];

        while (rowsRemaining > 0)
        {
            var rowsThisChunk = Math.Min(rowsPerChunk, rowsRemaining);
            var bytesThisChunk = rowsThisChunk * hdu.RowByteCount;
            ReadExactly(stream, chunk.AsSpan(0, bytesThisChunk));

            for (var i = 0; i < rowsThisChunk; i++)
            {
                var row = chunk.AsSpan(i * hdu.RowByteCount, hdu.RowByteCount);
                var x = FitsTableFormat.ReadScalar(row, plan.XColumn);
                var y = FitsTableFormat.ReadScalar(row, plan.YColumn);
                if (!double.IsFinite(x) || !double.IsFinite(y))
                {
                    continue;
                }

                var px = plan.Mode == EventBinningMode.FullExtent
                    ? (int)Math.Floor((x - plan.XLower) / (plan.XBinSize * plan.BlockFactor))
                    : (int)((x - plan.CenterX) / (plan.XBinSize * plan.BlockFactor) + plan.OutputCenterX);
                var py = plan.Mode == EventBinningMode.FullExtent
                    ? (int)Math.Floor((y - plan.YLower) / (plan.YBinSize * plan.BlockFactor))
                    : (int)((y - plan.CenterY) / (plan.YBinSize * plan.BlockFactor) + plan.OutputCenterY);
                if ((uint)px >= (uint)plan.OutputWidth || (uint)py >= (uint)plan.OutputHeight)
                {
                    continue;
                }

                var index = (plan.OutputHeight - 1 - py) * plan.OutputWidth + px;
                pixels[index] += 1.0f;
                binnedRows++;
            }

            rowsRemaining -= rowsThisChunk;
        }

        if (plan.Function == EventBinFunction.Average)
        {
            var divisor = plan.BlockFactor * plan.BlockFactor;
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] /= divisor;
            }
        }

        var stats = RasterStats.Compute(pixels);
        return new LoadedImage(
            path,
            plan.Mode == EventBinningMode.FullExtent
                ? $"EVENTS {plan.BinColumns}, full extent, bin {plan.BlockFactor}x, raster {plan.OutputWidth}x{plan.OutputHeight}, {plan.Function.ToString().ToLowerInvariant()}"
                : $"DS9 EVENTS {plan.BinColumns}, bin {plan.BlockFactor}x, buffer {plan.OutputWidth}x{plan.OutputHeight}, {plan.Function.ToString().ToLowerInvariant()}",
            hdu.Index,
            plan.OutputWidth,
            plan.OutputHeight,
            pixels,
            hdu.RowCount,
            stats.Min,
            stats.Max,
            stats.LowCut,
            stats.HighCut,
            Stopwatch.GetElapsedTime(startedAt),
            plan.SourceWidth,
            plan.SourceHeight,
            plan.BlockFactor,
            binnedRows,
            plan.BinColumns,
            WcsMetadata.FromHeader(hdu.Header),
            HeaderCards: hdu.Header.Cards);
    }

    public static bool IsDs9RelaxEventTable(FitsHduInfo hdu)
    {
        if (!hdu.IsBinaryTable || string.IsNullOrWhiteSpace(hdu.ExtensionName))
        {
            return false;
        }

        var name = hdu.ExtensionName.Trim();
        return name.StartsWith("STDEVT", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("EVENTS", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("RAYEVENT", StringComparison.OrdinalIgnoreCase);
    }

    private static int ChoosePreviewBlock(int sourceWidth, int sourceHeight, int maxRasterSide)
    {
        if (maxRasterSide <= 0)
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Ceiling(Math.Max(sourceWidth, sourceHeight) / (double)maxRasterSide));
    }

    private static double AlignCursorToDs9BinBoundary(double cursor, double lowerEdge, int binFactor)
    {
        if (binFactor < 1)
        {
            return cursor;
        }

        var alignedCursor = Math.Floor(cursor / binFactor) * binFactor + 0.5;
        var alignedLower = Math.Floor(lowerEdge / binFactor) * binFactor + 0.5;
        return alignedCursor - (alignedLower - lowerEdge);
    }

    private static (FitsColumnInfo X, FitsColumnInfo Y) SelectBinColumns(FitsHduInfo hdu, EventBinningOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.XColumnName) || !string.IsNullOrWhiteSpace(options.YColumnName))
        {
            return (
                FindRequiredColumn(hdu, options.XColumnName ?? "X"),
                FindRequiredColumn(hdu, options.YColumnName ?? "Y"));
        }

        var cpref = TryFindColumnPairFromHeader(hdu, "CPREF") ?? TryFindColumnPairFromHeader(hdu, "PREFX");
        if (cpref is not null)
        {
            return cpref.Value;
        }

        var exact = TryFindColumnPairByExactNames(hdu, "X", "Y");
        if (exact is not null)
        {
            return exact.Value;
        }

        var fuzzy = TryFindColumnPairByContains(hdu, "X", "Y");
        if (fuzzy is not null)
        {
            return fuzzy.Value;
        }

        throw new InvalidOperationException("Could not find DS9 event bin columns. Expected CPREF/PREFX or X,Y columns.");
    }

    private static (FitsColumnInfo X, FitsColumnInfo Y)? TryFindColumnPairFromHeader(FitsHduInfo hdu, string keyPrefix)
    {
        var xName = hdu.Header.GetString($"{keyPrefix}1");
        var yName = hdu.Header.GetString($"{keyPrefix}2");
        if (string.IsNullOrWhiteSpace(xName) || string.IsNullOrWhiteSpace(yName))
        {
            return null;
        }

        return (FindRequiredColumn(hdu, xName), FindRequiredColumn(hdu, yName));
    }

    private static (FitsColumnInfo X, FitsColumnInfo Y)? TryFindColumnPairByExactNames(FitsHduInfo hdu, string xName, string yName)
    {
        var xColumn = FindColumn(hdu, xName);
        var yColumn = FindColumn(hdu, yName);
        return xColumn is not null && yColumn is not null ? (xColumn, yColumn) : null;
    }

    private static (FitsColumnInfo X, FitsColumnInfo Y)? TryFindColumnPairByContains(FitsHduInfo hdu, string xToken, string yToken)
    {
        var xColumn = hdu.Columns.FirstOrDefault(column => column.TrimmedName.Contains(xToken, StringComparison.OrdinalIgnoreCase));
        var yColumn = hdu.Columns.FirstOrDefault(column => column.TrimmedName.Contains(yToken, StringComparison.OrdinalIgnoreCase));
        return xColumn is not null && yColumn is not null ? (xColumn, yColumn) : null;
    }

    private static FitsColumnInfo FindRequiredColumn(FitsHduInfo hdu, string columnName)
    {
        return FindColumn(hdu, columnName) ??
               throw new InvalidOperationException($"Column '{columnName}' was not found in HDU {hdu.Index}.");
    }

    private static FitsColumnInfo? FindColumn(FitsHduInfo hdu, string columnName)
    {
        return hdu.Columns.FirstOrDefault(column =>
            string.Equals(column.TrimmedName, columnName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        while (!buffer.IsEmpty)
        {
            var read = stream.Read(buffer);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            buffer = buffer[read..];
        }
    }
}
