using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;

namespace SaxonHECSharp.NativeInterop
{
    internal static class NativeLibraryLoader
    {
        private const string CoreLibraryName = "saxonc-core-ee";
        private const string LibraryName = "saxonc-ee";
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
                if (_libraryHandle != IntPtr.Zero && _coreLibraryHandle != IntPtr.Zero)
                    return;

                var rid = GetRuntimeIdentifier();
                var searchPaths = new[] {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", rid, "native"),
                    AppDomain.CurrentDomain.BaseDirectory
                };

                string coreLibPath = null;
                string mainLibPath = null;

                foreach (var path in searchPaths)
                {
                    if (!Directory.Exists(path))
                        continue;

                    var coreName = GetLibraryFileName(CoreLibraryName);
                    var mainName = GetLibraryFileName(LibraryName);

                    var testCorePath = Path.Combine(path, coreName);
                    var testMainPath = Path.Combine(path, mainName);

                    if (File.Exists(testCorePath))
                        coreLibPath = testCorePath;
                    if (File.Exists(testMainPath))
                        mainLibPath = testMainPath;

                    if (coreLibPath != null && mainLibPath != null)
                        break;
                }

                if (coreLibPath == null || mainLibPath == null)
                    throw new DllNotFoundException($"Saxon libraries not found in search paths: {string.Join(", ", searchPaths)}");

                try
                {
                    _coreLibraryHandle = LoadNativeLibrary(coreLibPath);
                    _libraryHandle = LoadNativeLibrary(mainLibPath);
                }
                catch (Exception ex)
                {
                    if (_coreLibraryHandle != IntPtr.Zero)
                        ReleaseLibrary(_coreLibraryHandle);
                    if (_libraryHandle != IntPtr.Zero)
                        ReleaseLibrary(_libraryHandle);

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

        private static void ReleaseLibrary(IntPtr handle)
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
            var searchPaths = new List<string>();
            var rid = GetRuntimeIdentifier();
            
            // Add standard search paths
            searchPaths.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", rid, "native"));
            searchPaths.Add(AppDomain.CurrentDomain.BaseDirectory);
            
            // Add macOS specific paths
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                searchPaths.Add("/usr/local/lib");
                searchPaths.Add("/usr/lib");
                
                // Add custom environment variable path if specified
                var saxonLibPath = Environment.GetEnvironmentVariable("SAXON_LIB_PATH");
                if (!string.IsNullOrEmpty(saxonLibPath))
                    searchPaths.Add(saxonLibPath);
            }

            string coreLibPath = null;
            string mainLibPath = null;

            foreach (var path in searchPaths)
            {
                if (!Directory.Exists(path))
                    continue;

                var coreName = GetLibraryFileName(CoreLibraryName);
                var mainName = GetLibraryFileName(LibraryName);
                
                var testCorePath = Path.Combine(path, coreName);
                var testMainPath = Path.Combine(path, mainName);

                if (File.Exists(testCorePath))
                    coreLibPath = testCorePath;
                if (File.Exists(testMainPath))
                    mainLibPath = testMainPath;

                if (coreLibPath != null && mainLibPath != null)
                    break;
            }

            if (coreLibPath == null)
                throw new FileNotFoundException($"Core library not found in search paths: {string.Join(", ", searchPaths)}");
            if (mainLibPath == null)
                throw new FileNotFoundException($"Main library not found in search paths: {string.Join(", ", searchPaths)}");

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
                const int RTLD_GLOBAL = 8;
                var handle = dlopen_unix(libraryPath, RTLD_NOW | RTLD_GLOBAL);
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
