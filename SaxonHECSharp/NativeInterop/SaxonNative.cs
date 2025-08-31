using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SaxonHECSharp.NativeInterop
{
    internal static class SaxonNative
    {
        private static string _actualCoreLibraryName;
        private static string _actualLibraryName;
        private static IntPtr _coreHandle;
        private static IntPtr _libraryHandle;

        static SaxonNative()
        {
            try
            {
                // Determine actual library names and load them
                SetupLibraryNames();
                LoadNativeLibraries();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SaxonNative init failed: " + ex);
                throw;
            }
        }

        private static void SetupLibraryNames()
        {
            string rid = GetRuntimeIdentifier();
            string nativeDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "runtimes",
                rid,
                "native"
            );

            if (!Directory.Exists(nativeDir))
            {
                throw new DirectoryNotFoundException($"Native library directory not found: {nativeDir}");
            }

            // Find the actual library files (they may have version numbers)
            _actualCoreLibraryName = FindActualLibraryName(nativeDir, "saxonc-core-ee");
            _actualLibraryName = FindActualLibraryName(nativeDir, "saxonc-ee");

            if (string.IsNullOrEmpty(_actualCoreLibraryName))
            {
                throw new FileNotFoundException($"Saxon core library not found in {nativeDir}");
            }

            if (string.IsNullOrEmpty(_actualLibraryName))
            {
                throw new FileNotFoundException($"Saxon library not found in {nativeDir}");
            }
        }

        private static string FindActualLibraryName(string directory, string baseName)
        {
            string prefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "" : "lib";
            string extension = GetLibraryExtension();

            // Try exact match first
            string exactName = $"{prefix}{baseName}{extension}";
            string exactPath = Path.Combine(directory, exactName);
            if (File.Exists(exactPath))
                return exactPath;

            // Try versioned libraries
            string pattern = $"{prefix}{baseName}.*{extension}";
            var files = Directory.GetFiles(directory, pattern);

            if (files.Length > 0)
            {
                // Prefer the one without version numbers if multiple exist
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName == exactName)
                        return file;
                }
                // Otherwise return the first versioned one
                return files[0];
            }

            return null;
        }

        private static void LoadNativeLibraries()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LoadWindowsLibraries();
            }
            else
            {
                LoadUnixLibraries();
            }
        }

        private static void LoadWindowsLibraries()
        {
            _coreHandle = LoadLibrary(_actualCoreLibraryName);
            if (_coreHandle == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new DllNotFoundException($"Failed to load {_actualCoreLibraryName} (error {err})");
            }

            _libraryHandle = LoadLibrary(_actualLibraryName);
            if (_libraryHandle == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new DllNotFoundException($"Failed to load {_actualLibraryName} (error {err})");
            }
        }

        private static void LoadUnixLibraries()
        {
            // Set library path for dependency resolution
            string nativeDir = Path.GetDirectoryName(_actualCoreLibraryName);
            SetLibraryPath(nativeDir);

            // Load core library
            _coreHandle = dlopen(_actualCoreLibraryName, RTLD_NOW | RTLD_GLOBAL);
            if (_coreHandle == IntPtr.Zero)
            {
                string error = GetDlError();
                throw new DllNotFoundException($"Failed to load {_actualCoreLibraryName}: {error}");
            }

            // Load Saxon library
            _libraryHandle = dlopen(_actualLibraryName, RTLD_NOW | RTLD_GLOBAL);
            if (_libraryHandle == IntPtr.Zero)
            {
                string error = GetDlError();
                throw new DllNotFoundException($"Failed to load {_actualLibraryName}: {error}");
            }
        }

        private static void SetLibraryPath(string nativeDir)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Set DYLD_LIBRARY_PATH for macOS
                string currentPath = Environment.GetEnvironmentVariable("DYLD_LIBRARY_PATH");
                string newPath = string.IsNullOrEmpty(currentPath) ? nativeDir : $"{nativeDir}:{currentPath}";
                Environment.SetEnvironmentVariable("DYLD_LIBRARY_PATH", newPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Set LD_LIBRARY_PATH for Linux
                string currentPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
                string newPath = string.IsNullOrEmpty(currentPath) ? nativeDir : $"{nativeDir}:{currentPath}";
                Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", newPath);
            }
        }

        private static string GetDlError()
        {
            IntPtr errorPtr = dlerror();
            return errorPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errorPtr) : "Unknown error";
        }

        // -------------------------
        // Graal isolate functions
        // -------------------------
        [DllImport("saxonc-core-ee", CallingConvention = CallingConvention.Cdecl)]
        public static extern int graal_create_isolate(IntPtr parameters, out IntPtr isolate, out IntPtr thread);

        // -------------------------
        // Saxon functions
        // -------------------------
        [DllImport("saxonc-core-ee", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr createSaxonProcessor(IntPtr thread, int license);

        [DllImport("saxonc-core-ee", CallingConvention = CallingConvention.Cdecl)]
        public static extern void j_gc(IntPtr thread);

        [DllImport("saxonc-core-ee", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr createXslt30Processor(IntPtr thread);

        [DllImport("saxonc-core-ee", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_compileFromFile(
            IntPtr thread,
            IntPtr xsltProc,
            [MarshalAs(UnmanagedType.LPStr)] string stylesheetFile,
            [MarshalAs(UnmanagedType.LPStr)] string baseUri,
            int closeAfterUse);

        [DllImport("saxonc-core-ee", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_transformToFile(
            IntPtr thread,
            [MarshalAs(UnmanagedType.LPStr)] string outputFile,
            IntPtr executable,
            [MarshalAs(UnmanagedType.LPStr)] string sourceFile,
            [MarshalAs(UnmanagedType.LPStr)] string baseUri,
            [MarshalAs(UnmanagedType.LPStr)] string options);

        [DllImport("saxonc-core-ee", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_transformToValue(
            IntPtr thread,
            IntPtr executable,
            [MarshalAs(UnmanagedType.LPStr)] string sourceFile,
            [MarshalAs(UnmanagedType.LPStr)] string baseUri);

        [DllImport("saxonc-core-ee", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_getErrorMessage(IntPtr thread);

        // -------------------------
        // Platform-specific imports
        // -------------------------

        // Windows
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        // Linux / macOS
        private const int RTLD_NOW = 2;
        private const int RTLD_GLOBAL = 8;

        [DllImport("libdl")]
        private static extern IntPtr dlopen(string fileName, int flags);

        [DllImport("libdl")]
        private static extern IntPtr dlerror();

        // -------------------------
        // Helper methods
        // -------------------------
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

        private static string GetLibraryExtension()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return ".dll";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return ".so";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return ".dylib";

            throw new PlatformNotSupportedException("Unsupported platform");
        }
    }
}