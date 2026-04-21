using Newtonsoft.Json;

namespace QualCompareCLI;

/// <summary>
/// Configuration for rendering pipeline - read from JSON.
/// Maps directly to Blender script arguments and render queue parameters.
/// </summary>
public class RenderConfig
{
    [JsonProperty("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0";

    // --- Blender and script paths ---
    [JsonProperty("blenderPath")]
    public required string BlenderPath { get; set; }

    [JsonProperty("renderScriptPath")]
    public required string RenderScriptPath { get; set; }

    // --- Input/Output directories ---
    [JsonProperty("inputDir")]
    public required string InputDir { get; set; }

    [JsonProperty("outputDir")]
    public required string OutputDir { get; set; }

    [JsonProperty("tempInputRoot")]
    public string? TempInputRoot { get; set; }

    [JsonProperty("tempOutputRoot")]
    public string? TempOutputRoot { get; set; }

    // --- File type and format ---
    [JsonProperty("objType")]
    public string ObjType { get; set; } = "obj"; // "obj" or "ply"

    [JsonProperty("fileType")]
    public string FileType { get; set; } = "everything"; // "everything", "source", "distorted"

    [JsonProperty("ext")]
    public string Extension { get; set; } = "png"; // "png" or "jpg"

    // --- Viewing and sampling ---
    [JsonProperty("positionsType")]
    public string PositionsType { get; set; } = "fibonacci"; // "fibonacci", "yfixed", "polyedric"

    [JsonProperty("nbViews")]
    public int NbViews { get; set; } = 12;

    [JsonProperty("yPos")]
    public double YPos { get; set; } = 0.0;

    [JsonProperty("upAxis")]
    public string UpAxis { get; set; } = "Y"; // "X", "Y", "Z"

    // --- Render parameters ---
    [JsonProperty("render")]
    public RenderParams Render { get; set; } = new();

    // --- PLY specific ---
    [JsonProperty("ply")]
    public PlyParams Ply { get; set; } = new();

    // --- Runtime behavior ---
    [JsonProperty("maxParallelism")]
    public int MaxParallelism { get; set; } = 0; // 0 = CPU/4

    [JsonProperty("prefetchToSSD")]
    public bool PrefetchToSSD { get; set; } = true;
}

public class RenderParams
{
    [JsonProperty("resX")]
    public int ResX { get; set; } = 650;

    [JsonProperty("resY")]
    public int ResY { get; set; } = 550;

    [JsonProperty("engine")]
    public string Engine { get; set; } = "BLENDER_EEVEE_NEXT";

    [JsonProperty("taa")]
    public int TAA { get; set; } = 64;

    [JsonProperty("filterSize")]
    public double FilterSize { get; set; } = 1.5;

    [JsonProperty("maskThreshold")]
    public int MaskThreshold { get; set; } = 10;

    [JsonProperty("sunEnergy")]
    public double SunEnergy { get; set; } = 5.0;

    [JsonProperty("sunTheta")]
    public double SunTheta { get; set; } = 30.0;

    [JsonProperty("sunPhi")]
    public double SunPhi { get; set; } = 50.0;

    [JsonProperty("bgColor")]
    public string BgColor { get; set; } = "#34322C";
}

public class PlyParams
{
    [JsonProperty("mode")]
    public string Mode { get; set; } = "sphere"; // "sphere", "surface", "voxel", "voxel_volume"

    [JsonProperty("pointRadiusFraction")]
    public double PointRadiusFraction { get; set; } = 0.003;

    [JsonProperty("voxelBits")]
    public int VoxelBits { get; set; } = 10;

    [JsonProperty("voxelRadiusMultiplier")]
    public double VoxelRadiusMultiplier { get; set; } = 1.0;
}
