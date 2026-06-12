using WinDS9.Engine;

var tests = new List<(string Name, Action Body)>
{
    ("reads event sample headers", ReadsEventSampleHeaders),
    ("builds full extent event binning plan", BuildsFullExtentEventBinningPlan),
    ("builds ds9 buffered event binning plan", BuildsDs9BufferedEventBinningPlan),
    ("loads soft event sample into full extent raster", LoadsSoftEventSampleIntoFullExtentRaster),
    ("ds9 event sum binning counts buffered rows", Ds9EventSumBinningCountsBufferedRows),
    ("renders sample raster to bgra", RendersSampleRasterToBgra),
    ("parses basic wcs metadata", ParsesBasicWcsMetadata),
    ("round-trips tan wcs metadata", RoundTripsTanWcsMetadata),
    ("parses ds9 image regions", ParsesDs9ImageRegions),
    ("parses extended ds9 regions", ParsesExtendedDs9Regions),
    ("serializes ds9 regions", SerializesDs9Regions),
    ("edits region geometry", EditsRegionGeometry),
    ("parses image catalog points", ParsesImageCatalogPoints),
    ("projects sky catalog points through wcs", ProjectsSkyCatalogPointsThroughWcs),
    ("loads header cards into image", LoadsHeaderCardsIntoImage),
    ("generates wcs grid segments", GeneratesWcsGridSegments),
    ("estimates zscale cuts", EstimatesZScaleCuts),
    ("analyzes raster and generates contours", AnalyzesRasterAndGeneratesContours),
    ("parses ds9 command subset", ParsesDs9CommandSubset)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine(ex);
    }
}

if (failures > 0)
{
    Environment.ExitCode = 1;
}

static void ReadsEventSampleHeaders()
{
    var path = SamplePath("fullfield.target.soft.evt");
    var reader = new FitsReader();
    var info = reader.ReadInfo(path);
    var events = info.Hdus.FirstOrDefault(hdu => string.Equals(hdu.ExtensionName, "EVENTS", StringComparison.OrdinalIgnoreCase));

    AssertNotNull(events);
    AssertEqual(1, events!.Index);
    AssertEqual(453059, events.RowCount);
    AssertEqual(12, events.RowByteCount);
    AssertTrue(events.Columns.Any(column => string.Equals(column.Name.Trim(), "x", StringComparison.OrdinalIgnoreCase)));
    AssertTrue(events.Columns.Any(column => string.Equals(column.Name.Trim(), "y", StringComparison.OrdinalIgnoreCase)));
    AssertTrue(Ds9EventBinner.IsDs9RelaxEventTable(events));
}

static void BuildsDs9BufferedEventBinningPlan()
{
    var path = SamplePath("fullfield.target.soft.evt");
    var info = new FitsReader().ReadInfo(path);
    var events = info.Hdus.First(hdu => Ds9EventBinner.IsDs9RelaxEventTable(hdu));
    var plan = new Ds9EventBinner().CreatePlan(events, new EventBinningOptions(MaxRasterSide: 2048, Mode: EventBinningMode.Ds9Buffer));

    AssertEqual("x", plan.XColumn.TrimmedName);
    AssertEqual("y", plan.YColumn.TrimmedName);
    AssertEqual(8192, plan.SourceWidth);
    AssertEqual(8192, plan.SourceHeight);
    AssertEqual(1, plan.BlockFactor);
    AssertEqual(1024, plan.OutputWidth);
    AssertEqual(1024, plan.OutputHeight);
    AssertNear(4096.5, plan.CenterX, 1e-9);
    AssertNear(4096.5, plan.CenterY, 1e-9);
    AssertEqual("bincols=(x,y)", plan.BinColumns);
}

static void BuildsFullExtentEventBinningPlan()
{
    var path = SamplePath("fullfield.target.soft.evt");
    var info = new FitsReader().ReadInfo(path);
    var events = info.Hdus.First(hdu => Ds9EventBinner.IsDs9RelaxEventTable(hdu));
    var plan = new Ds9EventBinner().CreatePlan(events, new EventBinningOptions(MaxRasterSide: 2048));

    AssertEqual("x", plan.XColumn.TrimmedName);
    AssertEqual("y", plan.YColumn.TrimmedName);
    AssertEqual(8192, plan.SourceWidth);
    AssertEqual(8192, plan.SourceHeight);
    AssertEqual(4, plan.BlockFactor);
    AssertEqual(2048, plan.OutputWidth);
    AssertEqual(2048, plan.OutputHeight);
    AssertEqual(EventBinningMode.FullExtent, plan.Mode);
    AssertNear(0.5, plan.CenterX, 1e-9);
    AssertNear(0.5, plan.CenterY, 1e-9);
    AssertEqual("bincols=(x,y)", plan.BinColumns);
}

static void LoadsSoftEventSampleIntoFullExtentRaster()
{
    var path = SamplePath("fullfield.target.soft.evt");
    var loader = new NativeImageLoader();
    var image = loader.Load(path);

    AssertEqual(2048, image.Width);
    AssertEqual(2048, image.Height);
    AssertEqual(8192, image.SourceWidth);
    AssertEqual(8192, image.SourceHeight);
    AssertEqual(4, image.BlockFactor);
    AssertEqual(453059L, image.SourceRows);
    AssertEqual(image.SourceRows, image.BinnedRows);
    AssertEqual("bincols=(x,y)", image.BinDescription);
    AssertTrue(image.DataMax > 0);
    AssertTrue(image.HighCut > image.LowCut);
}

static void Ds9EventSumBinningCountsBufferedRows()
{
    var path = SamplePath("fullfield.target.soft.evt");
    var info = new FitsReader().ReadInfo(path);
    var hdu = info.Hdus.First(candidate => Ds9EventBinner.IsDs9RelaxEventTable(candidate));
    var image = new Ds9EventBinner().Bin(
        path,
        hdu,
        new EventBinningOptions(MaxRasterSide: 2048, Mode: EventBinningMode.Ds9Buffer),
        System.Diagnostics.Stopwatch.GetTimestamp());
    var sum = image.Pixels.Sum(pixel => (long)pixel);

    AssertEqual(image.BinnedRows, sum);
    AssertTrue(image.BinnedRows < image.SourceRows);
}

static void RendersSampleRasterToBgra()
{
    var path = SamplePath("fullfield.target.soft.evt");
    var image = new NativeImageLoader().Load(path);
    var rendered = new RasterRenderer().Render(image, ImageStretch.Log);

    AssertEqual(image.Width, rendered.Width);
    AssertEqual(image.Height, rendered.Height);
    AssertEqual(image.Width * image.Height * 4, rendered.Bgra32.Length);
    AssertTrue(rendered.Bgra32.Any(value => value > 0));
}

static void ParsesBasicWcsMetadata()
{
    var header = new FitsHeader([
        Card("CTYPE1", "'RA---TAN'"),
        Card("CTYPE2", "'DEC--TAN'"),
        Card("CRPIX1", "1024.5"),
        Card("CRPIX2", "1024.5"),
        Card("CRVAL1", "150.0"),
        Card("CRVAL2", "2.0"),
        Card("CDELT1", "-0.0002777778"),
        Card("CDELT2", "0.0002777778")
    ]);

    var wcs = WcsMetadata.FromHeader(header);
    AssertNotNull(wcs);
    AssertTrue(wcs!.HasCelestialAxes);
    AssertTrue(wcs.HasLinearTransform);
    var world = wcs.PixelToWorld(1024.5, 1024.5);
    AssertNotNull(world);
    AssertNear(150.0, world!.Value.World1, 1e-9);
    AssertNear(2.0, world.Value.World2, 1e-9);
}

static void RoundTripsTanWcsMetadata()
{
    var wcs = BuildTanWcs();
    var pixel = wcs.WorldToPixel(150.0, 2.0);

    AssertNotNull(pixel);
    AssertNear(1024.5, pixel!.Value.Pixel1, 1e-6);
    AssertNear(1024.5, pixel.Value.Pixel2, 1e-6);

    var world = wcs.PixelToWorld(pixel.Value.Pixel1 + 20, pixel.Value.Pixel2 - 10);
    AssertNotNull(world);
    var roundTrip = wcs.WorldToPixel(world!.Value.World1, world.Value.World2);
    AssertNotNull(roundTrip);
    AssertNear(pixel.Value.Pixel1 + 20, roundTrip!.Value.Pixel1, 1e-6);
    AssertNear(pixel.Value.Pixel2 - 10, roundTrip.Value.Pixel2, 1e-6);
}

static void ParsesDs9ImageRegions()
{
    var text = """
        # Region file format: DS9
        global color=green
        image
        circle(10,20,5) # text={core}
        box(30,40,10,12,45)
        fk5
        circle(150.1,2.3,0.01)
        """;

    var regions = new Ds9RegionParser().Parse(text);

    AssertEqual(3, regions.Count);
    AssertEqual(Ds9RegionKind.Circle, regions[0].Kind);
    AssertEqual("image", regions[0].CoordinateSystem);
    AssertEqual("core", regions[0].Label);
    AssertEqual(Ds9RegionKind.Box, regions[1].Kind);
    AssertEqual("fk5", regions[2].CoordinateSystem);
}

static void ParsesExtendedDs9Regions()
{
    var text = """
        image
        polygon(1,2,3,4,5,6)
        annulus(20,20,3,8,12)
        text(30,40) # text={label}
        segment(1,1,2,2,3,3,4,4)
        """;

    var regions = new Ds9RegionParser().Parse(text);

    AssertEqual(4, regions.Count);
    AssertEqual(Ds9RegionKind.Polygon, regions[0].Kind);
    AssertEqual(Ds9RegionKind.Annulus, regions[1].Kind);
    AssertEqual(Ds9RegionKind.Text, regions[2].Kind);
    AssertEqual("label", regions[2].Label);
    AssertEqual(Ds9RegionKind.Segment, regions[3].Kind);
}

static void SerializesDs9Regions()
{
    var text = new Ds9RegionSerializer().Serialize([
        new Ds9Region(Ds9RegionKind.Circle, [10, 20, 5], "image", "core"),
        new Ds9Region(Ds9RegionKind.Line, [1, 2, 3, 4], "fk5")
    ]);

    AssertTrue(text.Contains("# Region file format: DS9", StringComparison.Ordinal));
    AssertTrue(text.Contains("circle(10,20,5) # text={core}", StringComparison.Ordinal));
    AssertTrue(text.Contains("fk5", StringComparison.Ordinal));
    AssertTrue(text.Contains("line(1,2,3,4)", StringComparison.Ordinal));
}

static void EditsRegionGeometry()
{
    var service = new RegionEditService();
    var region = new Ds9Region(Ds9RegionKind.Line, [1, 2, 3, 4], "image", "old");
    var moved = service.Move(region, 10, -1);
    var ok = service.TryUpdateValues(region, "5, 6, 7, 8", out var updated);
    var labeled = service.WithLabel(updated, "new");

    AssertNear(11, moved.Values[0], 1e-9);
    AssertNear(1, moved.Values[1], 1e-9);
    AssertNear(13, moved.Values[2], 1e-9);
    AssertNear(3, moved.Values[3], 1e-9);
    AssertTrue(ok);
    AssertNear(5, updated.Values[0], 1e-9);
    AssertEqual("new", labeled.Label);
}

static void ParsesImageCatalogPoints()
{
    var text = """
        id,x,y,flux
        a,10.5,20.25,100
        b,30,40,200
        """;

    var entries = new CatalogParser().Parse(text);

    AssertEqual(2, entries.Count);
    AssertEqual("a", entries[0].Label);
    AssertNear(10.5, entries[0].ImageX, 1e-9);
    AssertNear(20.25, entries[0].ImageY, 1e-9);
}

static void ProjectsSkyCatalogPointsThroughWcs()
{
    var text = """
        id,ra,dec
        center,150.0,2.0
        """;

    var entries = new CatalogParser().Parse(text);
    var projected = new CoordinateTransformService().WorldToImage(BuildTanWcs(), entries[0].First, entries[0].Second, entries[0].SkyFrame);

    AssertEqual(1, entries.Count);
    AssertEqual(CatalogCoordinateSystem.Sky, entries[0].CoordinateSystem);
    AssertNotNull(projected);
    AssertNear(1024.5, projected!.Value.ImageX, 1e-6);
    AssertNear(1024.5, projected.Value.ImageY, 1e-6);
}

static void LoadsHeaderCardsIntoImage()
{
    var path = SamplePath("fullfield.target.soft.evt");
    var image = new NativeImageLoader().Load(path);

    AssertNotNull(image.HeaderCards);
    AssertTrue(image.HeaderCards!.Count > 0);
    AssertTrue(image.HeaderCards.Any(card => card.StartsWith("XTENSION", StringComparison.Ordinal) || card.StartsWith("SIMPLE", StringComparison.Ordinal)));
}

static void GeneratesWcsGridSegments()
{
    var image = new LoadedImage(
        "synthetic.fits",
        "synthetic",
        0,
        128,
        128,
        new float[128 * 128],
        128 * 128,
        0,
        1,
        0,
        1,
        TimeSpan.Zero,
        128,
        128,
        Wcs: BuildTanWcs());

    var segments = new WcsGridGenerator().Generate(image, lineCount: 4, samplesPerLine: 8);

    AssertTrue(segments.Count > 0);
    AssertTrue(segments.Any(segment => segment.IsLongitude));
    AssertTrue(segments.Any(segment => !segment.IsLongitude));
}

static void EstimatesZScaleCuts()
{
    var pixels = Enumerable.Range(0, 1000).Select(value => (float)value).ToArray();
    var zscale = ZScaleEstimator.Estimate(pixels);

    AssertNotNull(zscale);
    AssertTrue(zscale!.Value.High > zscale.Value.Low);
    AssertTrue(zscale.Value.Low >= 0);
    AssertTrue(zscale.Value.High <= 999);
}

static void AnalyzesRasterAndGeneratesContours()
{
    var image = new LoadedImage(
        "synthetic.fits",
        "synthetic",
        0,
        4,
        4,
        [
            0, 1, 2, 3,
            1, 2, 3, 4,
            2, 3, 4, 5,
            3, 4, 5, 6
        ],
        16,
        0,
        6,
        1,
        5,
        TimeSpan.Zero,
        4,
        4);

    var analysis = new AnalysisService().Analyze(image, histogramBins: 3);
    var contours = new ContourGenerator().Generate(image, [3]);

    AssertEqual(16L, analysis.Statistics.Count);
    AssertNear(3.0, analysis.Statistics.Mean, 1e-9);
    AssertEqual(3, analysis.Histogram.Count);
    AssertTrue(contours.Count > 0);
}

static void ParsesDs9CommandSubset()
{
    var command = new CommandDispatcher().Parse("scale log");

    AssertEqual(Ds9CommandKind.Scale, command.Kind);
    AssertEqual("scale", command.Name);
    AssertEqual("log", command.Arguments[0]);
}

static WcsMetadata BuildTanWcs()
{
    var header = new FitsHeader([
        Card("CTYPE1", "'RA---TAN'"),
        Card("CTYPE2", "'DEC--TAN'"),
        Card("CRPIX1", "1024.5"),
        Card("CRPIX2", "1024.5"),
        Card("CRVAL1", "150.0"),
        Card("CRVAL2", "2.0"),
        Card("CDELT1", "-0.0002777778"),
        Card("CDELT2", "0.0002777778")
    ]);

    return WcsMetadata.FromHeader(header) ?? throw new InvalidOperationException("WCS was not parsed.");
}

static string SamplePath(string fileName)
{
    var samplesRoot = Path.Combine(FindWorkspaceRoot(), "samples");
    var path = Directory.EnumerateFiles(samplesRoot, fileName, SearchOption.AllDirectories).FirstOrDefault();
    if (path is null)
    {
        throw new FileNotFoundException(Path.Combine(samplesRoot, "**", fileName));
    }

    return path;
}

static string FindWorkspaceRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "WinDS9.sln")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not find workspace root.");
}

static void AssertNotNull(object? value)
{
    if (value is null)
    {
        throw new InvalidOperationException("Expected non-null value.");
    }
}

static void AssertTrue(bool value)
{
    if (!value)
    {
        throw new InvalidOperationException("Expected true.");
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void AssertNear(double expected, double actual, double tolerance)
{
    if (Math.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static string Card(string key, string value)
{
    return $"{key,-8}= {value}".PadRight(80);
}
