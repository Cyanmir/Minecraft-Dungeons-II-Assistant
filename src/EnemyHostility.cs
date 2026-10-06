// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 解析敌对判断与 OwnedTags 访问布局。
using System;
using System.Collections.Generic;
using System.Linq;

// 跨文件的只读游戏读取器；各 partial 文件共同持有同一连接/对象身份缓存。
sealed partial class HealthReader
{
    static readonly string[] hostilityPatterns =
    {
        "48 8B 06 FF 90 ?? ?? ?? ??",
        "48 8B D7 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B 08",
        "8B A8 ?? ?? ?? ?? 33 DB 3B DD",
        "49 8B 8E ?? ?? ?? ??",
        "48 8B 3D ?? ?? ?? ??"
    };
    // 要求代码特征匹配唯一，返回可验证的候选。
    static int SingleMatch(byte[] code, string pattern)
    {
        int[] matches = new GameLocator.Pattern(pattern).Matches(code).ToArray();
        if (matches.Length != 1)
            throw new Exception("敌对标签函数布局未识别。");
        return matches[0];
    }

    // 从已定位函数体解析标签容器访问布局。
    int TagsLayoutFromBody(long fn)
    {
        if (!executable(fn, 256))
            throw new Exception("敌对函数不在可执行区段。");
        byte[] code = M.Read(fn, 256);
        int at = SingleMatch(code, hostilityPatterns[1]);
        long helper = fn + at + 13 + BitConverter.ToInt32(code, at + 9);
        if (!executable(helper, 320))
            throw new Exception("敌对标签函数不在可执行区段。");
        byte[] body = M.Read(helper, 320);
        int count = BitConverter.ToInt32(body, SingleMatch(body, hostilityPatterns[2]) + 2), array = BitConverter.ToInt32(body, SingleMatch(body, hostilityPatterns[3]) + 3);
        int teamAt = SingleMatch(body, hostilityPatterns[4]);
        long teamGlobal = helper + teamAt + 7 + BitConverter.ToInt32(body, teamAt + 3);
        if (Name(M.I(M.Q(teamGlobal))) != "SW.Team" || array < 16 || array > 0x20000 || count != array + 8)
            throw new Exception("敌对标签结构校验失败。");
        long image = process.MainModule.BaseAddress.ToInt64();
        AdaptationRecord.Set("function.BaseCharacter.IsHostileTowards.body", "RVA=0x" + (fn - image).ToString("X") + "; bytes=" + BitConverter.ToString(code).Replace('-', ' '));
        AdaptationRecord.Set("function.BaseCharacter.IsHostileTowards.teamHelper", "RVA=0x" + (helper - image).ToString("X") + "; ownedTags=0x" + array.ToString("X") + "; count=0x" + count.ToString("X") + "; parentTag=SW.Team; bytes=" + BitConverter.ToString(body).Replace('-', ' '));
        return array;
    }

    // 查找并验证 OwnedTags 的实际偏移与容器结构。
    void OwnedTagsLayout(Identity actor)
    {
        if (ownedTagsOffset >= 0)
            return;
        long image = process.MainModule.BaseAddress.ToInt64();
        for (int i = 0; i < hostilityPatterns.Length; i++)
            AdaptationRecord.Set("signature.catalog.hostility." + i, hostilityPatterns[i]);
        // 优先定位带名称的原生实现；此处仅解析代码，不调用游戏函数。
        using (var symbols = new LocalSymbols(M.Handle, process.MainModule.FileName, image, process.MainModule.ModuleMemorySize))
            foreach (string name in new[]
            {
                "BaseCharacter::IsHostileTowards",
                "ABaseCharacter::IsHostileTowards"
            }

            )
            {
                LocalSymbols.Symbol symbol = symbols.Find(name);
                if (symbol == null)
                    continue;
                try
                {
                    ownedTagsOffset = TagsLayoutFromBody(symbol.Address);
                    AdaptationRecord.Set("hostility.lookup", "native symbols -> verified instruction patterns");
                    return;
                }
                catch
                {
                }
            }

        if (!Valid(hostilityFunction))
            throw new Exception("敌对函数名称未识别。");
        var offsets = new HashSet<int>();
        for (int slot = 120; slot <= 240; slot += 8)
        {
            long wrapper = M.Q(hostilityFunction.Address + slot);
            if (!executable(wrapper, 256))
                continue;
            try
            {
                byte[] code = M.Read(wrapper, 256);
                int at = SingleMatch(code, hostilityPatterns[0]), vtableOffset = BitConverter.ToInt32(code, at + 5);
                if (vtableOffset < 0 || vtableOffset > 0x4000 || vtableOffset % 8 != 0)
                    continue;
                long fn = M.Q(M.Q(actor.Address) + vtableOffset);
                if (!executable(fn, 256))
                    continue;
                byte[] jump = M.Read(fn, 5);
                if (jump[0] == 0xe9)
                    fn = fn + 5 + BitConverter.ToInt32(jump, 1);
                offsets.Add(TagsLayoutFromBody(fn));
                AdaptationRecord.Set("function.BaseCharacter.IsHostileTowards.wrapper", "reflected name; RVA=0x" + (wrapper - image).ToString("X") + "; vtableOffset=0x" + vtableOffset.ToString("X") + "; bytes=" + BitConverter.ToString(code).Replace('-', ' '));
            }
            catch
            {
            }
        }

        if (offsets.Count != 1)
            throw new Exception("敌对标签函数布局未识别。");
        ownedTagsOffset = offsets.First();
        AdaptationRecord.Set("hostility.lookup", "native symbols unavailable -> reflected function name -> verified instruction patterns");
    }

    // 读取对象拥有的完整标签，用于敌对/生命/可作用性判断。
    List<string> OwnedTags(Identity actor)
    {
        OwnedTagsLayout(actor);
        Identity asc = Token(M.Q(actor.Address + Offset(actor, "AbilitySystemComponent", 8)));
        if (!IsA(asc.Class, "SWAbilitySystemComponent") || M.Q(asc.Address + Offset(asc, "AvatarActor", 8)) != actor.Address)
            throw new Exception("敌对标签归属不匹配。");
        long h = asc.Address + ownedTagsOffset, data = M.Q(h);
        int n = M.I(h + 8), cap = M.I(h + 12);
        if (n < 0 || n > 512 || cap < n || cap > 2048)
            throw new Exception("完整标签数组无效。");
        byte[] bytes = M.Read(data, n * 8);
        var tags = new List<string>();
        for (int i = 0; i < n; i++)
        {
            string name = Name(BitConverter.ToInt32(bytes, i * 8));
            if (BitConverter.ToInt32(bytes, i * 8 + 4) != 0 || name == "None" || name.IndexOf('\0') >= 0)
                throw new Exception("完整标签内容无效。");
            tags.Add(name);
        }

        if (!Valid(actor) || !Valid(asc) || M.Q(h) != data || M.I(h + 8) != n)
            throw new Exception("完整标签正在变化。");
        return tags;
    }
}
