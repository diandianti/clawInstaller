using System;
using System.Windows.Forms;

namespace OpenClawInstaller
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // 关键：在启动前开启高 DPI 模式
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ApplicationConfiguration.Initialize();
            // 正常启动窗体
            Application.Run(new MainForm());
        }
    }

}
