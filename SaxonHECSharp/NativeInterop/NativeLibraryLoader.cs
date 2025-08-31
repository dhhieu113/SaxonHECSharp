using System.Reflection;
using System.Runtime.InteropServices;
using System.Reflection;

namespace SaxonHECSharp.NativeInterop
{
    internal static class NativeLibraryLoader
    {
        public const string CoreLibraryName = "saxonc-core-ee";
        public const string LibraryName = "saxonc-ee";
        private static IntPtr _coreLibraryHandle;
        private static IntPtr _libraryHandle;

        static NativeLibraryLoader()
        {
            // Register our custom library resolver
            NativeLibrary.SetDllImportResolver(typeof(NativeLibraryLoader).Assembly, ResolveLibrary);
            
            // Load the core library first
            LoadCoreLibrary();
        }

        private static void LoadCoreLibrary()
        {
            string rid = GetRuntimeIdentifier();
            string runtimePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "runtimes",
                rid,
                "native",
                GetLibraryFileName(CoreLibraryName));

            if (!NativeLibrary.TryLoad(runtimePath, out _coreLibraryHandle))
            {
                throw new DllNotFoundException($"Failed to load native core library: {CoreLibraryName}");
            }
        }

        private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != LibraryName)
                return IntPtr.Zero;

            if (_libraryHandle != IntPtr.Zero)
                return _libraryHandle;

            string rid = GetRuntimeIdentifier();
            string runtimePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "runtimes",
                rid,
                "native",
                GetLibraryFileName(LibraryName));

            if (!NativeLibrary.TryLoad(runtimePath, out _libraryHandle))
            {
                throw new DllNotFoundException($"Failed to load native library: {LibraryName}");
            }

            return _libraryHandle;
        }

        private static string GetRuntimeIdentifier()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "win-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";

            throw new PlatformNotSupportedException("Unsupported platform");
        }

        private static string GetLibraryFileName(string library)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return $"{library}.dll";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return $"lib{library}.so";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return $"lib{library}.dylib";

            throw new PlatformNotSupportedException("Unsupported platform");
        }
    }
}
