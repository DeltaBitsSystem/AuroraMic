namespace AuroraMic.Server

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Net.NetworkInformation
open System.Runtime.InteropServices
open System.Threading
open System.Text.Json
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Styling
open Avalonia.Themes.Fluent
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts
open SoundFlow.Backends.MiniAudio
open SoundFlow.Components
open SoundFlow.Providers
open SoundFlow.Enums
open SoundFlow.Visualization


module Config =
    type Settings = { Port: int; OutputDevice: string }
    
    let private settingsPath = "settings.json"
    let private defaults = { Port = 50006; OutputDevice = "" }
    
    let load() =
        try
            File.ReadAllText(settingsPath)
            |> JsonSerializer.Deserialize<Settings>
            |> Option.ofObj
            |> Option.defaultValue defaults
        with _ -> defaults
    
    let save (settings: Settings) =
        try File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings))
        with _ -> ()


module AudioEngine =
    
    type AudioState = {
        Engine: MiniAudioEngine
        Device: IDisposable
        Player: SoundPlayer
        Provider: QueueDataProvider
        Visualizer: WaveformVisualizer
    }

    let mutable private state: AudioState option = None

    let stop() =
        state |> Option.iter (fun s ->
            [s.Player :> IDisposable; s.Device; s.Provider; s.Visualizer; s.Engine]
            |> List.iter (fun d -> try d.Dispose() with _ -> ())
        )
        state <- None

    let start (deviceName: string) : Result<unit, string> =
        stop()
        try
            let format = SoundFlow.Structs.AudioFormat(
                Format = SampleFormat.F32, 
                SampleRate = 48000, 
                Channels = 1)
        
            let engine = new MiniAudioEngine()

            let deviceOpt =
                if String.IsNullOrEmpty(deviceName) then
                    engine.PlaybackDevices |> Seq.tryHead
                else
                    engine.PlaybackDevices |> Seq.tryFind (fun d -> d.Name = deviceName)
                    |> Option.orElse (engine.PlaybackDevices |> Seq.tryHead)
            
            match deviceOpt with
            | None -> Error "No playback devices found"
            | Some dev ->
                let device = engine.InitializePlaybackDevice(dev, format)
                let provider = new QueueDataProvider(format, 4800, QueueFullBehavior.Drop)
                let player = new SoundPlayer(engine, format, provider)
                let visualizer = new WaveformVisualizer()
                
                device.MasterMixer.AddComponent(player)
                device.Start()
                player.Play()
            
                state <- Some { Engine = engine; Device = device; Player = player; Provider = provider; Visualizer = visualizer }
                Ok()     
        with ex ->
            Error $"Audio initialization failed: {ex.Message}"

    let writeAudio (data: ReadOnlySpan<float32>) =
        match state with
        | Some s -> 
            s.Provider.AddSamples(data)
        | None -> ()


module NetworkServer =
    open System.Threading.Tasks
    
    let mutable private udpClient: UdpClient option = None
    let mutable private cts: CancellationTokenSource option = None
    let mutable private receiveTask: Task option = None
    let mutable private serverPort = 0
    let mutable private lastReceived = DateTime.Now
    
    let isRunning() = udpClient.IsSome
    let port() = serverPort
    let lastActivity() = lastReceived
    
    let private handleHandshake (udp: UdpClient) (remote: IPEndPoint) (data: byte[]) =
        match data.Length with
        | 4 when Text.Encoding.ASCII.GetString(data) = "RECV" ->
            udp.SendAsync(Text.Encoding.ASCII.GetBytes("REDY"), 4, remote) |> ignore
            true
        | _ -> false
    
    let stop() =
        cts |> Option.iter _.Cancel()
        receiveTask |> Option.iter (fun t -> try t.Wait(1000) |> ignore with _ -> ())
        udpClient |> Option.iter (fun u -> try u.Close(); u.Dispose() with _ -> ())
        udpClient <- None
        cts <- None
        receiveTask <- None
        serverPort <- 0

    let start (port: int) : Result<unit, string> =
        if isRunning() then stop()
        try
            if port < 1024 || port > 65535 then
                Error "Port must be between 1024-65535"
            else
                let source = new CancellationTokenSource()
                let udp = new UdpClient(port)
                udp.Client.ReceiveBufferSize <- 256 * 1024
                
                let receiveLoop () = task {
                    Thread.CurrentThread.Priority <- ThreadPriority.AboveNormal
                    while not source.Token.IsCancellationRequested do
                        try
                            let! result = udp.ReceiveAsync(source.Token)
                            lastReceived <- DateTime.Now
                            
                            if not (handleHandshake udp result.RemoteEndPoint result.Buffer) then
                                let floats = MemoryMarshal.Cast<byte, float32>(ReadOnlySpan result.Buffer)
                                AudioEngine.writeAudio floats
                        with 
                        | :? OperationCanceledException | :? SocketException -> ()
                        | ex -> printfn $"Receive error: {ex.Message}"
                }
                
                udpClient <- Some udp
                cts <- Some source
                receiveTask <- Some (receiveLoop())
                serverPort <- port
                lastReceived <- DateTime.Now
                Ok()
        with 
        | :? SocketException -> Error $"Port {port} is already in use"
        | ex -> Error $"Network error: {ex.Message}"


module NetworkInfo =
    let localIPv4() =
        NetworkInterface.GetAllNetworkInterfaces()
        |> Array.filter (fun n ->
            n.OperationalStatus = OperationalStatus.Up &&
            n.NetworkInterfaceType <> NetworkInterfaceType.Loopback)
        |> Array.collect (fun n -> n.GetIPProperties().UnicastAddresses |> Seq.toArray)
        |> Array.choose (fun a ->
            match a.Address.AddressFamily with
            | AddressFamily.InterNetwork -> Some (string a.Address)
            | _ -> None)
        |> Array.distinct


module AudioOutputs =
    let list() =
        try
            use engine = new MiniAudioEngine()
            engine.PlaybackDevices |> Seq.map _.Name |> Seq.toArray
        with _ -> [||]


module MainView =
    let private section title content =
        Border.create [
            Border.background "#1f2937"
            Border.cornerRadius 12.0
            Border.padding 20.0
            Border.child (
                StackPanel.create [
                    StackPanel.spacing 12.0
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.text title
                            TextBlock.fontSize 15.0
                            TextBlock.fontWeight FontWeight.SemiBold
                            TextBlock.foreground "#e5e7eb"
                        ]
                        yield! content
                    ]
                ]
            )
        ]
    
    let private statItem label value (color: string) =
        StackPanel.create [
            StackPanel.spacing 4.0
            StackPanel.children [
                TextBlock.create [
                    TextBlock.text label
                    TextBlock.fontSize 12.0
                    TextBlock.foreground "#9ca3af"
                ]
                TextBlock.create [
                    TextBlock.text value
                    TextBlock.fontSize 18.0
                    TextBlock.fontWeight FontWeight.Bold
                    TextBlock.foreground color
                ]
            ]
        ]
    
    let private settingField label control =
        StackPanel.create [
            StackPanel.spacing 8.0
            StackPanel.children [
                TextBlock.create [
                    TextBlock.text label
                    TextBlock.fontSize 13.0
                    TextBlock.foreground "#9ca3af"
                ]
                control
            ]
        ]
    
    let view() =
        Component(fun ctx ->
            let isRunning = ctx.useState false
            let settings = Config.load()
            let port = ctx.useState settings.Port
            let selectedOutput = ctx.useState settings.OutputDevice
            let errorMsg = ctx.useState ""

            let ips = NetworkInfo.localIPv4()
            let outputs = AudioOutputs.list()
            
            let savedOutputIsValid = outputs |> Array.contains selectedOutput.Current
            if (String.IsNullOrEmpty(selectedOutput.Current) || not savedOutputIsValid) && outputs.Length > 0 then
                selectedOutput.Set outputs[0]
            
            let startServer() =
                Config.save { Port = port.Current; OutputDevice = selectedOutput.Current }
                
                match AudioEngine.start selectedOutput.Current with
                | Error msg -> errorMsg.Set msg
                | Ok() ->
                    match NetworkServer.start port.Current with
                    | Error msg -> 
                        AudioEngine.stop()
                        errorMsg.Set msg
                    | Ok() ->
                        isRunning.Set true
                        errorMsg.Set ""
            
            let stopServer() =
                NetworkServer.stop()
                AudioEngine.stop()
                isRunning.Set false
                errorMsg.Set ""
            
            DockPanel.create [
                DockPanel.children [
                    Border.create [
                        DockPanel.dock Dock.Top
                        Border.background "#8b5cf6"
                        Border.padding (12.0, 12.0)
                        Border.child (
                            TextBlock.create [
                                TextBlock.text "AuroraMic"
                                TextBlock.fontSize 32.0
                                TextBlock.fontWeight FontWeight.Bold
                                TextBlock.foreground "#ffffff"
                                TextBlock.horizontalAlignment HorizontalAlignment.Center
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
                                Button.content (if isRunning.Current then "Stop Server" else "Start Server")
                                Button.background (if isRunning.Current then "#dc2626" else "#8b5cf6")
                                Button.foreground "#ffffff"
                                Button.padding (24.0, 14.0)
                                Button.cornerRadius 10.0
                                Button.fontSize 16.0
                                Button.horizontalAlignment HorizontalAlignment.Stretch
                                Button.onClick (fun _ -> if isRunning.Current then stopServer() else startServer())
                            ]
                        )
                    ]

                    ScrollViewer.create [
                        ScrollViewer.padding (24.0, 20.0)
                        ScrollViewer.content (
                            StackPanel.create [
                                StackPanel.spacing 20.0
                                StackPanel.children [
                                    section "Status" [
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
                                                        if isRunning.Current then $"Listening on port {NetworkServer.port()}"
                                                        else "Server stopped"
                                                    )
                                                    TextBlock.fontSize 16.0
                                                    TextBlock.fontWeight FontWeight.SemiBold
                                                    TextBlock.foreground "#f3f4f6"
                                                ]
                                            ]
                                        ]
                                        
                                        if not (String.IsNullOrEmpty(errorMsg.Current)) then
                                            Border.create [
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
                                            ]
                                    ]

                                    section "Connection Addresses" [
                                        if ips.Length = 0 then
                                            TextBlock.create [
                                                TextBlock.text "No network interfaces found"
                                                TextBlock.foreground "#6b7280"
                                                TextBlock.fontSize 13.0
                                            ] :> Types.IView
                                        else
                                            yield! ips |> Seq.map (fun ip ->
                                                Border.create [
                                                    Border.background "#374151"
                                                    Border.cornerRadius 6.0
                                                    Border.padding (12.0, 8.0)
                                                    Border.child (
                                                        TextBlock.create [
                                                            TextBlock.text $"{ip}:{port.Current}"
                                                            TextBlock.fontSize 14.0
                                                            TextBlock.foreground "#8b5cf6"
                                                            TextBlock.fontFamily "Consolas, Monaco, monospace"
                                                        ]
                                                    )
                                                ] :> Types.IView
                                            )
                                    ]

                                    section "Settings" [
                                        settingField "Audio Output Device" (
                                            ComboBox.create [
                                                ComboBox.dataItems outputs
                                                ComboBox.selectedItem selectedOutput.Current
                                                ComboBox.onSelectedItemChanged (fun v -> selectedOutput.Set (string v))
                                                ComboBox.fontSize 14.0
                                                ComboBox.padding (12.0, 8.0)
                                                ComboBox.isEnabled (not isRunning.Current)
                                            ]
                                        )
                                        
                                        settingField "Server Port" (
                                            NumericUpDown.create [
                                                NumericUpDown.minimum 1024m
                                                NumericUpDown.maximum 65535m
                                                NumericUpDown.value port.Current
                                                NumericUpDown.onValueChanged (fun (v: Nullable<decimal>) ->
                                                    if v.HasValue then port.Set (int v.Value)
                                                )
                                                NumericUpDown.fontSize 14.0
                                                NumericUpDown.padding (12.0, 8.0)
                                                NumericUpDown.isEnabled (not isRunning.Current)
                                            ]
                                        )
                                    ]
                                ]
                            ]
                        )
                    ]
                ]
            ]
        )

type MainWindow() =
    inherit HostWindow()
    do
        base.Title <- "AuroraMic Server"
        base.Width <- 480.0
        base.Height <- 800.0
        base.Background <- SolidColorBrush(Color.Parse("#111827"))
        base.Content <- MainView.view()

type App() =
    inherit Application()
    override _.Initialize() =
        base.Styles.Add(FluentTheme())
        base.RequestedThemeVariant <- ThemeVariant.Dark
        
    override _.OnFrameworkInitializationCompleted() =
        match base.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            desktop.MainWindow <- MainWindow()
        | _ -> ()