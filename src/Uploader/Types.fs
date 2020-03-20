namespace Uploader

module Types =

    open System
    open Thoth.Json.Net

    type UploadRequest =
        { TargetUrls: string list }
        with
            static member Encode instance =
                Encode.object
                    [ "targetUrls", instance.TargetUrls |> List.map Encode.string |> Encode.list ]

            static member Decoder : Decoder<UploadRequest> =
                Decode.map
                    (fun targetUrls ->
                        { TargetUrls = targetUrls }
                    )
                    (Decode.field "targetUrls" (Decode.list Decode.string))

    type UploadResult =
        { Locations: string list }
        with
            static member Decoder =
                Decode.map
                    (fun locations ->
                        { Locations = locations }
                    )
                    (Decode.field "locations" (Decode.list Decode.string))
