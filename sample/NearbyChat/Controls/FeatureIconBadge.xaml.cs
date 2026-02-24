namespace NearbyChat.Controls;

public partial class FeatureIconBadge : ContentView
{
    public static readonly BindableProperty BorderStyleProperty = BindableProperty.Create(
        propertyName: nameof(BorderStyle),
        returnType: typeof(Style),
        declaringType: typeof(FeatureIconBadge));

    public static readonly BindableProperty ImageStyleProperty = BindableProperty.Create(
        propertyName: nameof(ImageStyle),
        returnType: typeof(Style),
        declaringType: typeof(FeatureIconBadge),
        defaultValue: default(Style));

    public Style BorderStyle
    {
        get => (Style)GetValue(BorderStyleProperty);
        set => SetValue(BorderStyleProperty, value);
    }

    public Style ImageStyle
    {
        get => (Style)GetValue(ImageStyleProperty);
        set => SetValue(ImageStyleProperty, value);
    }

    public FeatureIconBadge()
    {
        InitializeComponent();
    }
}