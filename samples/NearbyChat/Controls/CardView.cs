namespace NearbyChat.Controls;

public class CardView : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        propertyName: nameof(Title),
        returnType: typeof(string),
        declaringType: typeof(CardView));

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        propertyName: nameof(Subtitle),
        returnType: typeof(string),
        declaringType: typeof(CardView));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }
}