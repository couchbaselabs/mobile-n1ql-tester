# Mobile N1QL Test Harness

This tool is for checking compliance, to the extent possible, of mobile N1QL queries vs Couchbase Server N1QL queries.  The source data for the test cases can be found in the [Couchbase Server Query Repo](https://github.com/couchbase/query/tree/master/test/filestore/test_cases/).

This harness is run in three stages.  Briefly they are as follows:

| Stage Name | Stage Job |
| ---------- | --------- |
| Prepare    | Download LiteCore shared library for the specified git commit SHA so that it can be tested |
| Migrate    | Parse the server test cases and convert them into a mobile compatible format, with databases |
| Run        | Run the converted queries, and check the results |

## Prerequisites

For your chosen working directory (if none is specified, the working directory is the same as the N1QLQueryHarness.exe folder), you must have the Couchbase Server Query Repo (see above) cloned into the `data` subdirectory.  The repo is very large, so a sparse checkout is recommended:

```
git clone --filter=blob:none --sparse https://github.com/couchbase/query data
cd data
git sparse-checkout init --cone
git sparse-checkout add test/filestore/test_cases
```

## Building / Running

This is a .NET 5.0 application, and as such it requires the [.NET 5.0 SDK](https://aka.ms/netcore) or higher to build.  A brief summary of the commands are:

| Command | What It Does |
| --------| ------------ |
| `dotnet build` | Builds the Debug variant of the program |
| `dotnet run`   | Runs the Debug variant of the program |
| `dotnet publish` | Publishes a distributable Debug version, dependent on the .NET runtime on the target machine |

Each of these commands can create a release variant instead by passing `-c Release` and in addition, the publish command can take `-r <runtime ID>` to generate a distributable version that contains the needed portions of the .NET runtime with it, so that it can run on the target machine without the .NET runtime installed.  

| :warning: Caution: Due to current limitations, a self contained standalone binary is not possible with Couchbase Lite .NET |
| ----------------- |



## Options

```
N1QLQueryHarness v0.1.0

A suite of tools for running N1QL tests using Couchbase Server test data

Usage: N1QLQueryHarness [command] [options]

Options:
  --version  Show version information.
  --help     Show help information.

Commands:
  migrate    Migrate server query test data to mobile format
  prepare    Prepare the specified version of LiteCore for use
  run        Executes the prepared query data and checks the results

Run 'N1QLQueryHarness [command] --help' for more information about a command.
```

## Prepare Stage

```
Prepare the specified version of LiteCore for use

Usage: N1QLQueryHarness prepare [options]

Options:
  --help                                                  Show help information.
  -s|--sha <SHA>                                          The SHA of the Git commit of LiteCore to use
  -w|--wd <WORKING_DIRECTORY>                             The directory to operate in (should be consistent between all subcommands)
  -c|--couchbase-log-directory <COUCHBASE_LOG_DIRECTORY>  The (optional) directory to log Couchbase Lite logs to
  -t|--trace[:<TRACE>]                                    Specifies the level of output to write at (normal|detailed|verbose)
                                                          Allowed values are: Normal, Detailed, Verbose.
```

This stage will download the LiteCore identified by the git commit SHA passed in with the `--sha` argument.  The rest of the arguments are optional.  The results will be placed in the `lib` subdirectory in the working directory (by default the same directory as the executable location).

## Migrate Stage

```
Migrate server query test data to mobile format

Usage: N1QLQueryHarness migrate [options]

Options:
  --help                                                  Show help information.
  -w|--working-directory <WORKING_DIRECTORY>              The directory to operate in (should be consistent between all subcommands)
  -c|--couchbase-log-directory <COUCHBASE_LOG_DIRECTORY>  The (optional) directory to log Couchbase Lite logs to
  -t|--trace[:<TRACE>]                                    Specifies the level of output to write at (normal|detailed|verbose)
                                                          Allowed values are: Normal, Detailed, Verbose.                                               
```

This stage will migrate data previously cloned in the prerequisite step into a folder called `out` in the working directory.  All arguments are optional.

## Run Stage

```
Executes the prepared query data and checks the results

Usage: N1QLQueryHarness run [options]

Options:
  --help                                                  Show help information.
  -o|--ignore-order                                       Considers result which only differ by ordering equal
  -j|--json-report                                        If specified, writes a JSON encoded report of the results to the given filename
  -s|--single-threaded                                    Run single threaded for debugging
  -w|--wd <WORKING_DIRECTORY>                             The directory to operate in (should be consistent between all subcommands)
  -c|--couchbase-log-directory <COUCHBASE_LOG_DIRECTORY>  The (optional) directory to log Couchbase Lite logs to
  -t|--trace[:<TRACE>]                                    Specifies the level of output to write at (normal|detailed|verbose)
                                                          Allowed values are: Normal, Detailed, Verbose.
```

This stage does the actual verification.  If the other two commands were successful there should be a `data`, `lib` and `out` folder in the working directory.  All arguments are optional but `-o` is recommended to ignore the incompatible ordering between mobile and server.  `-j` will summarize the results into a file in addition to printing the results to stdout (in color if possible). If all pass, the return code will be 0.  A positive return value indicates validation failure, and negative indicates the program could not run properly.