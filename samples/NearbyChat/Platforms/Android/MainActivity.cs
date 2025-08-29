using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Activity;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using static AndroidX.Activity.Result.Contract.ActivityResultContracts;
using JavaObject = Java.Lang.Object;

namespace NearbyChat;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        RequestPermissionsForResult.Instance.Register(this);
        var permissions = FromArray(GetRequiredPermissions() ?? []) ?? new JavaList<string>();

        var result = RequestPermissionsForResult.Instance.Launch(permissions);

        var r = result;
    }

    static string[] GetRequiredPermissions()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return
            [
                Android.Manifest.Permission.AccessWifiState,
                Android.Manifest.Permission.BluetoothAdvertise,
                Android.Manifest.Permission.BluetoothConnect,
                Android.Manifest.Permission.BluetoothScan,
                Android.Manifest.Permission.ChangeWifiState,
                Android.Manifest.Permission.NearbyWifiDevices
            ];
        }
        else if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            return
            [
                Android.Manifest.Permission.AccessCoarseLocation,
                Android.Manifest.Permission.AccessFineLocation,
                Android.Manifest.Permission.AccessWifiState,
                Android.Manifest.Permission.BluetoothAdvertise,
                Android.Manifest.Permission.BluetoothConnect,
                Android.Manifest.Permission.BluetoothScan,
                Android.Manifest.Permission.ChangeWifiState
            ];
        }
        else if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            return
            [
                Android.Manifest.Permission.AccessCoarseLocation,
                Android.Manifest.Permission.AccessFineLocation,
                Android.Manifest.Permission.AccessWifiState,
                Android.Manifest.Permission.Bluetooth,
                Android.Manifest.Permission.BluetoothAdmin,
                Android.Manifest.Permission.ChangeWifiState
            ];
        }
        else
        {
            return
            [
                Android.Manifest.Permission.AccessCoarseLocation,
                Android.Manifest.Permission.AccessWifiState,
                Android.Manifest.Permission.Bluetooth,
                Android.Manifest.Permission.BluetoothAdmin,
                Android.Manifest.Permission.ChangeWifiState
            ];
        }

    }
}

internal sealed class RequestPermissionsForResult : ActivityForResultRequest<RequestMultiplePermissions, Java.Lang.Boolean>
{
    static readonly Lazy<RequestPermissionsForResult> s_lazyInstance = new(new RequestPermissionsForResult());

    public static RequestPermissionsForResult Instance => s_lazyInstance.Value;
}

internal sealed class ActivityResultCallback<T>(Action<T> onActivityResult) : JavaObject, IActivityResultCallback
   where T : JavaObject
{
    readonly Action<T> _onActivityResult = onActivityResult;

    public void OnActivityResult(JavaObject? result)
    {
        if (result is not T t)
        {
            return;
        }
        _onActivityResult.Invoke(t);
    }
}

internal abstract class ActivityForResultRequest<TContract, TResult>
    where TContract : ActivityResultContract, new()
    where TResult : JavaObject
{
    protected ActivityResultLauncher? launcher;
    protected TaskCompletionSource<TResult>? tcs;

    /// <summary>
    /// Gets a value indicating whether the request is registered.
    /// </summary>
    protected bool IsRegistered => launcher is not null;

    /// <summary>
    /// Registers this request to start an activity for a result.
    /// </summary>
    /// <param name="componentActivity">The component activity to register the request with.</param>
    public void Register(ComponentActivity componentActivity)
    {
        var contract = new TContract();
        var callback = new ActivityResultCallback<TResult>(result => tcs?.SetResult(result));

        launcher = componentActivity.RegisterForActivityResult(contract, callback);
    }

    /// <summary>
    /// Launches the activity result request with the specified input.
    /// </summary>
    /// <typeparam name="T">The type of the input parameter.</typeparam>
    /// <param name="input">The input parameter to launch the request with.</param>
    /// <returns>
    /// A task that represents the asynchronous operation, containing the result of the activity.
    /// </returns>
    public Task<TResult> Launch<T>(T input)
        where T : JavaObject
    {
        tcs = new TaskCompletionSource<TResult>();

        if (!IsRegistered)
        {
            tcs.SetCanceled();
            return tcs.Task;
        }

        try
        {
            launcher?.Launch(input);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }
}