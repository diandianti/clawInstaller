using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace OpenClawInstaller
{
    public class DeployWorker
    {
        private readonly string installDir;
        private readonly string githubProxy;
        private readonly bool isDebug;

        private readonly string nodeUrl = "https://registry.npmmirror.com/-/binary/node/v25.8.0/node-v25.8.0-win-x64.zip";
        private readonly string gitUrl = "https://npmmirror.com/mirrors/git-for-windows/v2.44.0.windows.1/MinGit-2.44.0-64-bit.zip";

        public DeployWorker(string installDir, string githubProxy, bool isDebug = false)
        {
            this.installDir = Path.GetFullPath(installDir);
            this.githubProxy = githubProxy?.Trim();
            this.isDebug = isDebug;
        }

        private void DebugLog(IProgress<string> logger, string message)
        {
            if (isDebug) logger.Report($"[DEBUG] {message}");
        }

        public async Task RunAsync(IProgress<int> progress, IProgress<string> logger)
        {
            Directory.CreateDirectory(installDir);
            logger.Report($"开始部署... 安装目录: {installDir}");
            DebugLog(logger, $"当前工作目录: {Environment.CurrentDirectory}");

            string nodeZipPath = Path.Combine(installDir, "node_env.zip");
            string gitZipPath = Path.Combine(installDir, "mingit.zip");

            string nodejsDir = Path.Combine(installDir, "nodejs");
            string gitDir = Path.Combine(installDir, "git_env");
            string appDir = Path.Combine(installDir, "openclaw_app");

            // ==========================================
            // 1. 处理 Node.js 环境
            // ==========================================
            if (Directory.Exists(nodejsDir))
            {
                logger.Report("-> 检测到 Node.js 目录已存在，跳过下载与解压。");
                progress.Report(20);
            }
            else
            {
                if (File.Exists(nodeZipPath) && IsValidZip(nodeZipPath, logger))
                {
                    logger.Report("-> 检测到完整的 Node.js 压缩包，跳过下载。");
                }
                else
                {
                    logger.Report("正在下载 Node.js 环境...");
                    await Utils.DownloadFileAsync(nodeUrl, nodeZipPath, p => progress.Report((int)(p * 0.15)));
                }
                logger.Report("正在解压 Node.js...");
                Utils.ExtractAndRenameZip(nodeZipPath, installDir, "node-v", "nodejs");
                progress.Report(20);
            }

            // ==========================================
            // 2. 处理 MinGit 环境
            // ==========================================
            if (Directory.Exists(gitDir))
            {
                logger.Report("-> 检测到 MinGit 目录已存在，跳过下载与解压。");
                progress.Report(40);
            }
            else
            {
                if (File.Exists(gitZipPath) && IsValidZip(gitZipPath, logger))
                {
                    logger.Report("-> 检测到完整的 MinGit 压缩包，跳过下载。");
                }
                else
                {
                    logger.Report("正在下载 MinGit 环境...");
                    await Utils.DownloadFileAsync(gitUrl, gitZipPath, p => progress.Report(20 + (int)(p * 0.15)));
                }
                logger.Report("正在解压 MinGit...");
                Utils.ExtractZipDirect(gitZipPath, gitDir);
                progress.Report(40);
            }

            // ==========================================
            // 3. 初始化 OpenClaw 本地环境与配置
            // ==========================================
            Directory.CreateDirectory(appDir);
            
            string packageJsonPath = Path.Combine(appDir, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                File.WriteAllText(packageJsonPath, "{\n  \"name\": \"openclaw-env\",\n  \"version\": \"1.0.0\",\n  \"private\": true\n}", new UTF8Encoding(false));
            }

            // ==========================================
            // 4. 配置 NPM 环境、淘宝源和 Git 代理策略
            // ==========================================
            string gitCmdDir = Path.Combine(gitDir, "cmd");
            string gitExePath = Path.Combine(gitCmdDir, "git.exe");
            string npmCmdPath = Path.Combine(nodejsDir, "npm.cmd");
            string customPathEnv = $"{nodejsDir};{gitCmdDir};{Environment.GetEnvironmentVariable("PATH")}";

            logger.Report("正在配置 npm 淘宝镜像源...");
            progress.Report(45);
            
            var psiNpmConfig = new ProcessStartInfo
            {
                FileName = npmCmdPath,
                Arguments = "config set registry https://registry.npmmirror.com",
                WorkingDirectory = appDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            psiNpmConfig.EnvironmentVariables["PATH"] = customPathEnv;

            using (var process = new Process { StartInfo = psiNpmConfig })
            {
                process.Start();
                await process.WaitForExitAsync();
            }

            logger.Report("正在配置 Git 网络策略...");
            progress.Report(50);

            string targetGitUrl = "https://github.com/";
            if (!string.IsNullOrEmpty(githubProxy))
            {
                targetGitUrl = githubProxy.EndsWith("/") ? $"{githubProxy}https://github.com/" : $"{githubProxy}/https://github.com/";
                DebugLog(logger, $"已启用 GitHub 代理，目标路由将指向: {targetGitUrl}");
            }

            string[] gitPrefixesToReplace = {
                "ssh://git@github.com/",
                "git@github.com:",
                "git://github.com/",
                "https://github.com/"
            };

            foreach (string prefix in gitPrefixesToReplace)
            {
                var psiGitConfig = new ProcessStartInfo
                {
                    FileName = gitExePath,
                    Arguments = $"config --global url.\"{targetGitUrl}\".insteadOf \"{prefix}\"",
                    WorkingDirectory = appDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                psiGitConfig.EnvironmentVariables["PATH"] = customPathEnv;

                using (var process = new Process { StartInfo = psiGitConfig })
                {
                    process.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) DebugLog(logger, $"[Git 代理配置] {e.Data.Trim()}"); };
                    process.Start();
                    process.BeginOutputReadLine();
                    await process.WaitForExitAsync();
                }
            }

            // ==========================================
            // 5. 使用 npm 直接安装 OpenClaw (带重试机制)
            // ==========================================
            int maxRetries = 3;
            bool installSuccess = false;

            string npmInstallArgs = isDebug 
                ? "install openclaw --cache ../.npm-cache --loglevel verbose" 
                : "install openclaw --cache ../.npm-cache --no-fund --no-audit";

            for (int i = 0; i < maxRetries; i++)
            {
                logger.Report($"正在通过 npm 安装 OpenClaw 核心组件 (第 {i + 1} 次尝试，最多 {maxRetries} 次)...");
                if (i == 0) progress.Report(60);

                var psiInstall = new ProcessStartInfo
                {
                    FileName = npmCmdPath,
                    Arguments = npmInstallArgs,
                    WorkingDirectory = appDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                psiInstall.EnvironmentVariables["PATH"] = customPathEnv;

                DebugLog(logger, $"执行命令: {psiInstall.FileName} {psiInstall.Arguments}");

                using (var process = new Process { StartInfo = psiInstall })
                {
                    process.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) logger.Report($"[NPM] {e.Data.Trim()}"); };
                    process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) logger.Report($"[NPM ERR] {e.Data.Trim()}"); };
                    
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        installSuccess = true;
                        break;
                    }
                    else
                    {
                        logger.Report($"[警告] 第 {i + 1} 次 npm install 失败，退出码: {process.ExitCode}");
                        if (i < maxRetries - 1)
                        {
                            logger.Report("等待 5 秒后准备重试...");
                            await Task.Delay(5000);
                        }
                    }
                }
            }

            if (!installSuccess)
            {
                throw new Exception($"npm install openclaw 经过 {maxRetries} 次尝试后仍然失败。请检查网络连接或更换 GitHub 代理。");
            }
            progress.Report(80);

            // ==========================================
            // 6. 生成交互式 start.ps1 启动脚本 
            // ==========================================
            logger.Report("正在生成 start.ps1 交互式启动脚本...");
            string ps1Path = Path.Combine(installDir, "start.ps1");

            var ps1Builder = new StringBuilder();
            
            ps1Builder.AppendLine("$scriptDir = $PSScriptRoot");
            ps1Builder.AppendLine("");
            
            // 优先唤起 Windows Terminal
            ps1Builder.AppendLine("# 如果系统安装了 Windows Terminal 且当前不在其中，则使用其打开");
            ps1Builder.AppendLine("if ($env:WT_SESSION -eq $null -and (Get-Command wt.exe -ErrorAction SilentlyContinue)) {");
            ps1Builder.AppendLine("    $launchCmd = \"& `\"$PSCommandPath`\"\";");
            ps1Builder.AppendLine("    $encodedLaunch = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($launchCmd))");
            ps1Builder.AppendLine("    Start-Process wt.exe -ArgumentList \"-w new-tab --title OpenClaw启动器 powershell -NoExit -EncodedCommand $encodedLaunch\"");
            ps1Builder.AppendLine("    exit");
            ps1Builder.AppendLine("}");
            ps1Builder.AppendLine("");
            
            // 开启 ANSI 支持
            ps1Builder.AppendLine("if ($PSVersionTable.PSVersion.Major -le 5 -or $env:WT_SESSION -eq $null) {");
            ps1Builder.AppendLine("    try {");
            ps1Builder.AppendLine("        $code = @\"");
            ps1Builder.AppendLine("        using System;");
            ps1Builder.AppendLine("        using System.Runtime.InteropServices;");
            ps1Builder.AppendLine("        public class AnsiSupport {");
            ps1Builder.AppendLine("            [DllImport(\"kernel32.dll\", SetLastError = true)]");
            ps1Builder.AppendLine("            private static extern IntPtr GetStdHandle(int handle);");
            ps1Builder.AppendLine("            [DllImport(\"kernel32.dll\", SetLastError = true)]");
            ps1Builder.AppendLine("            private static extern bool GetConsoleMode(IntPtr handle, out uint mode);");
            ps1Builder.AppendLine("            [DllImport(\"kernel32.dll\", SetLastError = true)]");
            ps1Builder.AppendLine("            private static extern bool SetConsoleMode(IntPtr handle, uint mode);");
            ps1Builder.AppendLine("            public static void Enable() {");
            ps1Builder.AppendLine("                var handle = GetStdHandle(-11);");
            ps1Builder.AppendLine("                if (GetConsoleMode(handle, out uint mode)) {");
            ps1Builder.AppendLine("                    SetConsoleMode(handle, mode | 0x0004);");
            ps1Builder.AppendLine("                }");
            ps1Builder.AppendLine("            }");
            ps1Builder.AppendLine("        }");
            ps1Builder.AppendLine("\"@");
            ps1Builder.AppendLine("        Add-Type -TypeDefinition $code -ErrorAction SilentlyContinue");
            ps1Builder.AppendLine("        [AnsiSupport]::Enable()");
            ps1Builder.AppendLine("    } catch {}");
            ps1Builder.AppendLine("}");
            ps1Builder.AppendLine("");

            ps1Builder.AppendLine("$host.UI.RawUI.WindowTitle = \"OpenClaw启动器\"");
            ps1Builder.AppendLine("$env:PATH = \"$scriptDir\\nodejs;$scriptDir\\git_env\\cmd;$env:PATH\"");
            ps1Builder.AppendLine("Set-Location -Path \"$scriptDir\\openclaw_app\"");
            ps1Builder.AppendLine("");
            ps1Builder.AppendLine("function Show-Menu {");
            ps1Builder.AppendLine("    Clear-Host");
            ps1Builder.AppendLine("    Write-Host \"  🦞 OpenClaw\"");
            ps1Builder.AppendLine("    Write-Host \"  All your chats, one OpenClaw.\"");
            ps1Builder.AppendLine("    if (-not (Get-Command wt.exe -ErrorAction SilentlyContinue)) {");
            ps1Builder.AppendLine("        Write-Host \"  [提示] 您的系统未安装 Windows Terminal，界面排版和图标可能无法完美显示。\" -ForegroundColor DarkYellow");
            ps1Builder.AppendLine("    }");
            ps1Builder.AppendLine("    Write-Host \"\"");
            ps1Builder.AppendLine("    Write-Host \"1. 运行 Onboard 向导 (官方引导设置，英文版)\"");
            ps1Builder.AppendLine("    Write-Host \"2. 运行 Gateway\"");
            ps1Builder.AppendLine("    Write-Host \"3. 打开终端 (使用npx openclaw运行启动claw cli)\"");
            ps1Builder.AppendLine("    Write-Host \"4. 退出\"");
            ps1Builder.AppendLine("    Write-Host \"\"");
            ps1Builder.AppendLine("    $choice = Read-Host \"请输入选项 (1-4)，首次运行需要先执行 1 \"");
            ps1Builder.AppendLine("    switch ($choice) {");
            ps1Builder.AppendLine("        \"1\" { Run-Onboard }");
            ps1Builder.AppendLine("        \"2\" { Run-Gateway }");
            ps1Builder.AppendLine("        \"3\" { Open-Terminal }");
            ps1Builder.AppendLine("        \"4\" { exit }");
            ps1Builder.AppendLine("        default { Show-Menu }");
            ps1Builder.AppendLine("    }");
            ps1Builder.AppendLine("}");
            ps1Builder.AppendLine("");
            ps1Builder.AppendLine("function Run-Onboard {");
            ps1Builder.AppendLine("    Clear-Host");
            ps1Builder.AppendLine("    Write-Host \"✓ 正在运行 OpenClaw Onboard...\"");
            ps1Builder.AppendLine("    Write-Host \"========================================\"");
            ps1Builder.AppendLine("    & npx openclaw onboard");
            ps1Builder.AppendLine("    Write-Host \"\"");
            ps1Builder.AppendLine("    Write-Host \"设置向导结束！按任意键返回主菜单...\"");
            ps1Builder.AppendLine("    $null = $host.UI.RawUI.ReadKey(\"NoEcho,IncludeKeyDown\")");
            ps1Builder.AppendLine("    Show-Menu");
            ps1Builder.AppendLine("}");
            ps1Builder.AppendLine("");
            ps1Builder.AppendLine("function Run-Gateway {");
            ps1Builder.AppendLine("    $gatewayPath = \"$env:USERPROFILE\\gateway.cmd\"");
            ps1Builder.AppendLine("    if (Test-Path $gatewayPath) {");
            ps1Builder.AppendLine("        Clear-Host");
            ps1Builder.AppendLine("        Write-Host \"✓ 正在运行 Gateway...\"");
            ps1Builder.AppendLine("        Write-Host \"========================================\"");
            ps1Builder.AppendLine("        & $gatewayPath");
            ps1Builder.AppendLine("        Write-Host \"\"");
            ps1Builder.AppendLine("        Write-Host \"Gateway 执行结束！按任意键返回主菜单...\"");
            ps1Builder.AppendLine("        $null = $host.UI.RawUI.ReadKey(\"NoEcho,IncludeKeyDown\")");
            ps1Builder.AppendLine("    } else {");
            ps1Builder.AppendLine("        Write-Host \"错误: 未找到 $gatewayPath\"");
            ps1Builder.AppendLine("        Write-Host \"按任意键返回主菜单...\"");
            ps1Builder.AppendLine("        $null = $host.UI.RawUI.ReadKey(\"NoEcho,IncludeKeyDown\")");
            ps1Builder.AppendLine("    }");
            ps1Builder.AppendLine("    Show-Menu");
            ps1Builder.AppendLine("}");
            ps1Builder.AppendLine("");
            ps1Builder.AppendLine("function Open-Terminal {");
            ps1Builder.AppendLine("    $initCmd = \"Set-Location -LiteralPath `\"$scriptDir\\openclaw_app`\"; `$env:PATH = `\"$scriptDir\\nodejs;$scriptDir\\git_env\\cmd;`$env:PATH`\"\";");
            ps1Builder.AppendLine("    $encodedCmd = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($initCmd))");
            ps1Builder.AppendLine("    if (Get-Command wt.exe -ErrorAction SilentlyContinue) {");
            ps1Builder.AppendLine("        Start-Process wt.exe -ArgumentList \"-w new-tab --title OpenClaw终端 powershell -NoExit -EncodedCommand $encodedCmd\"");
            ps1Builder.AppendLine("    } else {");
            ps1Builder.AppendLine("        Start-Process powershell -ArgumentList \"-NoExit\", \"-EncodedCommand\", $encodedCmd");
            ps1Builder.AppendLine("    }");
            ps1Builder.AppendLine("    Write-Host \"已打开新终端窗口，可以直接运行 openclaw 命令。按任意键返回主菜单...\"");
            ps1Builder.AppendLine("    $null = $host.UI.RawUI.ReadKey(\"NoEcho,IncludeKeyDown\")");
            ps1Builder.AppendLine("    Show-Menu");
            ps1Builder.AppendLine("}");
            ps1Builder.AppendLine("");
            ps1Builder.AppendLine("Show-Menu");

            string ps1Content = ps1Builder.ToString();
            File.WriteAllText(ps1Path, ps1Content, new UTF8Encoding(true));
            DebugLog(logger, $"启动脚本已保存至: {ps1Path}");

            // ==========================================
            // 6.5 生成 start.bat 快捷启动文件 (解决双击 .ps1 变成编辑的问题)
            // ==========================================
            logger.Report("正在生成 start.bat 快捷启动文件...");
            string batPath = Path.Combine(installDir, "点我运行.bat");
            // 使用带有绕过执行策略参数的命令
            string batContent = "@echo off\r\n" +
                                "pushd \"%~dp0\"\r\n" +
                                "powershell -ExecutionPolicy Bypass -File \"start.ps1\"\r\n" +
                                "popd";
            File.WriteAllText(batPath, batContent, Encoding.Default); 
            DebugLog(logger, $"批处理启动文件已保存至: {batPath}");

            // ==========================================
            // 7. 清理临时文件
            // ==========================================
            logger.Report("正在清理临时文件...");
            string[] zips = { nodeZipPath, gitZipPath };
            foreach (var zip in zips) { if (File.Exists(zip)) File.Delete(zip); }
            
            string npmCache = Path.Combine(installDir, ".npm-cache");
            Utils.RobustDeleteDirectory(npmCache);

            progress.Report(100);
            logger.Report("部署完成！");
        }

        private bool IsValidZip(string filePath, IProgress<string> logger)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 100 * 1024) return false;

                using (var archive = ZipFile.OpenRead(filePath))
                {
                    var entries = archive.Entries; 
                }
                return true;
            }
            catch
            {
                logger.Report($"-> 发现损坏的压缩包 [{Path.GetFileName(filePath)}]，将重新下载...");
                return false;
            }
        }
    }
}
