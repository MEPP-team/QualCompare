using System;
using System.Windows;
using PatchifyLib;

namespace CompareMetrics
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            var app = new Application();
            var mainWindow = new MainWindow();
            app.Run(mainWindow);
        }
    }
}
