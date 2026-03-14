using System.Windows.Input;

namespace NearbyChat.Controls;

public partial class ConnectedDevicesChip : ContentView
{
    public static readonly BindableProperty CountProperty = BindableProperty.Create(
        propertyName: nameof(Count),
        returnType: typeof(int),
        declaringType: typeof(ConnectedDevicesChip));

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        propertyName: nameof(Command),
        returnType: typeof(ICommand),
        declaringType: typeof(ConnectedDevicesChip));

    public int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public ConnectedDevicesChip()
    {
        InitializeComponent();
    }
}