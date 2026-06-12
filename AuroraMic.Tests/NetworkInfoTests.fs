module AuroraMic.Tests.NetworkInfoTests

open Xunit
open AuroraMic.Server.NetworkInfo

[<Fact>]
let ``localIPv4_returnsNonEmpty`` () =
    let ips = localIPv4()
    Assert.True(ips.Length > 0)

[<Fact>]
let ``localIPv4_allAreValidIpFormat`` () =
    let ips = localIPv4()
    for ip in ips do
        let mutable parsed = System.Net.IPAddress.None
        Assert.True(System.Net.IPAddress.TryParse(ip, &parsed))

[<Fact>]
let ``localIPv4_noLoopback`` () =
    let ips = localIPv4()
    for ip in ips do
        Assert.True(ip <> "127.0.0.1")

[<Fact>]
let ``localIPv4_noDuplicates`` () =
    let ips = localIPv4()
    let distinct = ips |> Array.distinct
    Assert.Equal(ips.Length, distinct.Length)
