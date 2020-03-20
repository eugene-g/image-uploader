namespace Uploader

[<RequireQualifiedAccess>]
module Json =
    open System.Collections.Generic
    open Newtonsoft.Json
    open Newtonsoft.Json.Serialization

    module private Settings =
        type T =
            { DateTimeFormat: string
              DateFormatHandling: DateFormatHandling
              DateTimeZoneHandling: DateTimeZoneHandling
              DateParseHandling: DateParseHandling
              ReferenceLoopHandling: ReferenceLoopHandling
              NullValueHandling: NullValueHandling
              Formatting: Formatting }
            with
                static member Default =
                    { DateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK"
                      DateFormatHandling = DateFormatHandling.IsoDateFormat
                      DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind
                      DateParseHandling = DateParseHandling.None
                      ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                      NullValueHandling = NullValueHandling.Include
                      Formatting = Formatting.Indented }

    type InvalidJsonValueException (message: string) =
        inherit System.Exception (message)

    let private isoDateTimeConverter =
        Newtonsoft.Json.Converters.IsoDateTimeConverter (
            Culture=System.Globalization.CultureInfo.InvariantCulture,
            DateTimeFormat=Settings.T.Default.DateTimeFormat
        )

    let serializerSettings =
        JsonSerializerSettings (
            ContractResolver = CamelCasePropertyNamesContractResolver (),
            DateFormatHandling = Settings.T.Default.DateFormatHandling,
            DateTimeZoneHandling = Settings.T.Default.DateTimeZoneHandling,
            DateParseHandling = Settings.T.Default.DateParseHandling,
            NullValueHandling = Settings.T.Default.NullValueHandling,
            ReferenceLoopHandling = Settings.T.Default.ReferenceLoopHandling,
            Formatting = Settings.T.Default.Formatting,
            Converters =
                List<_> (
                    [|isoDateTimeConverter :> Newtonsoft.Json.JsonConverter|])
        )

    let serialize<'a> input = JsonConvert.SerializeObject (input, serializerSettings)
    let deserialize<'a> input = JsonConvert.DeserializeObject<'a> (input, serializerSettings)
