namespace AuroraMic.Client

open System
open System.Net
open System.Net.Sockets
open System.Runtime.InteropServices
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.Types
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Styling
open Avalonia.Themes.Fluent
open SoundFlow.Abstracts
open SoundFlow.Backends.MiniAudio
open SoundFlow.Components
open SoundFlow.Structs
open SoundFlow.Enums

module AndroidMic =
    let mutable private recorder : Recorder option = None
    let mutable private device : IDisposable option = None
    let mutable private engine : MiniAudioEngine option = None
    let mutable private udpClient : UdpClient option = None
    let mutable onRecordingStarted: (unit -> unit) option = None
    let mutable onStartCallback: (unit -> unit) option = None
    let mutable onStopCallback: (unit -> unit) option = None

    let isRunning () = recorder.IsSome

    let buildRecvPacket () = Text.Encoding.ASCII.GetBytes("RECV")

    let validateRedyResponse (response: byte[]) =
        response.Length = 4 && Text.Encoding.ASCII.GetString(response) = "REDY"

    let private checkServerReady (udp: UdpClient) (serverEndpoint: IPEndPoint) =
        try
            let request = buildRecvPacket()
            udp.Send(request, request.Length, serverEndpoint) |> ignore

            udp.Client.ReceiveTimeout <- 2000
            let mutable remote = IPEndPoint(IPAddress.Any, 0)
            let response = udp.Receive(&remote)
            udp.Client.ReceiveTimeout <- 0

            validateRedyResponse response
        with ex -> printfn "Server check error: %s" ex.Message; false

    let setCallbacks onStart onStop =
        onStartCallback <- Some onStart
        onStopCallback <- Some onStop

    let stop () =
        recorder |> Option.iter (fun r -> r.StopRecordingAsync().Wait(1000) |> ignore)
        device |> Option.iter _.Dispose()
        engine |> Option.iter (fun e -> e.Dispose())
        udpClient |> Option.iter (fun u -> u.Close())
        recorder <- None
        device <- None
        engine <- None
        udpClient <- None
        onStopCallback |> Option.iter (fun cb -> cb())

    let start (serverIp: string) (serverPort: int) =
        if isRunning() then stop()

        match IPAddress.TryParse(serverIp) with
        | false, _ -> raise (InvalidOperationException("Invalid IP address format"))
        | true, _ ->

        let eng = new MiniAudioEngine()
        let captureDevice =
            eng.CaptureDevices
            |> Seq.tryHead
            |> Option.defaultWith (fun () ->
                raise (InvalidOperationException("No capture devices found")))

        let format = AudioFormat(
            Format = SampleFormat.F32,
            SampleRate = 48000,
            Channels = 1
        )

        let capDevice = eng.InitializeCaptureDevice(captureDevice, format)
        device <- Some capDevice
        engine <- Some eng

        let udp = new UdpClient()
        udpClient <- Some udp
        let serverEndpoint = IPEndPoint(IPAddress.Parse(serverIp), serverPort)
        if not (checkServerReady udp serverEndpoint) then
            udp.Close()
            eng.Dispose()
            raise (InvalidOperationException("Server not ready to receive audio"))

        let sendAudio =
            AudioProcessCallback(fun samples _ ->
                try
                    let audioBytes = MemoryMarshal.Cast<float32, byte>(samples).ToArray()
                    udp.Send(audioBytes, audioBytes.Length, serverEndpoint) |> ignore
                with :? SocketException as ex -> printfn "UDP send error: %s" ex.Message
            )

        let recorderInstance = new Recorder(capDevice, sendAudio)
        recorder <- Some recorderInstance
        capDevice.Start()
        onRecordingStarted |> Option.iter (fun cb -> cb())
        recorderInstance.StartRecording() |> ignore
        onStartCallback |> Option.iter (fun cb -> cb())

module DiscoveryListener =
    open System.Threading
    open System.Threading.Tasks
    open System.Text

    let broadcastPort = 50007
    let marker = "AURORAMIC"

    type DiscoveredServer = { Ip: string; Port: int }

    let parseDiscoveryMessage (text: string) =
        if text.StartsWith(marker) then
            let parts = text.Split(':')
            if parts.Length = 2 then
                match Int32.TryParse(parts.[1]) with
                | true, port -> Some port
                | _ -> None
            else None
        else None

    let mutable private cts: CancellationTokenSource option = None
    let mutable private listenerTask: Task option = None
    let mutable private servers: DiscoveredServer list = []
    let private lockObj = obj()

    let getServers() = lock lockObj (fun () -> servers)

    let stop() =
        cts |> Option.iter _.Cancel()
        cts <- None
        listenerTask <- None
        lock lockObj (fun () -> servers <- [])

    let start (onFound: DiscoveredServer -> unit) =
        stop()
        let source = new CancellationTokenSource()
        let task = task {
            use udp = new UdpClient(broadcastPort)
            while not source.Token.IsCancellationRequested do
                try
                    let! result = udp.ReceiveAsync(source.Token)
                    let text = Encoding.ASCII.GetString(result.Buffer)
                    match parseDiscoveryMessage text with
                    | Some audioPort ->
                        let serverIp = result.RemoteEndPoint.Address.ToString()
                        let server = { Ip = serverIp; Port = audioPort }
                        let exists = lock lockObj (fun () -> servers |> List.exists (fun s -> s.Ip = serverIp))
                        if not exists then
                            lock lockObj (fun () -> servers <- server :: servers)
                            onFound server
                    | None -> ()
                with
                | :? OperationCanceledException -> ()
                | :? SocketException -> ()
                | ex -> printfn "Discovery listener error: %s" ex.Message
        }
        cts <- Some source
        listenerTask <- Some task

module MainView =
    let private clientSettingsPath =
        System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "AuroraMic", "client-settings.json")

    let private loadClientSettings () =
        try
            if System.IO.File.Exists(clientSettingsPath) then
                let json = System.IO.File.ReadAllText(clientSettingsPath)
                let doc = System.Text.Json.JsonDocument.Parse(json)
                let root = doc.RootElement
                let ip =
                    try root.GetProperty("LastServerIp").GetString()
                    with _ -> ""
                let port =
                    try root.GetProperty("LastServerPort").GetInt32()
                    with _ -> 50006
                if isNull ip then "", port else ip, port
            else
                "", 50006
        with _ -> "", 50006

    let private saveClientSettings (ip: string) (port: int) =
        try
            let dir = System.IO.Path.GetDirectoryName(clientSettingsPath)
            if not (System.IO.Directory.Exists(dir)) then System.IO.Directory.CreateDirectory(dir) |> ignore
            let json = sprintf """{"LastServerIp":"%s","LastServerPort":%d}""" ip port
            System.IO.File.WriteAllText(clientSettingsPath, json)
        with _ -> ()

    let view () =
        Component(fun ctx ->
            let savedIp, savedPort = loadClientSettings()
            let isRunning = ctx.useState false
            let isConnecting = ctx.useState false
            let serverIp = ctx.useState savedIp
            let serverPort = ctx.useState savedPort
            let errorMsg = ctx.useState ""
            let discoveredServers = ctx.useState ([] : DiscoveryListener.DiscoveredServer list)
            let discoveryActive = ctx.useState false

            if not discoveryActive.Current then
                discoveryActive.Set true
                DiscoveryListener.start (fun server ->
                    let current = discoveredServers.Current
                    if not (current |> List.exists (fun s -> s.Ip = server.Ip)) then
                        discoveredServers.Set (server :: current)
                )

            let statusCard =
                Border.create [
                    Border.background "#1f2937"
                    Border.cornerRadius 12.0
                    Border.padding 20.0
                    Border.child (
                        StackPanel.create [
                            StackPanel.spacing 12.0
                            StackPanel.children [
                                StackPanel.create [
                                    StackPanel.orientation Orientation.Horizontal
                                    StackPanel.spacing 10.0
                                    StackPanel.children [
                                        Border.create [
                                            Border.width 12.0
                                            Border.height 12.0
                                            Border.cornerRadius 6.0
                                            Border.background (if isRunning.Current then "#10b981" else "#6b7280")
                                            Border.verticalAlignment VerticalAlignment.Center
                                        ]
                                        TextBlock.create [
                                            TextBlock.text (
                                                if isRunning.Current then $"Streaming to {serverIp.Current}:{serverPort.Current}"
                                                else "Microphone stopped"
                                            )
                                            TextBlock.fontSize 16.0
                                            TextBlock.fontWeight FontWeight.SemiBold
                                            TextBlock.foreground "#f3f4f6"
                                            TextBlock.textWrapping TextWrapping.Wrap
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    )
                ]

            let errorCard =
                if String.IsNullOrEmpty(errorMsg.Current) then None
                else
                    Some (Border.create [
                        Border.background "#7f1d1d"
                        Border.borderBrush "#dc2626"
                        Border.borderThickness 1.0
                        Border.cornerRadius 8.0
                        Border.padding 12.0
                        Border.child (
                            TextBlock.create [
                                TextBlock.text errorMsg.Current
                                TextBlock.foreground "#fca5a5"
                                TextBlock.textWrapping TextWrapping.Wrap
                            ]
                        )
                    ])

            let discoveredCard =
                if isRunning.Current || discoveredServers.Current.Length = 0 then None
                else
                    Some (Border.create [
                        Border.background "#1f2937"
                        Border.cornerRadius 12.0
                        Border.padding 20.0
                        Border.child (
                            StackPanel.create [
                                StackPanel.spacing 12.0
                                StackPanel.children (
                                    [ TextBlock.create [
                                        TextBlock.text "Discovered Servers"
                                        TextBlock.fontSize 15.0
                                        TextBlock.fontWeight FontWeight.SemiBold
                                        TextBlock.foreground "#e5e7eb"
                                      ] ]
                                    @ (discoveredServers.Current |> List.map (fun server ->
                                        Button.create [
                                            Button.content $"{server.Ip}:{server.Port}"
                                            Button.background "#334155"
                                            Button.foreground "#f3f4f6"
                                            Button.padding (16.0, 12.0)
                                            Button.cornerRadius 8.0
                                            Button.fontSize 14.0
                                            Button.horizontalAlignment HorizontalAlignment.Stretch
                                            Button.onClick (fun _ ->
                                                serverIp.Set server.Ip
                                                serverPort.Set server.Port
                                            )
                                        ] :> IView
                                    ))
                                )
                            ]
                        )
                    ])

            let settingsCard =
                Border.create [
                    Border.background "#1f2937"
                    Border.cornerRadius 12.0
                    Border.padding 20.0
                    Border.child (
                        StackPanel.create [
                            StackPanel.spacing 16.0
                            StackPanel.children [
                                TextBlock.create [
                                    TextBlock.text "Connection Settings"
                                    TextBlock.fontSize 15.0
                                    TextBlock.fontWeight FontWeight.SemiBold
                                    TextBlock.foreground "#e5e7eb"
                                ]

                                StackPanel.create [
                                    StackPanel.spacing 8.0
                                    StackPanel.children [
                                        TextBlock.create [
                                            TextBlock.text "Server IP Address"
                                            TextBlock.fontSize 13.0
                                            TextBlock.foreground "#9ca3af"
                                        ]
                                        TextBox.create [
                                            TextBox.text serverIp.Current
                                            TextBox.onTextChanged (fun t -> serverIp.Set t)
                                            TextBox.watermark "192.168.1.100"
                                            TextBox.fontSize 14.0
                                            TextBox.padding (12.0, 8.0)
                                        ]
                                    ]
                                ]

                                StackPanel.create [
                                    StackPanel.spacing 8.0
                                    StackPanel.children [
                                        TextBlock.create [
                                            TextBlock.text "Server Port"
                                            TextBlock.fontSize 13.0
                                            TextBlock.foreground "#9ca3af"
                                        ]
                                        NumericUpDown.create [
                                            NumericUpDown.minimum 1024m
                                            NumericUpDown.maximum 65535m
                                            NumericUpDown.value (decimal serverPort.Current)
                                            NumericUpDown.onValueChanged (fun v ->
                                                if v.HasValue then
                                                    serverPort.Set (int v.Value)
                                            )
                                            NumericUpDown.fontSize 14.0
                                            NumericUpDown.padding (12.0, 8.0)
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    )
                ]

            let scrollContent =
                let cards : IView list =
                    [ statusCard :> IView ]
                    @ (errorCard |> Option.toList |> List.map (fun c -> c :> IView))
                    @ (discoveredCard |> Option.toList |> List.map (fun c -> c :> IView))
                    @ [ settingsCard :> IView ]
                StackPanel.create [
                    StackPanel.spacing 20.0
                    StackPanel.children cards
                ]

            DockPanel.create [
                DockPanel.children [
                    Border.create [
                        DockPanel.dock Dock.Top
                        Border.background "#8b5cf6"
                        Border.padding (12.0, 12.0)
                        Border.child (
                            StackPanel.create [
                                StackPanel.spacing 8.0
                                StackPanel.children [
                                    TextBlock.create [
                                        TextBlock.text "AuroraMic"
                                        TextBlock.fontSize 32.0
                                        TextBlock.fontWeight FontWeight.Bold
                                        TextBlock.foreground "#ffffff"
                                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                                    ]
                                ]
                            ]
                        )
                    ]

                    Border.create [
                        DockPanel.dock Dock.Bottom
                        Border.padding (24.0, 20.0)
                        Border.background "#1f2937"
                        Border.borderBrush "#374151"
                        Border.borderThickness (0.0, 1.0, 0.0, 0.0)
                        Border.child (
                            Button.create [
                                Button.content (
                                    if isConnecting.Current then "Connecting..."
                                    elif isRunning.Current then "Stop Microphone"
                                    else "Start Microphone")
                                Button.background (
                                    if isConnecting.Current then "#6b7280"
                                    elif isRunning.Current then "#dc2626"
                                    else "#8b5cf6")
                                Button.foreground "#ffffff"
                                Button.padding (24.0, 14.0)
                                Button.cornerRadius 10.0
                                Button.fontSize 16.0
                                Button.isEnabled (not isConnecting.Current)
                                Button.horizontalAlignment HorizontalAlignment.Stretch
                                Button.onClick (fun _ ->
                                    if isRunning.Current then
                                        AndroidMic.stop()
                                        isRunning.Set false
                                        isConnecting.Set false
                                        errorMsg.Set ""
                                    else
                                        if String.IsNullOrEmpty(serverIp.Current) then
                                            errorMsg.Set "Please enter the server IP address"
                                        else
                                            try
                                                isConnecting.Set true
                                                errorMsg.Set ""
                                                AndroidMic.start serverIp.Current serverPort.Current
                                                isRunning.Set true
                                                isConnecting.Set false
                                                saveClientSettings serverIp.Current serverPort.Current
                                            with ex ->
                                                isConnecting.Set false
                                                errorMsg.Set ex.Message
                                )
                            ]
                        )
                    ]

                    ScrollViewer.create [
                        ScrollViewer.padding (24.0, 20.0)
                        ScrollViewer.content scrollContent
                    ]
                ]
            ]
        )


type MainWindow() =
    inherit HostControl()
    do
        base.Background <- SolidColorBrush(Color.Parse("#111827"))
        base.Content <- MainView.view ()

type App() =
    inherit Application()
    override _.Initialize() =
        AppDomain.CurrentDomain.ProcessExit.Add(fun _ ->
            DiscoveryListener.stop()
            if AndroidMic.isRunning() then AndroidMic.stop()
        )
        base.Styles.Add(FluentTheme())
        base.RequestedThemeVariant <- ThemeVariant.Default
    override _.OnFrameworkInitializationCompleted() =
        match base.ApplicationLifetime with
        | :? ISingleViewApplicationLifetime as singleViewApplicationLifetime ->
            singleViewApplicationLifetime.MainView <- MainWindow()
        | _ -> ()
