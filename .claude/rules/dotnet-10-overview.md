## .NET 10 Overview (LTS)

Source: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview

### Runtime
- Improvements in JIT inlining, method devirtualization, stack allocations
- AVX10.2 support, NativeAOT enhancements
- Improved code generation for struct arguments, enhanced loop inversion

### Libraries
- New APIs: cryptography, globalization, numerics, serialization, collections, diagnostics
- JSON serialization: disallow duplicate properties, strict settings, `PipeReader` support
- Post-quantum cryptography: Windows CNG support, ML-DSA, Composite ML-DSA
- AES KeyWrap with Padding, `WebSocketStream`, TLS 1.3 for macOS clients
- Windows process group support

### SDK
- `Microsoft.Testing.Platform` support in `dotnet test`
- Console apps can natively create container images
- Platform-specific .NET tools with `any` RuntimeIdentifier
- One-shot tool execution with `dotnet tool exec` and `dnx` script
- File-based apps with publish support and native AOT

### C# 14 Key Features
- **Field-backed properties**: `field` contextual keyword for auto-property backing fields
- **`nameof` with unbound generics**: `nameof(List<>)` works
- **Span conversions**: First-class `Span<T>` and `ReadOnlySpan<T>` implicit conversions
- **Lambda parameter modifiers**: `ref`, `in`, `out` without specifying types
- **Partial constructors and events**: Complement partial methods/properties from C# 13
- **Extension blocks**: `extension` blocks for static extension methods, static/instance extension properties
- **Null-conditional assignment**: `?.` operator for assignment
- **User-defined compound assignment**: Custom `+=`, `-=`, `++`, `--` operators
