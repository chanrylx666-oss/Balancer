using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Numerics;
using Balancer.Core.Acquisition;
using Balancer.Core.Calibration;
using Balancer.Core.Domain;
using Balancer.Core.SignalProcessing;
using Balancer.Infrastructure.Alarms;
using Balancer.Infrastructure.Logging;
using Plane = Balancer.Core.Domain.Plane;

namespace Balancer.Wpf.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAppLogger _logger;
    private readonly IAlarmService _alarmService;
    private readonly CalibrationSession _calibration = new();
    private readonly InfluenceCoefficientSolver _solver = new();
    private readonly TrialWeight _trialA = new(Plane.A, 10, 100, Angle.Zero);
    private readonly TrialWeight _trialB = new(Plane.B, 10, 100, Angle.FromDegrees(90));
    private readonly SimulationInfluenceMatrix _trueMatrix = new(new(0.020, 0.004), new(-0.003, 0.016), new(0.006, -0.002), new(0.018, 0.003));
    private readonly Complex _initialA = Complex.FromPolarCoordinates(1800, Angle.FromDegrees(35).Radians);
    private readonly Complex _initialB = Complex.FromPolarCoordinates(1200, Angle.FromDegrees(210).Radians);
    private MeasurementRun? _latestRun;
    private MeasurementRun? _baselineRun;

    public MainViewModel(IAppLogger logger, IAlarmService alarmService)
    {
        _logger = logger;
        _alarmService = alarmService;
        _alarmService.Raised += (_, alarm) => Alarms.Add(new AlarmItemViewModel(alarm.Severity.ToString(), alarm.Message));
    }
    public ObservableCollection<string> Recipes { get; } = ["默认双平面演示"];
    public ObservableCollection<AlarmItemViewModel> Alarms { get; } = [];

    [ObservableProperty] private string _selectedRecipe = "默认双平面演示";
    [ObservableProperty] private string _connectionStatus = "未连接（仿真）";
    [ObservableProperty] private string _systemStatus = "等待开始标定";
    [ObservableProperty] private string _currentStep = "1 / 3：基线测量";
    [ObservableProperty] private double _targetRpm = 1800;
    [ObservableProperty] private double _measuredRpm;
    [ObservableProperty] private double _piezoAAmplitude;
    [ObservableProperty] private double _piezoAPhase;
    [ObservableProperty] private double _piezoBAmplitude;
    [ObservableProperty] private double _piezoBPhase;
    [ObservableProperty] private string _resultA = "--";
    [ObservableProperty] private string _resultB = "--";

    [RelayCommand]
    private void Connect()
    {
        ConnectionStatus = "已连接（仿真信号源）";
        SystemStatus = "连接成功，可开始采集";
        Raise(AlarmSeverity.Information, "SIM_CONNECTED", "已连接到仿真信号源");
    }

    [RelayCommand]
    private async Task StartAcquisitionAsync()
    {
        try
        {
            var planeA = _initialA + (_calibration.NextStep == CalibrationStep.TrialPlaneA ? _trialA.Unbalance.ToComplex() : Complex.Zero);
            var planeB = _initialB + (_calibration.NextStep == CalibrationStep.TrialPlaneB ? _trialB.Unbalance.ToComplex() : Complex.Zero);
            var source = new SimulationSignalSource(new SimulationSignalOptions
            {
                Rpm = TargetRpm, SampleRateHz = 5000, Duration = TimeSpan.FromSeconds(2),
                PlaneAUnbalance = planeA, PlaneBUnbalance = planeB, InfluenceMatrix = _trueMatrix,
                NoiseStandardDeviation = 0.002
            });
            var frames = new List<SignalFrame>();
            await foreach (var frame in source.ReadAsync()) frames.Add(frame);
            var tachometer = new TachometerAnalyzer().Analyze(frames.Select(x => x.Tachometer).ToArray());
            var analyzer = new SynchronousVibrationAnalyzer();
            var a = analyzer.Analyze(frames.Select(x => x.PiezoA).ToArray(), tachometer);
            var b = analyzer.Analyze(frames.Select(x => x.PiezoB).ToArray(), tachometer);
            var quality = tachometer.Quality.IsValid && a.Quality.IsValid && b.Quality.IsValid ? DataQuality.Good : DataQuality.Rejected;
            _latestRun = new MeasurementRun(new VibrationVector(a.Vector, b.Vector), tachometer.Rpm, quality);
            MeasuredRpm = tachometer.Rpm;
            PiezoAAmplitude = a.Amplitude; PiezoAPhase = Angle.FromRadians(a.PhaseRadians).Degrees;
            PiezoBAmplitude = b.Amplitude; PiezoBPhase = Angle.FromRadians(b.PhaseRadians).Degrees;
            SystemStatus = quality == DataQuality.Good ? "数据稳定，可记录当前标定步骤" : $"采集数据无效：{tachometer.Quality.Message}";
            if (quality != DataQuality.Good) Raise(AlarmSeverity.Warning, "SIGNAL_QUALITY", SystemStatus);
        }
        catch (Exception exception)
        {
            SystemStatus = "采集异常，请检查参数和日志";
            Raise(AlarmSeverity.Fault, "ACQUISITION", exception.Message, exception);
        }
    }

    [RelayCommand]
    private void RecordStep()
    {
        if (_latestRun is null) { Raise(AlarmSeverity.Warning, "NO_MEASUREMENT", "请先完成当前步骤的有效采集。"); return; }
        try
        {
            switch (_calibration.NextStep)
            {
                case CalibrationStep.Baseline: _calibration.RecordBaseline(_latestRun); _baselineRun = _latestRun; break;
                case CalibrationStep.TrialPlaneA: _calibration.RecordTrial(Plane.A, _trialA, _latestRun); break;
                case CalibrationStep.TrialPlaneB: _calibration.RecordTrial(Plane.B, _trialB, _latestRun); break;
                default: throw new InvalidOperationException("标定数据已齐全。");
            }
            CurrentStep = StepText(_calibration.NextStep);
            SystemStatus = _calibration.IsReady ? "可计算影响系数矩阵" : "请按向导调整试重后再次采集";
        }
        catch (Exception exception) { Raise(AlarmSeverity.Fault, "CALIBRATION", exception.Message, exception); }
    }

    [RelayCommand]
    private void CalculateCorrection()
    {
        try
        {
            if (_baselineRun is null) throw new InvalidOperationException("缺少基线测量。");
            var calibration = _calibration.BuildCalibration(_solver);
            var correction = _solver.Solve(calibration.Matrix, _baselineRun.Vibration, 100, 100);
            ResultA = $"{correction.PlaneA.MassGrams:F2} g @ {correction.PlaneA.Unbalance.Angle.Degrees:F1}°";
            ResultB = $"{correction.PlaneB.MassGrams:F2} g @ {correction.PlaneB.Unbalance.Angle.Degrees:F1}°";
            SystemStatus = $"校正建议已生成（矩阵条件数 {correction.ConditionNumber:F2}）；请执行模拟校正并验证残余振动";
        }
        catch (Exception exception) { Raise(AlarmSeverity.Fault, "CORRECTION", exception.Message, exception); }
    }

    [RelayCommand]
    private void SaveRecipe() => Raise(AlarmSeverity.Information, "RECIPE_SAVED", $"配方“{SelectedRecipe}”已保存");

    [RelayCommand]
    private void AcknowledgeAlarms() => Alarms.Clear();

    private static string StepText(CalibrationStep step) => step switch
    {
        CalibrationStep.TrialPlaneA => "2 / 3：面 A 试重",
        CalibrationStep.TrialPlaneB => "3 / 3：面 B 试重",
        CalibrationStep.Complete => "标定数据已齐全",
        _ => "1 / 3：基线测量"
    };

    private void Raise(AlarmSeverity severity, string code, string message, Exception? exception = null)
    {
        _alarmService.Raise(severity, code, message);
        if (severity == AlarmSeverity.Fault) _logger.Error(exception, "{Code}: {Message}", code, message);
        else if (severity == AlarmSeverity.Warning) _logger.Warning("{Code}: {Message}", code, message);
        else _logger.Information("{Code}: {Message}", code, message);
    }
}

public sealed record AlarmItemViewModel(string Level, string Message)
{
    public DateTime Time { get; } = DateTime.Now;
}
