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

        }
        return 0;
    }
}