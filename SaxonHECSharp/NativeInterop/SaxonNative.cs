using System.Runtime.InteropServices;

namespace SaxonHECSharp.NativeInterop
{
    internal static class SaxonNative
    {
        private const string LibraryName = "saxonc-ee";
        private const string CoreLibraryName = "saxonc-core-ee";
        static SaxonNative()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string saxonDir = Path.Combine(baseDir, "SaxonCEE");

            // Add the SaxonCEE/bin directory to PATH so dependencies can be found
            string binPath = Path.Combine(saxonDir, "bin");
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            Environment.SetEnvironmentVariable("PATH", $"{binPath};{path}");

            // Load core library first
            string coreDllPath = Path.Combine(binPath, $"{CoreLibraryName}.dll");
            if (!NativeLibrary.TryLoad(coreDllPath, out _))
            {
                throw new DllNotFoundException($"Failed to load {CoreLibraryName} from {coreDllPath}");
            }
        }

        // Graal isolate functions
        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int graal_create_isolate(IntPtr parameters, out IntPtr isolate, out IntPtr thread);

        // Saxon functions
        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr createSaxonProcessor(IntPtr thread, int license);

        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void j_gc(IntPtr thread);

        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr createXslt30Processor(IntPtr thread);

        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_compileFromFile(IntPtr thread, IntPtr xsltProc, [MarshalAs(UnmanagedType.LPStr)] string stylesheetFile, [MarshalAs(UnmanagedType.LPStr)] string? baseUri, int closeAfterUse);

        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_transformToFile(IntPtr thread, [MarshalAs(UnmanagedType.LPStr)] string? outputFile, IntPtr executable, [MarshalAs(UnmanagedType.LPStr)] string sourceFile, [MarshalAs(UnmanagedType.LPStr)] string? baseUri, [MarshalAs(UnmanagedType.LPStr)] string? options);

        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_transformToValue(IntPtr thread, IntPtr executable, [MarshalAs(UnmanagedType.LPStr)] string sourceFile, [MarshalAs(UnmanagedType.LPStr)] string? baseUri);

        [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_getErrorMessage(IntPtr thread);

        // All functions are now using CoreLibraryName
    }
}
