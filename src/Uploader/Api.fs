namespace Uploader

module Api =
    let imageById<'a> = PrintfFormat<_,_,_,_,'a> "/images/%O"

    let [<Literal>] Images = "/images"
