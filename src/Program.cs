using System.Diagnostics;
using SyncDirs;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        // show help
        if(args.Contains("-h") || args.Contains("--help"))
        {
            Console.WriteLine(CommandLineOptions.HelpText);
            return 0;
        }

        // read command line arguments
        CommandLineOptions options;
        try
        {
            options = CommandLineOptions.Parse(args);
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            Console.Error.WriteLine(CommandLineOptions.HelpText);
            return 1;
        }

        // start logger
        Logger log;
        try
        {
            log = new Logger(options.LogPath);
        }
        catch(Exception e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }

        using(log)
        {
            log.Info(options.ToString());

            using CancellationTokenSource cts = new();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            SyncEngine engine = new SyncEngine(options.SourcePath, options.ReplicaPath, log);
            Stopwatch sw = new();

            while(!cts.IsCancellationRequested)
            {
                try
                {
                    sw.Restart();
                    SyncStats stats = await engine.RunCycleAsync(cts.Token);
                    if(stats.HasChanges) 
                    {
                        log.Info($"cycle completed in {sw.Elapsed.TotalMilliseconds:F1}ms:");
                        log.Info($"{stats}");
                    }
                }
                catch(OperationCanceledException)
                {
                    break;
                }
                catch(Exception e)
                {
                    log.Error($"cycle failed: {e.Message}");
                }

                try
                {
                    await Task.Delay(options.Interval, cts.Token);
                }
                catch(OperationCanceledException)
                {
                    break;
                }
            }

            log.Info("sync stopped");
        }
        return 0;
    }
}