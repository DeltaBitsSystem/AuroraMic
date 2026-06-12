module AuroraMic.Tests.HandshakeTests

open Xunit
open AuroraMic.Server.NetworkServer

[<Fact>]
let ``validateHandshake_validRecv_returnsTrue`` () =
    let data = System.Text.Encoding.ASCII.GetBytes("RECV")
    Assert.True(validateHandshake data)

[<Fact>]
let ``validateHandshake_wrongBytes_returnsFalse`` () =
    let data = System.Text.Encoding.ASCII.GetBytes("ABCD")
    Assert.False(validateHandshake data)

[<Fact>]
let ``validateHandshake_tooShort_returnsFalse`` () =
    let data = System.Text.Encoding.ASCII.GetBytes("RE")
    Assert.False(validateHandshake data)

[<Fact>]
let ``validateHandshake_tooLong_returnsFalse`` () =
    let data = System.Text.Encoding.ASCII.GetBytes("RECVV")
    Assert.False(validateHandshake data)

[<Fact>]
let ``validateHandshake_emptyArray_returnsFalse`` () =
    let data = [||]
    Assert.False(validateHandshake data)

[<Fact>]
let ``validateHandshake_lowercaseRecv_returnsFalse`` () =
    let data = System.Text.Encoding.ASCII.GetBytes("recv")
    Assert.False(validateHandshake data)
