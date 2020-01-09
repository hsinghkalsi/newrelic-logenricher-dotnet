using System;
using Serilog;
using Serilog.Core;
using System.IO;
using System.Runtime.CompilerServices;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Sampler;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter.NewRelic;
using Microsoft.Extensions.Logging;

namespace NewRelic.LogEnrichers.Serilog.Examples
{
    class Program
    {
        private static Logger _logger;
        private static ILoggerFactory _loggerFactory;
        private static ITracer _tracer;
        //private static IConfiguration _config;

        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the New Relic Logging Extentions for Serilog");
            Console.WriteLine();

            Configure();


            // This log information will be visible in New Relic Logging. Since 
            // a transaction has not been started, this log message will not be
            // associated to a specific transaction.
            _logger.Information("Hello, welcome to Serilog Logs In Context sample app!");

            do
            {
                Console.WriteLine("Creating Logged Transactions");

                // Call three example methods that create transactions
                TestMethod("First Transaction");
                TestMethod("Second Transaction");
                TestMethod("Third Transaction");

                Console.WriteLine("Press <ENTER> to continue, Q to exit.");
            }
            while (Console.ReadLine() != "Q");

            // This log information will be visible in New Relic Logging. Since 
            // a transaction has not been started, this log message will not be
            // associated to a specific transaction.
            _logger.Information("Thanks for visitng, please come back soon!");
        }



        private static void Configure()
        {
            var config = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile("appsettings.json")
             .Build();

            var exporter = new NewRelicTraceExporter(config);

            var tracerFactory = TracerFactory.Create(b => 
            {
                b.AddProcessorPipeline(p => p.SetExporter(exporter));
                b.SetSampler(Samplers.AlwaysSample);
            });

            _tracer = tracerFactory.GetTracer("ExampleTracer");

            _logger = new LoggerConfiguration()
                .ReadFrom.Configuration(config)
                .Enrich.With(new NewRelicOpenTelemetryEnricher(_tracer, config))
                .CreateLogger();

            _loggerFactory = new LoggerFactory()
               .AddSerilog(_logger);

            exporter.WithLog(_loggerFactory.CreateLogger("bleh"));
        }


        /// <summary>
        /// This method will be recorded as a Transaction using the .Net Agent.
        /// With New Relic Logging Configured, the log messages will be associated
        /// to the transaction.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TestMethod(string testVal)
        {

            _tracer.StartActiveSpan($"{testVal}", out var rootSpan);

            _logger.Information("Starting TestMethod - {testValue}", testVal);

            try
            {
                for (var cnt = 0; cnt < 10; cnt++)
                {
                    _tracer.StartActiveSpan($"{testVal} - {cnt}", rootSpan, out var span);

                    Console.WriteLine("writing message");
                    _logger.Information("This is log message #{MessageID}", cnt);

                    span.End();
                }
            }
            catch (Exception ex)
            {
                rootSpan.Status = Status.Aborted;
                _logger.Error(ex, "Error has occurred in TestMethod - {testValue}", testVal);
            }
            finally
            {
                rootSpan.End();
            }

            _logger.Information("Ending TestMethod - {testValue}", testVal);
        }

    }


}
