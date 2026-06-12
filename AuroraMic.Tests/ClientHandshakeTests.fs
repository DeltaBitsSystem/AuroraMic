module AuroraMic.Tests.ClientHandshakeTests

open Xunit
open AuroraMic.Client.AndroidMic

[<Fact>]
let ``buildRecvPacket_returnsFourBytes`` () =
    let packet = buildRecvPacket()
    Assert.Equal(4, packet.Length)

[<Fact>]
let ``buildRecvPacket_isAsciiRECV`` () =
    let packet = buildRecvPacket()
    let text = System.Text.Encoding.ASCII.GetString(packet)
    Assert.Equal("RECV", text)

[<Fact>]
let ``validateRedyResponse_valid_returnsTrue`` () =
    let response = System.Text.Encoding.ASCII.GetBytes("REDY")
    Assert.True(validateRedyResponse response)

[<Fact>]
let ``validateRedyResponse_wrongBytes_returnsFalse`` () =
    let response = System.Text.Encoding.ASCII.GetBytes("ABCD")
    Assert.False(validateRedyResponse response)

[<Fact>]
let ``validateRedyResponse_tooShort_returnsFalse`` () =
    let response = System.Text.Encoding.ASCII.GetBytes("RED")
    Assert.False(validateRedyResponse response)

[<Fact>]
let ``validateRedyResponse_empty_returnsFalse`` () =
    let response = [||]
    Assert.False(validateRedyResponse response)
