namespace WinDS9.Engine;

public sealed class ContourGenerator
{
    public IReadOnlyList<ContourSegment> Generate(LoadedImage image, IReadOnlyList<double> levels, int maxCells = 750_000)
    {
        if (image.Width < 2 || image.Height < 2 || levels.Count == 0)
        {
            return [];
        }

        var step = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((image.Width - 1L) * (image.Height - 1L) / (double)Math.Max(1, maxCells))));
        var segments = new List<ContourSegment>();
        foreach (var level in levels.Where(double.IsFinite))
        {
            GenerateLevel(image, level, step, segments);
        }

        return segments;
    }

    public IReadOnlyList<double> AutoLevels(LoadedImage image, int count = 6, double? low = null, double? high = null)
    {
        var start = low ?? image.LowCut;
        var end = high ?? image.HighCut;
        if (!double.IsFinite(start) || !double.IsFinite(end) || end <= start)
        {
            start = image.DataMin;
            end = image.DataMax;
        }

        if (!double.IsFinite(start) || !double.IsFinite(end) || end <= start)
        {
            return [];
        }

        count = Math.Clamp(count, 1, 32);
        var levels = new List<double>(count);
        for (var i = 1; i <= count; i++)
        {
            levels.Add(start + (end - start) * i / (count + 1));
        }

        return levels;
    }

    private static void GenerateLevel(LoadedImage image, double level, int step, List<ContourSegment> segments)
    {
        for (var y = 0; y < image.Height - step; y += step)
        {
            for (var x = 0; x < image.Width - step; x += step)
            {
                var v00 = image.Pixels[y * image.Width + x];
                var v10 = image.Pixels[y * image.Width + x + step];
                var v11 = image.Pixels[(y + step) * image.Width + x + step];
                var v01 = image.Pixels[(y + step) * image.Width + x];
                if (!float.IsFinite(v00) || !float.IsFinite(v10) || !float.IsFinite(v11) || !float.IsFinite(v01))
                {
                    continue;
                }

                var points = new List<(double X, double Y)>(4);
                AddCrossing(points, level, v00, v10, x, y, x + step, y);
                AddCrossing(points, level, v10, v11, x + step, y, x + step, y + step);
                AddCrossing(points, level, v11, v01, x + step, y + step, x, y + step);
                AddCrossing(points, level, v01, v00, x, y + step, x, y);

                if (points.Count == 2)
                {
                    segments.Add(new ContourSegment(points[0].X, points[0].Y, points[1].X, points[1].Y, level));
                }
                else if (points.Count == 4)
                {
                    segments.Add(new ContourSegment(points[0].X, points[0].Y, points[1].X, points[1].Y, level));
                    segments.Add(new ContourSegment(points[2].X, points[2].Y, points[3].X, points[3].Y, level));
                }
            }
        }
    }

    private static void AddCrossing(
        List<(double X, double Y)> points,
        double level,
        double startValue,
        double endValue,
        double x1,
        double y1,
        double x2,
        double y2)
    {
        var startSide = startValue >= level;
        var endSide = endValue >= level;
        if (startSide == endSide)
        {
            return;
        }

        var t = (level - startValue) / (endValue - startValue);
        points.Add((x1 + (x2 - x1) * t, y1 + (y2 - y1) * t));
    }
}
