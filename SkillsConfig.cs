using System.Collections.Generic;

namespace OpenClawInstaller
{
    // 定义单个 Skill 工具的数据结构
    public class SkillToolConfig
    {
        public string Url { get; set; }
        public string Name { get; set; }
        public string BinDir { get; set; }
    }

    // 静态配置类，存放所有的 skills_bins
    public static class SkillsConfig
    {
        public static readonly List<SkillToolConfig> Tools = new List<SkillToolConfig>
        {
            new SkillToolConfig 
            { 
                Url = "https://github.com/cli/cli/releases/download/v2.88.1/gh_2.88.1_windows_amd64.zip", 
                Name = "gh", 
                BinDir = "/bin" // 将自动拼接到 gh 目录下
            },
            new SkillToolConfig 
            { 
                Url = "https://github.com/astral-sh/uv/releases/download/0.10.10/uv-x86_64-pc-windows-msvc.zip", 
                Name = "uv", 
                BinDir = "/" // 根目录
            },
            new SkillToolConfig
            {
                Url = "https://github.com/steipete/camsnap/releases/download/v0.2.0/camsnap_0.2.0_windows_amd64.zip",
                Name = "camsnap",
                BinDir = "/"
            },
            new SkillToolConfig
            {
                Url = "https://github.com/winpython/winpython/releases/download/17.2.20260225/WinPython64-3.13.12.0dotb3.zip",
                Name = "WinPython64-3.14",
                BinDir = "/WPy64-3.13.12.0/python"
            },
            new SkillToolConfig
            {
                Url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n8.0-latest-win64-gpl-shared-8.0.zip",
                Name = "ffmpeg",
                BinDir = "/ffmpeg-n8.0-latest-win64-gpl-shared-8.0/bin"
            },
            new SkillToolConfig
            {
                Url = "https://github.com/steipete/blucli/releases/download/v0.1.3/blucli-windows-amd64.zip",
                Name = "blucli",
                BinDir = "/"
            },
            new SkillToolConfig
            {
                Url = "https://github.com/steipete/gogcli/releases/download/v0.12.0/gogcli_0.12.0_windows_amd64.zip",
                Name = "gog",
                BinDir = "/"
            }

        };
    }
}