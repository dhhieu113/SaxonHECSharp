using System;
using System.IO;
using System.Linq;
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
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                Console.Error.WriteLine($"Checking Saxon libraries in: {Path.Combine(baseDir, "runtimes", GetRuntimeIdentifier(), "native")}");

                // Load core library first
                _coreHandle = LoadLibraryFromRuntimes(CoreLibraryName);
                if (_coreHandle == IntPtr.Zero)
                {
                    throw new DllNotFoundException($"Failed to load {CoreLibraryName} from runtimes folder");
                }

                // Load Saxon library
                _libraryHandle = LoadLibraryFromRuntimes(LibraryName);
                if (_libraryHandle == IntPtr.Zero)
                {
                    throw new DllNotFoundException($"Failed to load {LibraryName} from runtimes folder");
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

            if (!Directory.Exists(nativeDir))
            {
                Console.Error.WriteLine($"Native runtime dir not found: {nativeDir}");
                return IntPtr.Zero;
            }

            string ext = GetExtension();

            // Candidates to try in order:
            // 1) {libraryName}{ext}                 -> saxonc-core-ee.so
            // 2) lib{libraryName}{ext}              -> libsaxonc-core-ee.so
            // 3) any file starting with lib{libraryName} and extension -> libsaxonc-core-ee*.so (versioned)
            var candidates = new[]
            {
                Path.Combine(nativeDir, $"{libraryName}{ext}"),
                Path.Combine(nativeDir, $"lib{libraryName}{ext}")
            }.ToList();

            // add versioned matches: lib{libraryName}*{ext}
            try
            {
                string globPattern = $"lib{libraryName}*{ext}";
                var matched = Directory.GetFiles(nativeDir, globPattern);
                // Ensure the exact ones are not duplicated
                candidates.AddRange(matched.Where(p => !candidates.Contains(p)));
            }
            catch (Exception globEx)
            {
                // ignore glob errors, but log
                Console.Error.WriteLine($"Error while globbing for {libraryName} in {nativeDir}: {globEx.Message}");
            }

            Console.Error.WriteLine($"Attempting to load {libraryName} from {nativeDir}");
            foreach (var cand in candidates)
            {
                try
                {
                    if (File.Exists(cand))
                    {
                        var fi = new FileInfo(cand);
                        Console.Error.WriteLine($"Found library: {Path.GetFileName(cand)}");
                        Console.Error.WriteLine($"  Size: {fi.Length} bytes");
                        Console.Error.WriteLine($"  Last Write Time: {fi.LastWriteTime}");
                        Console.Error.WriteLine($"  Permissions: {GetFilePermissions(cand)}");

                        // Try loading with NativeLibrary.Load
                        try
                        {
                            IntPtr handle = NativeLibrary.Load(cand);
                            Console.Error.WriteLine($"Loaded {cand} (handle 0x{handle.ToString("x")})");
                            return handle;
                        }
                        catch (Exception loadEx)
                        {
                            Console.Error.WriteLine($"NativeLibrary.Load failed for {cand}: {loadEx.Message}");
                            // continue to next candidate
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"Candidate not found: {cand}");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error checking candidate {cand}: {ex}");
                }
            }

            // As a last resort, try letting the runtime resolve by name (no path) - might work if user put in LD_LIBRARY_PATH
            try
            {
                string probeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? $"{libraryName}{ext}"
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                        ? $"lib{libraryName}{ext}"
                        : $"lib{libraryName}{ext}";

                Console.Error.WriteLine($"Attempting NativeLibrary.Load by name: {probeName}");
                IntPtr h = NativeLibrary.Load(probeName);
                Console.Error.WriteLine($"Loaded by name {probeName} (handle 0x{h.ToString("x")})");
                return h;
            }
            catch (Exception nameLoadEx)
            {
                Console.Error.WriteLine($"NativeLibrary.Load by name failed: {nameLoadEx.Message}");
            }

            return IntPtr.Zero;
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

        private static string GetFilePermissions(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return $"{fi.Attributes}";
                // on Unix-like, try to run stat-ish information via FileInfo/Unix file mode
                return new System.Text.StringBuilder()
                    .Append(File.GetAttributes(path))
                    .ToString();
            }
            catch { return "<unknown>"; }
        }
    }
}
