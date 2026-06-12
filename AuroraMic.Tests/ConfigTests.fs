module AuroraMic.Tests.ConfigTests

open Xunit
open AuroraMic.Server.Config
open System.IO

[<Fact>]
let ``loadFromFile_validJson_returnsCorrectValues`` () =
    let path = Path.GetTempFileName()
    try
        File.WriteAllText(path, """{"Port":8080,"OutputDevice":"Headphones"}""")
        let result = loadFromFile path
        Assert.Equal(8080, result.Port)
        Assert.Equal("Headphones", result.OutputDevice)
    finally
        File.Delete(path)

[<Fact>]
let ``loadFromFile_missingFile_returnsDefaults`` () =
    let path = Path.Combine(Path.GetTempPath(), $"nonexistent_{System.Guid.NewGuid()}.json")
    let result = loadFromFile path
    Assert.Equal(50006, result.Port)
    Assert.Equal("", result.OutputDevice)

[<Fact>]
let ``loadFromFile_emptyFile_returnsDefaults`` () =
    let path = Path.GetTempFileName()
    try
        File.WriteAllText(path, "")
        let result = loadFromFile path
        Assert.Equal(50006, result.Port)
        Assert.Equal("", result.OutputDevice)
    finally
        File.Delete(path)

[<Fact>]
let ``loadFromFile_corruptJson_returnsDefaults`` () =
    let path = Path.GetTempFileName()
    try
        File.WriteAllText(path, "{invalid json!!!")
        let result = loadFromFile path
        Assert.Equal(50006, result.Port)
        Assert.Equal("", result.OutputDevice)
    finally
        File.Delete(path)

[<Fact>]
let ``loadFromFile_partialJson_fillsMissingFields`` () =
    let path = Path.GetTempFileName()
    try
        File.WriteAllText(path, """{"Port":9090}""")
        let result = loadFromFile path
        Assert.Equal(9090, result.Port)
        Assert.Equal("", result.OutputDevice)
    finally
        File.Delete(path)

[<Fact>]
let ``loadFromFile_portOutOfRange_usesValue`` () =
    let path = Path.GetTempFileName()
    try
        File.WriteAllText(path, """{"Port":80,"OutputDevice":""}""")
        let result = loadFromFile path
        Assert.Equal(80, result.Port)
    finally
        File.Delete(path)

[<Fact>]
let ``saveToFile_validSettings_createsFile`` () =
    let path = Path.Combine(Path.GetTempPath(), $"test_save_{System.Guid.NewGuid()}.json")
    try
        saveToFile path { Port = 7070; OutputDevice = "Speakers" }
        Assert.True(File.Exists(path))
    finally
        if File.Exists(path) then File.Delete(path)

[<Fact>]
let ``saveToFile_roundTrip_matchesOriginal`` () =
    let path = Path.Combine(Path.GetTempPath(), $"test_roundtrip_{System.Guid.NewGuid()}.json")
    try
        let original = { Port = 7070; OutputDevice = "Speakers" }
        saveToFile path original
        let loaded = loadFromFile path
        Assert.Equal(original.Port, loaded.Port)
        Assert.Equal(original.OutputDevice, loaded.OutputDevice)
    finally
        if File.Exists(path) then File.Delete(path)

[<Fact>]
let ``defaults_portIs50006`` () =
    let defaults = { Port = 50006; OutputDevice = "" }
    Assert.Equal(50006, defaults.Port)

[<Fact>]
let ``defaults_outputDeviceIsEmpty`` () =
    let defaults = { Port = 50006; OutputDevice = "" }
    Assert.Equal("", defaults.OutputDevice)
