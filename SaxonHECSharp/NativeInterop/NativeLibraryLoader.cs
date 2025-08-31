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
                if (_libraryHandle != IntPtr.Zero && _coreLibraryHandle != IntPtr.Zero)
                    return;

                var libraryPaths = GetLibraryPaths();
                _coreLibraryHandle = LoadNativeLibrary(libraryPaths.CoreLibPath);
                _libraryHandle = LoadNativeLibrary(libraryPaths.MainLibPath);
            }
        }

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibraryW([MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

        [DllImport("libdl", EntryPoint = "dlopen")]
        private static extern IntPtr dlopen_unix(string fileName, int flags);

        [DllImport("libdl", EntryPoint = "dlerror")]
        private static extern IntPtr dlerror();

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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "win-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";

            throw new PlatformNotSupportedException($"Unsupported OS: {RuntimeInformation.OSDescription}");
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
