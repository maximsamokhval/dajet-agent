using DaJet.Metadata;
using DaJet.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DaJet.Agent.Consumer
{
    public sealed class MessageConsumerService : BackgroundService
    {
        private const string LOG_TOKEN = "C-SVC";
        private const string SERVICE_HEARTBEAT_MESSAGE = "Message consumer service heartbeat.";
        private const string CRITICAL_ERROR_DELAY_TEMPLATE = "Consumer critical error delay of {0} seconds started.";
        private IServiceProvider Services { get; set; }
        private MessageConsumerSettings Settings { get; set; }
        private IMessageConsumer MessageConsumer { get; set; }
        public MessageConsumerService(IServiceProvider serviceProvider, IOptions<MessageConsumerSettings> options)
        {
            Settings = options.Value;
            Services = serviceProvider;
            MessageConsumer = Services.GetService<IMessageConsumer>();
        }
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            FileLogger.Log(LOG_TOKEN, "Message consumer service is started.");
            return base.StartAsync(cancellationToken);
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            FileLogger.Log(LOG_TOKEN, "Message consumer service is stopping ...");
            try
            {
                MessageConsumer.Dispose();
            }
            catch (Exception error)
            {
                FileLogger.Log(LOG_TOKEN, ExceptionHelper.GetErrorText(error));
            }
            FileLogger.Log(LOG_TOKEN, "Message consumer service is stopped.");
            return base.StopAsync(cancellationToken);
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Running the job in the background
            _ = Task.Run(async () => { await DoWork(stoppingToken); }, stoppingToken);
            // Return completed task to let other services to run
            return Task.CompletedTask;
        }
        private async Task DoWork(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                bool IsInCriticalErrorState = false;
                try
                {
                    MessageConsumer.Consume();
                    FileLogger.Log(LOG_TOKEN, SERVICE_HEARTBEAT_MESSAGE);
                }
                catch (Exception error)
                {
                    IsInCriticalErrorState = true;
                    FileLogger.Log(LOG_TOKEN, ExceptionHelper.GetErrorText(error)
                        + Environment.NewLine
                        + string.Format(CRITICAL_ERROR_DELAY_TEMPLATE, Settings.CriticalErrorDelay));
                }
                if (IsInCriticalErrorState)
                {
                    await Task.Delay(Settings.CriticalErrorDelay * 1000, stoppingToken);
                }
                else
                {
                    await Task.Delay(180000, stoppingToken);
                }
            }
        }
    }
}