// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：保存有限长度的动作日志和脱敏诊断快照。新日志字段不要包含完整个人路径、存档内容或凭据；高频事件使用 Change/Limited，避免把主循环变成持续磁盘写入。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

// 本地诊断有长度限制；记录工具动作，不记录原始输入或游戏内存内容。
// DiagnosticLog 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class DiagnosticLog : IDisposable
{
    readonly object gate = new object ();
    readonly string folder;
    readonly int limit;
    readonly Queue<string> pending = new Queue<string>(), recent = new Queue<string>();
    readonly Dictionary<string, string> states = new Dictionary<string, string>();
    readonly Dictionary<string, long> repeats = new Dictionary<string, long>();
    Timer timer;
    bool disposed;
    string storageError;
    // 初始化 DiagnosticLog 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public DiagnosticLog(string directory, int maxBytes = 1048576, bool background = true)
    {
        folder = Path.GetFullPath(directory);
        limit = Math.Max(1024, maxBytes);
        if (background)
            timer = new Timer(delegate
            {
                Flush();
            }, null, 2000, 2000);
    }

    // 移除可识别的个人路径/隐私内容，再写入日志或导出。
    public static string Redact(string text)
    {
        text = text ?? "";
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!String.IsNullOrEmpty(home))
            text = Regex.Replace(text, Regex.Escape(home) + @"[^\r\n]*", "[path]", RegexOptions.IgnoreCase);
        // 完整路径可能暴露账户名、存档位置或其他应用，记录前需要脱敏。
        text = Regex.Replace(text, @"[A-Za-z]:[\\/][^\r\n]*", "[path]");
        text = Regex.Replace(text, @"\\\\[^\\\r\n]+\\[^\r\n]*", "[path]");
        string account = Environment.UserName;
        if (!String.IsNullOrEmpty(account))
            text = Regex.Replace(text, Regex.Escape(account), "[user]", RegexOptions.IgnoreCase);
        return text.Replace("\0", "");
    }

    // 把一条记录加入有限长度缓冲，受统一锁保护。
    void Add(string category, string message)
    {
        message = Redact(message).Replace("\r", " ").Replace("\n", " | ");
        int length = Math.Min(4096, (limit - 160) / 4);
        if (message.Length > length)
            message = message.Substring(0, length) + " [truncated]";
        string line = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture) + " [" + category + "] " + message;
        recent.Enqueue(line);
        pending.Enqueue(line);
        while (recent.Count > 2000)
            recent.Dequeue();
        while (pending.Count > 2000)
            pending.Dequeue();
    }

    // 记录一个带时间/类别的动作事件。
    public void Record(string category, string message)
    {
        lock (gate)
        {
            if (!disposed)
                Add(category, message);
        }
    }

    // 仅在状态发生变化时记录，减少高频轮询噪声。
    public void Change(string category, string value)
    {
        lock (gate)
        {
            if (disposed)
                return;
            string previous;
            if (states.TryGetValue(category, out previous) && previous == value)
                return;
            states[category] = value;
            Add(category, value);
        }
    }

    // 按指定间隔节流重复日志。
    public void Limited(string category, string value)
    {
        lock (gate)
        {
            if (disposed)
                return;
            long now = Environment.TickCount & 0xffffffffL, last;
            string key = category;
            string previous;
            if (states.TryGetValue(key, out previous) && previous == value && repeats.TryGetValue(key, out last) && now >= last && now - last < 10000)
                return;
            states[key] = value;
            repeats[key] = now;
            Add(category, value);
        }
    }

    // 生成本次运行日志名，应用名为 MCD2A。
    string FileName(int index)
    {
        return Path.Combine(folder, index == 0 ? "session.log" : "session." + index + ".log");
    }

    // 在持有日志锁时把缓冲落盘，不允许并发破坏顺序。
    void FlushLocked()
    {
        if (pending.Count == 0)
            return;
        try
        {
            Directory.CreateDirectory(folder);
            while (pending.Count > 0)
            {
                string line = pending.Peek() + Environment.NewLine;
                int bytes = Encoding.UTF8.GetByteCount(line);
                string file = FileName(0);
                if (File.Exists(file) && new FileInfo(file).Length + bytes > limit)
                {
                    if (File.Exists(FileName(2)))
                        File.Delete(FileName(2));
                    if (File.Exists(FileName(1)))
                        File.Move(FileName(1), FileName(2));
                    File.Move(file, FileName(1));
                }

                File.AppendAllText(file, line, new UTF8Encoding(false));
                pending.Dequeue();
            }

            storageError = null;
        }
        catch (Exception e)
        {
            storageError = e.GetType().Name;
        }
    }

    // 用日志锁保护一次落盘。
    public void Flush()
    {
        lock (gate)
        {
            if (!disposed)
                FlushLocked();
        }
    }

    // 导出配置/遥测及近期记录，输出不自动上传。
    public void Export(string destination, string snapshot)
    {
        string target = Path.GetFullPath(destination);
        if (target.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose a destination outside the internal log folder.");
        string text;
        lock (gate)
        {
            FlushLocked();
            var report = new StringBuilder();
            report.AppendLine("MCD2A diagnostic report / 诊断日志");
            report.AppendLine("Version: 1.0.0");
            report.AppendLine("Exported: " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
            report.AppendLine("OS: " + Environment.OSVersion.VersionString + "; 64-bit OS: " + Environment.Is64BitOperatingSystem + "; process: " + (IntPtr.Size * 8) + "-bit; CLR: " + Environment.Version);
            report.AppendLine("Toolbox state and requested actions only. No raw input, account information, game memory dump or automatic upload.");
            report.AppendLine();
            report.AppendLine("=== Current state / 当前状态 ===");
            report.AppendLine(Redact(snapshot));
            report.AppendLine("=== Recent events / 最近事件 ===");
            bool fallback = storageError != null;
            for (int i = 2; i >= 0; i--)
                try
                {
                    if (File.Exists(FileName(i)))
                        report.Append(File.ReadAllText(FileName(i), Encoding.UTF8));
                }
                catch
                {
                    fallback = true;
                }

            if (fallback)
            {
                report.AppendLine("Local log storage unavailable. In-memory events follow (some entries may overlap). Error: " + storageError);
                foreach (string row in recent)
                    report.AppendLine(row);
            }

            text = report.ToString();
        }

        // 写入失败不会破坏用户已有文件。
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(true));
            if (File.Exists(target))
                File.Replace(temporary, target, null);
            else
                File.Move(temporary, target);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    // 释放本对象拥有的句柄、绘图对象或监听资源，避免退出后继续占用。
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            FlushLocked();
            disposed = true;
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
        }
    }
}

// ToolboxLog 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class ToolboxLog
{
    static DiagnosticLog current;
    // 建立本次运行日志，先结束旧实例。
    public static void Start()
    {
        if (current != null)
            return;
        current = new DiagnosticLog(Path.Combine(Path.GetDirectoryName(ToolboxSettings.ConfigPath), "logs"));
        Write("Session", "Started");
    }

    // 写入本组件的自有通信数据；不能写入角色存档。
    public static void Write(string category, string text)
    {
        if (current != null)
            current.Record(category, text);
    }

    // 仅在状态发生变化时记录，减少高频轮询噪声。
    public static void Change(string category, string text)
    {
        if (current != null)
            current.Change(category, text);
    }

    // 按指定间隔节流重复日志。
    public static void Limited(string category, string text)
    {
        if (current != null)
            current.Limited(category, text);
    }

    // 记录经过脱敏的异常和上下文。
    public static void Error(string category, Exception error)
    {
        Limited(category, error.ToString());
    }

    // 导出配置/遥测及近期记录，输出不自动上传。
    public static void Export(string path, string snapshot)
    {
        if (current == null)
            throw new InvalidOperationException("Diagnostics are not running.");
        current.Export(path, snapshot);
    }

    // 终止本工具拥有的请求并恢复禁用命令，清理未完成状态。
    public static void Stop()
    {
        if (current != null)
        {
            Write("Session", "Stopped");
            current.Dispose();
            current = null;
        }
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test(string directory)
    {
        Directory.CreateDirectory(directory);
        string report = Path.Combine(directory, "export.txt");
        using (var log = new DiagnosticLog(Path.Combine(directory, "logs"), 1024, false))
        {
            log.Record("Unicode", "中文 日本語 한국어");
            log.Change("Connection", "connected");
            log.Change("Connection", "connected");
            log.Limited("Error", "same error");
            log.Limited("Error", "same error");
            log.Export(report, "hp=30/100; slot1=ready; potion=cooling");
            string text = File.ReadAllText(report);
            if (!text.Contains("中文 日本語 한국어") || !text.Contains("hp=30/100") || text.Split(new[] { "[Connection]" }, StringSplitOptions.None).Length != 2 || text.Split(new[] { "[Error]" }, StringSplitOptions.None).Length != 2)
                throw new Exception("Diagnostic event export failed");
            for (int i = 0; i < 100; i++)
            {
                log.Record("Action", i + " " + new string ('x', 120));
                log.Flush();
            }

            string[] files = Directory.GetFiles(Path.Combine(directory, "logs"));
            if (files.Length != 3)
                throw new Exception("Log rotation count failed");
            foreach (string file in files)
                if (new FileInfo(file).Length > 1024)
                    throw new Exception("Log size limit failed");
            log.Record("Tail", "final event");
            log.Export(report, "complete snapshot");
            if (!File.ReadAllText(report).Contains("final event"))
                throw new Exception("Pending event flush failed");
            bool blocked = false;
            try
            {
                log.Export(Path.Combine(directory, "logs/session.log"), "");
            }
            catch (IOException)
            {
                blocked = true;
            }

            if (!blocked)
                throw new Exception("Internal log overwrite was not rejected");
            string bad = Path.Combine(directory, "missing/subfolder/export.txt");
            try
            {
                log.Export(bad, "");
                throw new Exception("Missing directory test failed");
            }
            catch (DirectoryNotFoundException)
            {
            }

            log.Record("Tail", "still running after export failure");
            log.Export(report, "after failure");
            if (!File.ReadAllText(report).Contains("still running after export failure"))
                throw new Exception("Logger did not recover after export failure");
        }

        string privateText = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\secret.txt\nC:\\Users\\SomeAccount\\private.txt\n\\\\server\\private\\file.txt\n" + Environment.UserName;
        string clean = DiagnosticLog.Redact(privateText);
        if (clean.Contains("private.txt") || clean.Contains("secret.txt") || clean.Contains(Environment.UserName))
            throw new Exception("Path/account redaction failed");
        string blockedPath = Path.Combine(directory, "blocked");
        File.WriteAllText(blockedPath, "test");
        using (var log = new DiagnosticLog(blockedPath, 1024, false))
        {
            log.Record("Memory", "storage failed but event retained");
            log.Export(report, "memory fallback");
            if (!File.ReadAllText(report).Contains("storage failed but event retained"))
                throw new Exception("In-memory fallback failed");
        }

        return "PASS: UTF-8 export, state snapshot, duplicate suppression, rate limiting, three-file rotation and size limits, pending flush, atomic overwrite, internal-file protection, write-failure recovery, path/account redaction and memory fallback. No game input sent; no user settings modified.";
    }
}
