# Installation

This guide covers how to install TUnit for new test projects and add it to existing projects.

## Prerequisites

- .NET SDK installed (TUnit supports .NET 6+)

## Quick Start with Template (Recommended)

The easiest way to create a new TUnit test project is using the official template:

```powershell
# Install the TUnit project template
dotnet new install TUnit.Templates

# Create a new test project
dotnet new TUnit -n "YourProjectName"
```

This creates a complete test project with sample tests demonstrating various TUnit features. Delete the samples when you're ready to write your own tests.

## Manual Installation

### For New Projects

1. **Create a new console application:**

```powershell
dotnet new console --name YourTestProjectNameHere
cd YourTestProjectNameHere
```

2. **Add the TUnit package:**

```powershell
dotnet add package TUnit --prerelease
```

3. **Remove the auto-generated `Program.cs`** - TUnit provides its own entry point through source generators.

### For Existing Projects

Simply add the TUnit package to your existing test project:

```powershell
dotnet add package TUnit --prerelease
```

## Project File Configuration

Your `.csproj` should look similar to this:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net9.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="TUnit" Version="*" />
    </ItemGroup>
</Project>
```

### Important: Do NOT Use Microsoft.NET.Test.Sdk

> ⚠️ **DANGER**: Unlike other testing frameworks, TUnit should **NOT** use the `Microsoft.NET.Test.Sdk` package. Including this package will break test discovery. TUnit uses its own source generator-based discovery mechanism.

## .NET Framework Support

If you're targeting .NET Framework, TUnit automatically polyfills missing types (like `ModuleInitialiserAttribute`) used by the compiler.

### Disabling Polyfills

If you encounter conflicts with other Polyfill libraries, disable TUnit's polyfills by adding this property to your `.csproj`:

```xml
<PropertyGroup>
    <EnableTUnitPolyfills>false</EnableTUnitPolyfills>
</PropertyGroup>
```

## Central Package Management (CPM)

TUnit fully supports NuGet Central Package Management. When CPM is enabled via `Directory.Packages.props`, TUnit automatically handles the Polyfill package using `VersionOverride`.

### CPM Options

You can either:

1. **Let TUnit manage it automatically** (recommended) - no action needed
2. **Manage manually** - Add to `Directory.Packages.props`:
   ```xml
   <PackageVersion Include="Polyfill" Version="x.x.x" />
   ```
3. **Disable auto-injection** - Add to your project file and manage manually:
   ```xml
   <PropertyGroup>
       <EnableTUnitPolyfills>false</EnableTUnitPolyfills>
   </PropertyGroup>
   ```

## Verification

After installation, verify everything is working:

1. Create a simple test file (e.g., `SampleTests.cs`):

```csharp
using TUnit.Core;

public class SampleTests
{
    [Test]
    public async Task First_test_passes()
    {
        await Assert.That(true).IsTrue();
    }
}
```

2. Run the tests:

```powershell
dotnet test
```

You should see test discovery and execution working correctly.

## Next Steps

- Read the [Quickstart](quickstart.md) to learn essential TUnit concepts
- Follow the [Project Style Guide](project_style_guide.md) for project-specific conventions
- Explore [Usage Patterns](../usage_patterns/) for common testing scenarios
