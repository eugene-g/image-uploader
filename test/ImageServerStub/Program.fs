namespace ImageServerStub

module Program =
    open System
    open System.IO
    open System.Net
    open System.Net.Http
    open System.Threading.Tasks
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Hosting
    open Microsoft.AspNetCore.Http
    open Microsoft.AspNetCore.Http.Features
    open Microsoft.AspNetCore.StaticFiles
    open Microsoft.Extensions.Configuration
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.Extensions.Logging
    open Serilog
    open Giraffe
    open Giraffe.Serialization
    open FSharp.Control.Tasks.V2.ContextInsensitive

    Log.Logger <-
         LoggerConfiguration()
            .ReadFrom.Configuration(Configuration.root)
            .CreateLogger()

    let unhandledError (logException: exn -> unit) (ex: exn) _ =
        logException ex

        setStatusCode (int HttpStatusCode.InternalServerError)
        >=> json "Internal server error"

    let handledError error =
        setStatusCode (int HttpStatusCode.OK)
        >=> text error

    // let getImageById (imageId: Guid) : HttpHandler =
    //     fun (next: HttpFunc) (ctx: HttpContext) ->
    //         task {
    //             let getImage =
    //                 match ctx.TryGetQueryStringValue "preview" with
    //                 | Some _ -> Db.getImagePreviewById
    //                 | None -> Db.getImageById

    //             match! getImage connectionString imageId with
    //             | Ok image -> return! setBody image next ctx
    //             | Error error -> return! handledError error next ctx
    //         }

    let notFound : HttpHandler =
        setStatusCode (int HttpStatusCode.NotFound)
        >=> fun next ctx ->
            json (sprintf "Path not found: %O" ctx.Request.Path) next ctx

    let composeApp =
        choose [
            //GET  >=> routef Api.imageById getImageById
            notFound
        ]

    type Startup () =
        let logException ex =
            Log.Error (ex, "An unhandled exception has occurred while executing the request.")

        member __.Configure (app: IApplicationBuilder) =
            app.UseGiraffeErrorHandler(unhandledError logException)
                .UseSerilogRequestLogging()
                .UseStaticFiles()
                .UseGiraffe (composeApp)

        member __.ConfigureServices (services: IServiceCollection) =
            services
                .AddGiraffe()
                .AddHttpContextAccessor()
                |> ignore

    [<EntryPoint>]
    let main _ =
        try
            try
                Log.Information "Starting ImageServerStub service..."

                WebHostBuilder()
                    .UseKestrel()
                    .UseConfiguration(Configuration.root)
                    .UseWebRoot(Directory.GetCurrentDirectory())
                    .UseStartup<Startup>()
                    .UseSerilog()
                    .Build()
                    .Run()

                0
            with
            | ex ->
                Log.Fatal (ex, "Host terminated unexpectedly")
                1
        finally
            Log.CloseAndFlush ()
