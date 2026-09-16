using PlantDoctor.Contracts;

namespace PlantMonitor.Core.Diagnostics;

/// <summary>Builds error events already stamped with a catalog code. Main application only.</summary>
public static class DiagnosticEvents
{
    /// <summary>
    /// Re-stamps a fault that arrived from a device: the catalog code goes into
    /// <see cref="ErrorEventDto.ErrorCode"/> and the device's own code is preserved.
    /// </summary>
    public static ErrorEventDto FromDevice(TransportChannel channel, ErrorEventDto reported)
    {
        var fault = DiagnosticCodes.Classify(reported.ErrorCode);
        reported.DeviceErrorCode = reported.ErrorCode;
        reported.ErrorCode = DiagnosticCodes.ForDevice(channel, fault);
        reported.SensorName ??= DiagnosticCodes.DeviceNameFor(channel);
        if (string.IsNullOrWhiteSpace(reported.Message))
            reported.Message = DiagnosticCodes.Describe(channel, fault);
        return reported;
    }

    /// <summary>A fault the main application detected itself while ingesting or processing data.</summary>
    public static ErrorEventDto FromApplication(ProcessingFault fault, string? detail = null,
        TransportChannel? channel = null, Exception? exception = null)
    {
        var description = DiagnosticCodes.Describe(fault);
        var device = channel is null ? null : DiagnosticCodes.DeviceNameFor(channel.Value);

        return new ErrorEventDto
        {
            TimestampUtc = DateTime.UtcNow,
            Level = LevelFor(fault),
            Source = "Application",
            SensorName = device,
            ErrorCode = DiagnosticCodes.ForApplication(fault),
            DeviceErrorCode = fault.ToString(),
            Message = detail is null ? description : $"{description} {detail}",
            StackTrace = exception?.ToString()
        };
    }

    private static string LevelFor(ProcessingFault fault) => fault switch
    {
        ProcessingFault.MalformedFrame or ProcessingFault.UnknownMessageType
            or ProcessingFault.MissingPayload or ProcessingFault.UnknownDevice => "Warning",
        ProcessingFault.ListenerFailed or ProcessingFault.LogWriteFailed
            or ProcessingFault.UnhandledProcessingError => "Critical",
        _ => "Error"
    };
}
