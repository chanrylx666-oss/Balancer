using System.Globalization;
using System.Text.Json;

namespace Balancer.Infrastructure.Acquisition;

public sealed record FrameParseResult(SignalFrame? Frame, string? ErrorMessage)
{
    public bool IsSuccess => Frame is not null;
}

/// <summary>Parser for one UTF-8 JSON line: timestampUtc, piezoA, piezoB, tach.</summary>
public static class TcpLineJsonFrameParser
{
    public static FrameParseResult Parse(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new(null, "Frame must be a JSON object.");

            var root = document.RootElement;
            if (!root.TryGetProperty("timestampUtc", out var timestampProperty)) return new(null, "Missing required field 'timestampUtc'.");
            if (!root.TryGetProperty("piezoA", out var piezoAProperty)) return new(null, "Missing required field 'piezoA'.");
            if (!root.TryGetProperty("piezoB", out var piezoBProperty)) return new(null, "Missing required field 'piezoB'.");
            if (!root.TryGetProperty("tach", out var tachProperty)) return new(null, "Missing required field 'tach'.");

            if (timestampProperty.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(timestampProperty.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
                return new(null, "Field 'timestampUtc' must be an ISO-8601 timestamp.");
            if (!piezoAProperty.TryGetDouble(out var piezoA) || !double.IsFinite(piezoA)) return new(null, "Field 'piezoA' must be a finite number.");
            if (!piezoBProperty.TryGetDouble(out var piezoB) || !double.IsFinite(piezoB)) return new(null, "Field 'piezoB' must be a finite number.");

            var tach = tachProperty.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when tachProperty.TryGetInt32(out var number) && (number is 0 or 1) => number == 1,
                _ => throw new FormatException("Field 'tach' must be true/false or 0/1.")
            };

            return new(new SignalFrame(timestamp.ToUniversalTime(), piezoA, piezoB, tach), null);
        }
        catch (JsonException)
        {
            return new(null, "Invalid JSON frame.");
        }
        catch (FormatException exception)
        {
            return new(null, exception.Message);
        }
    }
}
