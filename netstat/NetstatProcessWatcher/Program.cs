using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace NetstatProcessWatcher
{
    internal sealed class Program : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private readonly ILogger _logger;
        private readonly Task _task;
        private readonly string[] _names;

        private Program(ILogger logger, string[] args)
        {
            _logger = logger;
            _names = args;
            _cts = new CancellationTokenSource();
            _task = Task.Factory.StartNew<Task>(BackgroundProcess, _cts.Token);
        }

        public static Program Run(ILogger logger, string[] args)
        {
            return new Program(logger, args);
        }

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
                try
                {
                    _task.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException ex)
                {
                    _logger.Error(ex);
                }
                _task.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        private static void Main(string[] args)
        {
            var logger = NLog.LogManager.GetLogger("Watcher");
            try
            {
                using (Program.Run(logger, args))
                {
                    Console.WriteLine("Press a key to quit...");
                    Console.ReadKey(true);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                Console.WriteLine("Press a key to quit...");
                Console.ReadKey(true);
                Environment.Exit(1);
            }
        }

        private async Task BackgroundProcess()
        {
            var interval = TimeSpan.FromMinutes(1);
            using (_names != null && _names.Length !=0 ? new ProcessWatcher(_logger, interval, _names) : new DefaultWatcher(_logger, interval))
            {
                while (!_cts.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                }
            }
        }
    }
}