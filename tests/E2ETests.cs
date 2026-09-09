using System.Diagnostics;

namespace SyncDirs.Tests;

public class E2ETests
{
    private static string executable => Path.Combine(
            AppContext.BaseDirectory, 
            OperatingSystem.IsWindows() ? "syncdirs.exe" : "syncdirs");

    private async Task<(int ExitCode, string stdout, string stderr)> RunProgram(string[] args, bool interruptWithSigint)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute = false,
        };
        
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
        
        using Process p = Process.Start(startInfo)!;

        Task<string> stdout = p.StandardOutput.ReadToEndAsync();
        Task<string> stderr = p.StandardError.ReadToEndAsync();

        if(interruptWithSigint)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));

            using (Process kill = Process.Start("kill", ["-INT", p.Id.ToString()])!)
                await kill.WaitForExitAsync();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try { 
            await p.WaitForExitAsync(cts.Token); 
        }
        catch (OperationCanceledException)
        {
            p.Kill(entireProcessTree: true);
            throw new TimeoutException($"process did not exit within 15 seconds");
        }

        return (p.ExitCode, await stdout, await stderr);
    }

    [Theory]
    [Trait("Category", "E2E")]
    [InlineData(new[] { "--help" }, 0)]
    [InlineData(new[] { "-h" }, 0)]
    [InlineData(new string[0], 1)]
    [InlineData(new[] { "/source" }, 1)]
    public async Task Run_ArgumentHandling_ReturnsExpectedExitCode(string[] args, int expected)
    {
        var (exitCode, stdout, stderr) = await RunProgram(args, false);

        Assert.Equal(expected, exitCode);
        if(expected == 0)
            Assert.Contains("USAGE: ", stdout);
        else
            Assert.Contains("error:", stderr);
    }

    [PosixSignalFact]
    public async Task Run_ValidArguments_SyncFilesAndWritesLogs()
    {
        using var temp = new TempDirectoryUtils();
        string source  = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        string replica = Directory.CreateDirectory(Path.Combine(temp.Path, "replica")).FullName;
        string logPath = Path.Combine(temp.Path, "test.log"); 

        File.WriteAllText(Path.Combine(source, "data.txt"), "test");

        string[] args = {source, replica, "-i", "2", "-l", logPath};

        var (exitCode, stdout, stderr) = await RunProgram(args, true);

        Assert.Equal(0, exitCode);
        Assert.Equal("test", File.ReadAllText(Path.Combine(replica, "data.txt")));
        string logContent = File.ReadAllText(logPath);
        Assert.Contains("copied file: data.txt", logContent);
        Assert.Contains("sync stopped", logContent);
    }
}
