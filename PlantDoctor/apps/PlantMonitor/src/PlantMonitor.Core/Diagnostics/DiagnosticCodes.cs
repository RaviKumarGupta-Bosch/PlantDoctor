using PlantDoctor.Contracts;

namespace PlantMonitor.Core.Diagnostics;

/// <summary>A fault reported by a field device, normalised from the device's own error code.</summary>
public enum DeviceFault
{
    Timeout = 1,
    OutOfRange = 2,
    CommunicationDropped = 3,
    UnhandledException = 4,
    LinkLost = 5,
    Unclassified = 99
}

/// <summary>A fault raised by the main application while ingesting or processing device data.</summary>
public enum ProcessingFault
{
    MalformedFrame = 1,
    UnknownMessageType = 2,
    MissingPayload = 3,
    UnknownDevice = 4,
    ListenerFailed = 5,
    UnexpectedDisconnect = 6,
    LogWriteFailed = 7,
    UnhandledProcessingError = 99
}

/// <summary>
/// The main application's diagnostic code catalog. Codes are <c>E</c> + a device block digit +
/// a two-digit fault number, so the source is readable at a glance and the fault number means
/// the same thing on every device.
/// <code>
///   E1nn  Temperature sensor      E101 timeout, E102 out of range, E103 comms dropped, …
///   E2nn  Vibration sensor
///   E3nn  Pressure sensor
///   E4nn  Scanner
///   E9nn  Main application processing
/// </code>
/// This catalog lives in the main application only — devices keep reporting their own semantic
/// codes (SENSOR_TIMEOUT, COM_DROPPED, …), which are preserved alongside the E-code.
/// </summary>
public static class DiagnosticCodes
{
    public const string UnknownCode = "E000";

    private static int BlockFor(TransportChannel channel) => channel switch
    {
        TransportChannel.NamedPipe => 1,
        TransportChannel.Tcp => 2,
        TransportChannel.Bluetooth => 3,
        TransportChannel.ComPort => 4,
        _ => 0
    };

    public static string DeviceNameFor(TransportChannel channel) => channel switch
    {
        TransportChannel.NamedPipe => "Temperature",
        TransportChannel.Tcp => "Vibration",
        TransportChannel.Bluetooth => "Pressure",
        TransportChannel.ComPort => "Scanner",
        _ => "Unknown"
    };

    public static string ForDevice(TransportChannel channel, DeviceFault fault)
    {
        var block = BlockFor(channel);
        return block == 0 ? UnknownCode : $"E{block}{(int)fault:00}";
    }

    public static string ForApplication(ProcessingFault fault) => $"E9{(int)fault:00}";

    /// <summary>Normalises a device's own error code into the catalog's fault taxonomy.</summary>
    public static DeviceFault Classify(string? deviceErrorCode) => deviceErrorCode?.Trim().ToUpperInvariant() switch
    {
        "SENSOR_TIMEOUT" => DeviceFault.Timeout,
        "OUT_OF_RANGE" => DeviceFault.OutOfRange,
        "COM_DROPPED" => DeviceFault.CommunicationDropped,
        "UNHANDLED_EXCEPTION" => DeviceFault.UnhandledException,
        "LINK_LOST" => DeviceFault.LinkLost,
        _ => DeviceFault.Unclassified
    };

    public static string Describe(DeviceFault fault) => fault switch
    {
        DeviceFault.Timeout => "did not report within the expected interval",
        DeviceFault.OutOfRange => "reported a value outside its valid bounds",
        DeviceFault.CommunicationDropped => "dropped its communication link mid-transmission",
        DeviceFault.UnhandledException => "raised an unhandled exception",
        DeviceFault.LinkLost => "lost its link to the main application",
        _ => "reported an unclassified fault"
    };

    public static string Describe(ProcessingFault fault) => fault switch
    {
        ProcessingFault.MalformedFrame => "Received a frame that is not valid JSON.",
        ProcessingFault.UnknownMessageType => "Received a frame with an unrecognised message type.",
        ProcessingFault.MissingPayload => "Received a frame whose payload was missing for its declared type.",
        ProcessingFault.UnknownDevice => "Received data on a channel with no mapped device.",
        ProcessingFault.ListenerFailed => "A device channel listener failed and stopped accepting connections.",
        ProcessingFault.UnexpectedDisconnect => "A connected device dropped without being disconnected by the operator.",
        ProcessingFault.LogWriteFailed => "Failed to write an entry to the diagnostic log file.",
        _ => "Unhandled error while processing device data."
    };

    /// <summary>Human label for any catalog code, e.g. "E102 — Pressure sensor out of range".</summary>
    public static string Describe(TransportChannel channel, DeviceFault fault) =>
        $"{ForDevice(channel, fault)} — {DeviceNameFor(channel)} {Describe(fault)}";

    /// <summary>The full catalog, used for documentation and for asserting the codes stay unique.</summary>
    public static IEnumerable<DiagnosticCodeEntry> All()
    {
        foreach (var channel in TransportEndpoints.All)
            foreach (var fault in Enum.GetValues<DeviceFault>())
                yield return new DiagnosticCodeEntry(
                    ForDevice(channel, fault),
                    DeviceNameFor(channel),
                    $"{DeviceNameFor(channel)} {Describe(fault)}");

        foreach (var fault in Enum.GetValues<ProcessingFault>())
            yield return new DiagnosticCodeEntry(
                ForApplication(fault),
                "Main Application",
                Describe(fault));
    }
}

public sealed record DiagnosticCodeEntry(string Code, string Source, string Description);
