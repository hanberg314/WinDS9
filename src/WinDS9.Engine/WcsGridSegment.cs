namespace WinDS9.Engine;

public sealed record WcsGridSegment(
    double X1,
    double Y1,
    double X2,
    double Y2,
    string Label,
    bool IsLongitude);
