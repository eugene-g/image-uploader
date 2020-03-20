namespace Uploader

module Db =
    open FSharp.Control.Tasks.V2.ContextInsensitive
    open System
    open Npgsql
    open Serilog

    let private openConnection connectionString =
        task {
            let conn = new NpgsqlConnection (connectionString)
            do! conn.OpenAsync ()
            return conn
        }

    type NpgsqlCommand with
        member m.AddParameter (name: string, value) =
            m.Parameters.AddWithValue (name, value)
            |> ignore

        member m.AddBinaryParameter (name: string, value) =
            m.Parameters.AddWithValue (name, NpgsqlTypes.NpgsqlDbType.Bytea, value)
            |> ignore

    module private Sql =
        [<Literal>]
        let StoreImage = "
        	INSERT INTO image (id, preview, body, uploaded_at)
        	VALUES (@id, @preview, @body, @uploaded_at)"

        [<Literal>]
        let GetImageById = "
        	SELECT body FROM image
            WHERE id = @id"

        [<Literal>]
        let GetImagePreviewById = "
        	SELECT preview FROM image
            WHERE id = @id"

    let getImageById connectionString (imageId: Guid) =
        task {
            try
                use! conn = openConnection connectionString
                use cmd = new NpgsqlCommand (Sql.GetImageById, conn)

                cmd.AddParameter ("@id", imageId)

                match! cmd.ExecuteScalarAsync () with
                | :? DBNull ->
                    return Ok [||]
                | :? (byte[]) as result ->
                    return  Ok result
                | _ ->
                    let message = "getImageById; unexpected result type - db schema out of sync"
                    Log.Error (message)
                    return Error message
            with
            | ex ->
                let message = "Image uploader database error; getImageById"
                Log.Error (ex, message)
                return Error message
        }

    let getImagePreviewById connectionString (imageId: Guid) =
        task {
            try
                use! conn = openConnection connectionString
                use cmd = new NpgsqlCommand (Sql.GetImagePreviewById, conn)

                cmd.AddParameter ("@id", imageId)

                match! cmd.ExecuteScalarAsync () with
                | :? DBNull ->
                    return Ok [||]
                | :? (byte[]) as result ->
                    return  Ok result
                | _ ->
                    let message = "getImagePreviewById; unexpected result type - db schema out of sync"
                    Log.Error (message)
                    return Error message
            with
            | ex ->
                let message = "Image uploader database error; getImagePreviewById"
                Log.Error (ex, message)
                return Error message
        }

    let storeImage connectionString (imageId: Guid) (uploadedAt: DateTimeOffset) (preview: byte[]) (image: byte[]) =
        task {
            try
                use! conn = openConnection connectionString
                use cmd = new NpgsqlCommand (Sql.StoreImage, conn)

                cmd.AddParameter       ("@id", imageId)
                cmd.AddBinaryParameter ("@preview", preview)
                cmd.AddBinaryParameter ("@body", image)
                cmd.AddParameter       ("@uploaded_at", uploadedAt)

                let! _ = cmd.ExecuteNonQueryAsync ()
                return Ok ()

            with
            | ex ->
                let message = "Image uploader database error; storeImage"
                Log.Error (ex, message)
                return Error message
        }
