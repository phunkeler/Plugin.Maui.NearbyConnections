## .NET MAUI for .NET 10 - What's New

Source: https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10?view=net-maui-10.0

Focus: product quality improvements.

### .NET Aspire Integration
- New project template for .NET Aspire service defaults
- `builder.AddServiceDefaults()` in `MauiProgram.CreateMauiApp()`
- Configures OpenTelemetry, service discovery, HttpClient integration

### Animation
- Deprecated: `FadeTo`, `LayoutTo`, `RelRotateTo`, `RelScaleTo`, `RotateTo`, `ScaleTo`, `TranslateTo`
- Replacements: `FadeToAsync`, `LayoutToAsync`, `RelRotateToAsync`, `RelScaleToAsync`, `RotateToAsync`, `ScaleToAsync`, `TranslateToAsync`

### Controls - Breaking Changes & Deprecations
- **ListView**: Deprecated (use `CollectionView`)
- **TableView**: Deprecated (use `CollectionView`)
- **MessagingCenter**: Made internal (use `WeakReferenceMessenger` from CommunityToolkit.Mvvm)
- **Accelerator**: Removed (use `KeyboardAccelerator`)
- **ClickGestureRecognizer**: Removed (use `TapGestureRecognizer`)
- **Compatibility.Layout**: Removed (use .NET MAUI layouts)
- **Page.IsBusy**: Obsolete (use `ActivityIndicator`)
- **FontImageExtension**: Deprecated (use `FontImageSource`)
- **DisplayAlert/DisplayActionSheet**: Deprecated (use `DisplayAlertAsync`/`DisplayActionSheetAsync`)

### Controls - New Features
- **CollectionView/CarouselView**: New handlers are default on iOS/Mac Catalyst (performance improvements)
- **HybridWebView**: New `InvokeJavaScriptAsync` overload, JS exceptions sent to .NET, customizable initialization
- **Web Request Interception**: `BlazorWebView`/`HybridWebView` `WebResourceRequested` event
- **Picker**: Programmatic Open/Close API
- **DatePicker/TimePicker**: Nullable selection support
- **SearchBar**: `SearchIconColor` and `ReturnType` properties
- **Switch**: `OffColor` property
- **RefreshView**: `IsRefreshEnabled` property
- **SearchHandler**: `ShowSoftInputAsync`/`HideSoftInputAsync`
- **Vibration/HapticFeedback**: `IsSupported` property

### XAML Source Generator
Enable in csproj:
```xml
<PropertyGroup>
  <MauiXamlInflator>SourceGen</MauiXamlInflator>
</PropertyGroup>
```

### XAML Implicit/Global Namespaces
- New `http://schemas.microsoft.com/dotnet/maui/global` xmlns
- Aggregate multiple xmlns via `GlobalXmlns.cs` with `[assembly: XmlnsDefinition(...)]`
- Opt-in implicit default namespace (drop `xmlns` and `xmlns:x`):
```xml
<PropertyGroup>
    <DefineConstants>$(DefineConstants);MauiAllowImplicitXmlnsDeclaration</DefineConstants>
    <EnablePreviewFeatures>true</EnablePreviewFeatures>
</PropertyGroup>
```

### SafeArea Enhancements
- `SafeAreaEdges` enum: `None`, `SoftInput`, `Container`, `Default`, `All`
- Available on: `Layout`, `ContentView`, `ContentPage`, `Border`, `ScrollView`

### Platform Features
- **Modal Popover** (iOS/Mac Catalyst): `ModalPopoverSourceView`, `ModalPopoverRect`, `ModalPresentationStyle`
- **Geolocation**: `IsEnabled` property
- **MediaPicker**: Auto-rotate EXIF, `PickMultipleAsync`, `MaximumWidth`/`MaximumHeight`
- **TextToSpeech**: `SpeechOptions.Rate`
- **WebAuthenticator**: `CancellationToken` support

### Diagnostics
- ActivitySource: `"Microsoft.Maui"` for layout timing
- Metrics: `maui.layout.measure_count`, `maui.layout.measure_duration`, `maui.layout.arrange_count`, `maui.layout.arrange_duration`

### .NET for Android
- API 36 support, JDK 21
- `dotnet run` support for Android projects
- Marshal methods enabled by default
- (Experimental) CoreCLR runtime: `<UseMonoRuntime>false</UseMonoRuntime>`
- Default `SupportedOSPlatformVersion` raised to 24 (from 21)
- Build binding projects on Windows (no remote Mac needed)

### .NET for iOS
- Supports iOS 18.2, tvOS 18.2, Mac Catalyst 18.2, macOS 15.2
- Xcode 26 Beta 4 support (via `net9.0-ios26`)
- Trimmer enabled in more configurations (Simulator/arm64)
- Trimmer warnings enabled by default
- Build binding projects entirely on Windows
