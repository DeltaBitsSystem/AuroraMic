module AuroraMic.Tests.DiscoveryTests

open Xunit
open AuroraMic.Server.Discovery
open AuroraMic.Client.DiscoveryListener

[<Fact>]
let ``discoveryMarker_isCorrectFormat`` () =
    Assert.Equal("AURORAMIC", marker)

[<Fact>]
let ``discoveryBroadcastPort_is50007`` () =
    Assert.Equal(50007, broadcastPort)

[<Fact>]
let ``discoveryMessage_buildsCorrectly`` () =
    let msg = formatDiscoveryMessage 50006
    Assert.Equal("AURORAMIC:50006", msg)

[<Fact>]
let ``discoveryMessage_buildsCustomPort`` () =
    let msg = formatDiscoveryMessage 12345
    Assert.Equal("AURORAMIC:12345", msg)

[<Fact>]
let ``serverParse_validMessage_returnsSomePort`` () =
    let result = AuroraMic.Server.Discovery.parseDiscoveryMessage "AURORAMIC:50006"
    Assert.Equal(Some 50006, result)

[<Fact>]
let ``serverParse_customPort_returnsSomePort`` () =
    let result = AuroraMic.Server.Discovery.parseDiscoveryMessage "AURORAMIC:12345"
    Assert.Equal(Some 12345, result)

[<Fact>]
let ``serverParse_wrongMarker_returnsNone`` () =
    let result = AuroraMic.Server.Discovery.parseDiscoveryMessage "WRONG:50006"
    Assert.Equal(None, result)

[<Fact>]
let ``serverParse_noColon_returnsNone`` () =
    let result = AuroraMic.Server.Discovery.parseDiscoveryMessage "AURORAMIC50006"
    Assert.Equal(None, result)

[<Fact>]
let ``serverParse_badPort_returnsNone`` () =
    let result = AuroraMic.Server.Discovery.parseDiscoveryMessage "AURORAMIC:notanumber"
    Assert.Equal(None, result)

[<Fact>]
let ``serverParse_emptyString_returnsNone`` () =
    let result = AuroraMic.Server.Discovery.parseDiscoveryMessage ""
    Assert.Equal(None, result)

[<Fact>]
let ``clientParse_validMessage_returnsSomePort`` () =
    let result = parseDiscoveryMessage "AURORAMIC:50006"
    Assert.Equal(Some 50006, result)

[<Fact>]
let ``clientParse_wrongMarker_returnsNone`` () =
    let result = parseDiscoveryMessage "WRONG:50006"
    Assert.Equal(None, result)

[<Fact>]
let ``clientParse_noColon_returnsNone`` () =
    let result = parseDiscoveryMessage "AURORAMIC50006"
    Assert.Equal(None, result)

[<Fact>]
let ``clientParse_badPort_returnsNone`` () =
    let result = parseDiscoveryMessage "AURORAMIC:notanumber"
    Assert.Equal(None, result)

[<Fact>]
let ``clientParse_emptyString_returnsNone`` () =
    let result = parseDiscoveryMessage ""
    Assert.Equal(None, result)

[<Fact>]
let ``discoveryListener_startStop_noException`` () =
    stop()
    start (fun _ -> ())
    System.Threading.Thread.Sleep(100)
    stop()

[<Fact>]
let ``discoveryListener_getServers_afterStop_isEmpty`` () =
    stop()
    let servers = getServers()
    Assert.Empty(servers)
