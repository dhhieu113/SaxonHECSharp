# SaxonHECSharp

A .NET wrapper for Saxon-C HE, providing XSLT 3.0 transformation capabilities for .NET applications.

## Features

- XSLT 3.0 transformation support
- Cross-platform (Windows, Linux, macOS)
- Automatic native library management
- Streamable transformations
- Example project included

## Installation

```bash
dotnet add package SaxonHECSharp
```

## Quick Start

```csharp
using SaxonHECSharp;

// Initialize the Saxon processor
using var processor = new SaxonProcessor();

// Create an XSLT processor
var xsltProc = processor.CreateXsltProcessor();

// Compile and run a transformation
xsltProc.CompileStylesheet("transform.xsl");
xsltProc.Transform("input.xml", "output.html");
```

## Example Project

Check out the `SaxonHECSharp.Example` project for detailed examples of:
- Basic XSLT transformations
- Working with different file types
- Testing with sample files

## Building from Source

1. Clone the repository:
```bash
git clone https://github.com/dhhieu113/SaxonHECSharp.git
```

2. Build the solution:
```bash
dotnet build
```

3. Run the example project:
```bash
cd SaxonHECSharp.Example
dotnet run
```

## Platform Support

- Windows (x64)
- Linux (x64)
- macOS (arm64)

## License

This project is licensed under the MIT License - see the LICENSE file for details.
