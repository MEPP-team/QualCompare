using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CompareMetrics
{
    public partial class MainWindow : Window
    {
        private string objFilePath;
        private string outputDir;
        private string imgToPatchifyPath;
        public MainWindow()
        {
            InitializeComponent();
            ObjFilePathTextBox.Text                        = Properties.Settings.Default.OBJfileLocation;
            OutputDirTextBox.Text                          = Properties.Settings.Default.outputFolder;
            ImgToPatchifyTextBox.Text                      = Properties.Settings.Default.patchDir;
            NbViewsTextBox.Text                            = Properties.Settings.Default.nbViewsSave;
            comboBoxFileSelection.SelectedIndex            = Properties.Settings.Default.fileSelectionTypeIndex;
            MethodComboBox.SelectedIndex                   = Properties.Settings.Default.selectedMethod;
            FormatComboBox.SelectedIndex                   = Properties.Settings.Default.fileFormat;
            sliderHeight.Value                             = Properties.Settings.Default.cameraHeightValue;
            patchifySingleOrMultipleComboBox.SelectedIndex = Properties.Settings.Default.patchifySingleOrMultipleIndex;

            if (MethodComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedMethod = selectedItem.Content.ToString();
                NbViewsTextBox.IsEnabled = true;

                switch (selectedMethod)
                {
                    case "Fibonacci":
                        FibonacciGrid.Visibility = Visibility.Visible;
                        YFixedGrid.Visibility = Visibility.Collapsed;
                        break;
                    case "Y fixé":
                        YFixedGrid.Visibility = Visibility.Visible;
                        break;
                    case "Polyèdrale":
                        PolyhedronGrid.Visibility = Visibility.Visible;
                        YFixedGrid.Visibility = Visibility.Collapsed;
                        NbViewsTextBox.IsEnabled = false;
                        NbViewsTextBox.Text = "4"; // Tetraèdre sélectionné par défaut
                        break;
                }
            }

            Config = LoadConfig() ?? new AppConfig();
            ApplyConfigToUI();
            TryAutoFillOutputFolder();
        }

        private void SelectObjFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            // On définit le dossier présélectionné comme étant le dossier en mémoire 
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            dialog.SelectedPath = ObjFilePathTextBox.Text;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                objFilePath = dialog.SelectedPath;
                ObjFilePathTextBox.Text = objFilePath;
            }
        }

        private void SelectOutputDir_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();

            dialog.RootFolder = Environment.SpecialFolder.MyComputer;

            dialog.SelectedPath = OutputDirTextBox.Text;
            //// Print the selected path for debugging purposes
            //System.Windows.MessageBox.Show(dialog.SelectedPath);
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                outputDir = dialog.SelectedPath;
                OutputDirTextBox.Text = outputDir;
            }
        }

        private void SelectImageToPatchify_Click(object sender, RoutedEventArgs e)
        {
            if(patchifySingleOrMultipleComboBox.SelectedIndex == 0) // Single image patchification
            {
                System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
                // Filter for PNG or JPG files
                openFileDialog.Filter = "Image files (*.png;*.jpg)|*.png;*.jpg";
                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    imgToPatchifyPath = openFileDialog.FileName;
                    ImgToPatchifyTextBox.Text = imgToPatchifyPath;
                }
            }
            else // Multiple image patchification
            {
                System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
                // We set the root folder as the project folder
                folderBrowserDialog.RootFolder = Environment.SpecialFolder.MyComputer;
                // For selected path, we need to remove the file name from the path
                folderBrowserDialog.SelectedPath = ImgToPatchifyTextBox.Text.LastIndexOf("\\") > 0 ? ImgToPatchifyTextBox.Text.Substring(0, ImgToPatchifyTextBox.Text.LastIndexOf("\\")) : ImgToPatchifyTextBox.Text;
                if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    imgToPatchifyPath = folderBrowserDialog.SelectedPath;
                    ImgToPatchifyTextBox.Text = imgToPatchifyPath;
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.OBJfileLocation = ObjFilePathTextBox.Text;
            Properties.Settings.Default.outputFolder = OutputDirTextBox.Text;
            Properties.Settings.Default.patchDir = (FindName("ImgToPatchifyTextBox") as System.Windows.Controls.TextBox)?.Text;
            Properties.Settings.Default.nbViewsSave = NbViewsTextBox.Text;
            Properties.Settings.Default.fileSelectionTypeIndex = comboBoxFileSelection.SelectedIndex;
            Properties.Settings.Default.selectedMethod = MethodComboBox.SelectedIndex;
            Properties.Settings.Default.fileFormat = FormatComboBox.SelectedIndex;
            Properties.Settings.Default.cameraHeightValue = sliderHeight.Value;
            Properties.Settings.Default.patchifySingleOrMultipleIndex = patchifySingleOrMultipleComboBox.SelectedIndex;
            Properties.Settings.Default.Save();

            // Sauvegarde JSON
            ReadConfigFromUI();
            SaveConfig();
        }
    }
}