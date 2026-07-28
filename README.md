# 双平面动平衡仿真与标定

这是一个 .NET 10 WPF Demo，用模拟的两个压电振动通道和一个红外键相通道，演示双平面影响系数法。它不是已认证的计量设备；真实机器必须完成传感器、机械传递路径和试重标定验证后才可用于校正。

## 运行与验证

```powershell
dotnet build Balancer.slnx
dotnet test Balancer.slnx --no-build
dotnet run --project src/Balancer.Wpf/Balancer.Wpf.csproj
```

## 操作流程

1. 连接仿真采集源，设定目标转速。
2. 无试重状态采集并记录基线。
3. 仅在面 A 加试重，采集并记录；移除该试重。
4. 仅在面 B 加试重，采集并记录。
5. 点击“计算两面校正”。程序构建 2×2 复数影响系数矩阵，求解 `Uc = -H^-1 V0`，并显示各面质量和相对键相角度。

角度以键相上升沿为 0°，沿转子旋转方向增加。质量由不平衡量除以校正半径得到。

## 主要代码逻辑

`Balancer.Core` 不依赖 WPF：`SimulationSignalSource` 产生三通道采样帧；`TachometerAnalyzer` 通过键相上升沿求 RPM 与稳定性；`SynchronousVibrationAnalyzer` 对完整转周期做同步 DFT，提取两个测点的 1X 复矢量。`CalibrationSession` 强制基线→A 面→B 面的试验顺序并检查转速偏差；`InfluenceCoefficientSolver` 构建矩阵、检查可逆性并计算两面校正向量。

`Balancer.Wpf` 使用 CommunityToolkit.Mvvm 进行命令和数据绑定，使用 DI 创建主窗口与 ViewModel。界面采集按钮实际调用仿真源和上述分析器；记录按钮保存当前测量到标定会话；计算按钮显示求解出的配重建议。

`Balancer.Infrastructure` 提供配方/设置 JSON 存储、告警、Serilog 日志适配及通信接口。全局设置与配方分开保存：配方存工艺/试重/分析阈值，设置存日志与连接参数。

## TCP 采集 Demo

`TcpLineJsonSignalSource` 使用 TCP、UTF-8、每行一个 JSON 帧（换行结束）：

```json
{"timestampUtc":"2026-07-28T08:00:00.000Z","piezoA":0.12,"piezoB":-0.08,"tach":1}
```

连接参数包括主机/IP、端口、连接超时、读取超时和最大行长。`tach` 可为 `true`/`false` 或 `1`/`0`。连接失败、读超时、断线会置为故障并触发故障报警；非 JSON、字段缺失或数值无效会产生协议警告且跳过该帧。接入串口、Modbus TCP、NI-DAQ 等设备时，只需实现 Infrastructure 的异步 `ISignalSource` 并映射为三通道帧；Core 分析算法无需修改。

## 日志与报警

告警按信息、警告、故障分级，可在界面确认。基础设施的 `IAppLogger` 将连接生命周期、协议错误及异常上下文写为结构化日志；生产部署应将日志目录配置到可维护的本地路径，并避免记录敏感连接凭据。
