using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SaxonHECSharp.NativeInterop
{
    internal static class SaxonNative
    {
        private static IntPtr _coreHandle;
        private static IntPtr _libraryHandle;

        // Function delegates
        public delegate int GraalCreateIsolateDelegate(IntPtr parameters, out IntPtr isolate, out IntPtr thread);
        public delegate IntPtr CreateSaxonProcessorDelegate(IntPtr thread, int license);
        public delegate void JGcDelegate(IntPtr thread);
        public delegate IntPtr CreateXslt30ProcessorDelegate(IntPtr thread);
        public delegate IntPtr JCompileFromFileDelegate(IntPtr thread, IntPtr xsltProc, string stylesheetFile, string baseUri, int closeAfterUse);
        public delegate IntPtr JTransformToFileDelegate(IntPtr thread, string outputFile, IntPtr executable, string sourceFile, string baseUri, string options);
        public delegate IntPtr JTransformToValueDelegate(IntPtr thread, IntPtr executable, string sourceFile, string baseUri);
        public delegate IntPtr JGetErrorMessageDelegate(IntPtr thread);

        // Function pointers
        public static GraalCreateIsolateDelegate graal_create_isolate;
        public static CreateSaxonProcessorDelegate createSaxonProcessor;
        public static JGcDelegate j_gc;
        public static CreateXslt30ProcessorDelegate createXslt30Processor;
        public static JCompileFromFileDelegate j_compileFromFile;
        public static JTransformToFileDelegate j_transformToFile;
        public static JTransformToValueDelegate j_transformToValue;
        public static JGetErrorMessageDelegate j_getErrorMessage;

        static SaxonNative()
        {
            try
            {
                // Determine actual library names and load them
                SetupLibraryNames();
                LoadNativeLibraries();
                LoadFunctionPointers();
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

            Console.WriteLine($"Looking for Saxon libraries in: {nativeDir}");
            var files = Directory.GetFiles(nativeDir);
            foreach (var file in files)
            {
                Console.WriteLine($"Found: {Path.GetFileName(file)}");
            }
        }

        private static void LoadNativeLibraries()
        {
            string rid = GetRuntimeIdentifier();
            string nativeDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "runtimes",
                rid,
                "native"
            );

            // Find actual library files
            string coreLibPath = FindActualLibraryPath(nativeDir, "saxonc-core-ee");
            string saxonLibPath = FindActualLibraryPath(nativeDir, "saxonc-ee");

            if (string.IsNullOrEmpty(coreLibPath))
                throw new FileNotFoundException($"Saxon core library not found in {nativeDir}");

            if (string.IsNullOrEmpty(saxonLibPath))
                throw new FileNotFoundException($"Saxon library not found in {nativeDir}");

            Console.WriteLine($"Loading core library: {coreLibPath}");
            Console.WriteLine($"Loading Saxon library: {saxonLibPath}");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LoadWindowsLibraries(coreLibPath, saxonLibPath);
            }
            else
            {
                LoadUnixLibraries(coreLibPath, saxonLibPath, nativeDir);
            }
        }

        private static void LoadWindowsLibraries(string coreLibPath, string saxonLibPath)
        {
            _coreHandle = LoadLibrary(coreLibPath);
            if (_coreHandle == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new DllNotFoundException($"Failed to load {coreLibPath} (error {err})");
            }

            _libraryHandle = LoadLibrary(saxonLibPath);
            if (_libraryHandle == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new DllNotFoundException($"Failed to load {saxonLibPath} (error {err})");
            }
        }

        private static void LoadUnixLibraries(string coreLibPath, string saxonLibPath, string nativeDir)
        {
            // Set library path for dependency resolution
            SetLibraryPath(nativeDir);

            // Load core library with RTLD_GLOBAL so symbols are available
            _coreHandle = dlopen(coreLibPath, RTLD_NOW | RTLD_GLOBAL);
            if (_coreHandle == IntPtr.Zero)
            {
                string error = GetDlError();
                throw new DllNotFoundException($"Failed to load {coreLibPath}: {error}");
            }

            // Load Saxon library
            _libraryHandle = dlopen(saxonLibPath, RTLD_NOW | RTLD_GLOBAL);
            if (_libraryHandle == IntPtr.Zero)
            {
                string error = GetDlError();
                throw new DllNotFoundException($"Failed to load {saxonLibPath}: {error}");
            }
        }

        private static void LoadFunctionPointers()
        {
            // Load function pointers from the loaded libraries
            graal_create_isolate = GetFunction<GraalCreateIsolateDelegate>(_coreHandle, "graal_create_isolate");
            createSaxonProcessor = GetFunction<CreateSaxonProcessorDelegate>(_coreHandle, "createSaxonProcessor");
            j_gc = GetFunction<JGcDelegate>(_coreHandle, "j_gc");
            createXslt30Processor = GetFunction<CreateXslt30ProcessorDelegate>(_coreHandle, "createXslt30Processor");
            j_compileFromFile = GetFunction<JCompileFromFileDelegate>(_coreHandle, "j_compileFromFile");
            j_transformToFile = GetFunction<JTransformToFileDelegate>(_coreHandle, "j_transformToFile");
            j_transformToValue = GetFunction<JTransformToValueDelegate>(_coreHandle, "j_transformToValue");
            j_getErrorMessage = GetFunction<JGetErrorMessageDelegate>(_coreHandle, "j_getErrorMessage");
        }

        private static T GetFunction<T>(IntPtr handle, string functionName) where T : class
        {
            IntPtr functionPtr = GetProcAddress(handle, functionName);
            if (functionPtr == IntPtr.Zero)
            {
                throw new EntryPointNotFoundException($"Function '{functionName}' not found in loaded library");
            }
            return Marshal.GetDelegateForFunctionPointer<T>(functionPtr);
        }

        private static IntPtr GetProcAddress(IntPtr handle, string functionName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetProcAddressWin32(handle, functionName);
            else
                return dlsym(handle, functionName);
        }

        private static string FindActualLibraryPath(string directory, string baseName)
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
        // Platform-specific imports
        // -------------------------

        // Windows
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetProcAddressWin32(IntPtr hModule, string procName);

        // Linux / macOS
        private const int RTLD_NOW = 2;
        private const int RTLD_GLOBAL = 8;

        [DllImport("libdl")]
        private static extern IntPtr dlopen(string fileName, int flags);

        [DllImport("libdl")]
        private static extern IntPtr dlerror();

        [DllImport("libdl")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

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