using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace QualCompare
{
    public partial class MainWindow : Window
    {
        public enum RenderJobState
        {
            Pending,
            Running,
            Completed,
            Failed,
            Cancelled
        }

        public sealed class RenderJob : INotifyPropertyChanged
        {
            private string _title;
            public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }

            private string _summary;
            public string Summary { get => _summary; set { _summary = value; OnPropertyChanged(); } }

            private RenderJobState _state = RenderJobState.Pending;
            public RenderJobState State { get => _state; set { _state = value; OnPropertyChanged(); } }

            private double _progress;
            public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }

            private bool _autoCloseWhenDone;
            public bool AutoCloseWhenDone { get => _autoCloseWhenDone; set { _autoCloseWhenDone = value; OnPropertyChanged(); } }

            private string _logText = "";
            public string LogText { get => _logText; set { _logText = value; OnPropertyChanged(); } }

            public StringBuilder LogBuffer { get; } = new StringBuilder();

            // Snapshot parameters (no AppConfig exposed)
            public string BlenderPath { get; set; }
            public string RenderScript { get; set; }
            public string TempInputRoot { get; set; }
            public string TempOutputRoot { get; set; }

            public string InputDir { get; set; }
            public string OutputDir { get; set; }
            public string Extension { get; set; }
            public string PositionsType { get; set; }
            public string FileType { get; set; }
            public string ObjType { get; set; }
            public string YPos { get; set; }
            public int NbViews { get; set; }

            public int ResolutionX { get; set; }
            public int ResolutionY { get; set; }
            public string RenderEngine { get; set; }
            public int TaaSamples { get; set; }
            public double FilterSize { get; set; }
            public int MaskThreshold { get; set; }
            public double SunEnergy { get; set; }
            public double SunTheta { get; set; }
            public double SunPhi { get; set; }
            public double PointRadiusFraction { get; set; }
            public string PlyRenderMode { get; set; }
            public int PlyVoxelBits { get; set; }
            public double VoxelRadiusMultiplier { get; set; }
            public string BgHex { get; set; }
            public string UpAxis { get; set; }

            public int MaxParallelism { get; set; }
            public bool PrefetchToSSD { get; set; }


            public CancellationTokenSource Cts { get; } = new CancellationTokenSource();
            public ConcurrentDictionary<int, Process> RunningProcesses { get; } = new ConcurrentDictionary<int, Process>();

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public ObservableCollection<RenderJob> Jobs { get; } = new ObservableCollection<RenderJob>();

        private readonly BlockingCollection<RenderJob> _jobQueue =
            new BlockingCollection<RenderJob>(new ConcurrentQueue<RenderJob>());

        private Task _queueWorkerTask;
        private readonly object _queueLock = new object();
        private RenderJob _currentJob;

        private void EnsureQueueWorkerStarted()
        {
            lock (_queueLock)
            {
                if (_queueWorkerTask != null) return;

                _queueWorkerTask = Task.Run(() =>
                {
                    foreach (var job in _jobQueue.GetConsumingEnumerable())
                    {
                        _currentJob = job;
                        try { RunRenderJobAsync(job).GetAwaiter().GetResult(); }
                        finally { _currentJob = null; }
                    }
                });
            }
        }

        private void EnqueueJob(RenderJob job)
        {
            EnsureQueueWorkerStarted();
            _jobQueue.Add(job);
        }

        private void JobAppendLog(RenderJob job, string message)
        {
            if (job == null || string.IsNullOrWhiteSpace(message)) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                job.LogBuffer.AppendLine(message);
                job.LogText = job.LogBuffer.ToString();

                // Optional: also mirror to the main Render tab log
                LogTextBox.AppendText(message + Environment.NewLine);
                LogScrollViewer.ScrollToEnd();
            }));
        }

        private RenderJob CreateJobFromCurrentUIOrNull()
        {
            if (Config == null) Config = LoadConfig();
            ReadConfigFromUI();
            SaveConfig();




            if (!int.TryParse(NbViewsTextBox.Text, out int nbViews) || nbViews <= 0)
            {
                MessageBox.Show("Invilid number of views.");
                return null;
            }

            string[] viewingMethods = { "fibonacci", "yfixed", "polyedric" };
            string[] fileTypes = { "everything", "source", "distorted" };

            int selectedMethodIndex = Math.Max(0, MethodComboBox.SelectedIndex);
            int selectedFileTypeIndex = Math.Max(0, comboBoxFileSelection.SelectedIndex);

            string positionsType = viewingMethods[selectedMethodIndex];
            string fileType = fileTypes[selectedFileTypeIndex];

            string inputDir = ObjFilePathTextBox.Text;
            string outputDir = OutputDirTextBox.Text;
            string objType = FormatComboBox.Text;

            if (string.IsNullOrWhiteSpace(inputDir) || !Directory.Exists(inputDir))
            {
                MessageBox.Show("Input Folder invalide.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(outputDir))
            {
                MessageBox.Show("Output Folder invalide.");
                return null;
            }

            // Current hardcoded defaults from your RenderButton_Click
            string tempInputRoot = Config.TempInputRoot;
            string tempOutputRoot = Config.TempOutputRoot;
            string blenderPath = Config.BlenderPath;
            string renderScript = Config.RenderScriptPath;
            string extension = Config.DefaultImageExt;

            string ypos = sliderHeight.Value.ToString("F2", CultureInfo.InvariantCulture);

            var job = new RenderJob
            {
                Title = $"Job {Jobs.Count + 1}",
                Summary = $"{positionsType} | {fileType} | {nbViews}VP | {objType} | ypos={ypos}",
                AutoCloseWhenDone = false,

                BlenderPath = blenderPath,
                RenderScript = renderScript,
                TempInputRoot = tempInputRoot,
                TempOutputRoot = tempOutputRoot,

                InputDir = inputDir,
                OutputDir = outputDir,
                Extension = extension,
                PositionsType = positionsType,
                FileType = fileType,
                ObjType = objType,
                YPos = ypos,
                NbViews = nbViews,

                UpAxis = Config.UpAxis, 
                BgHex = Config.BackgroundColorHex,
                ResolutionX = Config.ResolutionX,
                ResolutionY = Config.ResolutionY,
                RenderEngine = Config.RenderEngine,
                TaaSamples = Config.TaaSamples,
                FilterSize = Config.FilterSize,
                MaskThreshold = Config.MaskThreshold,
                SunEnergy = Config.SunEnergy,
                SunTheta = Config.SunTheta,
                SunPhi = Config.SunPhi,
                PointRadiusFraction = Config.PointRadiusFraction,
                PlyRenderMode = Config.PlyRenderMode,
                PlyVoxelBits = Config.PlyVoxelBits,
                VoxelRadiusMultiplier = Config.VoxelRadiusMultiplier,

                MaxParallelism = Config.MaxParallelism,
                PrefetchToSSD = Config.PrefetchToSSD,
            };

            return job;
        }

        private async Task RunRenderJobAsync(RenderJob job)
        {
            if (job.Cts.IsCancellationRequested)
            {
                Dispatcher.Invoke(() => job.State = RenderJobState.Cancelled);
                return;
            }

            Dispatcher.Invoke(() =>
            {
                job.State = RenderJobState.Running;
                job.Progress = 0;

                StopRenderButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.Value = 0;
                LogTextBox.Clear();
            });

            var token = job.Cts.Token;

            Directory.CreateDirectory(job.TempInputRoot);
            Directory.CreateDirectory(job.TempOutputRoot);

            var hddReadGate = new SemaphoreSlim(1, 1);

            string searchPattern = "*." + job.ObjType;

            string[] allFiles = Directory.GetFiles(job.InputDir, searchPattern, SearchOption.AllDirectories)
                .Where(path =>
                {
                    string lowerPath = path.ToLowerInvariant();
                    string[] sourceKeywords = { "source", "ref", "reference", "src" };
                    bool isSource = sourceKeywords.Any(k => lowerPath.Split(Path.DirectorySeparatorChar).Any(seg => seg == k || seg.StartsWith(k + "_")));
                    // If we selected "source", we want only files in paths containing source keywords.
                    // If we selected "distorted", we want only files NOT in paths containing source keywords.
                    // If we selected "everything", we want all files
                    if (job.FileType == "source") return isSource;
                    if (job.FileType == "distorted") return !isSource;
                    return true;
                }).ToArray();

            int nbFiles = allFiles.Length;
            int currentIndex = 0;
            
            if (nbFiles == 0)
            {
                Dispatcher.Invoke(() => job.State = RenderJobState.Failed);
                JobAppendLog(job, "[ERROR] No file found for this job.");
                return;
            }

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                var po = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 4),
                    CancellationToken = token
                };

                await Task.Run(() =>
                {
                    Parallel.ForEach(allFiles, po, file =>
                    {
                        po.CancellationToken.ThrowIfCancellationRequested();

                        string cachedObjPath;
                        hddReadGate.Wait(po.CancellationToken);
                        try
                        {
                            cachedObjPath = PrefetchObjToSSD(file, job.TempInputRoot);
                        }
                        finally
                        {
                            hddReadGate.Release();
                        }

                        string name = Path.GetFileNameWithoutExtension(file);

                        //string args =
                        //    $"--background --python \"{job.RenderScript}\" -- " +
                        //    $"--obj \"{cachedObjPath}\" " +
                        //    $"--out \"{job.TempOutputRoot}\" " +
                        //    $"--nb_views {job.NbViews} " +
                        //    $"--ext {job.Extension} " +
                        //    $"--positions_type {job.PositionsType} " +
                        //    $"--file_type {job.FileType} " +
                        //    $"--obj_type {job.ObjType} " +
                        //    $"--ypos {job.YPos}";
                        string args = $"--background " +
                                      $"--python \"{job.RenderScript}\" " +
                                      $"-- " +
                                      // --- Input parameters ---
                                      $"--obj \"{cachedObjPath}\" " +
                                      $"--out \"{job.TempOutputRoot}\" " +
                                      $"--nb_views {job.NbViews} " +
                                      $"--positions_type {job.PositionsType} " +
                                      $"--ext {job.Extension} " +
                                      $"--file_type {job.FileType} " +
                                      $"--obj_type {job.ObjType} " +
                                      $"--ypos {job.YPos} " +
                                      $"--up_axis {job.UpAxis} " +
                                      $"--resx {job.ResolutionX} " +
                                      $"--resy {job.ResolutionY} " +
                                      $"--engine {job.RenderEngine} " +
                                      $"--taa {job.TaaSamples} " +
                                      $"--filter_size {job.FilterSize.ToString(CultureInfo.InvariantCulture)} " +
                                      $"--mask_threshold {job.MaskThreshold} " +
                                      $"--sun_energy {job.SunEnergy.ToString(CultureInfo.InvariantCulture)} " +
                                      $"--sun_theta {job.SunTheta.ToString(CultureInfo.InvariantCulture)} " +
                                      $"--sun_phi {job.SunPhi.ToString(CultureInfo.InvariantCulture)} " +
                                      $"--point_radius_fraction {job.PointRadiusFraction.ToString(CultureInfo.InvariantCulture)} " +
                                      $"--ply_render {job.PlyRenderMode} " +
                                      $"--ply_voxel_bits {job.PlyVoxelBits} " +
                                      $"--voxel_radius_multiplier {job.VoxelRadiusMultiplier.ToString(CultureInfo.InvariantCulture)} " +
                                      $"--bg_color {job.BgHex}";

                        var psi = new ProcessStartInfo
                        {
                            FileName = job.BlenderPath,
                            Arguments = args,
                            UseShellExecute = false,
                            RedirectStandardOutput = false,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            StandardErrorEncoding = Encoding.UTF8
                        };

                        using (Process p = Process.Start(psi))
                        {
                            if (p == null) throw new InvalidOperationException("Blender process couldn't start.");

                            job.RunningProcesses.TryAdd(p.Id, p);

                            p.ErrorDataReceived += (s, ea) =>
                            {
                                if (!string.IsNullOrWhiteSpace(ea.Data))
                                    JobAppendLog(job, "[ERROR] " + ea.Data);
                            };
                            p.BeginErrorReadLine();

                            p.WaitForExit();

                            job.RunningProcesses.TryRemove(p.Id, out _);
                        }

                        // Cleanup + copy cached output
                        try
                        {
                            Directory.Delete(Path.GetDirectoryName(cachedObjPath), true);

                            string cachedOutputDir = Path.Combine(job.TempOutputRoot, Path.GetFileNameWithoutExtension(file));
                            string finalOutputDir = Path.Combine(job.OutputDir, Path.GetFileNameWithoutExtension(file));

                            if (Directory.Exists(cachedOutputDir))
                            {
                                Directory.CreateDirectory(finalOutputDir);

                                foreach (string dirPath in Directory.GetDirectories(cachedOutputDir, "*", SearchOption.AllDirectories))
                                    Directory.CreateDirectory(dirPath.Replace(cachedOutputDir, finalOutputDir));

                                foreach (string newPath in Directory.GetFiles(cachedOutputDir, "*.*", SearchOption.AllDirectories))
                                    File.Copy(newPath, newPath.Replace(cachedOutputDir, finalOutputDir), true);

                                Directory.Delete(cachedOutputDir, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            JobAppendLog(job, "[ERROR] Cleanup/copy failed: " + ex.Message);
                        }

                        int index = Interlocked.Increment(ref currentIndex);

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            double pct = (double)index / nbFiles * 100.0;
                            job.Progress = pct;
                            ProgressBar.Value = pct;
                        }));

                        JobAppendLog(job, $"[{index}/{nbFiles}] - {name} rendered.");
                    });
                }, token);

                sw.Stop();

                Dispatcher.Invoke(() =>
                {
                    job.State = RenderJobState.Completed;
                    JobAppendLog(job, $"Rendering done in {sw.ElapsedMilliseconds} ms.");

                    StopRenderButton.IsEnabled = false;
                    ProgressBar.Value = 0;

                    if (job.AutoCloseWhenDone)
                        Jobs.Remove(job);
                });
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                Dispatcher.Invoke(() =>
                {
                    job.State = RenderJobState.Cancelled;
                    JobAppendLog(job, $"Rendering cancelled after {sw.ElapsedMilliseconds} ms.");

                    StopRenderButton.IsEnabled = false;
                    ProgressBar.Value = 0;

                    if (job.AutoCloseWhenDone)
                        Jobs.Remove(job);
                });
            }
            catch (Exception ex)
            {
                sw.Stop();

                Dispatcher.Invoke(() =>
                {
                    job.State = RenderJobState.Failed;
                    JobAppendLog(job, "[ERROR] " + ex.Message);

                    StopRenderButton.IsEnabled = false;
                    ProgressBar.Value = 0;

                    if (job.AutoCloseWhenDone)
                        Jobs.Remove(job);
                });
            }
        }

        private void CancelCurrentJob()
        {
            var job = _currentJob;
            if (job == null) return;

            if (!job.Cts.IsCancellationRequested)
                job.Cts.Cancel();

            foreach (var kv in job.RunningProcesses)
            {
                try
                {
                    var p = kv.Value;
                    if (p != null && !p.HasExited) p.Kill();
                }
                catch { }
            }

            JobAppendLog(job, "Rendering stopped by user.");
        }

        private void QueueCurrentJob_Click(object sender, RoutedEventArgs e)
        {
            var job = CreateJobFromCurrentUIOrNull();
            if (job == null) return;

            Jobs.Add(job);
            EnqueueJob(job);

            // Optional: switch to Queue tab for visibility
            tabControl.SelectedIndex = 1; // Adjust if you insert Queue elsewhere
        }

        private void CancelSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            var job = JobsTabControl.SelectedItem as RenderJob;
            if (job == null) return;

            job.Cts.Cancel();

            if (ReferenceEquals(job, _currentJob))
                CancelCurrentJob();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (job.State == RenderJobState.Pending)
                    job.State = RenderJobState.Cancelled;
            }));
        }

        private void RemoveSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            var job = JobsTabControl.SelectedItem as RenderJob;
            if (job == null) return;

            if (job.State == RenderJobState.Running)
            {
                MessageBox.Show("Impossible de supprimer un job en cours. Annule-le d'abord.");
                return;
            }

            Jobs.Remove(job);
        }
    }
}

