# 🦞 OpenClaw Installer (Windows 一键自动化部署工具)

![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![Language](https://img.shields.io/badge/Language-C%23_Native-green)
![License](https://img.shields.io/badge/License-MIT-brightgreen)

OpenClaw Installer 是一个专为 Windows 用户打造的 **C# 原生桌面应用程序**。它旨在彻底解决在部署 [OpenClaw](https://openclaw.ai/) 时遇到的 Node.js 环境配置、Git 权限报错、NPM 下载缓慢等痛点，提供真正的“一键式”开箱即用体验。

## ✨ 核心特性

* 📦 **环境绝对隔离**：自动下载便携版 Node.js (v22.13.1) 和 MinGit (v2.44.0)，不污染您的系统全局变量，随用随删。
* 🎒 **原生便携模式 (Portable Mode)**：可选将所有用户数据（`USERPROFILE`、`APPDATA` 等）重定向并保存在安装目录下的 `data` 文件夹中，实现真正的绿色免安装，装进 U 盘带走即可运行。
* 🚀 **专为国内网络优化**：
    * 自动配置 NPM 淘宝镜像源 (`registry.npmmirror.com`)。
    * 内置 GitHub 代理支持，利用 Git `insteadOf` 策略自动重写底层的 `ssh://` 和 `https://` 请求，彻底告别 `Exit code 128` 和权限拒绝错误。
* 🧠 **智能硬件探测**：在部署核心组件时，自动检测系统是否具备 NVIDIA 显卡 (`nvidia-smi`)，智能选择编译类型为 `cuda` 或 `cpu`，最大化利用硬件性能。
* 🛠️ **Skills 扩展组件支持**：一键可选挂载常用技能所需的环境和工具，自动化下载并配置环境变量。
* 🎮 **无脑一键启动**：部署完成后，自动生成交互式 `start.ps1` 和便捷的 `点我运行.bat`，双击即可唤出功能菜单。
* 🐛 **调试模式**：内置 Debug 开关，一键开启底层的完整进程日志，排障无忧。

## 🚀 快速上手

1. **下载程序**：在 [Releases](https://github.com/diandianti/clawInstaller/releases) 页面下载最新的 `OpenClawInstaller.exe`。
2. **运行程序**：
   * 选择你想要安装 OpenClaw 的目标目录。
   * *(可选)* 在“Github 代理”框中填入你信任的 GitHub 加速链接（默认已提供）。
3. **点击“一键部署”**：喝杯咖啡，等待进度条走完（工具会自动下载环境、配置网络、隔离安装依赖）。
4. **启动 OpenClaw**：部署完成后，进入安装目录，双击运行 `start.ps1`，即可体验你的专属 AI 网关！
5. **配置**：按照openclaw引导配置即可。 

## skills 程序支持

- gh
- gog
- python
- ffmpeg
- camsnap
- blucli
- uv

## 已知问题
~~win11下面启动gateway会失败，正在修补中~~

## TODO

- [ ] 已有环境检查

- [x] 将用户数据更改到安装目录

- [x] skills程序安装

## 🛠️ 本地编译构建

本项目使用 C# WinForms 开发。
* 框架：`.NET 8.0` 或更高版本。
* 无需任何第三方 NuGet 包，全原生库实现，极度轻量。

## 🤝 参与贡献

欢迎提交 Pull Requests 或发布 Issues 探讨新功能。如果你觉得这个工具帮到了你，请给个 ⭐️ Star！

## 📝 开源协议

[MIT License](LICENSE)










