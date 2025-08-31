namespace SaxonHECSharp
{
    public static class SaxonProcessorExtensions
    {
        /// <summary>
        /// Downloads and sets up Saxon-C native libraries for the current platform
        /// </summary>
        /// <param name="libDirectory">Optional directory to store the native libraries. If not specified, uses the default lib directory</param>
        /// <returns>A task that completes when the setup is done</returns>
        public static async Task SetupNativeLibrariesAsync(string? libDirectory = null)
        {
            libDirectory ??= Path.Combine(AppContext.BaseDirectory, "lib");
            await Utils.SaxonDownloader.DownloadAndSetupAsync(libDirectory);
        }

        /// <summary>
        /// Downloads and sets up Saxon-C native libraries for a specific platform
        /// </summary>
        /// <param name="platform">The target platform to download libraries for</param>
        /// <param name="libDirectory">Optional directory to store the native libraries. If not specified, uses the default lib directory</param>
        /// <returns>A task that completes when the setup is done</returns>
        public static async Task SetupNativeLibrariesAsync(Utils.Platform platform, string? libDirectory = null)
        {
            libDirectory ??= Path.Combine(AppContext.BaseDirectory, "lib");
            await Utils.SaxonDownloader.DownloadAndSetupAsync(libDirectory, platform);
        }
    }
}
