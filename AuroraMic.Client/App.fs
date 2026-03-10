namespace AuroraMic.Client

open System
open System.Net
open System.Net.Sockets
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts
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
    let private checkServerReady (udp: UdpClient) (serverEndpoint: IPEndPoint) =
        try
            let request = Text.Encoding.ASCII.GetBytes("RECV")
            udp.Send(request, request.Length, serverEndpoint) |> ignore
            
            udp.Client.ReceiveTimeout <- 2000
            let mutable remote = IPEndPoint(IPAddress.Any, 0)
            let response = udp.Receive(&remote)
            udp.Client.ReceiveTimeout <- 0
            
            Text.Encoding.ASCII.GetString(response) = "REDY"
        with
        | _ -> false

    
    
    let setCallbacks onStart onStop =
        onStartCallback <- Some onStart
        onStopCallback <- Some onStop
        
       
    let start (serverIp: string) (serverPort: int) =
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
            
        let sendAudio : AudioProcessCallback =
            AudioProcessCallback(fun samples _ ->
                let audioBytes = MemoryMarshal.Cast<float32, byte>(samples).ToArray()
                udp.Send(audioBytes, audioBytes.Length, serverEndpoint) |> ignore
            )


        let recorderInstance = new Recorder(capDevice, sendAudio)
        recorder <- Some recorderInstance
        capDevice.Start()
        onRecordingStarted |> Option.iter (fun cb -> cb())
        recorderInstance.StartRecording() |> ignore
        onStartCallback |> Option.iter (fun cb -> cb())
    
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
    
module MainView =
    let view () =
        Component(fun ctx ->
            let isRunning = ctx.useState false
            let serverIp = ctx.useState ""
            let serverPort = ctx.useState 50006
            let errorMsg = ctx.useState ""
            let rtt = ctx.useState TimeSpan.Zero

            
            
            DockPanel.create [
                DockPanel.children [
                    // Header
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

                    // Button
                    Border.create [
                        DockPanel.dock Dock.Bottom
                        Border.padding (24.0, 20.0)
                        Border.background "#1f2937"
                        Border.borderBrush "#374151"
                        Border.borderThickness (0.0, 1.0, 0.0, 0.0)
                        Border.child (
                            Button.create [
                                Button.content (if isRunning.Current then "Stop Microphone" else "Start Microphone")
                                Button.background (if isRunning.Current then "#dc2626" else "#8b5cf6")
                                Button.foreground "#ffffff"
                                Button.padding (24.0, 14.0)
                                Button.cornerRadius 10.0
                                Button.fontSize 16.0
                                Button.horizontalAlignment HorizontalAlignment.Stretch
                                Button.onClick (fun _ ->
                                    if isRunning.Current then
                                        AndroidMic.stop()
                                        isRunning.Set false
                                        errorMsg.Set ""
                                    else
                                        if String.IsNullOrEmpty(serverIp.Current) then
                                            errorMsg.Set "Please enter server IP"
                                        else
                                            try
                                                AndroidMic.start serverIp.Current serverPort.Current
                                                isRunning.Set true
                                                errorMsg.Set ""
                                            with ex ->
                                                errorMsg.Set ex.Message
                                )
                            ]
                        )
                    ]

                    // Content
                    ScrollViewer.create [
                        ScrollViewer.padding (24.0, 20.0)
                        ScrollViewer.content (
                            StackPanel.create [
                                StackPanel.spacing 20.0
                                StackPanel.children [
                                    // Status
                                    Border.create [
                                        Border.background "#1f2937"
                                        Border.cornerRadius 12.0
                                        Border.padding 20.0
                                        Border.child (
                                            StackPanel.create [
                                                StackPanel.spacing 12.0
                                                StackPanel.children [
                                                    StackPanel.create [
                                                        StackPanel.orientation Orientation.Vertical
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
                                                                    if isRunning.Current then $"🎤 Streaming to {serverIp.Current}:{serverPort.Current}"
                                                                    else "Microphone stopped"
                                                                )
                                                                TextBlock.fontSize 16.0
                                                                TextBlock.fontWeight FontWeight.SemiBold
                                                                TextBlock.foreground "#f3f4f6"
                                                            ]
                                                            if isRunning.Current then
                                                                let color =
                                                                    if rtt.Current.TotalMilliseconds > 100.0 then "#ef4444"
                                                                    elif rtt.Current.TotalMilliseconds > 50.0 then "#f59e0b"
                                                                    else "#10b981"
                                                                TextBlock.create [
                                                                    TextBlock.text $"RTT: {rtt.Current.TotalMilliseconds:F0}ms"
                                                                    TextBlock.foreground color
                                                                    TextBlock.fontSize 14.0
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
                                            ]
                                        )
                                    ]

                                    // Settings
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
                                ]
                            ]
                        )
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
        base.Styles.Add(FluentTheme())
        base.RequestedThemeVariant <- ThemeVariant.Default
    override _.OnFrameworkInitializationCompleted() =
        match base.ApplicationLifetime with
        | :? ISingleViewApplicationLifetime as singleViewApplicationLifetime ->
            singleViewApplicationLifetime.MainView <- MainWindow()
        | _ -> ()
