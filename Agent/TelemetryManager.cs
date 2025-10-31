using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Diagnostics;

namespace AIEnglishCoachWithAgent
{
    internal static class TelemetryManager
    {
        private const string ServiceName = "AIEnglishCoachWithAgent";
        private static TracerProvider? _tracerProvider;

        public static void Initialize()
        {
            try
            {
                var resourceBuilder = ResourceBuilder
                    .CreateDefault()
                    .AddService(ServiceName);

                _tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetResourceBuilder(resourceBuilder)
                    //.AddSource("*Microsoft.Agents.AI") // 自动追踪 AI 调用
                    //.AddSource("*Microsoft.Extensions.Agents*") // 自动追踪 Agent 调用
                    .AddSource("*") // 自动追踪 AI 和 Agent 调用
                                    //.AddSource("*Microsoft.Agents.AI") 会出问题
                    .AddConsoleExporter(option =>
                    {
                        // In a UI application, it's crucial to use a non-blocking exporter
                        // to prevent deadlocks on the UI thread.
                        option.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Debug;
                    })
                    //.AddProcessor(new BatchActivityExportProcessor(new ConsoleExporter(new OpenTelemetry.Exporter.ConsoleExporterOptions
                    //{
                    //    Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Debug
                    //})))
                    .Build();

                Debug.WriteLine("OpenTelemetry 初始化成功（控制台输出模式）");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenTelemetry 初始化失败: {ex.Message}");
            }
        }

        public static void Shutdown()
        {
            _tracerProvider?.Dispose();
            _tracerProvider = null;
            Debug.WriteLine("OpenTelemetry 已关闭");
        }
    }
}