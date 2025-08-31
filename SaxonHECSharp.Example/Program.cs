using SaxonHECSharp;
using SaxonHECSharp.Utils;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting SaxonHECSharp Example...");

        // Setup test cases
        var testCases = new[]
        {
            ("books.xml", "books.xsl", "books_output.html"),
            ("catalog.xml", "identity.xsl", "catalog_output.xml"),
            ("example.xml", "example.xsl", "example_output.html"),
            ("family.xml", "test.xsl", "family_output.xml"),
            ("othello.xml", "identity.xsl", "othello_output.xml")
        };

        // Allow command line arguments to override defaults
        if (args.Length >= 2)
        {
            await RunTransform(args[0], args[1]);
        }
        else
        {
            // Run all test cases
            Console.WriteLine("Running all test cases...\n");
            foreach (var (xml, xsl, output) in testCases)
            {
                Console.WriteLine($"\nTesting transformation: {xml} with {xsl}");
                await RunTransform(xml, xsl);
            }
        }

        // Set up the native libraries in runtimes directory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        try
        {
            await SaxonDownloader.DownloadAndSetupAsync(baseDir);
            Console.WriteLine("Saxon libraries downloaded and set up successfully.\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set up Saxon libraries: {ex.Message}");
            return;
        }
    }

    private static async Task RunTransform(string xmlFile, string xslFile)
    {
        try
        {
            // Initialize the Saxon processor
            using var processor = new SaxonProcessor();
            var xsltProc = processor.CreateXsltProcessor();

            // Get paths to test files
            string projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            string testDataDir = Path.Combine(projectDir, "test-data");
            string xsltPath = Path.Combine(testDataDir, xslFile);
            string xmlPath = Path.Combine(testDataDir, xmlFile);
            string outputName = Path.GetFileNameWithoutExtension(xmlFile) + "_output" + Path.GetExtension(xslFile);
            string outputPath = Path.Combine(testDataDir, outputName);

            Console.WriteLine($"Input XML: {xmlFile}");
            Console.WriteLine($"XSLT: {xslFile}");

            // Compile the XSLT stylesheet 
            xsltProc.CompileStylesheet(xsltPath);

            // Perform the transformation
            if (xsltProc.Transform(xmlPath, outputPath))
            {
                Console.WriteLine($"✓ Transformation successful - Output: {outputName}");
            }
            else
            {
                Console.WriteLine($"✗ Transformation failed for {xmlFile}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error transforming {xmlFile}: {ex.Message}");
        }
    }
}
