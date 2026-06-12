module AuroraMic.Tests.IntegrationTests

open Xunit
open AuroraMic.Server.NetworkServer

[<Fact>]
let ``serverStarts_listensOnPort`` () =
    let result = start 15000
    Assert.True(result.IsOk)
    Assert.Equal<int>(15000, port())
    stop()

[<Fact>]
let ``serverStops_releasesPort`` () =
    start 15001 |> ignore
    stop()
    Assert.Equal<int>(0, port())

[<Fact>]
let ``serverStopStart_cycle_works`` () =
    start 15002 |> ignore
    stop()
    let result = start 15002
    Assert.True(result.IsOk)
    stop()
