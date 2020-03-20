namespace Uploader

[<RequireQualifiedAccess>]
module Configuration =
    open System.IO
    open Microsoft.Extensions.Configuration

    let root =
        ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory ())
            .AddJsonFile("appsettings.json", optional=false, reloadOnChange=false)
            .AddJsonFile("appsettings.overrides.json", optional=true, reloadOnChange=false)
            .AddJsonFile("logsettings.json", optional=false, reloadOnChange=false)
            .AddEnvironmentVariables()
            .Build ()
