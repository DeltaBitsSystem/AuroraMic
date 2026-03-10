namespace AuroraMic.Server.Desktop
open System
open Avalonia

open AuroraMic.Server

module Program =

   [<EntryPoint>]
    let main(args: string[]) =
        AppBuilder
            .Configure<App>()
            
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)