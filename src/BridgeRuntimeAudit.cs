// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：只读检查已加载组件的包名、对象和有界字符串。诊断只能证明读取到的结构与内容，不能证明动作成功；组件归属检查必须使用完整包边界而非模糊子串。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

// BridgeAuditScope 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class BridgeAuditScope
{
    internal static readonly string[] Components =
    {
        "MCD2NearbyLootBridge",
        "MCD2CombatBridge",
        "MCD2EquipmentBridge"
    };
    // 按完整组件包边界识别对象，不用包含名称的任意对象替代。
    internal static string Component(string package)
    {
        foreach (string name in Components)
            if (package.StartsWith("/Game/Mods/" + name + "/", StringComparison.Ordinal))
                return name;
        return "";
    }

    // 检查原生字符串头的长度、容量和地址范围。
    internal static bool StringHeader(long pointer, int length, int capacity)
    {
        return length >= 0 && length <= 4096 && capacity >= length && capacity <= 16384 && (length == 0 || (pointer >= 65536 && pointer <= 0x7fffffffffffL - length * 2));
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        if (Component("/Game/Mods/MCD2CombatBridge/Receipt") != "MCD2CombatBridge" || Component("/Game/Mods/MCD2CombatBridgeOther/Receipt") != "" || Component("/Game/Characters/Request") != "" || Component("/Game/Mods/MCD2NearbyLootBridge/ModActor") != "MCD2NearbyLootBridge")
            throw new Exception("Component scope validation failed");
        if (!StringHeader(0, 0, 0) || !StringHeader(65536, 4096, 4096) || StringHeader(0, 1, 1) || StringHeader(65536, 4097, 4097) || StringHeader(65536, 2, 1) || StringHeader(65536, -1, 0) || StringHeader(65536, 1, 16385))
            throw new Exception("String bounds validation failed");
        return "PASS: exact component package boundaries and bounded UTF-16 string headers. No game input, native calls, process writes or save writes.\r\n";
    }
}

// 跨文件的只读游戏读取器；各 partial 文件共同持有同一连接/对象身份缓存。
sealed partial class HealthReader
{
    // 采集一个组件的已加载对象和槽归属信息。
    string BridgePackage(Identity token)
    {
        var seen = new HashSet<long>();
        for (int depth = 0; depth < 32 && token != null; depth++)
        {
            if (!Valid(token) || !seen.Add(token.Address))
                throw new Exception("Object outer chain changed or looped");
            if (ClassName(token.Class) == "Package")
                return Name(token.Name);
            long outer = M.Q(token.Address + 32);
            if (outer == 0)
                return "";
            token = Token(outer);
        }

        return "";
    }

    // 在受限长度内读取自有请求/回执字符串。
    string BridgeString(Identity token, string field)
    {
        Property p = Prop(token.Class, field, 16);
        if (Name(M.I(M.Q(p.Field + 8))) != "StrProperty")
            throw new Exception("String property type mismatch");
        long header = token.Address + p.Offset, pointer = M.Q(header);
        int length = M.I(header + 8), capacity = M.I(header + 12);
        if (!BridgeAuditScope.StringHeader(pointer, length, capacity))
            throw new Exception("String bounds rejected");
        string value = length == 0 ? "" : Encoding.Unicode.GetString(M.Read(pointer, length * 2));
        if (!Valid(token) || M.Q(header) != pointer || M.I(header + 8) != length || M.I(header + 12) != capacity)
            throw new Exception("String changed while reading");
        if (length > 0 && value[value.Length - 1] != '\0')
            throw new Exception("String terminator missing");
        return value.TrimEnd('\0');
    }

    // 构造单次只读组件通信快照。
    object BridgeSample(int index)
    {
        var rows = new List<object>();
        var errors = new List<string>();
        var packages = new HashSet<string>();
        var classPackages = new Dictionary<long, string>();
        var actors = BridgeAuditScope.Components.ToDictionary(n => n, n => 0);
        var receipts = BridgeAuditScope.Components.ToDictionary(n => n, n => 0);
        int matched = 0;
        foreach (Identity token in AllTokens())
            try
            {
                if (!Valid(token))
                    continue;
                string className = ClassName(token.Class), name = Name(token.Name), package;
                if (!classPackages.TryGetValue(token.Class, out package))
                {
                    package = BridgePackage(Token(token.Class));
                    classPackages[token.Class] = package;
                }

                string component = BridgeAuditScope.Component(package);
                if (component == "")
                {
                    // 包/类元数据只证明组件存在，不能证明执行成功。
                    if (className == "Package" || className.EndsWith("Class", StringComparison.Ordinal))
                    {
                        string ownPackage = className == "Package" ? name : BridgePackage(token);
                        if (BridgeAuditScope.Component(ownPackage) != "" || ownPackage.StartsWith("/Game/BlueprintLoader/", StringComparison.Ordinal) || ownPackage.StartsWith("/Game/Mods/BlueprintLoader/", StringComparison.Ordinal))
                            packages.Add(ownPackage);
                    }

                    continue;
                }

                packages.Add(package);
                matched++;
                bool defaults = name.StartsWith("Default__", StringComparison.Ordinal), actor = IsA(token.Class, "Actor"), save = IsA(token.Class, "SaveGame");
                string outerClass = "";
                try
                {
                    outerClass = ClassName(Token(M.Q(token.Address + 32)).Class);
                }
                catch
                {
                }

                bool levelActor = !defaults && actor && outerClass == "Level";
                if (levelActor)
                    actors[component]++;
                if (!defaults && save && className == "Receipt_C")
                    receipts[component]++;
                if (rows.Count >= 512)
                    continue;
                var fields = new Dictionary<string, object>();
                if (!defaults)
                {
                    string[] allowed = save && className == "Receipt_C" ? new[]
                    {
                        "Status"
                    }

                    : save && className == "Request_C" ? new[]
                    {
                        "Command"
                    }

                    : levelActor ? new[]
                    {
                        "instance",
                        "epoch",
                        "status",
                        "kind"
                    }

                    : new string[0];
                    foreach (string field in allowed)
                        try
                        {
                            fields[field] = BridgeString(token, field);
                        }
                        catch (Exception e)
                        {
                            fields[field] = new
                            {
                                readError = e.Message
                            };
                        }
                }

                rows.Add(new { component = component, package = package, className = className, objectName = name, number = token.Number, objectIndex = token.Index, serial = token.Serial, defaultObject = defaults, levelActor = levelActor, outerClass = outerClass, fields = fields, stillValid = Valid(token) });
            }
            catch (Exception e)
            {
                if (errors.Count < 10)
                    errors.Add(e.Message);
            }

        return new
        {
            sample = index,
            utc = DateTime.UtcNow,
            packages = packages.OrderBy(n => n).ToArray(),
            levelActorCounts = actors,
            receiptObjectCounts = receipts,
            matchedObjects = matched,
            rowsTruncated = matched > rows.Count,
            objects = rows,
            errors = errors
        };
    }

    // 在限定时间/次数内导出组件通信观察报告。
    public void AuditBridgeRuntime(string path)
    {
        var samples = new List<object>();
        samples.Add(BridgeSample(1));
        Thread.Sleep(1000);
        samples.Add(BridgeSample(2));
        var report = new
        {
            schema = 1,
            version = "1.0.0",
            readOnly = true,
            gameInput = false,
            nativeCalls = false,
            gameWrites = false,
            saveWrites = false,
            pid = Pid,
            processName = process.ProcessName,
            scope = "Exact MCD2 component packages only. Default objects and mounted classes do not prove BeginPlay or successful communication. Receipt strings may be transient or stale; compare both samples.",
            samples = samples,
            lookupRecord = AdaptationRecord.Contents()
        };
        File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(report), new UTF8Encoding(true));
    }
}
