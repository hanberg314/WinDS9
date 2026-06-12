namespace WinDS9.Engine;

public sealed class RasterRenderer : IRasterRenderer
{
    public RenderedRaster Render(
        LoadedImage image,
        ImageStretch stretch,
        ImageColorMap colorMap = ImageColorMap.Gray,
        double? lowCut = null,
        double? highCut = null)
    {
        var low = lowCut ?? image.LowCut;
        var high = highCut ?? image.HighCut;
        if (high <= low)
        {
            high = low + 1;
        }

        var bytes = new byte[image.Width * image.Height * 4];
        var range = high - low;

        for (var i = 0; i < image.Pixels.Length; i++)
        {
            var normalized = Normalize(image.Pixels[i], low, range, stretch);
            var (red, green, blue) = MapColor(normalized, colorMap);
            var offset = i * 4;
            bytes[offset] = blue;
            bytes[offset + 1] = green;
            bytes[offset + 2] = red;
            bytes[offset + 3] = 255;
        }

        return new RenderedRaster(image.Width, image.Height, bytes, low, high, stretch, colorMap);
    }

    private static double Normalize(double value, double low, double range, ImageStretch stretch)
    {
        var linear = Math.Clamp((value - low) / range, 0, 1);
        return stretch switch
        {
            ImageStretch.Log => Math.Log10(1 + 999 * linear) / Math.Log10(1000),
            ImageStretch.Sqrt => Math.Sqrt(linear),
            ImageStretch.Squared => linear * linear,
            ImageStretch.Power => Math.Pow(linear, 2.2),
            ImageStretch.Asinh => Math.Asinh(10 * linear) / Math.Asinh(10),
            ImageStretch.Sinh => Math.Sinh(linear) / Math.Sinh(1),
            _ => linear
        };
    }

    private static (byte Red, byte Green, byte Blue) MapColor(double normalized, ImageColorMap colorMap)
    {
        var value = Math.Clamp(normalized, 0, 1);
        return colorMap switch
        {
            ImageColorMap.Heat => Heat(value),
            ImageColorMap.Viridis => Viridis(value),
            ImageColorMap.Magma => Magma(value),
            ImageColorMap.Rainbow => Rainbow(value),
            ImageColorMap.Red => (ToByte(value), 0, 0),
            ImageColorMap.Green => (0, ToByte(value), 0),
            ImageColorMap.Blue => (0, 0, ToByte(value)),
            ImageColorMap.Invert => Gray(1 - value),
            _ => Gray(value)
        };
    }

    private static (byte Red, byte Green, byte Blue) Gray(double value)
    {
        var channel = ToByte(value);
        return (channel, channel, channel);
    }

    private static (byte Red, byte Green, byte Blue) Heat(double value)
    {
        return (
            ToByte(Math.Min(1, value * 2.2)),
            ToByte(Math.Clamp((value - 0.28) * 1.8, 0, 1)),
            ToByte(Math.Clamp((value - 0.68) * 2.6, 0, 1)));
    }

    private static (byte Red, byte Green, byte Blue) Viridis(double value)
    {
        var red = Interpolate(value, 0.267, 0.283, 0.254, 0.207, 0.993);
        var green = Interpolate(value, 0.005, 0.141, 0.265, 0.718, 0.906);
        var blue = Interpolate(value, 0.329, 0.458, 0.530, 0.472, 0.144);
        return (ToByte(red), ToByte(green), ToByte(blue));
    }

    private static (byte Red, byte Green, byte Blue) Magma(double value)
    {
        var red = Interpolate(value, 0.000, 0.184, 0.498, 0.848, 0.988);
        var green = Interpolate(value, 0.000, 0.060, 0.116, 0.364, 0.998);
        var blue = Interpolate(value, 0.000, 0.305, 0.507, 0.356, 0.645);
        return (ToByte(red), ToByte(green), ToByte(blue));
    }

    private static (byte Red, byte Green, byte Blue) Rainbow(double value)
    {
        var hue = (1 - Math.Clamp(value, 0, 1)) * 240;
        return HsvToRgb(hue, 1, 1);
    }

    private static (byte Red, byte Green, byte Blue) HsvToRgb(double hue, double saturation, double value)
    {
        var chroma = value * saturation;
        var h = (hue / 60) % 6;
        var x = chroma * (1 - Math.Abs(h % 2 - 1));
        (double red, double green, double blue) = h switch
        {
            < 1 => (chroma, x, 0.0),
            < 2 => (x, chroma, 0.0),
            < 3 => (0.0, chroma, x),
            < 4 => (0.0, x, chroma),
            < 5 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x)
        };

        var m = value - chroma;
        return (ToByte(red + m), ToByte(green + m), ToByte(blue + m));
    }

    private static double Interpolate(double value, params double[] stops)
    {
        if (stops.Length == 0)
        {
            return value;
        }

        var scaled = Math.Clamp(value, 0, 1) * (stops.Length - 1);
        var index = (int)Math.Floor(scaled);
        if (index >= stops.Length - 1)
        {
            return stops[^1];
        }

        var t = scaled - index;
        return stops[index] + (stops[index + 1] - stops[index]) * t;
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(value * 255.0, 0, 255);
}
