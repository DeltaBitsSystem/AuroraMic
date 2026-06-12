namespace AuroraMic.Client.Android

open Android.App
open Android.Content
open Android.Content.PM
open Android.OS
open Avalonia
open Avalonia.Android
open AuroraMic.Client

[<Service(Name = "com.auroramic.app.AudioStreamingService", ForegroundServiceType = ForegroundService.TypeMicrophone)>]
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

    override this.OnDestroy() =
        base.OnDestroy()
        if AndroidMic.isRunning() then
            AndroidMic.stop()
    
    member private this.CreateNotificationChannel() =
        if Build.VERSION.SdkInt >= BuildVersionCodes.O then
            let channel = new NotificationChannel(
                channelId,
                "Audio Streaming",
                NotificationImportance.Low
            )
            channel.Description <- "Keeps audio streaming active"
            match this.GetSystemService(Context.NotificationService) with
            | :? NotificationManager as manager -> manager.CreateNotificationChannel(channel)
            | _ -> ()
    
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
        AndroidMic.setCallbacks
            (fun () -> ForegroundServiceHelper.startService this)
            (fun () -> ForegroundServiceHelper.stopService this)