using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QualCompareCLI;

/// <summary>
/// Main entry point for QualCompare CLI.
/// Reads a JSON render configuration and executes cross-platform rendering via Blender.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ParseArguments(args);

            if (options == null)
                return PrintUsageAndExit();

            var logger = new ConsoleLogger(options.Verbose);

            // Load and validate configuration
            var config = LoadConfiguration(options.ConfigPath, logger);
            if (config == null)
                return 1;

            // Validate environment (paths, directories)
            var renderService = new BlenderRenderService(config, logger);
            if (!renderService.ValidateEnvironment())
            {
                logger.LogError("Environment validation failed. Check paths and installation.");
                return 1;
            }

            // Discover objects to render
            var objects = renderService.DiscoverObjects();
            if (objects.Length == 0)
            {
                logger.LogWarning("No objects found matching the configured criteria.");
                return 0;
            }

            // Render each object
            logger.LogInfo($"Starting render pipeline for {objects.Length} object(s)...");

            int successCount = 0;
            int failureCount = 0;

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                logger.LogWarning("Cancellation requested...");
                cts.Cancel();
            };

            // Sequential rendering at queue level (parallel would require SSD staging)
            foreach (var objectPath in objects)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    logger.LogWarning("Rendering interrupted by user.");
                    break;
                }

                var objectName = Path.GetFileNameWithoutExtension(objectPath);
                logger.LogInfo($"Rendering: {objectName}");

                var success = await renderService.RenderObjectAsync(objectPath, cts.Token);
                if (success)
                    successCount++;
                else
                    failureCount++;
            }

            // Summary
            logger.LogInfo($"Rendering complete: {successCount} succeeded, {failureCount} failed.");

            return failureCount > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Unexpected error: {ex.Message}");
            if (Environment.GetEnvironmentVariable("DEBUG") == "1")
                Console.Error.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            return 1;
        }
    }

    /// <summary>
    /// Parse command-line arguments.
    /// </summary>
    private static CliOptions? ParseArguments(string[] args)
    {
        var options = new CliOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--config":
                case "-c":
                    if (i + 1 < args.Length)
                        options.ConfigPath = args[++i];
                    break;

                case "--verbose":
                case "-v":
                    options.Verbose = true;
                    break;

                case "--help":
                case "-h":
                    return null; // Signal to print usage

                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(options.ConfigPath))
        {
            Console.Error.WriteLine("Error: --config path is required.");
            return null;
        }

        return options;
    }

    /// <summary>
    /// Load render configuration from JSON file.
    /// </summary>
    private static RenderConfig? LoadConfiguration(string configPath, ILogger logger)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                logger.LogError($"Configuration file not found: {configPath}");
                return null;
            }

            var json = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<RenderConfig>(json);

            if (config == null)
            {
                logger.LogError("Failed to deserialize configuration JSON.");
                return null;
            }

            logger.LogInfo($"Configuration loaded from {configPath} (schema v{config.SchemaVersion})");
            return config;
        }
        catch (JsonException ex)
        {
            logger.LogError($"JSON parsing error: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error loading configuration: {ex.Message}");
            return null;
        }
    }

    private static int PrintUsageAndExit()
    {
        Console.WriteLine(
            @"QualCompare CLI v1.0.0
Cross-platform batch rendering of 3D objects with Blender

Usage:
  qualcompare-cli --config <path> [--verbose] [--help]

Options:
  -c, --config <path>    Path to JSON configuration file (required)
  -v, --verbose          Enable verbose logging (debug output)
  -h, --help             Show this help message

Example:
  qualcompare-cli --config render_job.json --verbose

Configuration file format:
    See QualCompareCLI/CONFIG_SCHEMA.md for detailed JSON schema documentation.
");
        return 2;
    }

    private class CliOptions
    {
        public string ConfigPath { get; set; } = "";
        public bool Verbose { get; set; } = false;
    }
}
