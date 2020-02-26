using NLog;
using NLog.Common;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Layouts;
using System;
using System.Text;


namespace NewRelic.LogEnrichers.NLog
{
    [Layout("newrelic-csvlayout")]
    public class NewRelicCsvLayout : CsvLayout
    {
        internal const string TimestampLayoutRendererName = "nr-unix-timestamp";

        private readonly Lazy<NewRelic.Api.Agent.IAgent> _nrAgent;

        internal NewRelicCsvLayout(Func<NewRelic.Api.Agent.IAgent> agentFactory) : base()
        {
            _nrAgent = new Lazy<NewRelic.Api.Agent.IAgent>(agentFactory);
            LayoutRenderer.Register<UnixTimestampLayoutRenderer>(TimestampLayoutRendererName);


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
                            Columns.Add(new CsvColumn(pair.Key, pair.Value));
                        }
                    }
                }
                catch (Exception ex)
                {
                    InternalLogger.Error(ex, "Exception caught in NewRelicCsvLayout");
                }
            }
        }


        public NewRelicCsvLayout() : this(NewRelic.Api.Agent.NewRelic.GetAgent)
        {
        }

        //This prevents changing the properties that we don't want changed
        protected override void InitializeLayout()
        {
            // This reads XML configuration
            base.InitializeLayout();
        }

        protected override void RenderFormattedMessage(LogEventInfo logEvent, StringBuilder target)
        {

            // calls in to the JsonLayout to render the json as a single object
            base.RenderFormattedMessage(logEvent, target);

        }

    }
}

