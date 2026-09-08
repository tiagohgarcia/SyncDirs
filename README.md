# SyncDirs

A cross-platform command line tool that keeps a **replica** folder as an exact, one-way copy of a **source** folder.


## Requirements

- [.NET SDK 10.0] or later
- Linux or Windows (macOS not tested)

## Build, run, test

```bash
dotnet build                                     # build everything
dotnet run --project src -- <source> <replica>   # run
dotnet test                                      # run the test suite
```

## Usage

```
USAGE: synch_dirs <source> <replica> [options]

REQUIRED
<source>                    - path to source folder
<replica>                   - path to replica folder

OPTIONS
-i, --interval <seconds>    - synchronization interval in seconds (default: 60s)
-l, --logpath <path>        - path to log file (default: ./synch.log)
-h, --help                  - this info
```

Options may appear before, after or between the two positional paths.

```bash
dotnet run --project src -- ~/source ~/backup/replica -i 30 -l ~/logs/synch.log
```

## Project structure

```
docs/
    refinement.md                   project specifications and decisions
src/
    Program.cs                      entry point - arguments, call logger and loop
    Logger.cs                       custom logger: Interface (Info, Warn, Error) + console and file implementation
    PathHelper.cs                   helper for path normalization, verification and symlink chase
    CommandLineOptions.cs           parsing and validation, defaults and help
tests/
    utils/
        TempDirectory.cs            create and Dispose temporary directory for testing
        SymLinkUtils.cs             symlink creation allowed check
    PathHelperTests.cs
    CommandLineOptionsTests.cs
```