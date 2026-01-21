using System.Windows.Input;

namespace NearbyChat.Controls;

public partial class FeatureNavigationCard : ContentView
{
    public static readonly BindableProperty FeatureIconBadgeStyleProperty = BindableProperty.Create(
        propertyName: nameof(FeatureIconBadgeStyle),
        returnType: typeof(Style),
        declaringType: typeof(FeatureNavigationCard));

    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        propertyName: nameof(Icon),
        returnType: typeof(string),
        declaringType: typeof(FeatureNavigationCard),
        defaultValue: default(string));

    public static readonly BindableProperty IconStyleProperty = BindableProperty.Create(
        propertyName: nameof(IconStyle),
        returnType: typeof(Style),
        declaringType: typeof(FeatureNavigationCard),
        defaultValue: default(Style));

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        propertyName: nameof(Title),
        returnType: typeof(string),
        declaringType: typeof(FeatureNavigationCard),
        defaultValue: default(string));

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        propertyName: nameof(Subtitle),
        returnType: typeof(string),
        declaringType: typeof(FeatureNavigationCard),
        defaultValue: default(string));

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        propertyName: nameof(Command),
        returnType: typeof(ICommand),
        declaringType: typeof(FeatureNavigationCard),
        defaultValue: default(ICommand));

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        propertyName: nameof(CommandParameter),
        returnType: typeof(object),
        declaringType: typeof(FeatureNavigationCard),
        defaultValue: default(object));

    public static readonly BindableProperty FeatureIsActiveProperty = BindableProperty.Create(
        propertyName: nameof(FeatureIsActive),
        returnType: typeof(bool),
        declaringType: typeof(FeatureNavigationCard));

    public static readonly BindableProperty ActiveSubtitleProperty = BindableProperty.Create(
        propertyName: nameof(ActiveSubtitle),
        returnType: typeof(string),
        declaringType: typeof(FeatureNavigationCard),
        defaultValue: "Active");

    public string ActiveSubtitle
    {
        get => (string)GetValue(ActiveSubtitleProperty);
        set => SetValue(ActiveSubtitleProperty, value);
    }

    public bool FeatureIsActive
    {
        get => (bool)GetValue(FeatureIsActiveProperty);
        set => SetValue(FeatureIsActiveProperty, value);
    }

    public Style FeatureIconBadgeStyle
    {
        get => (Style)GetValue(FeatureIconBadgeStyleProperty);
        set => SetValue(FeatureIconBadgeStyleProperty, value);
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public Style IconStyle
    {
        get => (Style)GetValue(IconStyleProperty);
        set => SetValue(IconStyleProperty, value);
    }

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

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public FeatureNavigationCard()
    {
        InitializeComponent();
    }
}