// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 通过反射读取背包装备和稳定 SessionUID，输出只读结构化数据。
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

sealed partial class HealthReader
{
    // 按反射字段类型读取单个装备字段，未知字段保留不可读原因。
    object ReadItemField(long field, long address, int depth)
    {
        if (depth > 8)
            throw new Exception("Item field nesting exceeds limit");
        int size = M.I(field + 52);
        if (size < 1 || size > 65536)
            throw new Exception("Item field size invalid");
        string kind = Name(M.I(M.Q(field + 8)));
        if (kind == "StructProperty")
        {
            Identity type = Token(M.Q(field + 120));
            string name = Name(type.Name);
            if (ClassName(type.Class) != "ScriptStruct" || M.I(type.Address + 88) != size)
                throw new Exception("Item struct size invalid");
            if (name == "GameplayTag")
                return Name(M.I(address));
            if (name == "SWSessionUID")
                return InventoryIdentity(M.Read(address, 8));
            return ReadItemStruct(type.Address, address, depth + 1);
        }

        if (kind == "ArrayProperty")
        {
            long data = M.Q(address), inner = ArrayInner(field);
            int count = M.I(address + 8), cap = M.I(address + 12), stride = M.I(inner + 52);
            if (count < 0 || count > 512 || cap < count || cap > 2048 || stride < 1 || stride > 4096)
                throw new Exception("Item array invalid");
            var values = new List<object>();
            for (int i = 0; i < count; i++)
                values.Add(ReadItemField(inner, data + i * stride, depth + 1));
            if (M.Q(address) != data || M.I(address + 8) != count)
                throw new Exception("Item array changed");
            return values;
        }

        if (kind == "BoolProperty")
        {
            byte[] meta = M.Read(field + 120, 4);
            if (meta[0] != 1 || meta[1] > 8 || meta[3] == 0)
                throw new Exception("Item bool invalid");
            return (M.Read(address + meta[1], 1)[0] & meta[3]) != 0;
        }

        if (kind == "IntProperty" && size == 4)
            return M.I(address);
        if (kind == "FloatProperty" && size == 4)
            return M.F(address);
        if (kind == "DoubleProperty" && size == 8)
            return BitConverter.ToDouble(M.Read(address, 8), 0);
        if (kind == "ByteProperty" && size == 1)
            return M.Read(address, 1)[0];
        if (kind == "Int64Property" && size == 8)
            return M.Q(address);
        if (kind == "UInt64Property" && size == 8)
            return BitConverter.ToUInt64(M.Read(address, 8), 0);
        if (kind == "NameProperty" && size == 8)
            return Name(M.I(address));
        return new
        {
            unsupported = kind,
            size = size
        };
    }

    // 核对数组内层元素类型与尺寸，防止错误步长读取。
    long ArrayInner(long field)
    {
        var candidates = new HashSet<long>();
        for (int offset = 112; offset <= 160; offset += 8)
            try
            {
                long a = M.Q(field + offset);
                string kind = Name(M.I(M.Q(a + 8)));
                int size = M.I(a + 52);
                if (kind.EndsWith("Property") && size > 0 && size <= 4096 && M.I(a + 32) == M.I(field + 32))
                    candidates.Add(a);
            }
            catch
            {
            }

        if (candidates.Count != 1)
            throw new Exception("Array element schema unavailable");
        foreach (long value in candidates)
            return value;
        throw new Exception();
    }

    // 有界读取装备结构，不直接把任意地址认作物品。
    Dictionary<string, object> ReadItemStruct(long type, long address, int depth)
    {
        var values = new Dictionary<string, object>();
        for (long f = M.Q(type + 80), i = 0; f != 0 && i < 128; i++, f = M.Q(f + 24))
        {
            string name = Name(M.I(f + 32));
            if (name == "OwningPlayerID")
                continue;
            try
            {
                values[name] = ReadItemField(f, address + M.I(f + 72), depth);
                // 铁匠使用原生 UID；原整理模块保留原哈希，不改变回收协议。
                if (name == "SessionUID" && M.I(f + 52) == 8)
                {
                    byte[] uid = M.Read(address + M.I(f + 72), 8);
                    if (!Object.Equals(values[name], InventoryIdentity(uid))) throw new Exception("Inventory UID changed");
                    values["NativeSessionUID"] = BitConverter.ToInt64(uid, 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch (Exception e)
            {
                values[name] = new
                {
                    unread = e.Message
                };
            }
        }

        return values;
    }

    // 取得背包/物品稳定身份，避免对象地址复用。
    static string InventoryIdentity(byte[] uid)
    {
        using (var hash = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(uid)).Replace("-", "").Substring(0, 20);
    }

    // 采集当前本地玩家背包结构快照。
    public List<Dictionary<string, object>> ReadInventory()
    {
        Identity pawn = Pawn(), inv = Token(M.Q(pawn.Address + Offset(pawn, "InventoryManagerComponent", 8)));
        if (!IsA(inv.Class, "InventoryManagerComponent") || M.Q(inv.Address + 32) != pawn.Address)
            throw new Exception("Inventory owner mismatch");
        long container = Struct("SlotEntryContainerReplicated", 280), slotType = Struct("SlotEntry", 80), entryType = Struct("InventoryEntry", 232);
        long h = inv.Address + Offset(inv, "ReplicatedItems", 280) + Prop(container, "Items", 16).Offset, data = M.Q(h);
        int n = M.I(h + 8), cap = M.I(h + 12);
        if (n < 1 || n > 128 || cap < n || cap > 512)
            throw new Exception("Inventory slot array unavailable");
        var result = new List<Dictionary<string, object>>();
        for (int i = 0; i < n; i++)
        {
            long row = data + i * 80, items = row + Prop(slotType, "ItemInventoryEntries", 16).Offset, entries = M.Q(items);
            int count = M.I(items + 8), capacity = M.I(items + 12);
            if (count < 0 || count > 512 || capacity < count || capacity > 2048)
                throw new Exception("Inventory entries invalid");
            string slot = Name(M.I(row + Prop(slotType, "TypeTag", 8).Offset));
            for (int j = 0; j < count; j++)
            {
                var value = ReadItemStruct(entryType, entries + j * 232, 0);
                value["Container"] = slot;
                result.Add(value);
            }

            if (M.Q(items) != entries || M.I(items + 8) != count)
                throw new Exception("Inventory is changing");
        }

        if (!Valid(pawn) || !Valid(inv) || M.Q(h) != data || M.I(h + 8) != n)
            throw new Exception("Inventory session changed");
        return result;
    }

}
