using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Text;

namespace QualCompareCLI;

/// <summary>
/// Handles Blender process execution with arguments constructed from RenderConfig.
/// Mirrors the logic from QualCompare/RenderQueue.cs but for cross-platform compatibility.
/// </summary>
public class BlenderRenderService
{
    private readonly RenderConfig _config;
    private readonly ILogger _logger;

    public BlenderRenderService(RenderConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Validates that critical paths exist and Blender is callable.
    /// </summary>
    public bool ValidateEnvironment()
    {
        var issues = new List<string>();

        if (!File.Exists(_config.BlenderPath))
            issues.Add($"Blender executable not found: {_config.BlenderPath}");

        if (!File.Exists(_config.RenderScriptPath))
            issues.Add($"Render script not found: {_config.RenderScriptPath}");

        if (!Directory.Exists(_config.InputDir))
            issues.Add($"Input directory does not exist: {_config.InputDir}");

        if (string.IsNullOrWhiteSpace(_config.OutputDir))
            issues.Add("Output directory is not set");

        if (issues.Count > 0)
        {
            foreach (var issue in issues)
                _logger.LogError(issue);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Constructs the full Blender command line arguments for a single object render.
    /// Mirrors RenderQueue.cs:324-354 logic.
    /// </summary>
    public string BuildBlenderArguments(string objectPath)
    {
        var sb = new StringBuilder();

        // Blender command structure: blender --background --python script.py -- <args>
        sb.Append("--background ");
        sb.Append($"--python \"{_config.RenderScriptPath}\" ");
        sb.Append("-- ");

        // --- Core input parameters ---
        sb.Append($"--obj \"{objectPath}\" ");
        sb.Append($"--out \"{_config.TempOutputRoot ?? _config.OutputDir}\" ");
        sb.Append($"--nb_views {_config.NbViews} ");
        sb.Append($"--positions_type {_config.PositionsType} ");
        sb.Append($"--ext {_config.Extension} ");
        sb.Append($"--file_type {_config.FileType} ");
        sb.Append($"--obj_type {_config.ObjType} ");
        sb.Append($"--ypos {_config.YPos.ToString("0.00", CultureInfo.InvariantCulture)} ");
        sb.Append($"--up_axis {_config.UpAxis} ");

        // --- Render parameters ---
        sb.Append($"--resx {_config.Render.ResX} ");
        sb.Append($"--resy {_config.Render.ResY} ");
        sb.Append($"--engine {_config.Render.Engine} ");
        sb.Append($"--taa {_config.Render.TAA} ");
        sb.Append($"--filter_size {_config.Render.FilterSize.ToString(CultureInfo.InvariantCulture)} ");
        sb.Append($"--mask_threshold {_config.Render.MaskThreshold} ");
        sb.Append($"--sun_energy {_config.Render.SunEnergy.ToString(CultureInfo.InvariantCulture)} ");
        sb.Append($"--sun_theta {_config.Render.SunTheta.ToString(CultureInfo.InvariantCulture)} ");
        sb.Append($"--sun_phi {_config.Render.SunPhi.ToString(CultureInfo.InvariantCulture)} ");
        sb.Append($"--point_radius_fraction {_config.Ply.PointRadiusFraction.ToString(CultureInfo.InvariantCulture)} ");

        // --- PLY specific ---
        sb.Append($"--ply_render {_config.Ply.Mode} ");
        sb.Append($"--ply_voxel_bits {_config.Ply.VoxelBits} ");
        sb.Append($"--voxel_radius_multiplier {_config.Ply.VoxelRadiusMultiplier.ToString(CultureInfo.InvariantCulture)} ");
        sb.Append($"--bg_color {_config.Render.BgColor}");

        return sb.ToString();
    }

    /// <summary>
    /// Launches Blender for a single object render.
    /// </summary>
    public async Task<bool> RenderObjectAsync(string objectPath, CancellationToken cancellationToken = default)
    {
        var objectName = Path.GetFileNameWithoutExtension(objectPath);
        var arguments = BuildBlenderArguments(objectPath);

        var psi = new ProcessStartInfo
        {
            FileName = _config.BlenderPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogError($"Failed to start Blender process for {objectName}");
                return false;
            }

            // Subscribe to output streams
            var errorLines = new List<string>();
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    errorLines.Add(e.Data);
                    _logger.LogDebug($"[Blender] {e.Data}");
                }
            };
            process.BeginErrorReadLine();

            // Wait for completion or cancellation
            var exitTask = process.WaitForExitAsync(cancellationToken);
            await exitTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError($"Blender render failed for {objectName} (exit code: {process.ExitCode})");
                foreach (var line in errorLines)
                    _logger.LogError($"  {line}");
                return false;
            }

            _logger.LogInfo($"Successfully rendered {objectName}");
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"Render cancelled for {objectName}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception rendering {objectName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Returns the folder that Blender writes for a rendered object.
    /// </summary>
    public string GetRenderedObjectOutputFolder(string objectPath)
    {
        var objectName = Path.GetFileNameWithoutExtension(objectPath);
        var outputRoot = _config.TempOutputRoot ?? _config.OutputDir;
        return Path.Combine(outputRoot, objectName);
    }

    /// <summary>
    /// Discovers all objects matching the configured criteria (obj_type, file_type).
    /// </summary>
    public string[] DiscoverObjects()
    {
        if (!Directory.Exists(_config.InputDir))
            throw new DirectoryNotFoundException($"Input directory not found: {_config.InputDir}");

        var searchPattern = $"*.{_config.ObjType}";
        var allFiles = Directory.GetFiles(_config.InputDir, searchPattern, SearchOption.AllDirectories)
            .Where(path =>
            {
                // Apply source/distorted filtering (heuristic from RenderQueue.cs)
                if (_config.FileType == "everything")
                    return true;

                string lowerPath = path.ToLowerInvariant();
                string[] sourceKeywords = { "source", "ref", "reference", "src" };
                bool isSource = sourceKeywords.Any(k =>
                    lowerPath.Split(Path.DirectorySeparatorChar).Any(seg =>
                        seg == k || seg.StartsWith(k + "_")));

                return _config.FileType switch
                {
                    "source" => isSource,
                    "distorted" => !isSource,
                    _ => true
                };
            })
            .ToArray();

        _logger.LogInfo($"Found {allFiles.Length} objects matching criteria ({_config.FileType})");
        return allFiles;
    }
}
