using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Configuration;

namespace NewRelic.LogEnrichers.Serilog
{
    public static class SerilogExtensions
    {
        public static LoggerConfiguration WithNewRelicLogsInContext(this LoggerEnrichmentConfiguration enricherConfig)
        {
            return enricherConfig.With<NewRelicEnricher>();
        }


        public static LoggerConfiguration WithOpenTelemetryLogsInContext(this LoggerEnrichmentConfiguration enricherConfig,
            ITracer tracer, IConfiguration configProvider)
        {
            var enricher = new NewRelicOpenTelemetryEnricher(tracer, configProvider);

            return enricherConfig.With(enricher);
        }
    }
}
