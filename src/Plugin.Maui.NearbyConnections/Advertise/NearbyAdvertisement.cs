using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Represents structured metadata that can be advertised to nearby devices during discovery.
/// This data is transmitted before connection establishment and has strict size limits.
/// </summary>
/// <remarks>
/// <para>
/// **Size Constraints:**
/// - Android: Maximum 131 bytes when serialized
/// - iOS: Maximum 400 bytes total, 255 bytes per key-value pair
/// </para>
/// </remarks>
public sealed partial class NearbyAdvertisement
{
    /// <summary>
    /// Maximum size in bytes for Android's endpointInfo parameter.
    /// </summary>
    public const int MaxSizeAndroid = 131;

    /// <summary>
    /// Maximum total size in bytes for iOS's discovery info.
    /// </summary>
    public const int MaxSizeIos = 400;

    /// <summary>
    /// Maximum size in bytes for a single key-value pair on iOS.
    /// </summary>
    public const int MaxSizeIosKeyValue = 255;

    /// <summary>
    /// Plugin version for protocol compatibility checking.
    /// Auto-populated by the plugin.
    /// </summary>
    [JsonPropertyName("v")]
    public string PluginVersion { get; internal set; } = GetPluginVersion();

    /// <summary>
    /// Application package identifier to prevent cross-app connections.
    /// Auto-populated from AppInfo.PackageName.
    /// </summary>
    [JsonPropertyName("app")]
    public string AppId { get; internal set; } = AppInfo.PackageName;

    /// <summary>
    /// Platform identifier (iOS/Android) for cross-platform awareness.
    /// Auto-populated by the plugin.
    /// </summary>
    [JsonPropertyName("plat")]
    public string Platform { get; internal set; } = GetPlatformIdentifier();

    /// <summary>
    /// Device model/type for UI display and filtering.
    /// Can be overridden for privacy. Set to null to exclude.
    /// </summary>
    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceModel { get; set; } = DeviceInfo.Model;

    /// <summary>
    /// Creates a new advertisement with default metadata.
    /// </summary>
    public NearbyAdvertisement()
    {
    }

    /// <summary>
    /// Validates that the advertisement fits within platform size constraints.
    /// </summary>
    /// <param name="targetPlatform">The target platform to validate for. If null, validates for the current platform.</param>
    /// <returns>A validation result indicating success or failure with details.</returns>
    public AdvertisementValidationResult Validate(string? targetPlatform = null)
    {
        targetPlatform ??= Platform;

        // Serialize to check actual size
        var serialized = Serialize();

        if (targetPlatform.Equals("Android", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateAndroid(serialized);
        }
        else if (targetPlatform.Equals("iOS", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateIos();
        }

        // For unknown platforms, use the more restrictive Android limit
        return ValidateAndroid(serialized);
    }

    /// <summary>
    /// Serializes the advertisement to a JSON byte array.
    /// Uses compact JSON formatting to minimize size.
    /// </summary>
    /// <returns>UTF-8 encoded JSON byte array.</returns>
    public byte[] Serialize()
    {
        var json = JsonSerializer.Serialize(this, NearbyAdvertisementJsonContext.Default.NearbyAdvertisement);
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// Deserializes an advertisement from a JSON byte array.
    /// </summary>
    /// <param name="data">UTF-8 encoded JSON byte array.</param>
    /// <returns>Deserialized advertisement, or null if data is invalid.</returns>
    public static NearbyAdvertisement? Deserialize(byte[] data)
    {
        if (data is null || data.Length == 0)
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize(json, NearbyAdvertisementJsonContext.Default.NearbyAdvertisement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Converts the advertisement to a dictionary format suitable for iOS's NSDictionary.
    /// </summary>
    /// <returns>Dictionary with all non-null values.</returns>
    public Dictionary<string, string> ToDictionary()
    {
        var dict = new Dictionary<string, string>
        {
            ["v"] = PluginVersion,
            ["app"] = AppId,
            ["plat"] = Platform
        };

        if (DeviceModel is not null)
        {
            dict["model"] = DeviceModel;
        }

        return dict;
    }

    /// <summary>
    /// Creates an advertisement from a dictionary (typically from iOS discovery info).
    /// </summary>
    /// <param name="dictionary">Dictionary containing advertisement data.</param>
    /// <returns>Deserialized advertisement, or null if data is invalid.</returns>
    public static NearbyAdvertisement? FromDictionary(IDictionary<string, string>? dictionary)
    {
        if (dictionary is null || dictionary.Count == 0)
            return null;

        var ad = new NearbyAdvertisement();

        if (dictionary.TryGetValue("v", out var version))
            ad.PluginVersion = version;

        if (dictionary.TryGetValue("app", out var appId))
            ad.AppId = appId;

        if (dictionary.TryGetValue("plat", out var platform))
            ad.Platform = platform;

        if (dictionary.TryGetValue("model", out var model))
            ad.DeviceModel = model;

        return ad;
    }

    static AdvertisementValidationResult ValidateAndroid(byte[] serialized)
    {
        if (serialized.Length <= MaxSizeAndroid)
        {
            return AdvertisementValidationResult.Success(serialized.Length);
        }

        var excess = serialized.Length - MaxSizeAndroid;
        return AdvertisementValidationResult.Failure(
            $"Advertisement exceeds Android's 131-byte limit by {excess} bytes. " +
            $"Current size: {serialized.Length} bytes. Consider removing CustomData or DeviceModel.",
            serialized.Length,
            MaxSizeAndroid);
    }

    private AdvertisementValidationResult ValidateIos()
    {
        var dict = ToDictionary();
        var totalSize = 0;

        foreach (var (key, value) in dict)
        {
            var kvSize = Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(value);

            if (kvSize > MaxSizeIosKeyValue)
            {
                return AdvertisementValidationResult.Failure(
                    $"Key-value pair '{key}' exceeds iOS's 255-byte limit per pair. " +
                    $"Pair size: {kvSize} bytes.",
                    kvSize,
                    MaxSizeIosKeyValue);
            }

            totalSize += kvSize;
        }

        if (totalSize > MaxSizeIos)
        {
            var excess = totalSize - MaxSizeIos;
            return AdvertisementValidationResult.Failure(
                $"Advertisement exceeds iOS's 400-byte total limit by {excess} bytes. " +
                $"Current size: {totalSize} bytes. Consider removing CustomData or DeviceModel.",
                totalSize,
                MaxSizeIos);
        }

        return AdvertisementValidationResult.Success(totalSize);
    }

    private static string GetPluginVersion()
    {
        // Get from assembly version (set via Directory.Build.props <Version> property)
        var assembly = typeof(NearbyAdvertisement).Assembly;
        var version = assembly.GetName().Version;

        if (version is null)
        {
            // Fallback if version is not set (shouldn't happen in normal builds)
            return "0.0.0";
        }

        // Use Major.Minor.Patch format (ignore Revision)
        // If Build is -1 (not set), use 0
        var patch = version.Build >= 0 ? version.Build : 0;
        return $"{version.Major}.{version.Minor}.{patch}";
    }

    private static string GetPlatformIdentifier()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
            return "Android";

        if (DeviceInfo.Platform == DevicePlatform.iOS)
            return "iOS";

        return "Unknown";
    }
}

/// <summary>
/// Result of validating a <see cref="NearbyAdvertisement"/>.
/// </summary>
public sealed class AdvertisementValidationResult
{
    /// <summary>
    /// Indicates whether the advertisement is valid.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Error message if validation failed, otherwise null.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Actual size of the advertisement in bytes.
    /// </summary>
    public int ActualSize { get; }

    /// <summary>
    /// Maximum allowed size in bytes for the target platform.
    /// </summary>
    public int MaxSize { get; }

    private AdvertisementValidationResult(bool isValid, int actualSize, int maxSize, string? errorMessage = null)
    {
        IsValid = isValid;
        ActualSize = actualSize;
        MaxSize = maxSize;
        ErrorMessage = errorMessage;
    }

    internal static AdvertisementValidationResult Success(int actualSize)
        => new(true, actualSize, 0);

    internal static AdvertisementValidationResult Failure(string errorMessage, int actualSize, int maxSize)
        => new(false, actualSize, maxSize, errorMessage);

    /// <summary>
    /// Throws an exception if validation failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when validation failed.</exception>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException(ErrorMessage);
        }
    }

    /// <summary>
    /// Returns a string representation of the validation result.
    /// </summary>
    public override string ToString()
    {
        if (IsValid)
        {
            return $"Valid ({ActualSize} bytes)";
        }

        return $"Invalid: {ErrorMessage}";
    }
}

/// <summary>
/// JSON serialization context for NearbyAdvertisement using source generation.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(NearbyAdvertisement))]
public sealed partial class NearbyAdvertisementJsonContext : JsonSerializerContext
{
}