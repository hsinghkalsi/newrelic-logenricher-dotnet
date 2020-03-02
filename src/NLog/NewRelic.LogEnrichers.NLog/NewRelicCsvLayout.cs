using NewRelic.Api.Agent;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Layouts;
using System;
using System.Collections.Generic;
using System.Text;


namespace NewRelic.LogEnrichers.NLog
{

    [Layout("newrelic-csvlayout")]
    public class NewRelicCsvLayout : CsvLayout
    {
        internal const string TimestampLayoutRendererName = "nr-unix-timestamp";
        internal const string TraceDataLayoutRendererName = "nr-tracedata";
        internal const string TRACEID = "trace.id";
        internal const string SPANID = "span.id";

        private readonly Lazy<NewRelic.Api.Agent.IAgent> _nrAgent;

        internal NewRelicCsvLayout(Func<NewRelic.Api.Agent.IAgent> agentFactory) : base()
        {
            _nrAgent = new Lazy<NewRelic.Api.Agent.IAgent>(agentFactory);
            LayoutRenderer.Register<UnixTimestampLayoutRenderer>(TimestampLayoutRendererName);
            LayoutRenderer.Register<TraceDataLayoutRenderer>(TraceDataLayoutRendererName);

            // add new relic version for mc donalds and hardcode it to nr1
            // they insist.
            Columns.Add(new CsvColumn("newrelic.version", "nr1"));
            
            // Add Nr linking metadata
            if (_nrAgent.Value != null)
            {
                try
                {
                    var metadata = _nrAgent.Value.GetLinkingMetadata();
                    if (metadata != null)
                    {
                        foreach (var pair in metadata)
                        {
                            Columns.Add(new CsvColumn(pair.Key, "${" + TraceDataLayoutRendererName + ":metaproperty=" + pair.Key + "}"));
                        }
                    }

                }
                catch (Exception ex)
                {
                    InternalLogger.Error(ex, "Exception caught in NewRelicCsvLayout");
                }
            }

            //var traceMetadata = _nrAgent.Value.TraceMetadata;
            // If transaction is not started, the column header will not contain the traceid and spanid 
            // they are only available once transaction is started, hence adding coloumn
            Columns.Add(new CsvColumn(TRACEID, "${" + TraceDataLayoutRendererName + ":metaproperty=" + TRACEID + "}"));
            Columns.Add(new CsvColumn(SPANID, "${" + TraceDataLayoutRendererName + ":metaproperty=" + SPANID + "}"));

            Columns.Add(new CsvColumn(NewRelicLoggingProperty.Timestamp.GetOutputName(), "${" + TimestampLayoutRendererName + "}"));
            Columns.Add(new CsvColumn(NewRelicLoggingProperty.LogLevel.GetOutputName(), "${level:upperCase=true}"));
            Columns.Add(new CsvColumn(NewRelicLoggingProperty.MessageText.GetOutputName(), "${message}"));
            Columns.Add(new CsvColumn(NewRelicLoggingProperty.MessageTemplate.GetOutputName(), "${message:raw=true}"));


            // correlation
            Columns.Add(new CsvColumn(NewRelicLoggingProperty.ThreadId.GetOutputName(), "${threadid}"));
            Columns.Add(new CsvColumn(NewRelicLoggingProperty.CorrelationId.GetOutputName(), "${ActivityId}"));
            Columns.Add(new CsvColumn(NewRelicLoggingProperty.ProcessId.GetOutputName(), "${processid}"));

            // exceptions
            Columns.Add(new CsvColumn(NewRelicLoggingProperty.ErrorClass.GetOutputName(), "${exception:format=Type}"));
            Columns.Add(new CsvColumn(NewRelicLoggingProperty.ErrorMessage.GetOutputName(), "${exception:format=Message}"));
            Columns.Add(new CsvColumn(NewRelicLoggingProperty.ErrorStack.GetOutputName(), "${exception:format=StackTrace}"));

        }


        public NewRelicCsvLayout() : this(NewRelic.Api.Agent.NewRelic.GetAgent)
        {
        }

        //This prevents changing the properties that we don't want changed
        protected override void InitializeLayout()
        {
            // This reads XML configuration
            base.InitializeLayout();

            //Let this be controlled via configuration
            //WithHeader = false;
            //Header = null;
        }

        protected override void RenderFormattedMessage(LogEventInfo logEvent, StringBuilder target)
        {
            base.RenderFormattedMessage(logEvent, target);
        }
    }

    [LayoutRenderer(NewRelicCsvLayout.TraceDataLayoutRendererName)]
    public class TraceDataLayoutRenderer : LayoutRenderer
    {
        public string metaproperty { get; set; }
        private  Lazy<NewRelic.Api.Agent.IAgent> _nrAgent;
  
        protected override void InitializeLayoutRenderer()
        {
            Func<NewRelic.Api.Agent.IAgent>  agentFactory = NewRelic.Api.Agent.NewRelic.GetAgent;
            _nrAgent = new Lazy<NewRelic.Api.Agent.IAgent>(agentFactory);

        }

        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            if (_nrAgent != null)
            {
                var metadata = _nrAgent.Value.GetLinkingMetadata();
                if (metadata != null)
                {
                    foreach (var pair in metadata)
                    {
                        // match if we have right meta property
                        // When transaction startswe should eventually see traceid and span id pulled via metadata call.
                        if (pair.Key.CompareTo(metaproperty) == 0)
                        {
                            builder.Append(pair.Value);
                        }
                    }
                }
            }
        }
    }

}

