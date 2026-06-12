namespace AuroraMic.Server.Desktop
open System
open Avalonia

open AuroraMic.Server

module Program =

   [<EntryPoint>]
    let main(args: string[]) =
        AppDomain.CurrentDomain.ProcessExit.Add(fun _ ->
            Discovery.stop()
            NetworkServer.stop()
            AudioEngine.stop()
        )
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
