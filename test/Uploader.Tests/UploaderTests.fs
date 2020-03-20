namespace Uploader

module Tests =
    open FSharp.Control.Tasks.V2.ContextInsensitive
    open System
    open System.Collections.Generic
    open System.Drawing
    open System.IO
    open System.Net
    open System.Net.Http
    open System.Text
    open System.Threading.Tasks
    open Microsoft.AspNetCore.Hosting
    open Microsoft.AspNetCore.TestHost
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.Extensions.Logging

    open Xunit
    open Xunit.Abstractions
    open Serilog
    open Swensen.Unquote

    open Uploader.Types

    let [<Literal>] BaseAddress = "http://localhost:5000"

    let [<Literal>] EmptyImage = @".\..\..\..\assets\empty.png"
    let [<Literal>] Image640x480 = @".\..\..\..\assets\1_640x480.png"
    let [<Literal>] Image1280x798 = @".\..\..\..\assets\2_1280x798.png"

    // NOTE: we depend on ImageServerStub here
    // ImageServerStub must be up and images should match
    let [<Literal>] Image640x480Url = "http://localhost:7000/images/1_640x480.png"
    let [<Literal>] Image1280x798Url = "http://localhost:7000/images/2_1280x798.png"

    let verify = test

    let imageFromArray (input: byte[]) =
        use stream = new MemoryStream (input)
        Image.FromStream (stream)

    let verifyPreview (input: byte[]) =
        // there is no elegant way to validate preview content, but at least we can verify its properties

        let preview = imageFromArray (input)
        verify <@ preview.Height = 100 @>
        verify <@ preview.Width = 100 @>

    type T (output: ITestOutputHelper) =
        let server =
            let result =
                new TestServer (
                    WebHostBuilder()
                        .UseStartup<Program.Startup>()
                        .UseSerilog()
                )

            // TODO: align logging configurations
            Log.Logger <-
                 LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", Events.LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft.AspNetCore", Events.LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.TestOutput(output)
                    .CreateLogger()

            result

        let client =
            let result = server.CreateClient ()
            result.BaseAddress <- Uri BaseAddress
            result

        let postImages (requestUri: string) (images: IEnumerable<string>) =
            task {
                use request = new HttpRequestMessage (HttpMethod.Post, requestUri)
                use payload = new MultipartFormDataContent ()

                images
                |> Seq.iter (fun path ->
                    // oh no, we're not properly handle IDisposables...
                    // relax, it's OK to do so in tests, to keep it stupid simple
                    let stream = new FileStream (path, FileMode.Open)
                    let content = new StreamContent (stream)

                    let fileName = Path.GetFileName (path)
                    payload.Add (content, "file", fileName)
                )

                request.Content <- payload
                return! client.SendAsync (request)
            }

        let postUrls (requestUri: string) (payload: UploadRequest) =
            task {
                let payload = Json.serialize payload

                use payload = new StringContent (payload, Encoding.UTF8, "application/json")
                use request = new HttpRequestMessage (HttpMethod.Post, requestUri)
                request.Content <- payload

                return! client.SendAsync (request)
            }

        let getImage (requestUri: string) =
            task {
                return! client.GetByteArrayAsync (requestUri)
            }

        interface IDisposable
            with
                member __.Dispose () =
                    client.Dispose ()
                    server.Dispose ()

        [<Fact>]
        member __.NoImageData () = task {
            let! result = postImages Api.Images [EmptyImage]
            result.StatusCode =! HttpStatusCode.BadRequest
        }

        [<Theory>]
        [<InlineData(Image640x480)>]
        [<InlineData(Image1280x798)>]
        member __.SingleImage (input) = task {
            let! response = postImages Api.Images [input]
            verify <@ response.StatusCode = HttpStatusCode.OK @>

            // ASSERT
            let! uploadResult = response.Content.ReadAsStringAsync ()
            let uploadResult = Json.deserialize<UploadResult> (uploadResult)

            let location = List.exactlyOne uploadResult.Locations
            Assert.Matches ("^/images/(.)+", location)

            let expected = File.ReadAllBytes (input)

            let! result = getImage location
            Assert.Equal<byte[]>(expected, result)

            let previewLocation = location + "?preview"
            let! preview = getImage previewLocation
            verifyPreview preview
        }

        [<Fact>]
        member __.MultipleImages () = task {
            let! response = postImages Api.Images [Image640x480; Image1280x798]
            verify <@ response.StatusCode = HttpStatusCode.OK @>

            // ASSERT
            let! uploadResult = response.Content.ReadAsStringAsync ()
            let uploadResult = Json.deserialize<UploadResult> (uploadResult)

            verify <@ List.length uploadResult.Locations = 2 @>

            let expected =
                [ File.ReadAllBytes (Image640x480)
                  File.ReadAllBytes (Image1280x798) ]

            for location in uploadResult.Locations do
                Assert.Matches ("^/images/(.)+", location)

                let! result = getImage location
                Assert.Contains (result, expected)

                let previewLocation = location + "?preview"
                let! preview = getImage previewLocation
                verifyPreview preview
        }

        (* TODO: ImageServerStub

        [<Theory>]
        [<InlineData(Image640x480Url, Image640x480)>]
        [<InlineData(Image1280x798Url, Image1280x798)>]
        member __.SingleImageByUrl (targetUrl, expected) = task {
            let request = { TargetUrls = [targetUrl] }
            let! response = postUrls Api.Images request

            // ASSERT
            verify <@ response.StatusCode = HttpStatusCode.OK @>

            let! uploadResult = response.Content.ReadAsStringAsync ()
            let uploadResult = Json.deserialize<UploadResult> (uploadResult)

            let location = List.exactlyOne uploadResult.Locations
            Assert.Matches ("^/images/(.)+", location)

            let expected = File.ReadAllBytes (expected)

            let! result = getImage location
            Assert.Equal<byte[]>(expected, result)

            let previewLocation = location + "?preview"
            let! preview = getImage previewLocation
            verifyPreview preview
        }

        [<Fact>]
        member __.MultipleImagesByUrls () = task {

            let request = { TargetUrls = [Image640x480Url; Image1280x798Url] }
            let! response = postUrls Api.Images request

            // ASSERT
            verify <@ response.StatusCode = HttpStatusCode.OK @>

            let! uploadResult = response.Content.ReadAsStringAsync ()
            let uploadResult = Json.deserialize<UploadResult> (uploadResult)

            verify <@ List.length uploadResult.Locations = 2 @>

            let expected =
                [ File.ReadAllBytes (Image640x480)
                  File.ReadAllBytes (Image1280x798) ]

            for location in uploadResult.Locations do
                Assert.Matches ("^/images/(.)+", location)

                let! result = getImage location
                Assert.Contains (result, expected)

                let previewLocation = location + "?preview"
                let! preview = getImage previewLocation
                verifyPreview preview
        }

        *)

        // TODO: extract assertion method
