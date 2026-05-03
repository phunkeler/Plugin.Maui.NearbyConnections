using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Maui.Graphics.Converters;

namespace NearbyChat.Controls;

public partial class FeatureControlCard : CardView
{
    public static readonly BindableProperty ActiveButtonColorProperty = BindableProperty.Create(
        propertyName: nameof(ActiveButtonColor),
        returnType: typeof(Color),
        declaringType: typeof(FeatureControlCard),
        defaultValue: Colors.Fuchsia);

    public static readonly BindableProperty ActiveButtonTextProperty = BindableProperty.Create(
        propertyName: nameof(ActiveButtonText),
        returnType: typeof(string),
        declaringType: typeof(FeatureControlCard),
        defaultValue: "Stop");

    public static readonly BindableProperty ActiveSubtitleProperty = BindableProperty.Create(
        propertyName: nameof(ActiveSubtitle),
        returnType: typeof(string),
        declaringType: typeof(FeatureControlCard),
        defaultValue: "Active");

    public static readonly BindableProperty InactiveButtonColorProperty = BindableProperty.Create(
        propertyName: nameof(InactiveButtonColor),
        returnType: typeof(Color),
        declaringType: typeof(FeatureControlCard),
        defaultValue: Colors.Fuchsia);

    public static readonly BindableProperty InactiveButtonTextProperty = BindableProperty.Create(
        propertyName: nameof(InactiveButtonText),
        returnType: typeof(string),
        declaringType: typeof(FeatureControlCard),
        defaultValue: "Start");

    public static readonly BindableProperty InactiveSubtitleProperty = BindableProperty.Create(
        propertyName: nameof(InactiveSubtitle),
        returnType: typeof(string),
        declaringType: typeof(FeatureControlCard),
        defaultValue: "Inactive");

    [TypeConverter(typeof(ColorTypeConverter))]
    public Color ActiveButtonColor
    {
        get => (Color)GetValue(ActiveButtonColorProperty);
        set => SetValue(ActiveButtonColorProperty, value);
    }

    public string ActiveButtonText
    {
        get => (string)GetValue(ActiveButtonTextProperty);
        set => SetValue(ActiveButtonTextProperty, value);
    }

    public string ActiveSubtitle
    {
        get => (string)GetValue(ActiveSubtitleProperty);
        set => SetValue(ActiveSubtitleProperty, value);
    }

    [TypeConverter(typeof(ColorTypeConverter))]
    public Color InactiveButtonColor
    {
        get => (Color)GetValue(InactiveButtonColorProperty);
        set => SetValue(InactiveButtonColorProperty, value);
    }

    public string InactiveButtonText
    {
        get => (string)GetValue(InactiveButtonTextProperty);
        set => SetValue(InactiveButtonTextProperty, value);
    }

    public string InactiveSubtitle
    {
        get => (string)GetValue(InactiveSubtitleProperty);
        set => SetValue(InactiveSubtitleProperty, value);
    }

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        propertyName: nameof(Command),
        returnType: typeof(ICommand),
        declaringType: typeof(FeatureControlCard));

    public static readonly BindableProperty FeatureIsActiveProperty = BindableProperty.Create(
        propertyName: nameof(FeatureIsActive),
        returnType: typeof(bool),
        declaringType: typeof(FeatureControlCard));

    public static readonly BindableProperty FeatureIconBadgeStyleProperty = BindableProperty.Create(
        propertyName: nameof(FeatureIconBadgeStyle),
        returnType: typeof(Style),
        declaringType: typeof(FeatureControlCard));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
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

    public FeatureControlCard()
    {
        InitializeComponent();
    }
}