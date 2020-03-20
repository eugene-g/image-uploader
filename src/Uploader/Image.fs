namespace Uploader

module Image =
    open System
    open System.Drawing
    open System.IO

    let generatePreview (image: byte array) =
        use stream = new MemoryStream (image)
        use image = Image.FromStream (stream)

        let preview =
            image.GetThumbnailImage (
                100,
                100,
                (fun () -> false),
                IntPtr.Zero
            )

        use output = new MemoryStream ()
        preview.Save (output, Imaging.ImageFormat.Png)
        output.ToArray ()
