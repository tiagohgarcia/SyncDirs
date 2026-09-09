namespace SyncDirs;

internal sealed class CommandLineOptions
{
    private const int DefaultIntervalSeconds = 60;
    private const string DefaultLogFileName = "syncdirs.log";

    public string SourcePath { get; }
    public string ReplicaPath { get; }
    public TimeSpan Interval { get; }
    public string LogPath { get; }

    public const string HelpText = """
        
        USAGE: syncdirs <source> <replica> [options]
        
        REQUIRED
        <source>                    - path to source folder
        <replica>                   - path to replica folder 

        OPTIONS
        -i, --interval <seconds>    - synchronization interval in seconds (default: 60s)
        -l, --logpath <path>        - path to log file (default: ./syncdirs.log)
        -h, --help                  - this info

        """;

    private CommandLineOptions(string source, string replica, TimeSpan interval, string logPath)
    {
        SourcePath = source;
        ReplicaPath = replica;
        Interval = interval;
        LogPath = logPath;
    }
    
    public static CommandLineOptions Parse(string[] args)
    {
        List<string> arguments = new List<string>();
        string? intervalAux = null;
        string? logpathAux = null;

        for(int i = 0; i < args.Length; i++)
        {
            switch(args[i])
            {
                case "-i" or "--interval":
                    intervalAux = NextValue(args, ref i, "--interval");
                    break;
                case "-l" or "--logpath":
                    logpathAux = NextValue(args, ref i, "--logpath");
                    break;
                default:
                    if(args[i].StartsWith('-'))
                        throw new ArgumentException($"unknown option '{args[i]}'");
                    arguments.Add(args[i]);
                    break;
            }
        }

        if(arguments.Count != 2)
            throw new ArgumentException("expected exactly two paths: <source> <replica>");

        // interval verification
        int interval;
        if(intervalAux == null)
        {
            interval = DefaultIntervalSeconds;
        }
        else
        {
            if(!int.TryParse(intervalAux, out interval) || interval <= 0)
                throw new ArgumentException("interval must be a positive number");
        }
        
        TimeSpan intervalSpan = TimeSpan.FromSeconds(interval);

        // log path verification
        if(logpathAux == null)
            logpathAux = DefaultLogFileName;

        if(string.IsNullOrWhiteSpace(logpathAux))
            throw new ArgumentException($"log path is not valid: {logpathAux}");

        string logpath = PathHelper.NormalizePath(logpathAux);
        
        // source path verification
        string source = PathHelper.GetRealPath(arguments[0]);

        if (File.Exists(source))
            throw new ArgumentException($"<source> is a file, not a directory: {source}");

        if (!Directory.Exists(source))
            throw new ArgumentException($"<source> directory does not exist: {source}");

        // replica path verification
        string replica = PathHelper.GetRealPath(arguments[1]);
        if(File.Exists(replica))
            throw new ArgumentException($"<replica> is a file, not a directory: {replica}");

        // source and replica are not the same
        if(PathHelper.AreEqual(source, replica))
            throw new ArgumentException("<source> and <replica> cannot be the same folder");

        // replica is not inside source or source not inside replica
        if(PathHelper.IsInside(replica, source) || PathHelper.IsInside(source, replica))
            throw new ArgumentException("<replica> cannot be a subfolder of <source> and <source> cannot be a subfolder of <replica>");

        // log file not inside replica or source
        if(PathHelper.IsInside(logpath, replica) || PathHelper.IsInside(logpath, source))
            throw new ArgumentException("invalid log path provided. Log file cannot be inside source or replica");

        return new CommandLineOptions(source, replica, intervalSpan, logpath);
    }

    private static string NextValue(string[] args, ref int index, string option)
    {
        index++;
        if(index < args.Length)
        {
            string value = args[index];
            if (value.StartsWith('-')) 
                throw new ArgumentException($"{option} requires a value");

            return value;
        }
        else
        {
            throw new ArgumentException($"invalid value for {option}");
        }
        
    }

    public override string ToString()
    {
        return $"source={SourcePath} replica={ReplicaPath} interval={Interval.TotalSeconds}s log={LogPath}";
    }
}