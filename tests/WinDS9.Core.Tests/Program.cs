using WinDS9.Core;

var tests = new List<(string Name, Action Body)>
{
    ("detects vendor win ver ds9.exe", DetectsVendorWinVerDs9),
    ("configured directory overrides defaults", ConfiguredDirectoryOverridesDefaults),
    ("reports source tree without binary", ReportsSourceTreeWithoutBinary),
    ("builds startup argument list safely", BuildsStartupArgumentListSafely),
    ("builds xpa open command", BuildsXpaOpenCommand),
    ("deduplicates recent files", DeduplicatesRecentFiles),
    ("round trips settings json", RoundTripsSettingsJson)
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
        Console.WriteLine(ex.Message);
    }
}

if (failures > 0)
{
    Environment.ExitCode = 1;
}

static void DetectsVendorWinVerDs9()
{
    using var root = TempRoot.Create();
    var install = Path.Combine(root.Path, "vendor", "win ver");
    Directory.CreateDirectory(install);
    File.WriteAllText(Path.Combine(install, "ds9.exe"), string.Empty);
    File.WriteAllText(Path.Combine(install, "xpans.exe"), string.Empty);

    var status = new Ds9PathDetector().Detect(root.Path, null);

    AssertTrue(status.IsUsable);
    AssertEqual(Path.Combine(install, "ds9.exe"), status.ExecutablePath);
    AssertEqual(Path.Combine(install, "xpans.exe"), status.XpansPath);
    AssertNull(status.XpaSetPath);
}

static void ConfiguredDirectoryOverridesDefaults()
{
    using var root = TempRoot.Create();
    var configured = Path.Combine(root.Path, "custom ds9");
    Directory.CreateDirectory(configured);
    File.WriteAllText(Path.Combine(configured, "ds9.exe"), string.Empty);

    var status = new Ds9PathDetector().Detect(root.Path, configured);

    AssertTrue(status.IsUsable);
    AssertEqual(Path.Combine(configured, "ds9.exe"), status.ExecutablePath);
}

static void ReportsSourceTreeWithoutBinary()
{
    using var root = TempRoot.Create();
    Directory.CreateDirectory(Path.Combine(root.Path, "vendor", "SAOImageDS9"));

    var status = new Ds9PathDetector().Detect(root.Path, null);

    AssertFalse(status.IsUsable);
    AssertTrue(status.Message.Contains("source", StringComparison.OrdinalIgnoreCase));
}

static void BuildsStartupArgumentListSafely()
{
    var builder = new Ds9CommandBuilder();
    var file = Path.Combine("C:\\data set", "\u4e2d\u6587 file.fits");
    var args = builder.BuildStartupArguments(file, LaunchProfile.Resolve("quick-look")).ToList();

    AssertEqual("-geometry", args[0]);
    AssertEqual("1100x760", args[1]);
    AssertTrue(args.Contains(file));
    AssertTrue(args.Contains("-zscale"));
    AssertTrue(HasSequence(args, "-zoom", "to", "fit"));
}

static void BuildsXpaOpenCommand()
{
    var builder = new Ds9CommandBuilder();
    var file = Path.Combine("C:\\data set", "sample file.fits");

    var args = builder.BuildXpaOpenArguments("ds9", file, newFrame: true).ToList();

    AssertSequence(args, "-p", "ds9", "fits", "new", file);

    var currentFrameArgs = builder.BuildXpaOpenArguments("ds9", file, newFrame: false).ToList();
    AssertSequence(currentFrameArgs, "-p", "ds9", "fits", file);
}

static void DeduplicatesRecentFiles()
{
    var service = new RecentFilesService();
    var first = Path.GetFullPath("A.fits");
    var duplicate = first.ToUpperInvariant();
    var second = Path.GetFullPath("B.fits");

    var recent = service.Add(first, [second, duplicate]).ToList();

    AssertEqual(first, recent[0]);
    AssertEqual(second, recent[1]);
    AssertEqual(2, recent.Count);
}

static void RoundTripsSettingsJson()
{
    using var root = TempRoot.Create();
    var path = Path.Combine(root.Path, "settings.json");
    var store = new SettingsStore(path);
    var settings = new AppSettings
    {
        Ds9ExecutablePath = "C:\\Tools\\ds9\\ds9.exe",
        ProfileId = "plain",
        OpenInNewFrame = false,
        TryReuseRunningDs9 = false,
        RecentFiles = ["a.fits", "b.fits"]
    };

    store.Save(settings);
    var loaded = store.Load();

    AssertEqual(settings.Ds9ExecutablePath, loaded.Ds9ExecutablePath);
    AssertEqual(settings.ProfileId, loaded.ProfileId);
    AssertEqual(settings.OpenInNewFrame, loaded.OpenInNewFrame);
    AssertEqual(settings.TryReuseRunningDs9, loaded.TryReuseRunningDs9);
    AssertEqual(settings.RecentFiles.Count, loaded.RecentFiles.Count);
}

static bool HasSequence(IReadOnlyList<string> args, params string[] sequence)
{
    for (var i = 0; i <= args.Count - sequence.Length; i++)
    {
        var matched = true;
        for (var j = 0; j < sequence.Length; j++)
        {
            matched &= args[i + j] == sequence[j];
        }

        if (matched)
        {
            return true;
        }
    }

    return false;
}

static void AssertSequence(IReadOnlyList<string> actual, params string[] expected)
{
    AssertEqual(expected.Length, actual.Count);
    for (var i = 0; i < expected.Length; i++)
    {
        AssertEqual(expected[i], actual[i]);
    }
}

static void AssertTrue(bool value)
{
    if (!value)
    {
        throw new InvalidOperationException("Expected true.");
    }
}

static void AssertFalse(bool value)
{
    if (value)
    {
        throw new InvalidOperationException("Expected false.");
    }
}

static void AssertNull(object? value)
{
    if (value is not null)
    {
        throw new InvalidOperationException($"Expected null, got {value}.");
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

internal sealed class TempRoot : IDisposable
{
    private TempRoot(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TempRoot Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WinDS9Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TempRoot(path);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch
        {
        }
    }
}
