using Balancer.Infrastructure.Acquisition;

namespace Balancer.Infrastructure.Tests;

public sealed class TcpLineJsonFrameParserTests
{
    [Fact]
    public void Parse_reads_required_frame_fields()
    {
        const string json = "{\"timestampUtc\":\"2026-07-28T08:00:00.000Z\",\"piezoA\":0.12,\"piezoB\":-0.08,\"tach\":1}";

        var result = TcpLineJsonFrameParser.Parse(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.12d, result.Frame!.PiezoA);
        Assert.Equal(-0.08d, result.Frame.PiezoB);
        Assert.True(result.Frame.Tach);
    }

    [Fact]
    public void Parse_rejects_missing_required_fields()
    {
        var result = TcpLineJsonFrameParser.Parse("{\"piezoA\":0.12}");

        Assert.False(result.IsSuccess);
        Assert.Contains("timestampUtc", result.ErrorMessage);
    }
}
