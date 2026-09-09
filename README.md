# SyncDirs

A cross-platform command line tool that keeps a **replica** folder as an exact, one-way copy of a **source** folder. Synchronization runs periodically until the process is stopped, and every create, copy, update and delete is written to both the console and a log file.


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
USAGE: syncdirs <source> <replica> [options]

REQUIRED
<source>                    - path to source folder
<replica>                   - path to replica folder

OPTIONS
-i, --interval <seconds>    - synchronization interval in seconds (default: 60s)
-l, --logpath <path>        - path to log file (default: ./syncdirs.log)
-h, --help                  - this info
```

Options may appear before, after or between the two positional paths.

```bash
dotnet run --project src -- ~/source ~/backup/replica -i 30 -l ~/logs/sync.log
```

Press `Ctrl+C` to stop.
The current cycle is cancelled, the log file is flushed and closed, and the process exits with code `0`.
Invalid arguments exit with code `1` and print the reason on stderr.

## How a cycle works

Each cycle walks the source tree one directory at a time and, for every directory:

1. Creates the matching replica directory if it does not exist.
2. Snapshots the source directory's entries.
3. Copies each file whose content differs, and recurses into each subdirectory.
4. Deletes any replica entry whose name is not present in the source.

## Design decisions

### Change detection

A file is compared in three steps, stopping at the first one that decides:

1. **Different size** → different.
2. **Same size and same last-write time** → assumed identical.
3. **Otherwise** → both files are hashed with MD5 and the hashes compared.

The layered approach pays the cost of hashing only for the files that actually look suspicious.

**Modification times are copied along with content.**
After every copy the replica is stamped with the source file's `LastWriteTimeUtc`.

### Symlinks are followed

A symlinked file is copied as a regular file and a symlinked directory is materialised as a real directory, so the replica is a self-contained copy.

- **Deletion.** If the replica contains a symlink, only the link itself is removed, never the contents it points at.

### Validation happens once, before anything runs

`CommandLineOptions.Parse` resolves all three paths:

| Rules|
|------|
| Source must exist and be a directory |
| Replica must not be an existing **file** |
| Source and replica must differ |
| Neither may be nested inside the other |
| The log file must not sit inside either folder |

Paths are resolved through symlinks before these comparisons, so a replica that is a link pointing into the source is still caught.

### Errors are logged, not fatal

A locked file, a permission error or a dangling symlink is logged against that one entry and the cycle continues.


### Cross-platform behaviour

Paths are always built with `Path.Combine` and compared through a single `PathHelper` that picks its case sensitivity once at startup — ordinal on Linux, case-insensitive on Windows and macOS.

## Logging

Every operation is written to the console and appended to the log file in the same format:

```
2026-09-08 14:22:07.482 +01:00 [INFO ] created directory: sub
2026-09-08 14:22:07.494 +01:00 [INFO ] copied file: sub/report.txt
2026-09-08 14:22:07.501 +01:00 [INFO ] deleted file: obsolete.txt
2026-09-08 14:22:07.503 +01:00 [WARN ] replacing directory with file: notes
2026-09-08 14:22:07.510 +01:00 [ERROR] failed to process /data/source/locked.db: Permission denied
```

A cycle that changed nothing logs nothing.

## Project structure

```
.github/
    workflows/
        ci.yml                      build and test on Linux and Windows
docs/
    refinement.md                   project specifications and decisions
src/
    Program.cs                      entry point - arguments, call logger and loop
    Logger.cs                       custom logger: Interface (Info, Warn, Error) + console and file implementation
    PathHelper.cs                   helper for path normalization, verification and symlink chase
    CommandLineOptions.cs           parsing and validation, defaults and help
    SyncEngine.cs                   one cycle: create/copy pass, and delete pass
    FileComparer.cs                 size / timestamp / MD5 comparison
    SyncStats.cs                    pre-cycle counters and summary line
tests/
    utils/                          fixtures, fake log, and platform capabilities checks
    LoggerTests.cs                  format, append, failure handling, thread safety
    PathHelperTests.cs              path normalization, containment, symlink resolution
    CommandLineOptionsTests.cs      argument shapes and validation rules
    SyncEngineTests.cs              one cycle end to end, including error paths
    E2ETests.cs                     built binary, via real proccess
    SyncEnginePerformaceTests.cs    steady-cycle cost, measured and reported
```

## Testing

```bash
dotnet test                                                    # everything
dotnet test --filter "Category!=Performance"                   # fast feedback loop
dotnet test --filter "Category=Performance" --logger "console;verbosity=detailed"
dotnet test --collect:"XPlat Code Coverage"
```

### What is covered

Pure logic (`PathHelper`, `CommandLineOptions`, `FileComparer`) is covered by fast tests with no filesystem access where possible.\
`SyncEngine` is covered by integration tests against real temporary directories, since its goal is filesystem behaviour.\
`E2ETests` executes the built binary as a process and asserts on exit codes, stdout/stderr, and the log file it produces.

### Platform-specific tests are skipped, never silently passed

| Attribute | Gate |
|---|---|
| `[SymLinkFact]` | symlink creation is permitted (fails on Windows without Developer Mode) |
| `[FilePermissionFact]` | POSIX permissions are enforced for the current user (no-ops as root, unavailable on Windows) |
| `[PosixSignalFact]` | `SIGINT` can be sent (not available on Windows) |

### Performance

`SyncEnginePerformanceTests` builds a tree of 10,000 files and reports throughput for the initial sync and for the steady-state cycle that follows.

It asserts on the **ratio** between the two rather than on an absolute duration.

### Coverage

(On Linux)\
96% line coverage\
100% branch coverage

## Known Limitations

- **A file whose size and timestamp both match is assumed unchanged**, even if its content differs. This is the deliberate cost of the fast path; the case is tested so the behaviour is explicit rather than accidental.
- **The log file grows without bound.** There is no rotation.
- **Some error-path tests only run on Linux.** The tests that revoke permissions depend on POSIX modes, so they skip on Windows and when running as root. Deletion failures are verified on Linux only.