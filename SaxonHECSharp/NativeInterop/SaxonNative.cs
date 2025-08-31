using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Collections.Generic;
using System.Threading.Tasks;

#if NETCOREAPP
using System.Runtime.Loader;
#endif

namespace SaxonHECSharp.NativeInterop
{
    internal static partial class SaxonNative
    {
        static SaxonNative()
        {
            NativeLibraryLoader.SetDllImportResolver(typeof(SaxonNative).Assembly);
        }

        internal const string SaxonCLibrary = "saxonc-core-ee";

#if NETCOREAPP
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
#endif
        [DllImport(SaxonCLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sxnc_initialise")]
        public static extern int Initialise();

        // Graal isolate functions
        [DllImport(SaxonCLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern int graal_create_isolate(IntPtr parameters, out IntPtr isolate, out IntPtr thread);

        // Saxon functions
        [DllImport(SaxonCLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr createSaxonProcessor(IntPtr thread, int license);

        [DllImport(SaxonCLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern void j_gc(IntPtr thread);

        [DllImport(SaxonCLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr createXslt30Processor(IntPtr thread);

        [DllImport(SaxonCLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_compileFromFile(IntPtr thread, IntPtr xsltProc, [MarshalAs(UnmanagedType.LPStr)] string stylesheetFile, [MarshalAs(UnmanagedType.LPStr)] string? baseUri, int closeAfterUse);

        [DllImport(SaxonCLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_transformToFile(IntPtr thread, [MarshalAs(UnmanagedType.LPStr)] string? outputFile, IntPtr executable, [MarshalAs(UnmanagedType.LPStr)] string sourceFile, [MarshalAs(UnmanagedType.LPStr)] string? baseUri, [MarshalAs(UnmanagedType.LPStr)] string? options);

        [DllImport(SaxonCLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_transformToValue(IntPtr thread, IntPtr executable, [MarshalAs(UnmanagedType.LPStr)] string sourceFile, [MarshalAs(UnmanagedType.LPStr)] string? baseUri);

        [DllImport(SaxonCLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr j_getErrorMessage(IntPtr thread);

        // All functions are now using CoreLibraryName
    }
}
