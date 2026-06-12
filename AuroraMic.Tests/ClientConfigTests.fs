module AuroraMic.Tests.ClientConfigTests

open Xunit
open System.IO

type ClientSettings = { LastServerIp: string; LastServerPort: int }

let private defaultPath = Path.Combine(
    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
    "AuroraMic", "client-settings.json")

let private defaults = { LastServerIp = ""; LastServerPort = 50006 }

let loadFromFile (filePath: string) =
    try
        let text = File.ReadAllText(filePath)
        if System.String.IsNullOrEmpty(text) then defaults
        else
            let result = System.Text.Json.JsonSerializer.Deserialize<ClientSettings>(text)
            if box result |> isNull then defaults
            else
                { LastServerIp = if box result.LastServerIp |> isNull then "" else result.LastServerIp
                  LastServerPort = result.LastServerPort }
    with _ -> defaults

let saveToFile (filePath: string) (settings: ClientSettings) =
    try
        let dir = Path.GetDirectoryName(filePath)
        if not (Directory.Exists(dir)) then Directory.CreateDirectory(dir) |> ignore
        File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(settings))
    with _ -> ()

[<Fact>]
let ``loadFromFile_validJson_returnsIpAndPort`` () =
    let path = Path.GetTempFileName()
    try
        File.WriteAllText(path, """{"LastServerIp":"192.168.1.5","LastServerPort":50006}""")
        let result = loadFromFile path
        Assert.Equal("192.168.1.5", result.LastServerIp)
        Assert.Equal(50006, result.LastServerPort)
    finally
        File.Delete(path)

[<Fact>]
let ``loadFromFile_missingFile_returnsEmpty`` () =
    let path = Path.Combine(Path.GetTempPath(), $"nonexistent_{System.Guid.NewGuid()}.json")
    let result = loadFromFile path
    Assert.Equal("", result.LastServerIp)
    Assert.Equal(50006, result.LastServerPort)

[<Fact>]
let ``loadFromFile_corruptFile_returnsEmpty`` () =
    let path = Path.GetTempFileName()
    try
        File.WriteAllText(path, "{invalid!!!")
        let result = loadFromFile path
        Assert.Equal("", result.LastServerIp)
        Assert.Equal(50006, result.LastServerPort)
    finally
        File.Delete(path)

[<Fact>]
let ``saveToFile_roundTrip_matchesOriginal`` () =
    let path = Path.Combine(Path.GetTempPath(), $"test_client_{System.Guid.NewGuid()}.json")
    try
        let original = { LastServerIp = "10.0.0.1"; LastServerPort = 9090 }
        saveToFile path original
        let loaded = loadFromFile path
        Assert.Equal(original.LastServerIp, loaded.LastServerIp)
        Assert.Equal(original.LastServerPort, loaded.LastServerPort)
    finally
        if File.Exists(path) then File.Delete(path)

[<Fact>]
let ``defaults_lastServerIpIsEmpty`` () =
    Assert.Equal("", defaults.LastServerIp)

[<Fact>]
let ``defaults_lastServerPortIs50006`` () =
    Assert.Equal(50006, defaults.LastServerPort)
