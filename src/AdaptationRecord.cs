// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：记录当前连接的反射与兼容性查找结果。修改诊断字段时从 Set/Get 入手；访问记录必须持有 gate 锁，记录只用于排查，不用于执行固定地址调用。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.Collections.Generic;
using System.Text;

// AdaptationRecord 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class AdaptationRecord
{
    static readonly object gate = new object ();
    static readonly SortedDictionary<string, string> entries = new SortedDictionary<string, string>();
    // 在锁内清除上次连接记录并写入本次 UTC 会话时间。
    public static void Begin()
    {
        lock (gate)
        {
            entries.Clear();
            entries["session.utc"] = DateTime.UtcNow.ToString("o");
        }
    }

    // 在锁内设置已核验的查找来源或结果。
    public static void Set(string key, string value)
    {
        lock (gate)
            entries[key] = value;
    }

    // 在锁内读取一条兼容记录，缺失时返回空值。
    public static string Get(string key)
    {
        lock (gate)
        {
            string value;
            return entries.TryGetValue(key, out value) ? value : null;
        }
    }

    // 生成当前模块的可读文本，供界面或导出报告使用。
    public static string Contents()
    {
        lock (gate)
        {
            var text = new StringBuilder("Compatibility lookup record (module-relative addresses; no memory dump)\r\n");
            foreach (var entry in entries)
                text.AppendLine(entry.Key + " = " + entry.Value);
            return text.ToString();
        }
    }
}
