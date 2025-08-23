using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NearbyChat.Models;

/// <summary>
/// An image that represents an app user.
/// </summary>
/// <remarks>
/// This favors "System.Text.Json serialization" source generation over "ObservableProperty" because the two don't play nicely together.
/// </remarks>
public partial class Avatar : ObservableObject
{
    [JsonIgnore]
    public static Color DefaultBorderColor { get; } = Colors.Transparent;

    [JsonIgnore]
    public static double DefaultBorderWidth { get; } = 1.0;


    // INT
    int _id;
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    // TEXT
    string _backgroundColor = string.Empty;
    public string BackgroundColor
    {
        get => _backgroundColor;
        set => SetProperty(ref _backgroundColor, value);
    }

    // TEXT
    string _borderColor = string.Empty;
    public string BorderColor
    {
        get => _borderColor;
        set => SetProperty(ref _borderColor, value);
    }

    // REAL
    double _borderWidth;
    public double BorderWidth
    {
        get => _borderWidth;
        set => SetProperty(ref _borderWidth, value);
    }

    // BLOB
    byte[] _imageSource = [];
    public byte[] ImageSource
    {
        get => _imageSource;
        set => SetProperty(ref _imageSource, value);
    }

    // INT
    int _padding;
    public int Padding
    {
        get => _padding;
        set => SetProperty(ref _padding, value);
    }

    // TEXT
    string _text = string.Empty;
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    // TEXT
    string _textColor = string.Empty;
    public string TextColor
    {
        get => _textColor;
        set => SetProperty(ref _textColor, value);
    }
}

public class AvatarsJson
{
    public List<Avatar> Avatars { get; set; } = [];
}