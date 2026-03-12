using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenClawInstaller
{
    public class MainForm : Form
    {
        private TextBox pathInput;
        private TextBox proxyInput;
        private CheckBox debugCheck;
        private CheckBox saveDataLocalCheck; // 新增：保存数据到本地选项
        private Button deployBtn;
        private ProgressBar progressBar;
        private RichTextBox console;

        public MainForm()
        {
            Text = "OpenClaw 自动化部署工具";
            Size = new Size(640, 660);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Color.FromArgb(248, 249, 250); // 现代浅灰色背景

            try {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch 
            { 
                    // 容错处理：万一提取失败，系统会自动使用默认图标，不影响程序运行
            }

            SetupUI();
        }

        private void SetupUI()
        {
            // 顶部标题栏
            Panel headerPanel = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.FromArgb(0, 102, 204) };
            Label titleLabel = new Label { Text = "OpenClaw 自动化部署", ForeColor = Color.White, Font = new Font("Microsoft YaHei", 18F, FontStyle.Bold), AutoSize = true, Location = new Point(20, 25) };
            headerPanel.Controls.Add(titleLabel);
            Controls.Add(headerPanel);

            int yOffset = 110;

            // 1. 安装路径
            Label pathLabel = new Label { Text = "安装目录:", Location = new Point(20, yOffset + 5), AutoSize = true, ForeColor = Color.FromArgb(50, 50, 50) };
            pathInput = new TextBox { Location = new Point(140, yOffset), Width = 360, ReadOnly = true, PlaceholderText = "请选择软件的安装路径...", BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei", 10F) };
            Button browseBtn = new Button { Text = "浏览...", Location = new Point(510, yOffset - 2), Width = 80, Height = 28, Cursor = Cursors.Hand, BackColor = Color.FromArgb(224, 224, 224), FlatStyle = FlatStyle.Flat };
            browseBtn.FlatAppearance.BorderSize = 0;
            browseBtn.Click += (s, e) => {
                using var dialog = new FolderBrowserDialog { Description = "选择安装目录" };
                if (dialog.ShowDialog() == DialogResult.OK) pathInput.Text = dialog.SelectedPath;
            };

            yOffset += 50;

            // 2. GitHub 代理
            Label proxyLabel = new Label { Text = "Github 代理:", Location = new Point(20, yOffset + 5), AutoSize = true, ForeColor = Color.FromArgb(50, 50, 50) };
            proxyInput = new TextBox { Location = new Point(140, yOffset), Width = 360, Text = "https://gh-proxy.com/", PlaceholderText = "例如 https://gh-proxy.com/ (留空则不使用代理)", BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei", 10F) };
            
            // 选项开关
            debugCheck = new CheckBox { Text = "Debug 日志", Location = new Point(25, yOffset + 50), AutoSize = true, Cursor = Cursors.Hand, ForeColor = Color.DimGray };
            
            yOffset +=30;
            // 新增：将数据保存在安装目录选项 (默认勾选)
            saveDataLocalCheck = new CheckBox { Text = "将数据保存在安装目录 (便携模式)", Location = new Point(25, yOffset + 50), AutoSize = true, Cursor = Cursors.Hand, ForeColor = Color.DimGray, Checked = true };

            yOffset += 110;

            // 3. 一键部署按钮
            deployBtn = new Button { Text = "🚀 一键部署", Location = new Point(25, yOffset), Width = 565, Height = 45, Cursor = Cursors.Hand, BackColor = Color.FromArgb(0, 102, 204), ForeColor = Color.White, Font = new Font("Microsoft YaHei", 12F, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            deployBtn.FlatAppearance.BorderSize = 0;
            deployBtn.Click += async (s, e) => await StartDeployment();

            yOffset += 65;

            // 4. 进度条
            progressBar = new ProgressBar { Location = new Point(25, yOffset), Width = 565, Height = 8, Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Continuous };

            yOffset += 25;

            // 5. 控制台日志
            Label logLabel = new Label { Text = "部署日志:", Location = new Point(25, yOffset), AutoSize = true, ForeColor = Color.FromArgb(50, 50, 50) };
            console = new RichTextBox { 
                Location = new Point(25, yOffset + 25), Width = 565, Height = 170, 
                ReadOnly = true, BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.FromArgb(0, 210, 106), // 黑底绿字极客风
                Font = new Font("Consolas", 9.5F), BorderStyle = BorderStyle.None
            };

            // 添加到窗体
            Controls.AddRange(new Control[] { pathLabel, pathInput, browseBtn, proxyLabel, proxyInput, debugCheck, saveDataLocalCheck, deployBtn, progressBar, logLabel, console });
        }

        private async Task StartDeployment()
        {
            string installDir = pathInput.Text.Trim();
            string githubProxy = proxyInput.Text.Trim();
            bool isDebug = debugCheck.Checked;
            bool saveDataLocal = saveDataLocalCheck.Checked; // 获取新选项

            if (string.IsNullOrEmpty(installDir)) { MessageBox.Show("请先选择安装目录！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            // 更改按钮状态提供反馈
            deployBtn.Enabled = false;
            deployBtn.Text = "部署中，请稍候...";
            deployBtn.BackColor = Color.Gray;
            console.Clear();
            progressBar.Value = 0;

            var progress = new Progress<int>(percent => progressBar.Value = percent);
            var logger = new Progress<string>(msg => {
                console.AppendText(msg + Environment.NewLine);
                console.ScrollToCaret();
            });

            try
            {
                // 传入 saveDataLocal 变量
                var worker = new DeployWorker(installDir, githubProxy, isDebug, saveDataLocal);
                await worker.RunAsync(progress, logger);
                
                MessageBox.Show("OpenClaw 部署成功！\n\n您现在可以运行安装目录下的 \"start.ps1\" 或者 \"点我运行.bat\" 进行初始化配置。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ((IProgress<string>)logger).Report($"\n[严重错误] 部署中断: {ex.Message}");
                if (isDebug)
                {
                    ((IProgress<string>)logger).Report($"[DEBUG 堆栈] {ex.StackTrace}");
                }
                MessageBox.Show($"部署失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // 恢复按钮状态
                deployBtn.Enabled = true;
                deployBtn.Text = "🚀 一键部署";
                deployBtn.BackColor = Color.FromArgb(0, 102, 204);
            }
        }
    }
}

