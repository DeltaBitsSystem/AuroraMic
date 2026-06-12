module AuroraMic.Tests.AudioFormatTests

open Xunit
open SoundFlow.Enums
open SoundFlow.Structs

[<Fact>]
let ``format_is48kHzMonoFloat32`` () =
    let format = AudioFormat(Format = SampleFormat.F32, SampleRate = 48000, Channels = 1)
    Assert.Equal(48000, format.SampleRate)
    Assert.Equal(1, format.Channels)
    Assert.Equal(SampleFormat.F32, format.Format)

[<Fact>]
let ``bytesPerSample_is4`` () =
    Assert.Equal(4, sizeof<float32>)
