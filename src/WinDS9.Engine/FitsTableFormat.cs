using System.Globalization;

namespace WinDS9.Engine;

internal static class FitsTableFormat
{
    public static int GetByteSize(string format)
    {
        var (repeat, code) = Parse(format);
        var elementSize = code switch
        {
            'L' => 1,
            'X' => 1,
            'B' => 1,
            'I' => 2,
            'J' => 4,
            'K' => 8,
            'A' => 1,
            'E' => 4,
            'D' => 8,
            'C' => 8,
            'M' => 16,
            _ => 0
        };

        return repeat * elementSize;
    }

    public static double ReadScalar(ReadOnlySpan<byte> row, FitsColumnInfo column)
    {
        var code = GetScalarCode(column);
        var bytes = row.Slice(column.Offset, column.ByteSize);

        var rawValue = code switch
        {
            'B' => bytes[0],
            'I' => EndianReader.Int16(bytes),
            'J' => EndianReader.Int32(bytes),
            'K' => EndianReader.Int64(bytes),
            'E' => EndianReader.Single(bytes),
            'D' => EndianReader.Double(bytes),
            _ => double.NaN
        };

        return double.IsNaN(rawValue)
            ? rawValue
            : rawValue * column.Scale + column.Zero;
    }

    public static char GetScalarCode(FitsColumnInfo column)
    {
        var (repeat, code) = Parse(column.Format);
        return repeat == 1 ? code : '\0';
    }

    private static (int Repeat, char Code) Parse(string format)
    {
        var trimmed = format.Trim();
        var digitCount = 0;
        while (digitCount < trimmed.Length && char.IsDigit(trimmed[digitCount]))
        {
            digitCount++;
        }

        var repeat = digitCount == 0
            ? 1
            : int.Parse(trimmed[..digitCount], CultureInfo.InvariantCulture);
        var code = digitCount < trimmed.Length ? char.ToUpperInvariant(trimmed[digitCount]) : '\0';
        return (repeat, code);
    }
}
