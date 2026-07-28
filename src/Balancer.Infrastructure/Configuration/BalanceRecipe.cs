namespace Balancer.Infrastructure.Configuration;

/// <summary>可独立保存的工艺参数；不包含全局连接与日志设置。</summary>
public sealed record BalanceRecipe(
    string Name,
    double TargetRpm,
    double CorrectionRadiusAmm,
    double CorrectionRadiusBmm,
    double TrialMassAg,
    double TrialAngleADegrees,
    double TrialMassBg,
    double TrialAngleBDegrees,
    double SampleRateHz,
    double MaxRpmDeviationPercent,
    double MaxMatrixConditionNumber)
{
    public static BalanceRecipe Default { get; } = new(
        "默认双平面演示", 1800, 100, 100, 10, 0, 10, 0, 5000, 2, 50);
}
