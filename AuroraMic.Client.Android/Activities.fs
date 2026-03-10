namespace AuroraMic.Client.Android

open System
open System.IO
open Android.App
open Android.Content
open Android.Content.PM
open Android.Media.Audiofx
open Android.OS
open Avalonia
open Avalonia.Android
open AuroraMic.Client

[<Service(Name = "com.CompanyName.AvaloniaTest.AudioStreamingService", ForegroundServiceType = ForegroundService.TypeMicrophone)>]
type AudioStreamingService() =
    inherit Service()
    
    let channelId = "audio_streaming_channel"
    
    override this.OnBind _ = null
    
    override this.OnCreate() =
        base.OnCreate()
        this.CreateNotificationChannel()
    
    override this.OnStartCommand(_, _, _) =
        let notification = this.BuildNotification()
        this.StartForeground(1, notification, ForegroundService.TypeMicrophone)
        StartCommandResult.Sticky
    
    member private this.CreateNotificationChannel() =
        if Build.VERSION.SdkInt >= BuildVersionCodes.O then
            let channel = new NotificationChannel(
                channelId,
                "Audio Streaming",
                NotificationImportance.Low
            )
            channel.Description <- "Keeps audio streaming active"
            let manager = this.GetSystemService(Context.NotificationService) :?> NotificationManager
            manager.CreateNotificationChannel(channel)
    
    member private this.BuildNotification() =
        let builder = new Notification.Builder(this, channelId)
        builder
            .SetContentTitle("AuroraMic")
            .SetContentText("Streaming audio...")
            .SetSmallIcon(Android.Resource.Drawable.IcMediaPlay)
            .SetOngoing(true)
            .Build()

module ForegroundServiceHelper =
    let mutable private serviceIntent: Intent option = None
    
    let startService (context: Context) =
        let intent = new Intent(context, typeof<AudioStreamingService>)
        serviceIntent <- Some intent
        if Build.VERSION.SdkInt >= BuildVersionCodes.O then
            context.StartForegroundService(intent) |> ignore
        else
            context.StartService(intent) |> ignore
    
    let stopService (context: Context) =
        serviceIntent |> Option.iter (fun intent ->
            context.StopService(intent) |> ignore
        )
        serviceIntent <- None

[<Activity(
    Label = "AuroraMic.Client",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = (ConfigChanges.Orientation ||| ConfigChanges.ScreenSize ||| ConfigChanges.UiMode))>]
type MainActivity() =
    inherit AvaloniaMainActivity<App>()
    

    override this.CustomizeAppBuilder(builder) =
        base.CustomizeAppBuilder(builder)
            .WithInterFont()
    
    override this.OnRequestPermissionsResult(requestCode, permissions, grantResults) =
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults)
    
    override this.OnCreate(savedInstanceState) =
        base.OnCreate(savedInstanceState)
        let disableAudioEffects () =
        // AudioRecord session 0 aplica a todos los streams de captura activos
            if AutomaticGainControl.IsAvailable then
                try
                    use agc = AutomaticGainControl.Create(0)
                    agc.SetEnabled(false) |> ignore
                with _ -> ()
            
            if NoiseSuppressor.IsAvailable then
                try
                    use ns = NoiseSuppressor.Create(0)
                    ns.SetEnabled(false) |> ignore
                with _ -> () 
        let permissionsToRequest = [|
            yield Android.Manifest.Permission.RecordAudio
            if Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu then
                yield Android.Manifest.Permission.PostNotifications
        |]
        
        permissionsToRequest
        |> Array.filter (fun p -> this.CheckSelfPermission(p) <> Permission.Granted)
        |> function
           | [||] -> ()
           | denied -> this.RequestPermissions(denied, 0)
        AndroidMic.onRecordingStarted <- Some disableAudioEffects
        AndroidMic.setCallbacks
            (fun () -> ForegroundServiceHelper.startService this)
            (fun () -> ForegroundServiceHelper.stopService this)