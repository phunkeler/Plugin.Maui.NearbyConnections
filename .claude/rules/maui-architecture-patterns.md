## .NET MAUI Enterprise Architecture Patterns

Source: https://learn.microsoft.com/en-us/dotnet/architecture/maui/

Based on the "Enterprise Application Patterns Using .NET MAUI" e-book (eShop reference app).

### Core Architecture Principles
- Partition apps into discrete, loosely coupled components
- Clean separation between UI controls and business logic
- Use dependency injection containers for object instantiation
- View-model-first navigation for testability
- Message-based communication between loosely coupled components

### MVVM Pattern

**Three components:**
- **Model**: Non-visual classes encapsulating app data (DTOs, POCOs, entities)
- **View**: UI structure/layout (`ContentPage`/`ContentView`). Avoid business logic in code-behind
- **ViewModel**: Properties + commands for data binding, implements `INotifyPropertyChanged`

**Key rules:**
- View "knows about" ViewModel, ViewModel "knows about" Model, Model knows neither
- Keep UI responsive with async operations in ViewModels
- Expose commands as `ICommand` (not concrete types like `RelayCommand`)
- Use `ObservableCollection<T>` for collection binding
- Don't reference view types from ViewModels

**Connecting Views to ViewModels:**
```csharp
// Constructor injection (preferred with DI)
public LoginView(LoginViewModel viewModel)
{
    InitializeComponent();
    BindingContext = viewModel;
}
```

**Commands** - use `RelayCommand`/`AsyncRelayCommand` from CommunityToolkit.Mvvm:
```csharp
public ICommand SignInCommand { get; }
SignInCommand = new AsyncRelayCommand(async () => await SignInAsync());
```

**Behaviors** - use `EventToCommandBehavior` from MAUI Community Toolkit for controls without command support:
```xml
<Entry.Behaviors>
    <mct:EventToCommandBehavior EventName="TextChanged" Command="{Binding ValidateCommand}" />
</Entry.Behaviors>
```

### Dependency Injection

Uses `Microsoft.Extensions.DependencyInjection` via `MauiProgram.CreateMauiApp()`.

**Registration pattern** - organize with extension methods:
```csharp
public static MauiApp CreateMauiApp()
    => MauiApp.CreateBuilder()
        .UseMauiApp<App>()
        .RegisterAppServices()
        .RegisterViewModels()
        .RegisterViews()
        .Build();
```

**Lifetimes:**
| Method | Use When |
|--------|----------|
| `AddSingleton<T>` | Always available, shared instance (root ViewModels, services) |
| `AddTransient<T>` | Situational, short-lived (detail pages, dialogs) |

**Interface registration:**
```csharp
mauiAppBuilder.Services.AddSingleton<ISettingsService, SettingsService>();
```

**Resolution** - Shell automatically resolves via DI during navigation:
```csharp
Routing.RegisterRoute("Filter", typeof(FiltersView));
// FiltersView constructor receives dependencies automatically
```

### Navigation (Shell-based)

**INavigationService interface:**
```csharp
public interface INavigationService
{
    Task InitializeAsync();
    Task NavigateToAsync(string route, IDictionary<string, object> routeParameters = null);
    Task PopAsync();
}
```

**Route registration** - XAML for Shell-managed views, code-behind for push navigation:
```csharp
// Code-behind for pages pushed onto navigation stack
Routing.RegisterRoute("Filter", typeof(FiltersView));
Routing.RegisterRoute("Basket", typeof(BasketView));
```

**Passing parameters:**
```csharp
await NavigationService.NavigateToAsync("OrderDetail",
    new Dictionary<string, object>{ { "OrderNumber", order.OrderNumber } });

[QueryProperty(nameof(OrderNumber), "OrderNumber")]
public class OrderDetailViewModel : ViewModelBase
{
    public int OrderNumber { get; set; }
}
```

### Recommended Project Structure (eShop pattern)

| Folder | Purpose |
|--------|---------|
| Models | Data model classes |
| ViewModels | Application logic exposed to pages |
| Views | Pages and UI |
| Services | Service interfaces and implementations |
| Controls | Custom controls |
| Converters | Value converters for bindings |
| Behaviors | Reusable behaviors |
| Validations | Data input validation |
| Extensions | Extension methods |
| Helpers | Helper classes |

### Recommended Frameworks
- **CommunityToolkit.Mvvm**: MVVM Toolkit (`RelayCommand`, `ObservableObject`, `WeakReferenceMessenger`)
- **CommunityToolkit.Maui**: `EventToCommandBehavior`, additional controls
- **Microsoft.Extensions.DependencyInjection**: Built-in DI container
