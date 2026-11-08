using Switchknife.Pace;

namespace Subgate.Api;

/// <summary>The words the API uses for the engine's enums and meters.</summary>
internal static class ApiNames
{
    public static string Of(Decision decision) => decision switch
    {
        Decision.Allow => "allow",
        Decision.Hold => "hold",
        Decision.Api => "api",
        Decision.Cheap => "cheap",
        _ => "hold",
    };

    public static string Of(PaceState state) => state switch
    {
        PaceState.Ok => "OK",
        PaceState.OverPace => "OVER_PACE",
        PaceState.Limited => "LIMITED",
        PaceState.Stale => "STALE",
        _ => "STALE",
    };

    public static string Of(Estimator estimator) => estimator == Estimator.Calibrated ? "calibrated" : "apportion";

    public static string Of(PaceMode mode) => mode == PaceMode.Enforce ? "enforce" : "observe";

    public static string Window(MeterWindow window) => window switch
    {
        MeterWindow.FiveHour => "5h",
        MeterWindow.SevenDay => "7d",
        _ => "7d_scoped",
    };

    /// <summary>The key a reading has in <c>/v1/status</c>: a scoped window carries its scope.</summary>
    public static string Key(MeterReading reading) =>
        reading.Window == MeterWindow.SevenDayScoped ? $"7d_scoped:{reading.Scope}" : Window(reading.Window);
}
