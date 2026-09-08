# Refinement

## Specifications

Program that synchronizes two folders: source and replica

- Should maintain a full, indentical copy of source folder at replica folder
- Synchronization must be one-way: replica folder must match exactly the content from source folder
- Synchronnization must be done periodically
- File creation/copy/removal operations should be logged to a file and console output
- Folder paths, synchronization interval and log file path should be provided using command line args
- No use of third-party libraries that implement folder synchronization
- It is allowed use of external libraries for well-known algorithms
- Should be cross platform (Linux & Windows)

### Aproach

---------------------------------------------------------------------
1. **Console app + args**
    1. source path
    2. replica path
    3. interval in seconds (flag)
    4. log path (flag)
    5. help (flag)

    - Path validation
        - source must exist
        - replica cannot be inside source or source inside replica
        - replica cannot be an existing file
        - replica and source cannot be the same
        - log path cannot be inside source or replica

    - defaults
        - interval and log path are optional
        - interval: 60 seconds
        - log path: ./syncdirs.log

    - No third-party necessary: 
        - Only 4 arguments - easy to handle with switch case
        - path validation is the tricky part

    example:
    `syncdirs <source> <replica> [--interval <seconds>] [--logpath <path>] [--help]`

---------------------------------------------------------------------

2. **Detection of difference: "How to decide which files need copying/deletion"**

    - File/Dir exists in source and:
        1. File/Dir does not exist in replica --> then copy

        2. File exists in replica:
            1. size
            2. last write time
            3. content hash (MD5)
            - **Process:** filter by size and 'last write time' first (cheap/fast), and only if these don't match hash content and compare (more expensive)
            - if it does not match --> then copy
        
        3. edge cases: 
            - replica has a dir with same name of file in source --> then delete replica dir and copy file from source
            - replica has a file with same name of dir in source --> then delete replica file and copy dir from source

    - File/Dir exists in replica but not in source --> then delete

    - symbolic links will be accepted:
        - Need a guard for infinite recursing (double linked files)
        - if replica dir is a symlink, when deleting, delete the link and not the target file
        - In Linux should work without problems, but windows can be trickier (testing + creating symlinks)

    - Errors: File is locked or permission denied --> log it and continue iteration
        - Errors should be logged but not stop the iteration cycle

---------------------------------------------------------------------

3. **PathHelper: Will need to valide paths for command line parsing and during change detection cycle**
    - Worth creating an helper that:
        - Normalizes paths
        - get target path behind a symlink
        - check is path is inside another (ex: source/replica)
        - check if paths are the same

---------------------------------------------------------------------

4. **After finishing iteration wait "interval" defined time before starting next iteration**
    
    - **decision: use a delay instead of periodic timer**, because we want to restart the stopwatch only when we finish the current iteration and not right away while in the middle of an iteration cycle
        - reason: to avoid the scenario where iteration cycle is still going and next iteration cycle should be starting

---------------------------------------------------------------------

5. **Logging: Create custom logger that writes to console and a file**
    
    - Third-party won't be necessary
        - would still need to create the file sink
        - very minimalistic approach with `Info, Warning and Error`

    - Every operation is written to the console and appended to file

    - structure draf: "Timestamp [LEVEL] message"

    - decisions:
        - format timestamp with InvariantCulture to avoid shape changes depending on machine locale
        - Warning and Errors go to stderr
        - if a cycle does not change anything, then no logs (avoid noise)
        - no log rotation (simplicity for this example)


## Project strucure (DRAFT)
```
docs/
    refinement.md           this file
src/
    Program.cs              entry point - arguments, call logger and loop
    Logger.cs               custom logger: Interface (Info, Warn, Error) + console and file implementation
    PathHelper.cs           helper for path normalization, verification (inside or equal) and symlink chase
    CommandLineOptions.cs   parsing and validation, defaults and help
    SyncEngine.cs           cycle: create/copy pass + deletion pass (stray files)
tests/
    PathHelperTests.cs
    CommandLineOptionsTests.cs
    SyncEngineTests.cs
```

### Testing
    
- xUnit allows use of [Theory] and [InlineData] - useful for tests with different paths
    
    Test cases:
    
    PathHelper:

    | Case        | Expected    |
    | ----------- | ----------- |
    | Normalize path with extra '/' | Trim extra chat      |
    | Normalize path that is root  | root stays - no change     |
    | Check if paths are inside other paths ([InlineData]) | true or false      |
    | Check if paths are the same ([InlineData]) - try names with different capitaization (Windows and Linux handle it differently) | true or false      |
    | Get real path from symlinks (link to file, link to directory, link to link, double linked, nonexistent) - detail: will need to create temp files and might not work on Windows (depends on version) | true or false      |
    
    CommandLineOptions:

    | Case        | Expected    |
    | ----------- | ----------- |
    | Parse parameters with invalid shape |  Exception     |
    | source (does not exist / is a file / subdirectory of replica) |  Exception     |
    | replica (is a file / same as source / subdirectory of source) |  Exception     |
    | log file (same as source or replica/ inside of source or replica / is a directory) |  Exception     |
    | valid arguments |  valid object     |
    | valid arguments in different order |  valid object     |
    | log path and interval are not passed |  valid object with default interval and log  |

    SyncEngine:

    | Case        | Expected    |
    | ----------- | ----------- |
    | Compare files (size/mtime/hash) - size first, then mtime and finally hash (limitation mentioned in Approach (2.)) |  true or false     |
    | New file in source |  file copied in replica     |
    | No changes in source |  no changes in replica    |
    | file deleted in source |  file deleted in replica    |
    | empty directories and nested directories |  copied to replica    |
    | source has file but replica has dir with same name |  delete dir in replica and copy file from source to replica   |
    | source has dir but replica has file with same name |  delete file in replica and create dir in replica    |
    | mtime changes in file inside source but not the content |  file not copied (hash still matches)    |
