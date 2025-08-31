# SaxonHECSharp

A .NET Core binding for Saxon-HE C/C++ library. This project provides a managed interface to use Saxon's XSLT and XQuery processing capabilities through the Saxon-C API.

## Prerequisites

- .NET 7.0 SDK or later

## Setup

There are two ways to set up the native libraries:

### Automatic Setup (Recommended)

```csharp
// Download and setup native libraries for the current platform
await SaxonProcessorExtensions.SetupNativeLibrariesAsync();

// Or specify a platform explicitly
await SaxonProcessorExtensions.SetupNativeLibrariesAsync(Platform.WindowsX64);
```

### Manual Setup

1. Download the Saxon-C library from [Saxonica's website](https://www.saxonica.com/download/c.xml)
2. Extract the downloaded archive
3. Copy the following files to the `lib` directory:
   - `libsaxon-hec-12.8.dll` (Windows)
   - `libsaxon-hec-12.8.so` (Linux)
   - `libsaxon-hec-12.8.dylib` (macOS)

Supported Platforms:
- Windows x64
- Linux x64
- Linux ARM64
- macOS x64
- macOS ARM64 (Apple Silicon)

## Usage

```csharp
using SaxonHECSharp;

using (var processor = new SaxonProcessor())
{
    using (var xslt = processor.CreateXsltProcessor())
    {
        xslt.SetSourceFile("input.xml");
        xslt.SetStylesheetFile("transform.xsl");
        
        if (xslt.CompileStylesheet())
        {
            xslt.Transform();
        }
    }
}
```

## Building

```powershell
dotnet build
```

## License

This project is licensed under the MIT License. Note that the Saxon-HE C/C++ library has its own license terms.
