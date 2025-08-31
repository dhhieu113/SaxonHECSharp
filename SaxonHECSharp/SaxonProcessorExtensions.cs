using System;
using System.IO;

namespace SaxonHECSharp
{
    public static class SaxonProcessorExtensions
    {
        /// <summary>
        /// Gets the native library directory path
        /// </summary>
        /// <param name="libDirectory">Optional directory to store the native libraries. If not specified, uses the default lib directory</param>
        /// <returns>The path to the native library directory</returns>
        public static string GetNativeLibraryDirectory(string? libDirectory = null)
        {
            return libDirectory ?? Path.Combine(AppContext.BaseDirectory, "lib");
        }
    }
}
