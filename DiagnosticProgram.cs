using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

class TestProgram
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Azure Storage Manager Diagnostic Tool ===");
        Console.WriteLine("Current directory: " + Directory.GetCurrentDirectory());
        
        // List all CS files in the main directory
        Console.WriteLine("\nCS files in main directory:");
        try {
            var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.cs", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                Console.WriteLine("- " + Path.GetFileName(file));
                
                // Read first few lines of each file to check for top-level statements
                try {
                    var firstLines = File.ReadLines(file).Take(5).ToList();
                    foreach (var line in firstLines)
                    {
                        if (!string.IsNullOrWhiteSpace(line) && 
                            !line.TrimStart().StartsWith("//") && 
                            !line.TrimStart().StartsWith("using") &&
                            !line.TrimStart().StartsWith("namespace"))
                        {
                            Console.WriteLine("  First non-comment, non-using, non-namespace line: " + line.Trim());
                            break;
                        }
                    }
                } catch (Exception ex) {
                    Console.WriteLine("  Error reading file: " + ex.Message);
                }
            }
        } catch (Exception ex) {
            Console.WriteLine("Error listing files: " + ex.Message);
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
