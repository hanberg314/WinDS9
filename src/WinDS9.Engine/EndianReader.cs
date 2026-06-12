using System.Buffers.Binary;

namespace WinDS9.Engine;

internal static class EndianReader
{
    public static short Int16(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadInt16BigEndian(bytes);

    public static int Int32(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadInt32BigEndian(bytes);

    public static long Int64(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadInt64BigEndian(bytes);

    public static float Single(ReadOnlySpan<byte> bytes)
    {
        var value = BinaryPrimitives.ReadInt32BigEndian(bytes);
        return BitConverter.Int32BitsToSingle(value);
    }

    public static double Double(ReadOnlySpan<byte> bytes)
    {
        var value = BinaryPrimitives.ReadInt64BigEndian(bytes);
        return BitConverter.Int64BitsToDouble(value);
    }
}
