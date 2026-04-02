using Microsoft.SqlServer.Server;
using PatchifyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;
using WpfControls = System.Windows.Controls; // Alias pour WPF Controls
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Drawing;
namespace QualCompare
{
    public partial class MainWindow : Window
    {
        // =========================
        // 1) CONFIG UTILISATEUR
        // =========================
        
        public sealed class AppConfig
        {
            public string BlenderPath { get; set; }
            public string RenderScriptPath { get; set; }
            public string TempInputRoot { get; set; }
            public string TempOutputRoot { get; set; }
            public string ModelCsvFilePath { get; set; }
            public string DefaultImageExt { get; set; } = "png";      // jpg|png pour --ext
            public int MaxParallelism { get; set; } = 0;           // 0 => CPU/4 (fallback)
            public bool PrefetchToSSD { get; set; } = true;         // copie OBJ+textures sur SSD
            public string UpAxis { get; set; } = "Y"; // X, Y, Z but Y default
            public string DefaultOutputRoot { get; set; }
            public string RenderFamilyName { get; set; } = "New_Render";

            // ---- Blender rendering parameters ----
            public int ResolutionX { get; set; } = 650; // default values defined for Graphics-LPIPS
            public int ResolutionY { get; set; } = 550;
            public string RenderEngine { get; set; } = "BLENDER_EEVEE_NEXT";
            public int TaaSamples { get; set; } = 64; // temporal anti-aliasing samples
            public double FilterSize { get; set; } = 1.5; // pixel filter size
            public int MaskThreshold { get; set; } = 10;
            public double SunEnergy { get; set; } = 5.0; // Sun light energy. Subjective so need to have access to it 
            public double SunTheta { get; set; } = 30.0; // Sun Orientation angles. Those are important according to Graphics-LPIPS/TMQA paper.
            public double SunPhi { get; set; } = 50.0;
            public string PlyRenderMode { get; set; } = "sphere"; // "sphere", "surface" or "voxel"
            public double PointRadiusFraction { get; set; } = 0.003; // for point cloud rendering with "sphere" mode
            public int PlyVoxelBits { get; set; } = 12; // number of quantization bits for "voxel" mode
            public string BackgroundColorHex { get; set; } = "#34322C"; // Light Gray
            public double VoxelRadiusMultiplier { get; set; } = 1.2; // multiplier for voxel size when rendering with "voxel" mode
            // ---- Patchification parameters ----
            public int PatchSize { get; set; } = 64;
            public int PatchStepX { get; set; } = 32;
            public int PatchStepY { get; set; } = 32;
            public double PatchOverlapThreshold { get; set; } = 0.65;
        }

        private AppConfig Config;
        private string ConfigPath =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "QualCompare", "settings.json");

        private static string ApplicationRootPath =>
            AppDomain.CurrentDomain.BaseDirectory;

        private static string ResolveBundledFile(params string[] relativeSegments)
        {
            try
            {
                var candidate = System.IO.Path.Combine(new[] { ApplicationRootPath }.Concat(relativeSegments).ToArray());
                return File.Exists(candidate) ? candidate : null;
            }
            catch
            {
                return null;
            }
        }

        private static string DetectBundledRenderScriptPath()
        {
            return ResolveBundledFile("scripts", "render_single.py")
                ?? ResolveBundledFile("obj2png", "render_single.py")
                ?? ResolveBundledFile("render_single.py");
        }

        private static string DetectBundledModelCsvPath()
        {
            return ResolveBundledFile("resources", "Models_characteristics_and_settings.csv")
                ?? ResolveBundledFile("obj2png", "Models_characteristics_and_settings.csv")
                ?? ResolveBundledFile("Models_characteristics_and_settings.csv");
        }

        private static string DetectInstalledBlenderPath()
        {
            try
            {
                string[] roots =
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };

                foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r) && Directory.Exists(r)))
                {
                    var blenderFoundation = System.IO.Path.Combine(root, "Blender Foundation");
                    if (!Directory.Exists(blenderFoundation)) continue;

                    var candidates = Directory.GetDirectories(blenderFoundation, "Blender *")
                        .Select(dir => System.IO.Path.Combine(dir, "blender.exe"))
                        .Where(File.Exists)
                        .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (candidates.Count > 0)
                        return candidates[0];
                }
            }
            catch { }

            return @"C:\Program Files\Blender Foundation\Blender 4.4\blender.exe";
        }

        private static string GetDefaultTempInputRoot()
        {
            return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QualCompare", "in");
        }

        private static string GetDefaultTempOutputRoot()
        {
            return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QualCompare", "out");
        }

        private static string GetDefaultOutputRoot()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "QualCompare",
                "out");
        }

        private AppConfig ApplyDefaultPaths(AppConfig cfg)
        {
            if (cfg == null) cfg = new AppConfig();

            if (string.IsNullOrWhiteSpace(cfg.BlenderPath))
                cfg.BlenderPath = DetectInstalledBlenderPath();

            if (string.IsNullOrWhiteSpace(cfg.RenderScriptPath) || !File.Exists(cfg.RenderScriptPath))
            {
                var bundledScript = DetectBundledRenderScriptPath();
                if (!string.IsNullOrWhiteSpace(bundledScript))
                    cfg.RenderScriptPath = bundledScript;
            }

            if (string.IsNullOrWhiteSpace(cfg.TempInputRoot))
                cfg.TempInputRoot = GetDefaultTempInputRoot();

            if (string.IsNullOrWhiteSpace(cfg.TempOutputRoot))
                cfg.TempOutputRoot = GetDefaultTempOutputRoot();

            if (string.IsNullOrWhiteSpace(cfg.ModelCsvFilePath) || !File.Exists(cfg.ModelCsvFilePath))
            {
                var bundledCsv = DetectBundledModelCsvPath();
                if (!string.IsNullOrWhiteSpace(bundledCsv))
                    cfg.ModelCsvFilePath = bundledCsv;
            }

            if (string.IsNullOrWhiteSpace(cfg.DefaultOutputRoot))
                cfg.DefaultOutputRoot = GetDefaultOutputRoot();

            if (string.IsNullOrWhiteSpace(cfg.RenderFamilyName))
                cfg.RenderFamilyName = "New_Render";

            cfg.UpAxis = NormalizeUpAxis(cfg.UpAxis);
            cfg.BackgroundColorHex = NormalizeHexColor(cfg.BackgroundColorHex);

            return cfg;
        }

        private AppConfig LoadConfig()
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var cfg = JsonConvert.DeserializeObject<AppConfig>(json);
                    return ApplyDefaultPaths(cfg);
                }
            }
            catch { }
            return ApplyDefaultPaths(new AppConfig());
        }

        private bool HasPersistedConfig()
        {
            return File.Exists(ConfigPath);
        }

        private static bool IsExistingFilePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        private void EnsureFirstRunConfiguration()
        {
            bool hadPersistedConfig = HasPersistedConfig();
            bool shouldPersist = !hadPersistedConfig;

            Config = ApplyDefaultPaths(Config);

            if (!hadPersistedConfig)
                shouldPersist = true;

            if (shouldPersist)
                SaveConfig();

            var issues = new List<string>();

            if (!IsExistingFilePath(Config.BlenderPath))
                issues.Add("- Blender executable not found. Please select blender.exe in Settings.");

            if (!IsExistingFilePath(Config.RenderScriptPath))
                issues.Add("- Render script not found in the application layout.");

            if (issues.Count == 0)
            {
                if (!hadPersistedConfig)
                {
                    MessageBox.Show(
                        "QualCompare initial configuration has been created automatically.",
                        "QualCompare setup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            var message = string.Join(Environment.NewLine, issues);

            if (!hadPersistedConfig)
            {
                message = "QualCompare created its initial configuration, but some required paths still need attention."
                    + Environment.NewLine + Environment.NewLine + message;
            }
            else
            {
                message = "Some required paths are currently missing or invalid."
                    + Environment.NewLine + Environment.NewLine + message;
            }

            MessageBox.Show(message, "QualCompare setup", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void SaveConfig()
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d’enregistrer les paramètres : {ex.Message}", "Paramètres",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Pousse la config dans les contrôles si l’onglet existe
        private void ApplyConfigToUI()
        {
            (FindName("BlenderPathTextBox") as TextBox)?.SetCurrentValue(TextBox.TextProperty, Config.BlenderPath);
            (FindName("RenderScriptPathTextBox") as TextBox)?.SetCurrentValue(TextBox.TextProperty, Config.RenderScriptPath);
            (FindName("TempInTextBox") as TextBox)?.SetCurrentValue(TextBox.TextProperty, Config.TempInputRoot);
            (FindName("TempOutTextBox") as TextBox)?.SetCurrentValue(TextBox.TextProperty, Config.TempOutputRoot);
            (FindName("CsvPathTextBox") as TextBox)?.SetCurrentValue(TextBox.TextProperty, Config.ModelCsvFilePath);
            (FindName("ImageExtComboBox") as ComboBox)?.SetCurrentValue(ComboBox.TextProperty, Config.DefaultImageExt);
            (FindName("PrefetchCheckBox") as CheckBox)?.SetCurrentValue(CheckBox.IsCheckedProperty, Config.PrefetchToSSD);
            (FindName("UpAxisComboBox") as ComboBox)?.SetCurrentValue(ComboBox.TextProperty, Config.UpAxis);
            (FindName("BgColorTextBox") as TextBox)?.SetCurrentValue(TextBox.TextProperty, Config.BackgroundColorHex);

            var mp = FindName("MaxParallelismNumeric") as TextBox;
            if (mp != null) mp.Text = Config.MaxParallelism.ToString(CultureInfo.InvariantCulture);

            // --- Nouveaux paramètres de rendu ---

            var resX = FindName("ResolutionXNumeric") as TextBox;
            var resY = FindName("ResolutionYNumeric") as TextBox;
            if (resX != null) resX.Text = Config.ResolutionX.ToString(CultureInfo.InvariantCulture);
            if (resY != null) resY.Text = Config.ResolutionY.ToString(CultureInfo.InvariantCulture);

            var engineCb = FindName("RenderEngineComboBox") as ComboBox;
            if (engineCb != null) engineCb.Text = Config.RenderEngine;

            var taaTb = FindName("TaaSamplesNumeric") as TextBox;
            if (taaTb != null) taaTb.Text = Config.TaaSamples.ToString(CultureInfo.InvariantCulture);

            var filterTb = FindName("FilterSizeNumeric") as TextBox;
            if (filterTb != null) filterTb.Text = Config.FilterSize.ToString(CultureInfo.InvariantCulture);

            var maskTb = FindName("MaskThresholdNumeric") as TextBox;
            if (maskTb != null) maskTb.Text = Config.MaskThreshold.ToString(CultureInfo.InvariantCulture);

            var sunEnergyTb = FindName("SunEnergyNumeric") as TextBox;
            var sunThetaTb = FindName("SunThetaNumeric") as TextBox;
            var sunPhiTb = FindName("SunPhiNumeric") as TextBox;
            if (sunEnergyTb != null) sunEnergyTb.Text = Config.SunEnergy.ToString(CultureInfo.InvariantCulture);
            if (sunThetaTb != null) sunThetaTb.Text = Config.SunTheta.ToString(CultureInfo.InvariantCulture);
            if (sunPhiTb != null) sunPhiTb.Text = Config.SunPhi.ToString(CultureInfo.InvariantCulture);

            var plyModeCb = FindName("PlyRenderModeComboBox") as ComboBox;
            if (plyModeCb != null) plyModeCb.Text = Config.PlyRenderMode;

            var plyBitsTb = FindName("PlyVoxelBitsNumeric") as TextBox;
            if (plyBitsTb != null) plyBitsTb.Text = Config.PlyVoxelBits.ToString(CultureInfo.InvariantCulture);

            var vxMultTb = FindName("VoxelRadiusMultiplierNumeric") as TextBox;
            if (vxMultTb != null) vxMultTb.Text = Config.VoxelRadiusMultiplier.ToString(CultureInfo.InvariantCulture);

            var prfTb = FindName("PointRadiusFractionNumeric") as TextBox;
            if (prfTb != null) prfTb.Text = Config.PointRadiusFraction.ToString(CultureInfo.InvariantCulture);

            // --- Wiring auto-output (existant) ---
            var ds = FindName("DatasetComboBox") as ComboBox;

            if (NbViewsTextBox != null) NbViewsTextBox.TextChanged += (s, e) => TryAutoFillOutputFolder();
            if (comboBoxFileSelection != null) comboBoxFileSelection.SelectionChanged += (s, e) => TryAutoFillOutputFolder();
            if (ObjFilePathTextBox != null) ObjFilePathTextBox.TextChanged += (s, e) => TryAutoFillOutputFolder();
            if (sliderHeight != null) sliderHeight.ValueChanged += (s, e) => TryAutoFillOutputFolder();
            if (ds != null) ds.SelectionChanged += (s, e) => TryAutoFillOutputFolder();
            if (filterTb != null) filterTb.TextChanged += (s, e) => TryAutoFillOutputFolder();
        }

        private void ReadConfigFromUI()
        {
            var tb = FindName("BlenderPathTextBox") as TextBox;
            if (tb != null && !string.IsNullOrWhiteSpace(tb.Text)) Config.BlenderPath = tb.Text.Trim();

            tb = FindName("RenderScriptPathTextBox") as TextBox;
            if (tb != null && !string.IsNullOrWhiteSpace(tb.Text)) Config.RenderScriptPath = tb.Text.Trim();

            tb = FindName("TempInTextBox") as TextBox;
            if (tb != null && !string.IsNullOrWhiteSpace(tb.Text)) Config.TempInputRoot = tb.Text.Trim();

            tb = FindName("TempOutTextBox") as TextBox;
            if (tb != null && !string.IsNullOrWhiteSpace(tb.Text)) Config.TempOutputRoot = tb.Text.Trim();

            tb = FindName("CsvPathTextBox") as TextBox;
            if (tb != null && !string.IsNullOrWhiteSpace(tb.Text)) Config.ModelCsvFilePath = tb.Text.Trim();

            var cb = FindName("ImageExtComboBox") as ComboBox;
            if (cb != null && cb.Text is string s && !string.IsNullOrWhiteSpace(s))
                Config.DefaultImageExt = s.Trim().ToLowerInvariant();

            var ck = FindName("PrefetchCheckBox") as CheckBox;
            if (ck != null && ck.IsChecked.HasValue) Config.PrefetchToSSD = ck.IsChecked.Value;

            var mp = FindName("MaxParallelismNumeric") as TextBox;
            if (mp != null && int.TryParse(mp.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
                Config.MaxParallelism = val;

            var upcb = FindName("UpAxisComboBox") as ComboBox;
            if (upcb != null && upcb.Text is string up && !string.IsNullOrWhiteSpace(up))
                Config.UpAxis = NormalizeUpAxis(up);

            // --- Rendering parameters ---

            tb = FindName("ResolutionXNumeric") as TextBox;
            if (tb != null && int.TryParse(tb.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var resx))
                Config.ResolutionX = resx;

            tb = FindName("ResolutionYNumeric") as TextBox;
            if (tb != null && int.TryParse(tb.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var resy))
                Config.ResolutionY = resy;

            var engineCb = FindName("RenderEngineComboBox") as ComboBox;
            if (engineCb != null && engineCb.Text is string eng && !string.IsNullOrWhiteSpace(eng))
                Config.RenderEngine = eng.Trim();

            tb = FindName("TaaSamplesNumeric") as TextBox;
            if (tb != null && int.TryParse(tb.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var taa))
                Config.TaaSamples = taa;

            tb = FindName("FilterSizeNumeric") as TextBox;
            if (tb != null && double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fs))
                Config.FilterSize = fs;

            tb = FindName("MaskThresholdNumeric") as TextBox;
            if (tb != null && int.TryParse(tb.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mt))
                Config.MaskThreshold = mt;

            tb = FindName("SunEnergyNumeric") as TextBox;
            if (tb != null && double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var se))
                Config.SunEnergy = se;

            tb = FindName("SunThetaNumeric") as TextBox;
            if (tb != null && double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var st))
                Config.SunTheta = st;

            tb = FindName("SunPhiNumeric") as TextBox;
            if (tb != null && double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var sp))
                Config.SunPhi = sp;

            var plyModeCb = FindName("PlyRenderModeComboBox") as ComboBox;
            if (plyModeCb != null && plyModeCb.Text is string pm && !string.IsNullOrWhiteSpace(pm))
                Config.PlyRenderMode = pm.Trim();

            tb = FindName("PlyVoxelBitsNumeric") as TextBox;
            if (tb != null && int.TryParse(tb.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bits))
                Config.PlyVoxelBits = bits;

            var bgtb = FindName("BgColorTextBox") as TextBox;
            if (bgtb != null && !string.IsNullOrWhiteSpace(bgtb.Text))
                Config.BackgroundColorHex = NormalizeHexColor(bgtb.Text);

            tb = FindName("VoxelRadiusMultiplierNumeric") as TextBox;
            if (tb != null && double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var vrm))
                Config.VoxelRadiusMultiplier = vrm;

            // TODO : Point Radius TextBox
            tb = FindName("PointRadiusFractionNumeric") as TextBox;
            if (tb != null && double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var prf))
                Config.PointRadiusFraction = prf;
        }
        private bool IsAntiAliasingDisabled()
        {
            // 1) Essayer la valeur UI si elle existe et est valide
            var tb = FindName("FilterSizeNumeric") as TextBox;
            if (tb != null && !string.IsNullOrWhiteSpace(tb.Text))
            {
                if (double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedUi))
                    return Math.Abs(parsedUi) < 1e-6; // 0 => NAA
                                                      // Si parsing échoue, ne conclus pas à NAA
                return false;
            }

            // 2) Sinon, utiliser la config si elle est disponible
            if (Config != null)
                return Math.Abs(Config.FilterSize) < 1e-6;

            // 3) Démarrage à froid (pas de UI ni de Config) ? considérer AA comme actif
            return false;
        }

        // =========================
        // 2) UTILS
        // =========================

        private readonly ConcurrentBag<Process> runningBlenderProcesses = new ConcurrentBag<Process>();

        private CancellationTokenSource cancellationTokenSource;
        private CancellationTokenSource _patchifyCts;
        private string _patchifyLogPath;

        private void ScrollToBottom() => LogTextBox.ScrollToEnd();

        private void NumberInput_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void NumberInput_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!int.TryParse(text, out _)) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        void floatInput_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            string text = sender is TextBox textBox ? textBox.Text : string.Empty;
            string newText = string.Concat(text, e.Text);
            if (newText.Contains(".") || newText.Contains("-"))
            {
                int dotIndex = newText.IndexOf('.');
                int dashIndex = newText.IndexOf('-');
                if (Math.Abs(dotIndex - dashIndex) == 2 || (dashIndex == 0 && dotIndex == -1))
                    e.Handled = false;
                else
                    e.Handled = true;
            }
            e.Handled |= !(int.TryParse(e.Text, out int value) || e.Text == "." || e.Text == "-");
            e.Handled |= (e.Text == "." && text.Contains("."));
            e.Handled |= (e.Text == "." && text.Length == 0);
            e.Handled |= (e.Text == "-" && text.Length > 0);
            e.Handled |= (e.Text == "." && text.Length == 1 && text[0] == '-');
        }

        private void MethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            FibonacciGrid?.SetValue(VisibilityProperty, Visibility.Collapsed);
            YFixedGrid?.SetValue(VisibilityProperty, Visibility.Collapsed);
            PolyhedronGrid?.SetValue(VisibilityProperty, Visibility.Collapsed);

            if (MethodComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedMethod = selectedItem.Content.ToString();
                NbViewsTextBox.IsEnabled = true;
                switch (selectedMethod)
                {
                    case "Fibonacci":
                        FibonacciGrid.Visibility = Visibility.Visible; break;
                    case "Y fixé":
                        YFixedGrid.Visibility = Visibility.Visible; break;
                    case "Polyèdrale":
                        PolyhedronGrid.Visibility = Visibility.Visible;
                        NbViewsTextBox.IsEnabled = false;
                        NbViewsTextBox.Text = "4";
                        break;
                }
            }
            TryAutoFillOutputFolder();

        }
        private static string NormalizeUpAxis(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Y";
            var t = s.Trim().ToUpperInvariant();
            if (t == "X" || t == "Y" || t == "Z") return t;
            return "Y";
        }

        private static bool LooksLikeDrive(string s)
        {
            // e.g. "D:"
            return s != null && s.Length == 2 && char.IsLetter(s[0]) && s[1] == ':';
        }

        private static bool LooksLikeFile(string s)
        {
            // crude detection by extension
            return !string.IsNullOrEmpty(s) && s.IndexOf('.') >= 0;
        }

        // NEW: single predicate for dataset-like folder segments
        private static bool IsDatasetFolderCandidate(string seg)
        {
            if (string.IsNullOrEmpty(seg)) return false;
            if (LooksLikeDrive(seg) || LooksLikeFile(seg)) return false;
            if (seg.Length < 3) return false;                    // avoid very short tokens like "DB"
            if (seg != seg.ToUpperInvariant()) return false;     // reject if any lowercase
                                                                 // Must contain at least one A-Z; allowed chars are A-Z, digits, underscore, hyphen, parentheses
            return System.Text.RegularExpressions.Regex.IsMatch(seg, @"^(?=.*[A-Z])[A-Z0-9_()\-]+$");
        }

        private static string ExtractDatasetFromPath(string inputPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(inputPath)) return "DATASET";

                string path = inputPath.Replace('/', System.IO.Path.DirectorySeparatorChar)
                                       .Replace('\\', System.IO.Path.DirectorySeparatorChar);

                var parts = path.Split(new[] { System.IO.Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "DATASET";

                // Single right-to-left pass: pick the rightmost candidate
                for (int i = parts.Length - 1; i >= 0; i--)
                {
                    string seg = parts[i];
                    if (IsDatasetFolderCandidate(seg)) return seg;
                }
            }
            catch { }

            return "DATASET";
        }
        private double GetRoundedYpos()
        {
            double raw = 0.0;

            if (sliderHeight != null)
                raw = sliderHeight.Value;
            else
            {
                var tb = FindName("HauteurYValueTextBox") as TextBox;
                if (tb != null && double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    raw = parsed;
            }

            // Round to one decimal
            return Math.Round(raw, 1);
        }
        private string GetMethodFolderName(string uiLabel)
        {
            if (string.IsNullOrWhiteSpace(uiLabel)) return "Method";
            var t = uiLabel.Trim();
            if (string.Equals(t, "Fibonacci", StringComparison.OrdinalIgnoreCase)) return "Fibonacci";
            if (t.StartsWith("Y", StringComparison.OrdinalIgnoreCase))
            {
                double roundedHeight = GetRoundedYpos();
                string heightStr = roundedHeight.ToString("0.0", CultureInfo.InvariantCulture);
                if (heightStr == "0.0")
                {
                    heightStr = "0";
                }
                return "Y_fixed_" + heightStr;
            }
            if (t.StartsWith("Poly", StringComparison.OrdinalIgnoreCase)) return "Polyhedral";

            // Fallback: clean spaces
            return t.Replace(" ", "");
        }


        private static string GetFileTypeFolderName(int selectedIndex)
        {
            // 0: everything, 1: source, 2: distorted 
            if (selectedIndex == 1) return "Source";
            if (selectedIndex == 2) return "Distorted";
            return "Everything";
        }

        private string GetSelectedDataset()
        {
            var ds = FindName("DatasetComboBox") as ComboBox;
            if (ds != null && ds.Text is string t && !string.IsNullOrWhiteSpace(t))
                return t.Trim();

            string inputPath = ObjFilePathTextBox != null ? ObjFilePathTextBox.Text : null;
            return ExtractDatasetFromPath(inputPath);
        }

        private string BuildSuggestedOutputPath()
        {
            // Racine et famille de rendu depuis Config
            string root = (Config != null && !string.IsNullOrWhiteSpace(Config.DefaultOutputRoot))
                          ? Config.DefaultOutputRoot
                          : @"D:\These\Projets\QualCompare\out";
            string family = (Config != null && !string.IsNullOrWhiteSpace(Config.RenderFamilyName))
                            ? Config.RenderFamilyName
                            : "New_Render";

            // Dataset
            string dataset = GetSelectedDataset();

            // Method
            string methodLabel = "";
            if (MethodComboBox != null && MethodComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
                methodLabel = cbi.Content.ToString();
            string methodFolder = GetMethodFolderName(methodLabel);
            // If no anti-aliasing (filter size 0), append "_NAA" to method folder
            if (IsAntiAliasingDisabled())
            {
                methodFolder += "_NAA";
            }

            // File type
            int fileTypeIndex = comboBoxFileSelection != null ? comboBoxFileSelection.SelectedIndex : 0;
            string fileTypeFolder = GetFileTypeFolderName(fileTypeIndex);

            // Nb views
            int nbv = 0;
            if (NbViewsTextBox != null) int.TryParse(NbViewsTextBox.Text, out nbv);
            if (nbv <= 0) nbv = 1;
            string viewsFolder = nbv.ToString(CultureInfo.InvariantCulture) + "VP";

            string path = System.IO.Path.Combine(root, dataset, family, methodFolder, fileTypeFolder, viewsFolder);
            return path;
        }

        private void TryAutoFillOutputFolder()
        {
            var auto = FindName("AutoOutputCheckBox") as CheckBox;
            if (auto != null && !(auto.IsChecked ?? false)) return;

            var tb = OutputDirTextBox as TextBox;
            if (tb == null) return;

            var patchifyTb = ImgToPatchifyTextBox as TextBox;
            if(patchifyTb == null) return;

            string suggested = BuildSuggestedOutputPath();
            if(!string.IsNullOrWhiteSpace(suggested))
            {
                tb.Text = suggested;
                patchifyTb.Text = suggested;
            }
        }

        private string FindRefModel(string distortedModelNamePath)
        {
            string refModelName = null;
            string pattern = @"(.+)_simpL([0-9]+)_qp([0-9]+)_qt([0-9]+)_decompJPEG_([0-9]+x[0-9]+)_Q([0-9]+)";
            Match match = Regex.Match(distortedModelNamePath, pattern);
            if (match.Success)
            {
                string fullName = match.Groups[1].Value;
                refModelName = Path.GetFileName(fullName);
            }
            else
            {
                Console.WriteLine("No match found.");
            }
            return refModelName;
        }

        private string[] ReadModelTransformsFromCsv(string csvFilePath, string inputModelName)
        {
            string[] lines = File.ReadAllLines(csvFilePath);
            string[] modelInfos = null;
            foreach (string line in lines)
            {
                if (line.Contains(inputModelName))
                {
                    modelInfos = line.Split(',');
                    break;
                }
            }
            return modelInfos;
        }

        static string AppendDirSep(string path)
        {
            if (string.IsNullOrEmpty(path)) return Path.DirectorySeparatorChar.ToString();
            char last = path[path.Length - 1];
            if (last != Path.DirectorySeparatorChar && last != Path.AltDirectorySeparatorChar)
                return path + Path.DirectorySeparatorChar;
            return path;
        }

        static string MakeRelativePath(string basePath, string targetPath)
        {
            if (string.IsNullOrEmpty(basePath)) basePath = Directory.GetCurrentDirectory();
            if (string.IsNullOrEmpty(targetPath)) return string.Empty;

            basePath = Path.GetFullPath(AppendDirSep(basePath));
            targetPath = Path.GetFullPath(targetPath);

            var baseUri = new Uri(basePath, UriKind.Absolute);
            var targetUri = new Uri(targetPath, UriKind.Absolute);
            if (!string.Equals(baseUri.Scheme, targetUri.Scheme, StringComparison.OrdinalIgnoreCase))
                return targetPath;

            var relativeUri = baseUri.MakeRelativeUri(targetUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());
            if (string.Equals(targetUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
                relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return relativePath;
        }

        static IEnumerable<string> ParseMtlTextures(string mtlPath)
        {
            var list = new List<string>();
            foreach (var line in File.ReadLines(mtlPath))
            {
                var l = line.Trim();
                if (l.StartsWith("map_", StringComparison.OrdinalIgnoreCase) || l.StartsWith("norm", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                        list.Add(parts[parts.Length - 1].Trim('"'));
                }
            }
            return list.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        static void SafeCopy(string src, string dst)
        {
            var dir = Path.GetDirectoryName(dst);
            if (string.IsNullOrEmpty(dir)) dir = Directory.GetCurrentDirectory();
            Directory.CreateDirectory(dir);

            var inF = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024 * 1024);
            try
            {
                var outF = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024);
                try { inF.CopyTo(outF); }
                finally { outF.Dispose(); }
            }
            finally { inF.Dispose(); }
        }

        static string PrefetchObjToSSD(string objPath, string tempInputRoot)
        {
            var objDir = Path.GetDirectoryName(objPath);
            if (string.IsNullOrEmpty(objDir))
                throw new ArgumentException("Invalid objPath (no directory).", nameof(objPath));

            var relRoot = Path.GetFileName(objDir);
            if (string.IsNullOrEmpty(relRoot)) relRoot = "obj";

            var objName = Path.GetFileNameWithoutExtension(objPath);
            if (string.IsNullOrEmpty(objName)) objName = "obj";

            var cacheRoot = Path.Combine(tempInputRoot, relRoot, objName);
            if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
            Directory.CreateDirectory(cacheRoot);

            string objDst = Path.Combine(cacheRoot, Path.GetFileName(objPath));
            SafeCopy(objPath, objDst);

            string mtlSrc = null;
            foreach (var line in File.ReadLines(objPath))
            {
                var l = line.Trim();
                if (l.StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase))
                {
                    var mtl = l.Substring(7).Trim().Trim('"');
                    mtlSrc = Path.GetFullPath(Path.Combine(objDir, mtl));
                    break;
                }
            }

            if (mtlSrc != null && File.Exists(mtlSrc))
            {
                string mtlRel = MakeRelativePath(objDir, mtlSrc);
                string mtlDst = Path.Combine(cacheRoot, mtlRel);
                SafeCopy(mtlSrc, mtlDst);

                var mtlDir = Path.GetDirectoryName(mtlSrc);
                if (string.IsNullOrEmpty(mtlDir)) mtlDir = objDir;

                foreach (var texRel in ParseMtlTextures(mtlSrc))
                {
                    string texAbs = Path.GetFullPath(Path.Combine(mtlDir, texRel));
                    if (File.Exists(texAbs))
                    {
                        string texRelToObj = MakeRelativePath(objDir, texAbs);
                        string texDst = Path.Combine(cacheRoot, texRelToObj);
                        SafeCopy(texAbs, texDst);
                    }
                }
            }

            return objDst;
        }
        private static string GetStartDirFromText(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                string t = text.Trim().Trim('"');

                if (Directory.Exists(t)) return t;                 // un dossier
                if (File.Exists(t))                                 // un fichier ? dossier parent
                {
                    string d = Path.GetDirectoryName(t);
                    if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return d;
                }

                // peut-être un chemin de fichier non créé ? tenter son parent
                string maybeParent = Path.GetDirectoryName(t);
                if (!string.IsNullOrEmpty(maybeParent) && Directory.Exists(maybeParent)) return maybeParent;
            }
            catch { /* ignore */ }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private static string SafeFileNameFrom(string text)
        {
            try { return string.IsNullOrWhiteSpace(text) ? "" : Path.GetFileName(text.Trim().Trim('"')) ?? ""; }
            catch { return ""; }
        }

        private static bool IsValidHexColor(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim();
            if (t.StartsWith("#")) t = t.Substring(1);
            if (t.Length != 6) return false;
            for (int i = 0; i < 6; i++)
            {
                char c = t[i];
                bool isHex = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }
            return true;
        }

        private static string NormalizeHexColor(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "#000000";
            var t = s.Trim();
            if (!t.StartsWith("#")) t = "#" + t;
            if (!IsValidHexColor(t)) return "#000000";
            return "#" + t.Substring(1).ToUpperInvariant();
        }

        private static string ColorToHex(System.Drawing.Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }
        private void ChooseBgColorButton_Click(object sender, RoutedEventArgs e)
        {
            var tb = FindName("BgColorTextBox") as TextBox;
            string current = tb != null ? tb.Text : "#000000";
            var dlg = new System.Windows.Forms.ColorDialog();
            try
            {
                // Init with current color if valid
                if (IsValidHexColor(current))
                {
                    var hex = NormalizeHexColor(current);
                    int r = int.Parse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
                    int g = int.Parse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
                    int b = int.Parse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
                    dlg.Color = System.Drawing.Color.FromArgb(r, g, b);
                }
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && tb != null)
                {
                    tb.Text = ColorToHex(dlg.Color);
                }
            }
            finally { dlg.Dispose(); }
        }

        // =========================
        // 3) RENDU (paramètres)
        // =========================
        private void RenderButton_Click(object sender, RoutedEventArgs e)
        {
            QueueCurrentJob_Click(sender, e);

        }
        private void StopRenderButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentJob();
            StopRenderButton.IsEnabled = false;
        }

        private void ReadStream(System.IO.StreamReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null) AppendLog(line);
        }

        private async void AppendLog(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                await Task.Run(() =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        LogTextBox.AppendText(message + Environment.NewLine);
                        const int maxLines = 1000;
                        if (LogTextBox.LineCount > maxLines)
                        {
                            try
                            {
                                int charIndex = LogTextBox.GetCharacterIndexFromLineIndex(0);
                                int removeUntilIndex = LogTextBox.GetCharacterIndexFromLineIndex(LogTextBox.LineCount - maxLines);
                                LogTextBox.Select(charIndex, removeUntilIndex);
                                LogTextBox.SelectedText = "";
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error while deleting old lines : " + ex.Message);
                            }
                        }
                        LogScrollViewer.ScrollToEnd();
                    }));
                });
            }
        }

        private void PolyhedronComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            string[] verticesTabIndex = { "4", "6", "8", "12", "20" };
            if (PolyhedronComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                NbViewsTextBox.Text = verticesTabIndex[PolyhedronComboBox.SelectedIndex];
            }
        }

        private void HauteurYValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                e.Handled = !(float.TryParse(textBox.Text, out float result) && result >= -1 && result <= 1);
            }
        }
        private async void Patchify_Click(object sender, RoutedEventArgs e)
        {
            string imagePath = ImgToPatchifyTextBox.Text;
            bool isFolder = (patchifySingleOrMultipleComboBox.SelectedIndex == 1);
            
            // Validation
            if (isFolder)
            {
                if (string.IsNullOrWhiteSpace(imagePath) || !Directory.Exists(imagePath))
                {
                    System.Windows.MessageBox.Show("Please select a valid image directory to patchify.");
                    return;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                {
                    System.Windows.MessageBox.Show("Please select a valid image file location.");
                    return;
                }
            }

            // Normalisation si le wrapper attend des "/"
            imagePath = imagePath.Replace("\\", "/");

            var btn = sender as System.Windows.Controls.Button;
            if (btn != null) btn.IsEnabled = false;

            var progressBar = FindName("PatchifyProgressBar") as System.Windows.Controls.ProgressBar;
            var progressText = FindName("PatchifyProgressText") as System.Windows.Controls.TextBlock;
            if (progressBar != null)
            {
                progressBar.Visibility = System.Windows.Visibility.Visible;
                progressBar.Minimum = 0;
                progressBar.Value = 0;
                progressBar.IsIndeterminate = isFolder;
                if (!isFolder) progressBar.Maximum = 1;
            }
            if (progressText != null) progressText.Text = isFolder ? "Processing folder..." : "Processing file...";
            
            _patchifyLogPath = CreateNewPatchifyLogFile();
            AppendPatchifyLog("Patchify started. Target = " + imagePath + (isFolder ? " (folder)" : " (file)"));

            _patchifyCts = new System.Threading.CancellationTokenSource();
            var token = _patchifyCts.Token;

            try
            {
                if (isFolder)
                {
                    await Task.Run(() => PatchifyWrapper.ProcessImageFolder(imagePath), token);
                }
                else
                {
                    await Task.Run(() => PatchifyWrapper.ProcessImage(imagePath), token);
                    if (progressBar != null) progressBar.Value = 1;
                }

                AppendPatchifyLog("Patchify done.");
                if (progressText != null) progressText.Text = "Done.";
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    var log = FindName("PatchifyLogTextBox") as System.Windows.Controls.TextBox;
                    if (log != null) { log.AppendText("[Patchify] Cancelled by user.\n"); log.ScrollToEnd(); }
                });
            }
            catch (DllNotFoundException ex)
            {
                await Dispatcher.InvokeAsync(() => System.Windows.MessageBox.Show("DLL not found : " + ex.Message));
            }
            catch (EntryPointNotFoundException ex)
            {
                await Dispatcher.InvokeAsync(() => System.Windows.MessageBox.Show("Unknown function in DLL : " + ex.Message));
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => System.Windows.MessageBox.Show("Error while executing DLL : " + ex.Message));
            }
            finally
            {
                // Ici, on est revenu sur le thread UI (grâce à l’absence de ConfigureAwait(false) plus haut).
                if (progressBar != null)
                {
                    progressBar.IsIndeterminate = false;
                    progressBar.Visibility = System.Windows.Visibility.Collapsed;
                }
                if (progressText != null) progressText.Text = "";
                if (btn != null) btn.IsEnabled = true;
                _patchifyCts = null;
            }
        }
        private string CreateNewPatchifyLogFile()
        {
            try
            {
                string root = (Config != null && !string.IsNullOrWhiteSpace(Config.TempOutputRoot))
                              ? Config.TempOutputRoot
                              : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QualCompare");
                string dir = System.IO.Path.Combine(root, "logs", "patchify");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, "patchify_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
                File.WriteAllText(path, "=== Patchify session " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===" + Environment.NewLine);
                return path;
            }
            catch { return null; }
        }

        // Ajoute une ligne aux logs (UI + fichier)
        private void AppendPatchifyLog(string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;

            // UI
            _ = Dispatcher.InvokeAsync(() =>
            {
                var tb = FindName("PatchifyLogTextBox") as System.Windows.Controls.TextBox;
                if (tb != null)
                {
                    tb.AppendText(line + Environment.NewLine);
                    tb.ScrollToEnd();
                }
            }, System.Windows.Threading.DispatcherPriority.Background);

            // Fichier
            try
            {
                if (!string.IsNullOrEmpty(_patchifyLogPath))
                    File.AppendAllText(_patchifyLogPath, line + Environment.NewLine);
            }
            catch { /* ignore */ }
        }

        // Boutons logs
        private void OpenPatchifyLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_patchifyLogPath) && File.Exists(_patchifyLogPath))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = "\"" + _patchifyLogPath + "\"",
                        UseShellExecute = false
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                else
                {
                    System.Windows.MessageBox.Show("No log file for this session.");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Log file can't be opened : " + ex.Message);
            }
        }

        private void CopyPatchifyLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tb = FindName("PatchifyLogTextBox") as System.Windows.Controls.TextBox;
                if (tb != null) System.Windows.Clipboard.SetText(tb.Text);
            }
            catch { }
        }

        private void ClearPatchifyLog_Click(object sender, RoutedEventArgs e)
        {
            var tb = FindName("PatchifyLogTextBox") as System.Windows.Controls.TextBox;
            if (tb != null) tb.Clear();
        }

        // =========================
        // 5) HANDLERS ONGLET PARAMS
        // =========================
        // À câbler dans le XAML si tu ajoutes l’onglet Paramètres :
        // - Buttons: BrowseBlenderButton, BrowseScriptButton, BrowseTempInButton, BrowseTempOutButton, BrowseCsvButton
        // - Buttons: SaveSettingsButton, ResetSettingsButton
        private void BrowseBlenderButton_Click(object sender, RoutedEventArgs e)
        {
            var tb = FindName("BlenderPathTextBox") as TextBox;
            string startDir = GetStartDirFromText(tb != null ? tb.Text : null);

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "blender.exe|blender.exe|Executables|*.exe",
                InitialDirectory = startDir,
                FileName = SafeFileNameFrom(tb != null ? tb.Text : null),
                RestoreDirectory = true
            };

            if (dlg.ShowDialog() == true && tb != null)
                tb.Text = dlg.FileName;
        }

        private void BrowseScriptButton_Click(object sender, RoutedEventArgs e)
        {
            var tb = FindName("RenderScriptPathTextBox") as TextBox;
            string startDir = GetStartDirFromText(tb != null ? tb.Text : null);

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Python (*.py)|*.py|Tous les fichiers|*.*",
                InitialDirectory = startDir,
                FileName = SafeFileNameFrom(tb != null ? tb.Text : null),
                RestoreDirectory = true
            };

            if (dlg.ShowDialog() == true && tb != null)
                tb.Text = dlg.FileName;
        }


        private void BrowseTempInButton_Click(object sender, RoutedEventArgs e)
        {
            var tb = FindName("TempInTextBox") as TextBox;
            string startDir = GetStartDirFromText(tb != null ? tb.Text : null);

            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            try
            {
                dlg.SelectedPath = startDir; // .NET Fx: pas d'InitialDirectory ? SelectedPath
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && tb != null)
                    tb.Text = dlg.SelectedPath;
            }
            finally { dlg.Dispose(); }
        }


        private void BrowseTempOutButton_Click(object sender, RoutedEventArgs e)
        {
            var tb = FindName("TempOutTextBox") as TextBox;
            string startDir = GetStartDirFromText(tb != null ? tb.Text : null);

            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            try
            {
                dlg.SelectedPath = startDir;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && tb != null)
                    tb.Text = dlg.SelectedPath;
            }
            finally { dlg.Dispose(); }
        }


        private void BrowseCsvButton_Click(object sender, RoutedEventArgs e)
        {
            var tb = FindName("CsvPathTextBox") as TextBox;
            string startDir = GetStartDirFromText(tb != null ? tb.Text : null);

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV (*.csv)|*.csv|Tous les fichiers|*.*",
                InitialDirectory = startDir,
                FileName = SafeFileNameFrom(tb != null ? tb.Text : null),
                RestoreDirectory = true
            };

            if (dlg.ShowDialog() == true && tb != null)
                tb.Text = dlg.FileName;
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ReadConfigFromUI();
            SaveConfig();
            MessageBox.Show("Paramètres enregistrés.", "Paramètres", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Config = new AppConfig();
            ApplyConfigToUI();
        }
        private void OpenRenderParams_Click(object sender, RoutedEventArgs e)
        {
            if (Config == null) Config = LoadConfig();

            // Brouillon détaché
            var draftJson = Newtonsoft.Json.JsonConvert.SerializeObject(Config);
            var draft = Newtonsoft.Json.JsonConvert.DeserializeObject<AppConfig>(draftJson);

            var dlg = new RenderParametersDialog { Owner = this, DataContext = draft };
            var ok = dlg.ShowDialog();
            if (ok == true)
            {
                // Appliquer le brouillon
                this.Config = draft;
                SaveConfig();
                ApplyConfigToUI();          // met à jour les champs visibles côté Render si tu en affiches
                AppendLog("Render parameters updated.");
                TryAutoFillOutputFolder();  // recalcule le chemin si nécessaire
            }
        }

        private void OpenPatchifyParams_Click(object sender, RoutedEventArgs e)
        {
            if (Config == null) Config = LoadConfig();

            // Brouillon détaché
            var draftJson = Newtonsoft.Json.JsonConvert.SerializeObject(Config);
            var draft = Newtonsoft.Json.JsonConvert.DeserializeObject<AppConfig>(draftJson);

            var dlg = new PatchifyParametersDialog { Owner = this, DataContext = draft };
            var ok = dlg.ShowDialog();
            if (ok == true)
            {
                this.Config = draft;
                SaveConfig();
                AppendLog("Patchify parameters updated.");
            }
        }
        private void sliderHeight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double roundedHeight = GetRoundedYpos();
            sliderHeight.Value = roundedHeight;
        }
    }
}

