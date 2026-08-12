using Android.Content;
using AndroidUri = Android.Net.Uri;
using Path = System.IO.Path;

namespace Plugin.Maui.NearbyConnections;

// Resolving a human-readable file name from an Android URI is a self-contained concern with no
// dependency on connection or transfer state — split from PlatformNearby.android.cs for
// navigability. Every member here is called only from BuildFilePayload, in that file. Named
// PlatformNearbyFileNames rather than PlatformNearby.android.filenames so the .android.cs suffix
// MAUI's SDK multi-targeting relies on to exclude this file from other TFMs stays the last segment.
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

    /// <summary>
    /// Best-effort resolution of a human-readable resource name (including extension) from a URI.
    /// <para>
    /// For <c>content://</c> URIs the following sources are tried in order:
    /// <list type="number">
    ///   <item><description><c>_display_name</c> — already contains the extension for well-behaved providers (MediaStore, SAF, Downloads).</description></item>
    ///   <item><description><c>_data</c> — the underlying file path; its filename gives a reliable name + extension for MediaStore URIs.</description></item>
    ///   <item><description><see cref="ContentResolver.GetType"/> — maps the MIME type to an extension via <see cref="Android.Webkit.MimeTypeMap"/>.</description></item>
    ///   <item><description>Decoded <c>LastPathSegment</c> — opaque but human-readable.</description></item>
    /// </list>
    /// </para>
    /// For <c>file://</c> URIs, the real filesystem path is used directly.
    /// </summary>
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

    static (string? displayName, string? dataPath) QueryContentColumns(AndroidUri uri)
    {
        string? displayName = null;
        string? dataPath = null;

        using var cursor = Application.Context.ContentResolver?.Query(
            uri,
            [Android.Provider.IOpenableColumns.DisplayName, Android.Provider.MediaStore.IMediaColumns.Data],
            selection: null,
            selectionArgs: null,
            sortOrder: null);

        if (cursor is null)
        {
            return (displayName, dataPath);
        }

        if (!cursor.MoveToFirst())
        {
            return (displayName, dataPath);
        }

        var nameIndex = cursor.GetColumnIndex(Android.Provider.IOpenableColumns.DisplayName);

        if (nameIndex >= 0)
        {
            displayName = cursor.GetString(nameIndex);
        }

        var dataIndex = cursor.GetColumnIndex(Android.Provider.MediaStore.IMediaColumns.Data);

        if (dataIndex >= 0)
        {
            dataPath = cursor.GetString(dataIndex);
        }

        return (displayName, dataPath);
    }

    static string? NameWithExtension(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName)
        && Path.GetExtension(displayName).Length > 0
            ? displayName
            : null;

    static string? NameFromDataPath(string? dataPath)
    {
        if (!string.IsNullOrEmpty(dataPath)
            && Path.GetFileName(dataPath) is { Length: > 0 } name)
        {
            return name;
        }

        return null;
    }

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
