using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SaxonHECSharp.NativeInterop
{
    internal static class NativeLibraryLoader
    {
        public const string CoreLibraryName = "saxonc-core-ee";
        public const string LibraryName = "saxonc-ee";
        private static IntPtr _coreLibraryHandle;
        private static IntPtr _libraryHandle;

        private static readonly object LoadLock = new object();

        static NativeLibraryLoader()
        {
#if NETCOREAPP
            NativeLibrary.SetDllImportResolver(typeof(NativeLibraryLoader).Assembly, ResolveLibrary);
#endif
            LoadLibraries();
        }

        private static void LoadLibraries()
        {
            lock (LoadLock)
            {
                try
                {
                    if (_libraryHandle != IntPtr.Zero && _coreLibraryHandle != IntPtr.Zero)
                        return;

                    var libraryPaths = GetLibraryPaths();
                    
                    // Load core library first
                    _coreLibraryHandle = LoadNativeLibrary(libraryPaths.CoreLibPath);
                    if (_coreLibraryHandle == IntPtr.Zero)
                        throw new DllNotFoundException($"Failed to load core library: {libraryPaths.CoreLibPath}");
                    
                    // Then load main library
                    _libraryHandle = LoadNativeLibrary(libraryPaths.MainLibPath);
                    if (_libraryHandle == IntPtr.Zero)
                        throw new DllNotFoundException($"Failed to load main library: {libraryPaths.MainLibPath}");
                }
                catch (Exception ex)
                {
                    // Clean up if either library failed to load
                    if (_coreLibraryHandle != IntPtr.Zero)
                        FreeLibrary(_coreLibraryHandle);
                    if (_libraryHandle != IntPtr.Zero)
                        FreeLibrary(_libraryHandle);
                        
                    _coreLibraryHandle = IntPtr.Zero;
                    _libraryHandle = IntPtr.Zero;
                    
                    throw new DllNotFoundException($"Failed to load Saxon libraries: {ex.Message}", ex);
                }
            }
        }

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibraryW([MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

        [DllImport("libdl", EntryPoint = "dlopen")]
        private static extern IntPtr dlopen_unix(string fileName, int flags);

        [DllImport("libdl", EntryPoint = "dlerror")]
        private static extern IntPtr dlerror();

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("libdl")]
        private static extern int dlclose(IntPtr handle);

        private static void FreeLibrary(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!FreeLibrary(handle))
                    throw new InvalidOperationException($"Failed to free library. Error: {Marshal.GetLastWin32Error()}");
            }
            else
            {
                if (dlclose(handle) != 0)
                {
                    var error = Marshal.PtrToStringAnsi(dlerror());
                    throw new InvalidOperationException($"Failed to free library. Error: {error}");
                }
            }
        }

        private static (string CoreLibPath, string MainLibPath) GetLibraryPaths()
        {
            var rid = GetRuntimeIdentifier();
            var nativeDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", rid, "native");
            
            if (!Directory.Exists(nativeDir))
            {
                throw new DirectoryNotFoundException($"Native library directory not found: {nativeDir}");
            }

            var coreLibPath = Path.Combine(nativeDir, GetLibraryFileName(CoreLibraryName));
            var mainLibPath = Path.Combine(nativeDir, GetLibraryFileName(LibraryName));

            if (!File.Exists(coreLibPath))
                throw new FileNotFoundException($"Core library not found: {coreLibPath}");
            if (!File.Exists(mainLibPath))
                throw new FileNotFoundException($"Main library not found: {mainLibPath}");

            return (coreLibPath, mainLibPath);
        }

        private static IntPtr LoadNativeLibrary(string libraryPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var handle = LoadLibraryW(libraryPath);
                if (handle == IntPtr.Zero)
                {
                    throw new DllNotFoundException($"Failed to load {libraryPath}. Error: {Marshal.GetLastWin32Error()}");
                }
                return handle;
            }
            else
            {
                const int RTLD_NOW = 2;
                var handle = dlopen_unix(libraryPath, RTLD_NOW);
                if (handle == IntPtr.Zero)
                {
                    var error = Marshal.PtrToStringAnsi(dlerror());
                    throw new DllNotFoundException($"Failed to load {libraryPath}. Error: {error}");
                }
                return handle;
            }
        }

#if NETCOREAPP
        private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName == LibraryName)
                return _libraryHandle;
            if (libraryName == CoreLibraryName)
                return _coreLibraryHandle;
            return IntPtr.Zero;
        }
#endif

        private static string GetRuntimeIdentifier()
        {
            var architecture = RuntimeInformation.ProcessArchitecture;
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (architecture != Architecture.X64)
                    throw new PlatformNotSupportedException($"Unsupported architecture on Windows: {architecture}. Only x64 is supported.");
                return "win-x64";
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (architecture == Architecture.Arm64)
                    return "linux-arm64";
                if (architecture == Architecture.X64)
                    return "linux-x64";
                throw new PlatformNotSupportedException($"Unsupported architecture on Linux: {architecture}. Supported: x64, arm64");
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (architecture == Architecture.Arm64)
                    return "osx-arm64";
                if (architecture == Architecture.X64)
                    return "osx-x64";
                throw new PlatformNotSupportedException($"Unsupported architecture on macOS: {architecture}. Supported: x64, arm64");
            }

            throw new PlatformNotSupportedException(
                $"Unsupported platform: OS={RuntimeInformation.OSDescription}, " +
                $"Architecture={architecture}, OSVersion={Environment.OSVersion}");
        }

        private static string GetLibraryFileName(string libraryName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return $"{libraryName}.dll";
            
            string prefix = "lib";
            string extension = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".dylib" : ".so";
            return $"{prefix}{libraryName}{extension}";
        }
    }
}
