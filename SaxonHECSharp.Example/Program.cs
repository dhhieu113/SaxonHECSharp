using SaxonHECSharp;
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting SaxonHECSharp Example...");

        // Setup test cases
        var testCases = new[]
        {
            ("books.xml", "books.xsl", "books_output.html")
        };

        var testDataCases = new[]
        {
            ("catalog.xml", "identity.xsl", "catalog_output.xml"),
            ("example.xml", "example.xsl", "example_output.html"),
            ("family.xml", "test.xsl", "family_output.xml"),
            ("othello.xml", "identity.xsl", "othello_output.xml")
        };

        try
        {
            // Allow command line arguments to override defaults
            if (args.Length >= 2)
            {
                await RunTransform(args[0], args[1]);
            }
            else
            {
                // Run all test cases
                Console.WriteLine("Running all test cases...\n");
                foreach (var (xml, xsl, _) in testCases)
                {
                    Console.WriteLine($"\nTesting transformation: {xml} with {xsl}");
                    await RunTransform(xml, xsl);
                }

                Console.WriteLine("Running all test data cases...\n");
                foreach (var (xml, xsl, _) in testCases)
                {
                    Console.WriteLine($"\nTesting data transformation: {xml} with {xsl}");
                    await RunTransform($"test-data/{xml}", $"test-data/{xsl}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }

        await Task.Delay(10); // Add a meaningful asynchronous operation to resolve the warning.
    }

    private static async Task RunTransform(string xmlFile, string xslFile)
    {
        try
        {
            // Initialize the Saxon processor
            using var processor = new SaxonHE();

            // Get paths to files
            var baseDir = AppContext.BaseDirectory;
            string xsltPath = Path.Combine(baseDir, xslFile);
            string xmlPath = Path.Combine(baseDir, xmlFile);
            string outputName = Path.GetFileNameWithoutExtension(xmlFile) + "_output" + Path.GetExtension(xslFile);
            string outputPath = Path.Combine(baseDir, outputName);

            Console.WriteLine($"Input XML: {xmlFile}");
            Console.WriteLine($"XSLT: {xslFile}");

            // Compile the XSLT stylesheet 

            // Perform the transformation
            if (processor.Transform(xmlPath, xsltPath, outputPath))
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
            var exOrigin = ex.InnerException ?? ex;
            Console.WriteLine($"✗ Error transforming {xmlFile}: {exOrigin.Message}");
            throw;
        }
    }
}
