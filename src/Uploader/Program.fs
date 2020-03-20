namespace Uploader

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
    open Microsoft.Extensions.Configuration
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.Extensions.Logging
    open Serilog
    open Giraffe
    open Giraffe.Serialization
    open FSharp.Control.Tasks.V2.ContextInsensitive

    open Uploader.Types

    let inline stringf format (x: ^a) =
       (^a : (member ToString : string -> string) (x, format))

    Log.Logger <-
         LoggerConfiguration()
            .ReadFrom.Configuration(Configuration.root)
            .CreateLogger()

    let private connectionString = Configuration.root.["db"]

    let unhandledError (logException: exn -> unit) (ex: exn) _ =
        logException ex

        setStatusCode (int HttpStatusCode.InternalServerError)
        >=> json "Internal server error"

    let handledError error =
        setStatusCode (int HttpStatusCode.OK)
        >=> text error

    let getImageById (imageId: Guid) : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let getImage =
                    match ctx.TryGetQueryStringValue "preview" with
                    | Some _ -> Db.getImagePreviewById
                    | None -> Db.getImageById

                match! getImage connectionString imageId with
                | Ok image -> return! setBody image next ctx
                | Error error -> return! handledError error next ctx
            }

    let imageLocation (imageId: Guid) =
        stringf "N" imageId
        |> sprintf "/images/%s"

    let saveImage (file: IFormFile) =
        task {
            use stream = new MemoryStream ()
            let! _ = file.CopyToAsync (stream)

            let image = stream.ToArray ()

            if Seq.isEmpty (image) then
                return Error "No image data"
            else
                let preview = Image.generatePreview (image)
                let imageId = Guid.NewGuid ()
                match! Db.storeImage connectionString imageId DateTimeOffset.UtcNow preview image with
                | Ok () -> return Ok (imageLocation imageId)
                | Error error -> return Error error
        }

    // TODO: HttpClientFactory
    let getImage (imageUrl: string) =
        task {
            use client = new HttpClient ()
            return! client.GetByteArrayAsync (imageUrl)
        }

    let saveImageFromUrl url =
        task {
            Log.Error url
            let! image = getImage url

            if Seq.isEmpty (image) then
                return Error "No image data"
            else
                let preview = Image.generatePreview (image)
                let imageId = Guid.NewGuid ()
                match! Db.storeImage connectionString imageId DateTimeOffset.UtcNow preview image with
                | Ok () -> return Ok (imageLocation imageId)
                | Error error -> return Error error
        }

    let saveImageList (files: IFormFileCollection) =
        task {
            return!
                files
                |> Seq.map saveImage
                |> Task.WhenAll
        }

    let handleForm =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! locations = saveImageList (ctx.Request.Form.Files)
                let locations = locations |> Seq.choose (function Ok x -> Some x | _ -> None)

                if Seq.isEmpty locations then
                    return! RequestErrors.BAD_REQUEST "Bad request" next ctx
                else
                    let result = { Locations = List.ofSeq locations }
                    return! json result next ctx
            }

    let handleJson =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! request = ctx.BindModelAsync<UploadRequest>()
                let! locations =
                    request.TargetUrls
                    |> Seq.map saveImageFromUrl
                    |> Task.WhenAll

                let locations = locations |> Seq.choose (function Ok x -> Some x | _ -> None)

                if Seq.isEmpty locations then
                    return! RequestErrors.BAD_REQUEST "Bad request" next ctx
                else
                    let result = { Locations = List.ofSeq locations }
                    return! json result next ctx
            }

    let hasJsonContentType (request: HttpRequest) =
        request.ContentType.StartsWith "application/json"

    let uploadImage : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                if ctx.Request.HasFormContentType then
                    return! handleForm next ctx
                elif hasJsonContentType ctx.Request then
                    return! handleJson next ctx
                else
                    return! RequestErrors.BAD_REQUEST "Bad request" next ctx
            }

    let notFound : HttpHandler =
        setStatusCode (int HttpStatusCode.NotFound)
        >=> fun next ctx ->
            json (sprintf "Path not found: %O" ctx.Request.Path) next ctx

    let composeApp =
        choose [
            GET  >=> routef Api.imageById getImageById
            POST >=> route Api.Images >=> uploadImage
            notFound
        ]

    type Startup () =
        let logException ex =
            Log.Error (ex, "An unhandled exception has occurred while executing the request.")

        member __.Configure (app: IApplicationBuilder) =
            app.UseGiraffeErrorHandler(unhandledError logException)
                .UseSerilogRequestLogging()
                .UseGiraffe (composeApp)

        member __.ConfigureServices (services: IServiceCollection) =
            services
                .AddGiraffe()
                .AddHttpContextAccessor()
                .Configure(Action<FormOptions>(fun x ->
                    // HACK: not the best workaround, consider streaming options
                    // also note that default limit for request size is 28.6MB, which you might want to increase
                    x.ValueLengthLimit <- Int32.MaxValue
                    x.MultipartBodyLengthLimit <- Int64.MaxValue
                ))
                .AddSingleton<IJsonSerializer>(NewtonsoftJsonSerializer Json.serializerSettings)
                |> ignore

    [<EntryPoint>]
    let main _ =
        try
            try
                Log.Information "Starting Image Uploader service..."

                WebHostBuilder()
                    .UseKestrel()
                    .UseConfiguration(Configuration.root)
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
