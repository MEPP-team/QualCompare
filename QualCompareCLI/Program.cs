using Newtonsoft.Json;
using System.Diagnostics;
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
        var overallStopwatch = Stopwatch.StartNew();
        ILogger? logger = null;
        bool renderPipelineStarted = false;
        int exitCode = 1;

        try
        {
            var options = ParseArguments(args);

            if (options == null)
            {
                exitCode = PrintUsageAndExit();
                return exitCode;
            }

            logger = new ConsoleLogger(options.Verbose);

            if (!string.IsNullOrWhiteSpace(options.PatchifyPath))
            {
                exitCode = RunPatchifyMode(options.PatchifyPath, logger);
                return exitCode;
            }

            // Load and validate configuration
            var config = LoadConfiguration(options.ConfigPath, logger);
            if (config == null)
            {
                exitCode = 1;
                return exitCode;
            }

            // Validate environment (paths, directories)
            var renderService = new BlenderRenderService(config, logger);
            if (!renderService.ValidateEnvironment())
            {
                logger.LogError("Environment validation failed. Check paths and installation.");
                exitCode = 1;
                return exitCode;
            }

            // Discover objects to render
            var objects = renderService.DiscoverObjects();
            if (objects.Length == 0)
            {
                logger.LogWarning("No objects found matching the configured criteria.");
                exitCode = 0;
                return exitCode;
            }

            // Render each object
            logger.LogInfo($"Starting render pipeline for {objects.Length} object(s)...");
            renderPipelineStarted = true;
            int maxParallelism = GetEffectiveMaxParallelism(config);
            logger.LogInfo($"Using max parallelism: {maxParallelism}");

            int successCount = 0;
            int failureCount = 0;
            int completedCount = 0;

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                logger.LogWarning("Cancellation requested...");
                cts.Cancel();
            };

            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxParallelism,
                    CancellationToken = cts.Token
                };

                await Parallel.ForEachAsync(objects, parallelOptions, async (objectPath, ct) =>
                {
                    var objectName = Path.GetFileNameWithoutExtension(objectPath);
                    logger.LogInfo($"Rendering: {objectName}");

                    var success = await renderService.RenderObjectAsync(objectPath, ct);
                    if (success)
                        Interlocked.Increment(ref successCount);
                    else
                        Interlocked.Increment(ref failureCount);

                    int completed = Interlocked.Increment(ref completedCount);
                    logger.LogInfo($"Progress: {completed}/{objects.Length}");
                });
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Rendering interrupted by user.");
            }

            // Summary
            logger.LogInfo($"Rendering complete: {successCount} succeeded, {failureCount} failed.");

            exitCode = failureCount > 0 ? 1 : 0;
            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Unexpected error: {ex.Message}");
            if (Environment.GetEnvironmentVariable("DEBUG") == "1")
                Console.Error.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            exitCode = 1;
            return exitCode;
        }
        finally
        {
            if (overallStopwatch.IsRunning)
            {
                overallStopwatch.Stop();
                if (renderPipelineStarted)
                {
                    var totalMessage = $"Total render generation time: {FormatElapsed(overallStopwatch.Elapsed)}";
                    if (logger != null)
                        logger.LogInfo(totalMessage);
                    else
                        Console.Error.WriteLine($"[INFO] {totalMessage}");
                }
            }
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

                case "--patchify":
                case "-p":
                    if (i + 1 < args.Length)
                        options.PatchifyPath = args[++i];
                    break;

                case "--help":
                case "-h":
                    return null; // Signal to print usage

                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(options.ConfigPath) && string.IsNullOrWhiteSpace(options.PatchifyPath))
        {
            Console.Error.WriteLine("Error: either --config or --patchify path is required.");
            return null;
        }

        return options;
    }

    private static int RunPatchifyMode(string path, ILogger logger)
    {
        try
        {
            logger.LogInfo($"Starting patchify for: {path}");

            var exitCode = Directory.Exists(path)
                ? PatchifyNative.ProcessFolder(path)
                : PatchifyNative.ProcessImage(path);

            if (exitCode == 0)
            {
                logger.LogInfo("Patchify complete.");
                return 0;
            }

            logger.LogError($"Patchify failed with exit code {exitCode}.");
            return exitCode;
        }
        catch (DllNotFoundException ex)
        {
            logger.LogError($"Patchify native library not found: {ex.Message}");
            return 1;
        }
        catch (BadImageFormatException ex)
        {
            logger.LogError($"Patchify native library has an incompatible architecture: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            logger.LogError($"Patchify execution failed: {ex.Message}");
            return 1;
        }
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
Cross-platform batch rendering of 3D objects with Blender and native patch extraction

Usage:
    qualcompare-cli --config <path> [--verbose] [--help]
    qualcompare-cli --patchify <path> [--verbose] [--help]

Options:
  -c, --config <path>    Path to JSON configuration file (required)
  -v, --verbose          Enable verbose logging (debug output)
    -p, --patchify <path>  Path to a rendered image or rendered object folder
  -h, --help             Show this help message

Example:
  qualcompare-cli --config render_job.json --verbose

    qualcompare-cli --patchify /path/to/rendered/object

Configuration file format:
    See QualCompareCLI/CONFIG_SCHEMA.md for detailed JSON schema documentation.
");
        return 2;
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.ToString(@"d\.hh\:mm\:ss\.fff");
    }

    private static int GetEffectiveMaxParallelism(RenderConfig config)
    {
        if (config.MaxParallelism > 0)
            return config.MaxParallelism;

        return Math.Max(1, Environment.ProcessorCount / 4);
    }

    private class CliOptions
    {
        public string ConfigPath { get; set; } = "";
        public string PatchifyPath { get; set; } = "";
        public bool Verbose { get; set; } = false;
    }
}
