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
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);
        
        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        private static extern IntPtr LoadLibraryPosix([MarshalAs(UnmanagedType.LPStr)] string fileName, int flags);
        
        [DllImport("libdl.dylib", EntryPoint = "dlopen")]
        private static extern IntPtr LoadLibraryMac([MarshalAs(UnmanagedType.LPStr)] string fileName, int flags);
        
        // Define a resolver delegate to match the expected signature
        public delegate IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath);
        
        // Dictionary to store resolvers
        private static readonly System.Collections.Generic.Dictionary<Assembly, DllImportResolver> Resolvers = 
            new System.Collections.Generic.Dictionary<Assembly, DllImportResolver>();
            
        /// <summary>
        /// Set a custom DllImport resolver for an assembly
        /// </summary>
        public static void SetDllImportResolver(Assembly assembly, DllImportResolver resolver)
        {
            Resolvers[assembly] = resolver;
        }
        
        /// <summary>
        /// Load a native library
        /// </summary>
        public static IntPtr Load(string libraryName)
        {
            IntPtr handle = IntPtr.Zero;
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                handle = LoadLibrary(libraryName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                handle = LoadLibraryPosix(libraryName, 2); // RTLD_NOW = 2
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                handle = LoadLibraryMac(libraryName, 2); // RTLD_NOW = 2
            }
            
            if (handle == IntPtr.Zero)
            {
                throw new DllNotFoundException($"Failed to load native library: {libraryName}");
            }
            
            return handle;
        }
    }
}
