// Permissions the plugin itself requires, declared at assembly level so they merge into the
// consuming application's AndroidManifest.xml automatically.
//
// Without these, an app that installs the NuGet package gets none of the permissions Nearby
// Connections needs, and advertising/discovery fails silently at runtime — no exception, no log.
// Declaring them here means the package works on install; the app only has to request the
// dangerous ones at runtime.
//
// Only permissions the plugin's own code path requires are declared. Notably absent are the
// READ_MEDIA_* permissions: reading a file the app chooses to send is the app's concern, not the
// plugin's, and a library that silently demands media access would be over-reaching.
//
// Consuming apps override any of these by redeclaring them in their own
// Platforms/Android/AndroidManifest.xml — the app's declaration wins. Note that `tools:node="remove"`
// does NOT work against these (verified): the directive is copied into the final manifest verbatim
// and the permission survives.

// Normal permissions — granted at install time, no runtime request needed.
[assembly: UsesPermission(Android.Manifest.Permission.AccessWifiState)]
[assembly: UsesPermission(Android.Manifest.Permission.ChangeWifiState)]
[assembly: UsesPermission(Android.Manifest.Permission.Internet)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessNetworkState)]

// Legacy Bluetooth — replaced by the granular BLUETOOTH_* permissions on API 31+, so these are
// capped with maxSdkVersion to avoid requesting more than necessary on modern devices.
[assembly: UsesPermission(Android.Manifest.Permission.Bluetooth, MaxSdkVersion = 30)]
[assembly: UsesPermission(Android.Manifest.Permission.BluetoothAdmin, MaxSdkVersion = 30)]

// Location — Nearby Connections required location for BLE scanning before API 31. On API 31+ the
// BLUETOOTH_SCAN permission with neverForLocation replaces it, so these are capped at 32.
[assembly: UsesPermission(Android.Manifest.Permission.AccessCoarseLocation, MaxSdkVersion = 32)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessFineLocation, MaxSdkVersion = 32)]

// Runtime permissions (API 31+). The app must still request these before starting; declaring them
// here only makes them requestable.
[assembly: UsesPermission(Android.Manifest.Permission.BluetoothAdvertise)]
[assembly: UsesPermission(Android.Manifest.Permission.BluetoothConnect)]

// BLUETOOTH_SCAN and NEARBY_WIFI_DEVICES are declared WITHOUT usesPermissionFlags, because
// UsesPermissionAttribute exposes only Name and MaxSdkVersion, and AndroidManifestOverlay does not
// propagate from a library to consuming apps (verified: an overlay declared here never reaches the
// app's merged manifest).
//
// Declaring them plainly is still the right trade. Without a declaration the permission cannot be
// granted at all, so Nearby Connections fails outright on API 31+. With it, everything works; the
// only cost is that Android treats the permissions as implying location access.
//
// Consuming apps that do not derive location from scan results SHOULD add
// android:usesPermissionFlags="neverForLocation" in their own Platforms/Android/AndroidManifest.xml.
// The manifest merger applies the app's attributes over these. See the README for the snippet.
[assembly: UsesPermission(Android.Manifest.Permission.BluetoothScan)]
[assembly: UsesPermission(Android.Manifest.Permission.NearbyWifiDevices)]
