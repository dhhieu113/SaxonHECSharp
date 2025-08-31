using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SaxonHECSharp.NativeInterop
{
    internal static class SaxonNative
    {
        private const string CoreLibraryName = "saxonc-core-ee";
        private const string LibraryName = "saxonc-ee";

        private static IntPtr _coreHandle;
        private static IntPtr _libraryHandle;

        static SaxonNative()
        {
            try
            {
                // Load core library first
                _coreHandle = LoadLibraryFromRuntimes(CoreLibraryName);
                if (_coreHandle == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new DllNotFoundException(
                        $"Failed to load {CoreLibraryName} from runtimes folder (error {err})"
                    );
                }

                // Load Saxon library
                _libraryHandle = LoadLibraryFromRuntimes(LibraryName);
                if (_libraryHandle == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new DllNotFoundException(
                        $"Failed to load {LibraryName} from runtimes folder (error {err})"
                    );
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SaxonNative init failed: " + ex);
                throw;
            }
        }

        // -------------------------
        // Graal isolate functions
        // -------------------------
        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int graal_create_isolate(IntPtr parameters, out IntPtr isolate, out IntPtr thread);

        // -------------------------
        // Saxon functions
        // -------------------------
        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr createSaxonProcessor(IntPtr thread, int license);

        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void j_gc(IntPtr thread);

        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr createXslt30Processor(IntPtr thread);

        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_compileFromFile(
            IntPtr thread,
            IntPtr xsltProc,
            [MarshalAs(UnmanagedType.LPStr)] string stylesheetFile,
            [MarshalAs(UnmanagedType.LPStr)] string baseUri,
            int closeAfterUse);

        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_transformToFile(
            IntPtr thread,
            [MarshalAs(UnmanagedType.LPStr)] string outputFile,
            IntPtr executable,
            [MarshalAs(UnmanagedType.LPStr)] string sourceFile,
            [MarshalAs(UnmanagedType.LPStr)] string baseUri,
            [MarshalAs(UnmanagedType.LPStr)] string options);

        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_transformToValue(
            IntPtr thread,
            IntPtr executable,
            [MarshalAs(UnmanagedType.LPStr)] string sourceFile,
            [MarshalAs(UnmanagedType.LPStr)] string baseUri);

        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_getErrorMessage(IntPtr thread);

        // -------------------------
        // Helpers
        // -------------------------
        private static IntPtr LoadLibraryFromRuntimes(string libraryName)
        {
            string rid = GetRuntimeIdentifier();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string nativeDir = Path.Combine(baseDir, "runtimes", rid, "native");

            // Try without lib prefix
            string candidate1 = Path.Combine(nativeDir, $"{libraryName}{GetExtension()}");
            // Try with lib prefix (Linux/macOS convention)
            string candidate2 = Path.Combine(nativeDir, $"lib{libraryName}{GetExtension()}");



            if (File.Exists(candidate1))
                return LoadLibraryCrossPlatform(candidate1);

            if (File.Exists(candidate2))
                return LoadLibraryCrossPlatform(candidate2);

            return IntPtr.Zero;
        }

        private static string GetExtension()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return ".dll";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return ".so";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return ".dylib";

            throw new PlatformNotSupportedException("Unsupported platform");
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

        private static IntPtr LoadLibraryCrossPlatform(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return LoadLibrary(path);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return dlopen(path, RTLD_NOW);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return dlopen(path, RTLD_NOW);

            throw new PlatformNotSupportedException();
        }


        // Windows
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        private const int RTLD_NOW = 2;

        [DllImport("libdl.so.2")]
        private static extern IntPtr dlopen(string fileName, int flags);

        [DllImport("libdl.so.2")]
        private static extern IntPtr dlerror();
    }
}
