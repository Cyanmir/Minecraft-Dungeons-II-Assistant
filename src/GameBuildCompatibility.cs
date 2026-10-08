// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// Steam 游戏更新的准入表统一供战斗、收集、整理和铁匠使用。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

sealed class GameCompatibilityException : Exception
{
    internal readonly string ReasonKey;
    internal GameCompatibilityException(string key) : base(L10n.T(key)) { ReasonKey = key; }
}

static class GameBuildCompatibility
{
    internal const string Steam111 = "231147BD0C655A4AE73F90873675D42917F2BFB3A9EE164FC64F217D6D6BD4EF";
    internal const string Steam112 = "3A8703406FD50520F83C4F70A0212C000CB3B584EF28EB38032902230C01EBDD";
    const int Steam112ImageSize = 216018944;
    static readonly object gate = new object();
    static readonly Dictionary<string, string> hashes = new Dictionary<string, string>(StringComparer.Ordinal);

    internal static bool MatchesSteam(string hash)
    {
        return String.Equals(hash, Steam111, StringComparison.OrdinalIgnoreCase) ||
            String.Equals(hash, Steam112, StringComparison.OrdinalIgnoreCase);
    }

    // 后台背包读取会重置全局诊断记录；版本准入不能依赖那个临时可空的字典。
    // 直接核对该进程的 EXE，按进程启动时间和文件元数据缓存，避免每次观察重读整个文件。
    internal static void RequireSteam(Process process)
    {
        if (process.ProcessName != "Dungeons-Win64-Shipping")
            throw new GameCompatibilityException("此游戏版本尚未适配，请更新工具；重复安装组件无效");
        var module = process.MainModule;
        string hash;
        try
        {
            var file = new FileInfo(module.FileName);
            long length = file.Length, modified = file.LastWriteTimeUtc.Ticks;
            string key = process.Id + "|" + process.StartTime.ToUniversalTime().Ticks + "|" + file.FullName + "|" + length + "|" + modified;
            lock (gate)
            {
                if (!hashes.TryGetValue(key, out hash))
                {
                    using (var input = File.OpenRead(file.FullName))
                    using (var sha = SHA256.Create())
                        hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
                    file.Refresh();
                    if (file.Length != length || file.LastWriteTimeUtc.Ticks != modified)
                        throw new IOException("Game executable changed during version lookup");
                    if (hashes.Count >= 16) hashes.Clear();
                    hashes[key] = hash;
                }
            }
        }
        catch (Exception error)
        {
            ToolboxLog.Error("Compatibility.SteamFingerprint", error);
            throw new GameCompatibilityException("无法核对游戏版本，请重新连接游戏");
        }
        if (!MatchesSteam(hash) || String.Equals(hash, Steam112, StringComparison.OrdinalIgnoreCase) && module.ModuleMemorySize != Steam112ImageSize)
        {
            AdaptationRecord.Set("native.steam.rejected", hash + "; imageSize=" + module.ModuleMemorySize);
            throw new GameCompatibilityException("此游戏版本尚未适配，请更新工具；重复安装组件无效");
        }
        AdaptationRecord.Set("native.steam.profile", hash + "; " + (hash == Steam112 ? "Steam 1.1.2.0" : "previous Steam build"));
    }
}
