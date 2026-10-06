// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 从本项目 GitHub Releases 检查正式/Dev 发行包，校验后暂存并覆盖安装。
// 配置保留在 LocalAppData；更新助手使用当前程序的临时副本，等待主窗口退出后才替换文件。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

// GitHub JSON 字段名保持原样，避免平台字段翻译后无法反序列化。
sealed class UpdateAsset
{
    // GitHub 资产名、公开下载 URL、服务端 SHA256 和字节数；供包选择与完整性校验使用。
    public string name { get; set; }
    public string browser_download_url { get; set; }
    public string digest { get; set; }
    public long size { get; set; }
}
sealed class UpdateRelease
{
    // draft 永不进入更新列表；prerelease 决定正式/Dev 通道，published_at 用于同版本排序。
    public string tag_name { get; set; }
    public string published_at { get; set; }
    public bool draft { get; set; }
    public bool prerelease { get; set; }
    public UpdateAsset[] assets { get; set; }
}
// 检查结果同时带有版本、通道与发行包哈希；安装前不再猜测下载文件名称。
sealed class AppUpdateOffer
{
    // Channel：0 正式、1 Dev；Different 按 EXE 字节哈希判断，支持同版本手动覆盖。
    public string Tag, PackageHash, ExeHash;
    public int Channel;
    public Version Version;
    public UpdateAsset Asset;
    public bool Different;
}
// 更新计划仅保存本次允许替换的文件；不遍历游戏目录或用户配置目录。
sealed class AppUpdateFile
{
    // Source 相对暂存 payload，Destination 相对工具目录；Existed/Touched 仅供回滚记录使用。
    public string Source, Destination, Sha256;
    public bool Existed, Touched;
}
sealed class AppUpdatePlan
{
    // ParentStarted 防止 PID 复用；OriginalHash 确保目标仍是发起本次更新的旧程序。
    public string Directory, Target, Executable, OriginalHash;
    public int ParentId, Language;
    public long ParentStarted;
    public AppUpdateFile[] Files;
}

static class AppUpdater
{
    internal const string Repository = "Cyanmir/Minecraft-Dungeons-II-Assistant";
    const long MaxDownload = 256L * 1024 * 1024;
    // 两端共用产品文件白名单；安装助手也必须验证，不能仅信任下载端的清单。
    static readonly HashSet<string> ProductFiles = new HashSet<string>(new[] { "MCD2A.exe", "README.md", "LICENSE", "LICENSE.zh-CN.md", "THIRD_PARTY_NOTICES.md", "docs/usage.md", "docs/build.md", "src/assets/toolbox.png" }, StringComparer.OrdinalIgnoreCase);
    // 版本展示与检查共用程序集元数据，避免窗口/日志保留旧的硬编码版本。
    internal static string CurrentVersion
    {
        get
        {
            var value = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyInformationalVersionAttribute));
            return value == null ? Assembly.GetExecutingAssembly().GetName().Version.ToString(3) : value.InformationalVersion;
        }
    }
    internal static string CacheRoot
    {
        get { return Path.Combine(Path.GetDirectoryName(ToolboxSettings.ConfigPath), "updates"); }
    }
    // 只接受带语义版本的发行标签；Dev 后缀仍保留在界面中显示。
    internal static Version ParseVersion(string value)
    {
        var match = Regex.Match(value ?? "", @"^[vV]?(\d+)\.(\d+)\.(\d+)(?:[-+].*)?$");
        Version version;
        if (!match.Success || !Version.TryParse(match.Groups[1].Value + "." + match.Groups[2].Value + "." + match.Groups[3].Value, out version))
            return null;
        return version;
    }
    // 同一主版本仍比较 Dev 序号；如 dev.2 高于 dev.1，正式版高于同版本预发布。
    internal static int CompareVersions(string left, string right)
    {
        Version a = ParseVersion(left), b = ParseVersion(right);
        if (a == null || b == null)
            throw new InvalidDataException("Invalid release version");
        int core = a.CompareTo(b);
        if (core != 0)
            return core;
        Func<string, string[]> suffix = value => value.Split('+')[0].Contains("-") ? value.Split('+')[0].Substring(value.IndexOf('-') + 1).Split('.') : new string[0];
        var first = suffix(left);
        var second = suffix(right);
        if (first.Length == 0 || second.Length == 0)
            return first.Length == second.Length ? 0 : first.Length == 0 ? 1 : -1;
        for (int index = 0; index < Math.Min(first.Length, second.Length); index++)
        {
            long x, y;
            bool nx = Int64.TryParse(first[index], out x), ny = Int64.TryParse(second[index], out y);
            int part = nx && ny ? x.CompareTo(y) : nx != ny ? nx ? -1 : 1 : String.CompareOrdinal(first[index], second[index]);
            if (part != 0)
                return part;
        }
        return first.Length.CompareTo(second.Length);
    }
    // 文件完整性使用 SHA256；游戏组件的原有校验协议不受此模块影响。
    internal static string Hash(string path)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    // 接受固定长度十六进制哈希，缺少校验信息的发行包不能执行覆盖安装。
    static bool HashValue(string value)
    {
        return Regex.IsMatch(value ?? "", "^[a-fA-F0-9]{64}$");
    }
    // 下载地址限于本仓库；重定向交给 HTTPS 请求处理，不发送账号凭据。
    static Uri AssetUri(UpdateAsset asset)
    {
        Uri uri;
        if (asset == null || !Uri.TryCreate(asset.browser_download_url, UriKind.Absolute, out uri) || uri.Scheme != "https" || uri.Host != "github.com" || !uri.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(L10n.T("更新下载地址无效"));
        return uri;
    }
    // 网络临时断开最多重试两次；用户取消、校验失败或普通 HTTP 错误不重复下载。
    static byte[] Fetch(Uri uri, string destination, long maximum, CancellationToken cancel, Action<int> progress)
    {
        for (int attempt = 0; ; attempt++)
            try { return FetchOnce(uri, destination, maximum, cancel, progress); }
            catch (WebException error)
            {
                cancel.ThrowIfCancellationRequested();
                var response = error.Response as HttpWebResponse;
                bool transient = response != null ? (int)response.StatusCode >= 500 : error.Status == WebExceptionStatus.Timeout || error.Status == WebExceptionStatus.ConnectFailure || error.Status == WebExceptionStatus.ConnectionClosed || error.Status == WebExceptionStatus.ReceiveFailure;
                if (!transient || attempt >= 2)
                    throw;
                if (cancel.WaitHandle.WaitOne(1000 * (attempt + 1)))
                    cancel.ThrowIfCancellationRequested();
            }
    }
    // 有限长度和超时的流式下载；取消时 Abort 当前连接，不阻塞窗口线程。
    static byte[] FetchOnce(Uri uri, string destination, long maximum, CancellationToken cancel, Action<int> progress)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var request = (HttpWebRequest)WebRequest.Create(uri);
        request.UserAgent = "MCD2A/" + CurrentVersion;
        request.Accept = "application/vnd.github+json";
        request.Headers["X-GitHub-Api-Version"] = "2022-11-28";
        request.Timeout = 20000;
        request.ReadWriteTimeout = 20000;
        using (cancel.Register(request.Abort))
        using (var response = (HttpWebResponse)request.GetResponse())
        using (var input = response.GetResponseStream())
        using (Stream output = destination == null ? (Stream)new MemoryStream() : File.Create(destination))
        {
            if (response.ResponseUri.Scheme != "https" || response.ContentLength > maximum)
                throw new InvalidDataException(L10n.T("更新文件大小或地址无效"));
            byte[] buffer = new byte[65536];
            long total = 0;
            int count, reported = -1;
            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancel.ThrowIfCancellationRequested();
                total += count;
                if (total > maximum)
                    throw new InvalidDataException(L10n.T("更新文件大小或地址无效"));
                output.Write(buffer, 0, count);
                if (progress != null && response.ContentLength > 0)
                {
                    int percent = (int)Math.Min(100, total * 100 / response.ContentLength);
                    if (percent != reported)
                    {
                        reported = percent;
                        progress(percent);
                    }
                }
            }
            if (response.ContentLength >= 0 && total != response.ContentLength)
                throw new InvalidDataException(L10n.T("更新文件下载不完整"));
            return destination == null ? ((MemoryStream)output).ToArray() : null;
        }
    }
    // 正式通道排除预发布；Dev 通道只读取预发布，不把源码分支 ZIP 当作可执行包。
    internal static AppUpdateOffer Check(int channel, CancellationToken cancel)
    {
        var serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        var releases = serializer.Deserialize<UpdateRelease[]>(Encoding.UTF8.GetString(Fetch(new Uri("https://api.github.com/repos/" + Repository + "/releases?per_page=100"), null, 4 * 1024 * 1024, cancel, null)));
        var release = (releases ?? new UpdateRelease[0]).Where(r => !r.draft && r.prerelease == (channel == 1) && ParseVersion(r.tag_name) != null)
            .OrderByDescending(r => r.tag_name, Comparer<string>.Create(CompareVersions)).ThenByDescending(r => r.published_at).FirstOrDefault();
        if (release == null)
            return null;
        var assets = release.assets ?? new UpdateAsset[0];
        var package = assets.FirstOrDefault(a => Regex.IsMatch(a.name ?? "", @"^MCD2A-.+-win-x64\.zip$", RegexOptions.IgnoreCase))
            ?? assets.FirstOrDefault(a => Regex.IsMatch(a.name ?? "", @"^MCD2A(?:-.+)?\.exe$", RegexOptions.IgnoreCase));
        if (package == null || package.size <= 0 || package.size > MaxDownload)
            throw new InvalidDataException(L10n.T("此版本没有可用的安装包"));
        AssetUri(package);
        string packageHash = (package.digest ?? "").Replace("sha256:", ""), exeHash = null;
        var checksum = assets.FirstOrDefault(a => Regex.IsMatch(a.name ?? "", @"^MCD2A-.+-SHA256\.txt$", RegexOptions.IgnoreCase));
        if (checksum != null)
        {
            string contents = Encoding.UTF8.GetString(Fetch(AssetUri(checksum), null, 16384, cancel, null));
            foreach (string line in contents.Split('\n'))
            {
                var match = Regex.Match(line.Trim(), @"^([a-fA-F0-9]{64})\s+\*?(.+)$");
                if (!match.Success)
                    continue;
                if (match.Groups[2].Value == package.name)
                {
                    if (HashValue(packageHash) && !packageHash.Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(L10n.T("更新文件校验失败"));
                    packageHash = match.Groups[1].Value;
                }
                if (match.Groups[2].Value == "MCD2A.exe")
                    exeHash = match.Groups[1].Value;
            }
        }
        if (package.name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            exeHash = packageHash;
        if (!HashValue(packageHash) || !HashValue(exeHash))
            throw new InvalidDataException(L10n.T("此版本缺少更新校验文件"));
        cancel.ThrowIfCancellationRequested();
        return new AppUpdateOffer { Tag = release.tag_name, Version = ParseVersion(release.tag_name), Channel = channel, Asset = package, PackageHash = packageHash, ExeHash = exeHash, Different = !Hash(ApplicationPath()).Equals(exeHash, StringComparison.OrdinalIgnoreCase) };
    }
    // 使用实际运行文件路径，不能依赖启动工作目录或用户给 EXE 起的名称。
    static string ApplicationPath() { return Assembly.GetExecutingAssembly().Location; }
    // 只替换发行包的产品文件；任何 ZIP 越界、重复项、超大项均直接拒绝。
    internal static string Prepare(AppUpdateOffer offer, CancellationToken cancel, Action<int> progress)
    {
        string directory = Path.Combine(CacheRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string package = Path.Combine(directory, "package.download"), payload = Path.Combine(directory, "payload");
            Fetch(AssetUri(offer.Asset), package, MaxDownload, cancel, progress);
            if (new FileInfo(package).Length != offer.Asset.size || !Hash(package).Equals(offer.PackageHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(L10n.T("更新文件校验失败"));
            Directory.CreateDirectory(payload);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (offer.Asset.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using (var stream = File.OpenRead(package))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    if (zip.Entries.Count > 4096)
                        throw new InvalidDataException(L10n.T("更新文件大小或地址无效"));
                    long expanded = 0;
                    foreach (var entry in zip.Entries)
                    {
                        cancel.ThrowIfCancellationRequested();
                        string name = entry.FullName.Replace('\\', '/');
                        if (name.StartsWith("/") || name.Contains(":") || name.Split('/').Any(part => part == "..") || entry.Length > MaxDownload)
                            throw new InvalidDataException(L10n.T("更新文件大小或地址无效"));
                        if (!ProductFiles.Contains(name))
                            continue;
                        expanded += entry.Length;
                        if (expanded > MaxDownload)
                            throw new InvalidDataException(L10n.T("更新文件大小或地址无效"));
                        if (!names.Add(name))
                            throw new InvalidDataException(L10n.T("更新文件校验失败"));
                        string file = Path.Combine(payload, name.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(file));
                        using (var input = entry.Open())
                        using (var output = File.Create(file))
                        {
                            byte[] buffer = new byte[65536];
                            int count;
                            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cancel.ThrowIfCancellationRequested();
                                output.Write(buffer, 0, count);
                            }
                        }
                    }
                }
            }
            else
            {
                File.Copy(package, Path.Combine(payload, "MCD2A.exe"));
                names.Add("MCD2A.exe");
            }
            string executable = Path.Combine(payload, "MCD2A.exe");
            if (!File.Exists(executable) || !Hash(executable).Equals(offer.ExeHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(L10n.T("更新文件校验失败"));
            // 保留用户实际 EXE 文件名，兼容 MCD2A-版本.exe 等手动改名后的启动方式。
            string application = ApplicationPath();
            var current = Process.GetCurrentProcess();
            var plan = new AppUpdatePlan { Directory = directory, Target = Path.GetDirectoryName(application), Executable = Path.GetFileName(application), OriginalHash = Hash(application), ParentId = current.Id, ParentStarted = current.StartTime.ToUniversalTime().Ticks, Language = L10n.Language };
            plan.Files = names.Select(name => new AppUpdateFile { Source = name, Destination = name == "MCD2A.exe" ? plan.Executable : name, Sha256 = Hash(Path.Combine(payload, name.Replace('/', Path.DirectorySeparatorChar))) }).ToArray();
            string manifest = Path.Combine(directory, "install.json");
            File.WriteAllText(manifest, new JavaScriptSerializer().Serialize(plan), new UTF8Encoding(false));
            File.Copy(application, Path.Combine(directory, "UpdateRunner.exe"));
            return manifest;
        }
        catch
        {
            // 这里只删除本函数刚创建的随机暂存目录，不触碰安装目录和配置。
            Directory.Delete(directory, true);
            throw;
        }
    }
    // 独立助手在临时目录运行；普通目录无需提权，受保护目录使用 Windows UAC。
    internal static void StartInstaller(string manifest)
    {
        var plan = new JavaScriptSerializer().Deserialize<AppUpdatePlan>(File.ReadAllText(manifest));
        bool elevate = false;
        string probe = Path.Combine(plan.Target, ".mcd2a-update-" + Guid.NewGuid().ToString("N") + ".tmp");
        try { using (File.Create(probe)) { } File.Delete(probe); }
        catch (UnauthorizedAccessException) { elevate = true; }
        var start = new ProcessStartInfo(Path.Combine(plan.Directory, "UpdateRunner.exe"), "--apply-update \"" + manifest + "\" " + Hash(manifest)) { WorkingDirectory = plan.Directory, UseShellExecute = elevate, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        if (elevate)
            start.Verb = "runas";
        using (var process = Process.Start(start))
            if (process == null)
                throw new IOException(L10n.T("无法启动更新安装程序"));
    }
    // 限制相对路径和目录链接，防止发行包经子目录跳转覆盖其他文件。
    static string SafePath(string root, string relative)
    {
        if (String.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.Contains(":") || relative.Split('/', '\\').Any(part => part == ".."))
            throw new InvalidDataException("Invalid update path");
        string result = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Update path escaped installation directory");
        for (string path = result; !path.Equals(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase); path = Path.GetDirectoryName(path))
            if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked update path is not supported");
        return result;
    }
    // 等待旧进程退出后备份、写入和校验；失败按相反顺序回滚，恢复旧版本。
    internal static int Apply(string manifest, string manifestHash)
    {
        AppUpdatePlan plan = null;
        var changed = new List<AppUpdateFile>();
        try
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(manifest));
            Guid id;
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out id) || !Path.GetDirectoryName(directory).Equals(Path.GetFullPath(CacheRoot), StringComparison.OrdinalIgnoreCase) || Path.GetFileName(manifest) != "install.json" || !ApplicationPath().Equals(Path.Combine(directory, "UpdateRunner.exe"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid update installation plan");
            if (!HashValue(manifestHash) || !Hash(manifest).Equals(manifestHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(L10n.T("更新文件校验失败"));
            plan = new JavaScriptSerializer().Deserialize<AppUpdatePlan>(File.ReadAllText(manifest));
            L10n.Language = Math.Max(0, Math.Min(5, plan.Language));
            if (plan.Directory != directory || plan.Files == null || plan.Files.Length == 0 || !plan.Files.Any(f => f.Source == "MCD2A.exe" && f.Destination == plan.Executable))
                throw new InvalidDataException("Incomplete update installation plan");
            if (Path.GetFileName(plan.Executable) != plan.Executable || !plan.Executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !HashValue(plan.OriginalHash) || !Hash(ApplicationPath()).Equals(plan.OriginalHash, StringComparison.OrdinalIgnoreCase) || !Hash(SafePath(plan.Target, plan.Executable)).Equals(plan.OriginalHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(L10n.T("更新文件校验失败"));
            try
            {
                using (var parent = Process.GetProcessById(plan.ParentId))
                    if (parent.StartTime.ToUniversalTime().Ticks == plan.ParentStarted && !parent.WaitForExit(45000))
                        throw new IOException(L10n.T("请先关闭正在运行的工具"));
            }
            catch (ArgumentException) { }
            string payload = Path.Combine(directory, "payload"), backup = Path.Combine(directory, "backup");
            // 完成所有目标和哈希预检后再替换第一项，安装过程中逐项保留回滚记录。
            foreach (var file in plan.Files)
            {
                if (!ProductFiles.Contains(file.Source) || file.Destination != (file.Source == "MCD2A.exe" ? plan.Executable : file.Source))
                    throw new InvalidDataException("Unsupported update file");
                SafePath(plan.Target, file.Destination);
                if (!HashValue(file.Sha256) || !Hash(SafePath(payload, file.Source)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L10n.T("更新文件校验失败"));
            }
            foreach (var file in plan.Files)
            {
                string target = SafePath(plan.Target, file.Destination), saved = SafePath(backup, file.Destination);
                file.Existed = File.Exists(target);
                if (file.Existed)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(saved));
                    File.Copy(target, saved);
                }
                changed.Add(file);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                // 临时文件放在目标同一目录，完整写好后原子替换，避免留下半份 EXE。
                string staged = SafePath(plan.Target, file.Destination + ".mcd2a-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.Copy(SafePath(payload, file.Source), staged);
                    if (!Hash(staged).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(L10n.T("更新文件校验失败"));
                    if (file.Existed)
                        File.Replace(staged, target, null);
                    else
                        File.Move(staged, target);
                    file.Touched = true;
                }
                finally
                {
                    if (File.Exists(staged))
                        File.Delete(staged);
                }
                if (!Hash(target).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L10n.T("更新文件校验失败"));
            }
            File.WriteAllText(Path.Combine(directory, "result.txt"), "Installed", Encoding.UTF8);
            Process.Start(new ProcessStartInfo(SafePath(plan.Target, plan.Executable)) { WorkingDirectory = plan.Target, UseShellExecute = true });
            // 清除大体积下载和备份；助手本身在下一次启动时再清理。
            try { Directory.Delete(payload, true); Directory.Delete(backup, true); File.Delete(Path.Combine(directory, "package.download")); } catch { }
            return 0;
        }
        catch (Exception error)
        {
            var recovery = new List<string>();
            if (plan != null)
                foreach (var file in changed.AsEnumerable().Reverse())
                    try
                    {
                        if (!file.Touched)
                            continue;
                        string target = SafePath(plan.Target, file.Destination);
                        if (file.Existed)
                            File.Copy(SafePath(Path.Combine(plan.Directory, "backup"), file.Destination), target, true);
                        else if (file.Touched && File.Exists(target))
                            File.Delete(target);
                    }
                    catch (Exception rollback) { recovery.Add(rollback.Message); }
            string message = error.Message + (recovery.Count == 0 ? "" : "\n" + String.Join("\n", recovery));
            if (plan != null)
                try { File.WriteAllText(Path.Combine(plan.Directory, "result.txt"), message, Encoding.UTF8); } catch { }
            System.Windows.Forms.MessageBox.Show(L10n.T("更新安装失败，已保留旧版或备份") + "\n" + message, "MCD2A", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            return 1;
        }
    }
}
