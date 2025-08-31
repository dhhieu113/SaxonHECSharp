using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SaxonHECSharp.NativeInterop
{
    internal static class SaxonNative
    {

#if WINDOWS
    private const string CoreLibraryName = "saxonc-core-ee";
    private const string LibraryName = "saxonc-ee";
#else
        private const string CoreLibraryName = "libsaxonc-core-ee";
        private const string LibraryName = "libsaxonc-ee";
#endif

        private static IntPtr _coreHandle;
        private static IntPtr _libraryHandle;

        private const string LinuxLib = "libdl.so.2";
        private const string MacLib = "libdl.dylib";

        static SaxonNative()
        {
            try
            {
                // Load core library first
                _coreHandle = LoadLibraryFromRuntimes(CoreLibraryName);
                if (_coreHandle == IntPtr.Zero)
                {
                    // The custom exception in LoadLibraryCrossPlatform will give more details
                    throw new DllNotFoundException(
                        $"Failed to load {CoreLibraryName} from runtimes folder. See inner exception for details."
                    );
                }

                // Load Saxon library
                _libraryHandle = LoadLibraryFromRuntimes(LibraryName);
                if (_libraryHandle == IntPtr.Zero)
                {
                    throw new DllNotFoundException(
                        $"Failed to load {LibraryName} from runtimes folder. See inner exception for details."
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

            string candidate1 = Path.Combine(nativeDir, $"{libraryName}{GetExtension()}");
            //string candidate2 = Path.Combine(nativeDir, $"lib{libraryName}{GetExtension()}");

            if (File.Exists(candidate1))
                return LoadLibraryCrossPlatform(candidate1, nativeDir, libraryName);

            //if (File.Exists(candidate2))
            //    return LoadLibraryCrossPlatform(candidate2, nativeDir, libraryName);

            throw new DllNotFoundException($"Could not find native library {libraryName} in {nativeDir} for RID {rid}. Looked for {candidate1}.");
        }

        private static string GetExtension()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return ".dll";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return ".so";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return ".dylib";
            throw new PlatformNotSupportedException("Unsupported platform");
        }

        private static string GetRuntimeIdentifier()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "win-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
            throw new PlatformNotSupportedException("Unsupported platform");
        }

        private static IntPtr LoadLibraryCrossPlatform(string path,
            string nativeDir,
            string libraryName)
        {
            IntPtr handle = IntPtr.Zero;
            string errorMessage = "Unknown error";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                handle = LoadLibrary(path);
                if (handle == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    errorMessage = $"Win32 error code: {errorCode}";
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string versioned = Path.Combine(nativeDir, $"{libraryName}.so.12.8.0");
                string soname = Path.Combine(nativeDir, $"{libraryName}.so.12");
                try
                {
                    var ln = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ln",
                        Arguments = $"-s \"{versioned}\" \"{soname}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var proc = System.Diagnostics.Process.Start(ln);
                    proc.WaitForExit();

                    if (proc.ExitCode != 0)
                    {
                        string error = proc.StandardError.ReadToEnd();
                        Console.Error.WriteLine($"Failed to create symlink for {libraryName}: {error}");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Exception while creating symlink: {ex}");
                }

                dlerror_linux(); // Clear any existing error
                if (File.Exists(soname))
                    handle = LoadLibraryLinux(soname, RTLD_NOW);

                // Fallback to versioned file
                if (File.Exists(versioned))
                    handle = LoadLibraryLinux(versioned, RTLD_NOW);

                if (handle == IntPtr.Zero)
                {
                    IntPtr errorPtr = dlerror_linux();
                    errorMessage = Marshal.PtrToStringAnsi(errorPtr) ?? "dlerror returned null";
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string soname = Path.Combine(nativeDir, $"{libraryName}.12.dylib");
                string versioned = Path.Combine(nativeDir, $"{libraryName}.12.8.0.dylib");

                Symlink(path, soname);
                Symlink(soname, versioned);

                dlerror_mac(); // Clear any existing error
                handle = LoadLibraryMac(path, RTLD_NOW | RTLD_GLOBAL); // Try loading the exact path first

                if (File.Exists(soname))
                    handle = LoadLibraryMac(soname, RTLD_NOW | RTLD_GLOBAL);

                // Fallback to versioned file
                if (File.Exists(versioned))
                    handle = LoadLibraryMac(versioned, RTLD_NOW | RTLD_GLOBAL);

                if (handle == IntPtr.Zero)
                {
                    IntPtr errorPtr = dlerror_mac();
                    errorMessage = Marshal.PtrToStringAnsi(errorPtr) ?? "dlerror returned null";
                }
            }

            if (handle == IntPtr.Zero)
            {
                throw new DllNotFoundException($"Failed to load native library: {path}. Error: {errorMessage}");
            }

            return handle;
        }

        private static void Symlink(string versioned, string soname)
        {
            try
            {
                var ln = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ln",
                    Arguments = $"-s \"{versioned}\" \"{soname}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = System.Diagnostics.Process.Start(ln);
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                {
                    string error = proc.StandardError.ReadToEnd();
                    Console.Error.WriteLine($"Failed to create symlink for {versioned} -> {soname}: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception while creating symlink: {ex}");
            }

        }

        // Windows
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        // POSIX flags for dlopen
        private const int RTLD_NOW = 2;
        private const int RTLD_GLOBAL = 0x100;

        // Linux dlopen/dlerror
        [DllImport(LinuxLib, EntryPoint = "dlopen")]
        private static extern IntPtr LoadLibraryLinux([MarshalAs(UnmanagedType.LPStr)] string fileName, int flags);

        [DllImport(LinuxLib, EntryPoint = "dlerror")]
        private static extern IntPtr dlerror_linux();

        // macOS dlopen/dlerror
        [DllImport(MacLib, EntryPoint = "dlopen")]
        private static extern IntPtr LoadLibraryMac([MarshalAs(UnmanagedType.LPStr)] string fileName, int flags);

        [DllImport(MacLib, EntryPoint = "dlerror")]
        private static extern IntPtr dlerror_mac();
    }
}
