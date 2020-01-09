using System;
using OpenTelemetry.Trace;
using Serilog.Core;
using Serilog.Events;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace NewRelic.LogEnrichers.Serilog
{
    public class NewRelicOpenTelemetryEnricher : ILogEventEnricher
    {

        private readonly ITracer _tracer;
        private readonly string _hostName;
        private readonly INewRelicDataService _dataSvc;
        private readonly NewRelicConfiguration _config;
        private readonly Lazy<string> _entityID;

        internal NewRelicOpenTelemetryEnricher(ITracer tracer, IDnsUtility dnsExtensions, IConfiguration configProvider, INewRelicDataService dataSvc)
        {
            _config = new NewRelicConfiguration(configProvider);
            _tracer = tracer;
            _hostName = dnsExtensions.GetFullHostName() ?? dnsExtensions.GetHostName();
            _dataSvc = dataSvc;
            _entityID = new Lazy<string>(GetEntityGuid);
        }

        private string GetEntityGuid()
        {
            var entityModel = _dataSvc.GetEntityAsync(_config.ServiceName, _config.LicenseKey).Result;
            return entityModel.Guid;
        }

        public NewRelicOpenTelemetryEnricher(ITracer tracer, IConfiguration configProvider) 
            : this(tracer, new DnsUtility(), configProvider, new NewRelicDataService(configProvider))
        {

        }

        private Dictionary<string,string> GetLinkingMetadata()
        {
            var linkingMetadata = new Dictionary<string, string>();

            var currentSpan = _tracer.CurrentSpan;

            if (currentSpan == null)
            {
                return linkingMetadata;
            }

            var traceId = _tracer.CurrentSpan.Context.TraceId;
            var spanId = _tracer.CurrentSpan.Context.SpanId;
            var entityGuid = _entityID.Value;
            var entityName = _config.ServiceName;


            if (traceId != null && traceId != default) linkingMetadata.Add("trace.id", traceId.ToHexString());
            if (spanId != null && spanId != default) linkingMetadata.Add("span.id", spanId.ToHexString());

            if (entityGuid != null) linkingMetadata.Add("entity.guid", entityGuid);
            if (entityName != null) linkingMetadata.Add("entity.name", entityName);
            if (_config.ServiceName != null) linkingMetadata.Add("entity.type", "SERVICE");

            if (_config.ServiceName != null) linkingMetadata.Add("service.name", entityName);

            if (_hostName != null) linkingMetadata.Add("hostname", _hostName);

            return linkingMetadata;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var linkingMetadata = GetLinkingMetadata();

            if (linkingMetadata != null && linkingMetadata.Keys.Count != 0)
            {
                // our key is unique enough that we are okay with overwriting it.
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(LoggingExtensions.LinkingMetadataKey, linkingMetadata));
            }
        }
    }
}
