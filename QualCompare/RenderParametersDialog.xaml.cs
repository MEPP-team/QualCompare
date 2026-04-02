// RenderParametersDialog.xaml.cs
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using System.Drawing; // for Color

namespace QualCompare
{
    public partial class RenderParametersDialog : Window
    {
        public RenderParametersDialog()
        {
            InitializeComponent();
        }

        private static string GetStartDirFromText(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                string t = text.Trim().Trim('"');

                if (Directory.Exists(t)) return t; // directory
                if (File.Exists(t))
                {
                    string d = Path.GetDirectoryName(t);
                    if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return d;
                }

                string maybeParent = Path.GetDirectoryName(t);
                if (!string.IsNullOrEmpty(maybeParent) && Directory.Exists(maybeParent)) return maybeParent;
            }
            catch { }
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

        private static string ColorToHex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        // -----------------------
        // Numeric input filters
        // -----------------------
        // Integers only (0-9)
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

        // Floats like: 123, -123, 12.3, -0.5 (single dot, optional leading minus)
        private void floatInput_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            string text = sender is TextBox tb ? tb.Text : string.Empty;
            string newText = string.Concat(text, e.Text);

            // Reject multiple dots
            if (e.Text == "." && text.Contains(".")) { e.Handled = true; return; }
            // Dot cannot be first char (disallow ".5" -> require "0.5")
            if (e.Text == "." && text.Length == 0) { e.Handled = true; return; }
            // Only one leading minus
            if (e.Text == "-" && text.Length > 0) { e.Handled = true; return; }
            // Disallow characters other than digits, dot, minus
            bool okChar = int.TryParse(e.Text, out _) || e.Text == "." || e.Text == "-";
            if (!okChar) { e.Handled = true; return; }
        }

        // -----------------------
        // File/folder pickers
        // -----------------------
        private void BrowseBlenderButton_Click(object sender, RoutedEventArgs e)
        {
            var tb = this.FindName("BlenderPathTextBoxDlg") as TextBox;
            string startDir = GetStartDirFromText(tb != null ? tb.Text : null);

            var dlg = new OpenFileDialog
            {
                Filter = "blender.exe|blender.exe|Executables|*.exe",
                InitialDirectory = startDir,
                FileName = SafeFileNameFrom(tb != null ? tb.Text : null),
                CheckFileExists = true,
                RestoreDirectory = true
            };

            if (dlg.ShowDialog(this) == true && tb != null)
                tb.Text = dlg.FileName;
        }

        private void BrowseScriptButton_Click(object sender, RoutedEventArgs e)
        {
            var tb = this.FindName("RenderScriptPathTextBoxDlg") as TextBox;
            string startDir = GetStartDirFromText(tb != null ? tb.Text : null);

            var dlg = new OpenFileDialog
            {
                Filter = "Python (*.py)|*.py|All files|*.*",
                InitialDirectory = startDir,
                FileName = SafeFileNameFrom(tb != null ? tb.Text : null),
                CheckFileExists = true,
                RestoreDirectory = true
            };

            if (dlg.ShowDialog(this) == true && tb != null)
                tb.Text = dlg.FileName;
        }

        private void BrowseTempInButton_Click(object sender, RoutedEventArgs e)
        {
            var tb = this.FindName("TempInTextBoxDlg") as TextBox;
            string startDir = GetStartDirFromText(tb != null ? tb.Text : null);

            using (var dlg = new Forms.FolderBrowserDialog())
            {
                dlg.SelectedPath = startDir;
                if (dlg.ShowDialog() == Forms.DialogResult.OK && tb != null)
                    tb.Text = dlg.SelectedPath;
            }
        }

        private void BrowseTempOutButton_Click(object sender, RoutedEventArgs e)
        {
            var tb = this.FindName("TempOutTextBoxDlg") as TextBox;
            string startDir = GetStartDirFromText(tb != null ? tb.Text : null);

            using (var dlg = new Forms.FolderBrowserDialog())
            {
                dlg.SelectedPath = startDir;
                if (dlg.ShowDialog() == Forms.DialogResult.OK && tb != null)
                    tb.Text = dlg.SelectedPath;
            }
        }

        private void BrowseCsvButton_Click(object sender, RoutedEventArgs e)
        {
            var tb = this.FindName("CsvPathTextBoxDlg") as TextBox;
            string startDir = GetStartDirFromText(tb != null ? tb.Text : null);

            var dlg = new OpenFileDialog
            {
                Filter = "CSV (*.csv)|*.csv|All files|*.*",
                InitialDirectory = startDir,
                FileName = SafeFileNameFrom(tb != null ? tb.Text : null),
                CheckFileExists = true,
                RestoreDirectory = true
            };

            if (dlg.ShowDialog(this) == true && tb != null)
                tb.Text = dlg.FileName;
        }

        private void ChooseBgColorButton_Click(object sender, RoutedEventArgs e)
        {
            var tb = this.FindName("BgColorTextBoxDlg") as TextBox;
            string current = tb != null ? tb.Text : "#000000";

            using (var dlg = new Forms.ColorDialog())
            {
                try
                {
                    if (IsValidHexColor(current))
                    {
                        var hex = NormalizeHexColor(current);
                        int r = int.Parse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        int g = int.Parse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        int b = int.Parse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        dlg.Color = Color.FromArgb(r, g, b);
                    }
                    if (dlg.ShowDialog(this.GetIWin32Window()) == Forms.DialogResult.OK && tb != null)
                    {
                        tb.Text = ColorToHex(dlg.Color);
                    }
                }
                catch
                {
                    // Ignore invalid color formats
                }
            }
        }

        // Helper to get IWin32Window from WPF Window for WinForms dialogs
        // Avoids ownerless WinForms dialog in some environments.
        private Forms.IWin32Window GetIWin32Window()
        {
            var interopHelper = new System.Windows.Interop.WindowInteropHelper(this);
            return new Win32Window(interopHelper.Handle);
        }

        private class Win32Window : Forms.IWin32Window
        {
            private readonly IntPtr _handle;
            public Win32Window(IntPtr handle) { _handle = handle; }
            public IntPtr Handle { get { return _handle; } }
        }

        // OK/Cancel
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // If TextBoxes are two-way bound to DataContext properties, setting Text above
            // already updated the DataContext. Simply close dialog with OK.
            DialogResult = true;
            Close();
        }
    }
}

