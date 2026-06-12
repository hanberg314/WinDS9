using System.Text;

namespace WinDS9.Engine;

public sealed class FitsReader
{
    private const int BlockSize = 2880;
    private const int CardSize = 80;

    public FitsFileInfo ReadInfo(string path)
    {
        using var stream = File.OpenRead(path);
        var hdus = new List<FitsHduInfo>();
        var index = 0;

        while (stream.Position < stream.Length)
        {
            var headerOffset = stream.Position;
            var cards = ReadHeaderCards(stream);
            if (cards.Count == 0)
            {
                break;
            }

            var header = new FitsHeader(cards);
            AlignStream(stream);
            var dataOffset = stream.Position;
            var hdu = BuildHduInfo(index, headerOffset, dataOffset, header);
            hdus.Add(hdu);

            stream.Position = dataOffset + RoundUpToBlock(hdu.DataByteCount);
            index++;
        }

        return new FitsFileInfo(path, hdus);
    }

    private static IReadOnlyList<string> ReadHeaderCards(Stream stream)
    {
        var cards = new List<string>();
        var buffer = new byte[CardSize];

        while (stream.Position + CardSize <= stream.Length)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read != CardSize)
            {
                break;
            }

            var card = Encoding.ASCII.GetString(buffer);
            cards.Add(card);
            if (card.StartsWith("END", StringComparison.Ordinal))
            {
                break;
            }
        }

        return cards;
    }

    private static FitsHduInfo BuildHduInfo(int index, long headerOffset, long dataOffset, FitsHeader header)
    {
        var extensionType = header.GetString("XTENSION") ?? (index == 0 ? "PRIMARY" : string.Empty);
        var bitPix = header.GetInt32("BITPIX", 8);
        var nAxis = header.GetInt32("NAXIS", 0);
        var axes = Enumerable.Range(1, nAxis)
            .Select(axis => header.GetInt32($"NAXIS{axis}", 0))
            .ToList();

        var rowByteCount = header.GetInt32("NAXIS1", 0);
        var rowCount = header.GetInt32("NAXIS2", 0);
        var columns = string.Equals(extensionType, "BINTABLE", StringComparison.OrdinalIgnoreCase)
            ? BuildColumns(header)
            : [];

        var dataByteCount = CalculateDataByteCount(header, bitPix, axes);

        return new FitsHduInfo(
            index,
            extensionType,
            header.GetString("EXTNAME"),
            headerOffset,
            dataOffset,
            dataByteCount,
            bitPix,
            nAxis,
            axes,
            rowByteCount,
            rowCount,
            columns,
            header);
    }

    private static IReadOnlyList<FitsColumnInfo> BuildColumns(FitsHeader header)
    {
        var fields = header.GetInt32("TFIELDS", 0);
        var columns = new List<FitsColumnInfo>(fields);
        var offset = 0;

        for (var i = 1; i <= fields; i++)
        {
            var format = header.GetString($"TFORM{i}") ?? string.Empty;
            var size = FitsTableFormat.GetByteSize(format);
            columns.Add(new FitsColumnInfo(
                i,
                header.GetString($"TTYPE{i}") ?? $"COL{i}",
                format,
                offset,
                size,
                header.GetDouble($"TLMIN{i}"),
                header.GetDouble($"TLMAX{i}"),
                header.GetDouble($"TDBIN{i}", 1),
                header.GetDouble($"TSCAL{i}", 1),
                header.GetDouble($"TZERO{i}", 0)));
            offset += size;
        }

        return columns;
    }

    private static long CalculateDataByteCount(FitsHeader header, int bitPix, IReadOnlyList<int> axes)
    {
        var pCount = header.GetInt32("PCOUNT", 0);
        var gCount = header.GetInt32("GCOUNT", 1);
        if (axes.Count == 0)
        {
            return pCount * Math.Max(gCount, 1L);
        }

        long elements = 1;
        foreach (var axis in axes)
        {
            elements *= Math.Max(axis, 0);
        }

        return ((elements * Math.Abs(bitPix)) / 8 + pCount) * Math.Max(gCount, 1);
    }

    private static void AlignStream(Stream stream)
    {
        var next = RoundUpToBlock(stream.Position);
        stream.Position = next;
    }

    private static long RoundUpToBlock(long value)
    {
        return ((value + BlockSize - 1) / BlockSize) * BlockSize;
    }
}
