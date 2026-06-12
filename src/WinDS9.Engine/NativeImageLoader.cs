using System.Diagnostics;

namespace WinDS9.Engine;

public sealed class NativeImageLoader
{
    private const int DefaultMaxRasterSide = 2048;
    private readonly FitsReader fitsReader = new();
    private readonly Ds9EventBinner eventBinner = new();

    public LoadedImage Load(string path, int maxRasterSide = DefaultMaxRasterSide)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var info = fitsReader.ReadInfo(path);

        var eventHdu = info.Hdus.FirstOrDefault(IsEventHdu);
        if (eventHdu is not null)
        {
            return eventBinner.Bin(path, eventHdu, new EventBinningOptions(MaxRasterSide: maxRasterSide), startedAt);
        }

        var imageHdu = info.Hdus.FirstOrDefault(hdu => hdu.IsImage);
        if (imageHdu is not null)
        {
            return LoadImage(path, imageHdu, maxRasterSide, startedAt, planeIndex: 0);
        }

        throw new InvalidOperationException("No supported FITS image HDU or EVENTS binary table was found.");
    }

    public IReadOnlyList<LoadedImage> LoadAll(string path, int maxRasterSide = DefaultMaxRasterSide)
    {
        var info = fitsReader.ReadInfo(path);
        var loadable = info.Hdus
            .Where(hdu => IsEventHdu(hdu) || hdu.IsImage)
            .ToList();
        if (loadable.Count == 0)
        {
            throw new InvalidOperationException("No supported FITS image HDU or EVENTS binary table was found.");
        }

        var images = new List<LoadedImage>(loadable.Count);
        foreach (var hdu in loadable)
        {
            images.Add(LoadHdu(path, hdu.Index, planeIndex: 0, maxRasterSide));
        }

        return images;
    }

    public LoadedImage LoadHdu(
        string path,
        int hduIndex,
        int planeIndex = 0,
        int maxRasterSide = DefaultMaxRasterSide)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var info = fitsReader.ReadInfo(path);
        var hdu = info.Hdus.FirstOrDefault(candidate => candidate.Index == hduIndex)
            ?? throw new InvalidOperationException($"HDU {hduIndex} was not found.");

        if (IsEventHdu(hdu))
        {
            return eventBinner.Bin(path, hdu, new EventBinningOptions(MaxRasterSide: maxRasterSide), startedAt);
        }

        if (hdu.IsImage)
        {
            return LoadImage(path, hdu, maxRasterSide, startedAt, planeIndex);
        }

        throw new InvalidOperationException($"HDU {hduIndex} is not a supported image or EVENTS table.");
    }

    public FitsFileInfo ReadInfo(string path) => fitsReader.ReadInfo(path);

    private static bool IsEventHdu(FitsHduInfo hdu)
    {
        return Ds9EventBinner.IsDs9RelaxEventTable(hdu);
    }

    private static LoadedImage LoadImage(string path, FitsHduInfo hdu, int maxRasterSide, long startedAt, int planeIndex)
    {
        var sourceWidth = hdu.Axes[0];
        var sourceHeight = hdu.Axes[1];
        var cubeDepth = Math.Max(1, hdu.Axes.Skip(2).Aggregate(1, (current, axis) => current * Math.Max(1, axis)));
        var boundedPlaneIndex = Math.Clamp(planeIndex, 0, cubeDepth - 1);
        var stride = Math.Max(1, (int)Math.Ceiling(Math.Max(sourceWidth, sourceHeight) / (double)maxRasterSide));
        var width = Math.Max(1, (int)Math.Ceiling(sourceWidth / (double)stride));
        var height = Math.Max(1, (int)Math.Ceiling(sourceHeight / (double)stride));
        var pixels = new float[width * height];

        using var stream = File.OpenRead(path);
        var bytesPerPixel = Math.Abs(hdu.BitPix) / 8;
        var scale = hdu.Header.GetDouble("BSCALE", 1);
        var zero = hdu.Header.GetDouble("BZERO", 0);
        var pixelBuffer = new byte[bytesPerPixel];
        var planeByteCount = checked((long)sourceWidth * sourceHeight * bytesPerPixel);
        stream.Position = hdu.DataOffset + planeByteCount * boundedPlaneIndex;

        for (var y = 0; y < sourceHeight; y++)
        {
            for (var x = 0; x < sourceWidth; x++)
            {
                ReadExactly(stream, pixelBuffer);
                if (x % stride != 0 || y % stride != 0)
                {
                    continue;
                }

                var value = ReadImagePixel(pixelBuffer, hdu.BitPix) * scale + zero;
                var px = x / stride;
                var py = y / stride;
                if ((uint)px < (uint)width && (uint)py < (uint)height)
                {
                    pixels[(height - 1 - py) * width + px] = (float)value;
                }
            }
        }

        var stats = RasterStats.Compute(pixels);
        return new LoadedImage(
            path,
            cubeDepth > 1
                ? $"Image HDU {hdu.Index}, plane {boundedPlaneIndex + 1}/{cubeDepth}, sample {stride}x"
                : $"Image HDU {hdu.Index}, sample {stride}x",
            hdu.Index,
            width,
            height,
            pixels,
            sourceWidth * (long)sourceHeight,
            stats.Min,
            stats.Max,
            stats.LowCut,
            stats.HighCut,
            Stopwatch.GetElapsedTime(startedAt),
            sourceWidth,
            sourceHeight,
            stride,
            sourceWidth * (long)sourceHeight,
            $"image sample {stride}x",
            WcsMetadata.FromHeader(hdu.Header),
            cubeDepth,
            boundedPlaneIndex,
            hdu.Header.Cards);
    }

    private static double ReadImagePixel(ReadOnlySpan<byte> bytes, int bitPix)
    {
        return bitPix switch
        {
            8 => bytes[0],
            16 => EndianReader.Int16(bytes),
            32 => EndianReader.Int32(bytes),
            64 => EndianReader.Int64(bytes),
            -32 => EndianReader.Single(bytes),
            -64 => EndianReader.Double(bytes),
            _ => double.NaN
        };
    }

    private static void ReadExactly(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }
}
