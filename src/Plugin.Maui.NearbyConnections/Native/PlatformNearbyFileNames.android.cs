using Android.Content;
using AndroidUri = Android.Net.Uri;
using Path = System.IO.Path;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby
{
    static AndroidUri? TryCreateUri(string fileUri)
    {
        if (string.IsNullOrWhiteSpace(fileUri))
        {
            return null;
        }

        try
        {
            AndroidUri? uri;

            if (Path.IsPathRooted(fileUri))
            {
                using var file = new Java.IO.File(fileUri);
                uri = AndroidUri.FromFile(file);
            }
            else
            {
                uri = AndroidUri.Parse(fileUri);
            }

            return IsSupportedScheme(uri)
                ? uri
                : null;
        }
        catch
        {
            return null;
        }
    }

    static bool IsSupportedScheme(AndroidUri? uri)
        => uri?.Scheme is { } scheme
            && (scheme.Equals(ContentResolver.SchemeFile, StringComparison.OrdinalIgnoreCase)
                || scheme.Equals(ContentResolver.SchemeContent, StringComparison.OrdinalIgnoreCase));

    string ResolveResourceName(AndroidUri uri) =>
        ContentResolver.SchemeContent.Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase)
            ? ResolveContentUriName(uri)
            : ResolveFileUriName(uri);

    string ResolveContentUriName(AndroidUri uri)
    {
        try
        {
            var (displayName, dataPath) = QueryContentColumns(uri);

            return NameWithExtension(displayName)
                ?? NameFromDataPath(dataPath)
                ?? NameFromMimeType(uri, displayName)
                ?? displayName
                ?? uri.LastPathSegment
                ?? Guid.NewGuid().ToString("N");
        }
        catch (Exception ex)
        {
            LogCouldNotResolveContentUriName(ex);
            return Guid.NewGuid().ToString("N");
        }
    }

    /// <summary>
    /// Reads <c>_display_name</c>, and <c>_data</c> where the platform still populates it. Returns
    /// a null <c>dataPath</c> on API 29 and above, where that column is not queried at all.
    /// </summary>
    static (string? DisplayName, string? DataPath) QueryContentColumns(AndroidUri uri)
    {
        var queryData = !OperatingSystem.IsAndroidVersionAtLeast(29);

        string[] projection = queryData
            ? [Android.Provider.IOpenableColumns.DisplayName, Android.Provider.MediaStore.IMediaColumns.Data]
            : [Android.Provider.IOpenableColumns.DisplayName];

        using var cursor = Application.Context.ContentResolver?.Query(
            uri,
            projection,
            selection: null,
            selectionArgs: null,
            sortOrder: null);

        if (cursor is null || !cursor.MoveToFirst())
        {
            return (null, null);
        }

        var nameIndex = cursor.GetColumnIndex(Android.Provider.IOpenableColumns.DisplayName);

        var displayName = nameIndex >= 0
            ? cursor.GetString(nameIndex)
            : null;

        if (!queryData)
        {
            return (displayName, null);
        }

        var dataIndex = cursor.GetColumnIndex(Android.Provider.MediaStore.IMediaColumns.Data);

        var dataPath = dataIndex >= 0
            ? cursor.GetString(dataIndex)
            : null;

        return (displayName, dataPath);
    }

    static string? NameFromDataPath(string? dataPath)
        => !string.IsNullOrEmpty(dataPath)
            && Path.GetFileName(dataPath) is { Length: > 0 } name
            ? name
            : null;

    static string? NameWithExtension(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName)
        && Path.GetExtension(displayName).Length > 0
            ? displayName
            : null;

    // Derives an extension from the MIME type and pairs it with the display name stem.
    static string? NameFromMimeType(AndroidUri uri, string? displayName)
    {
        var mimeType = Application.Context.ContentResolver?.GetType(uri);

        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return null;
        }

        var ext = Android.Webkit.MimeTypeMap.Singleton?.GetExtensionFromMimeType(mimeType);

        if (string.IsNullOrWhiteSpace(ext))
        {
            return null;
        }

        var stem = !string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileNameWithoutExtension(displayName)
            : Guid.NewGuid().ToString("N");

        return $"{stem}.{ext}";
    }

    static string ResolveFileUriName(AndroidUri uri)
    {
        if (uri?.Path is { Length: > 0 } filePath)
        {
            return Path.GetFileName(filePath) is { Length: > 0 } fileName
                ? fileName
                : filePath;
        }

        return Guid.NewGuid().ToString("N");
    }
}