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
    internal static class SaxonNative
    {
        private const string LibraryName = "saxonc-ee";
        private const string CoreLibraryName = "saxonc-core-ee";
        // Static constructor not needed as NativeLibraryLoader handles library loading

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
