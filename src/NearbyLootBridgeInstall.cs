// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System.IO;

// 兼容旧诊断命令；所有安装入口现在都更新完整组件包。
static class NearbyComponentInstaller
{
    // 转交统一安装器，返回旧调用方期待的本组件目录。
    public static string Install(string root)
    {
        ComponentBundleInstaller.Install(root);
        root = GameProcess.InstallRoot(root, Directory.Exists, File.Exists);
        return Path.Combine(root, "Dungeons", "Content", "Paks", "~mods", "MCD2NearbyLootBridge");
    }
}

// 兼容历史 UI 回调，实际操作集中在组件管理页。
sealed partial class ToolboxForm
{
    // 旧入口重定向到统一安装，避免只更新部分组件。
    void InstallNearbyComponent()
    {
        ShowPage(3);
        InstallAllComponents();
    }
}
