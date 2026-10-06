// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：导出敌人继承关系、反射属性与现场状态，用于研究兼容性。Replay/Audit 是诊断入口，不参与自动攻击；报告属于本地输出，不自动上传。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// 跨文件的只读游戏读取器；各 partial 文件共同持有同一连接/对象身份缓存。
sealed partial class HealthReader
{
    // 读取诊断输入并重放识别逻辑，不发送游戏攻击。
    public static string ReplayEnemyAudit(string path)
    {
        var serializer = new JavaScriptSerializer
        {
            MaxJsonLength = Int32.MaxValue
        };
        var report = (Dictionary<string, object>)serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8));
        int enemies = 0, neutral = 0, pets = 0;
        foreach (var value in (object[])report["actors"])
        {
            var actor = (Dictionary<string, object>)value;
            string cl = (string)actor["className"];
            if ((string)actor["readError"] != "" || (string)actor["kind"] != "mob-candidate")
                continue;
            string[] tags = ((object[])actor["ownedTags"]).Cast<string>().ToArray();
            bool result = ThreatRule.HostileTeam(tags);
            if (cl == "BP_JellyBunny_Meadows_C")
            {
                if (result)
                    throw new Exception("Neutral world actor included");
                neutral++;
            }

            if (cl == "BP_WolfCharacter_C")
            {
                if (result)
                    throw new Exception("Friendly companion included");
                pets++;
            }

            if (cl == "BP_WellspringRangedFella_C" || cl == "BP_WellspringMelee_C" || cl == "BP_Bonker_C")
            {
                if (!result)
                    throw new Exception("Captured hostile enemy excluded");
                enemies++;
                if (ThreatRule.HostileTeam(tags.Concat(new[] { "SW.Team.Player" })))
                    throw new Exception("Converted friendly enemy included");
                if (ThreatRule.HostileTeam(tags.Where(t => !t.StartsWith("SW.Team."))))
                    throw new Exception("Unknown team accepted");
            }
        }

        if (enemies == 0 || neutral == 0 || pets == 0)
            throw new Exception("Replay fixture lacks hostile/neutral/pet evidence");
        return "PASS: recorded live hostile enemies=" + enemies + ", neutral world actors=" + neutral + ", pets=" + pets + "; player conversion and unknown-team rejection. No game input sent.";
    }

    // 只读导出候选对象的继承链。
    List<string> AuditLineage(long cl)
    {
        var result = new List<string>();
        for (int i = 0; cl != 0 && i < 32; i++, cl = M.Q(cl + 64))
            result.Add(ClassName(cl));
        return result;
    }

    // 只读导出指定类的反射属性与偏移。
    List<object> AuditProperties(long cl)
    {
        var result = new List<object>();
        for (int depth = 0; cl != 0 && depth < 32; depth++, cl = M.Q(cl + 64))
        {
            for (long f = M.Q(cl + 80), n = 0; f != 0 && n < 512; f = M.Q(f + 24), n++)
            {
                string name = Name(M.I(f + 32));
                var refs = new List<object>();
                for (int offset = 112; offset <= 160; offset += 8)
                    try
                    {
                        Identity type = Token(M.Q(f + offset));
                        refs.Add(new { fieldOffset = offset, type = Name(type.Name), kind = ClassName(type.Class) });
                    }
                    catch
                    {
                    }

                string propertyKind = "";
                try
                {
                    propertyKind = Name(M.I(M.Q(f + 8)));
                }
                catch
                {
                }

                result.Add(new { name = name, propertyKind = propertyKind, declaredBy = ClassName(cl), offset = M.I(f + 72), size = M.I(f + 52), typeReferences = refs });
            }
        }

        return result;
    }

    // 采集敌人和威胁快照，生成本地诊断报告。
    public void AuditEnemies(string path, int observeSeconds = 0)
    {
        if (observeSeconds < 0 || observeSeconds > 30)
            throw new Exception("Enemy observation duration out of bounds");
        var actors = new List<object>();
        var types = new List<object>();
        var functions = new List<object>();
        var tokens = new List<Identity>();
        int count = M.I(objects + 20);
        long table = M.Q(objects);
        for (int ci = 0; ci * 65536 < count; ci++)
        {
            byte[] chunk = M.Read(M.Q(table + ci * 8), Math.Min(65536, count - ci * 65536) * 24);
            for (int at = 0; at < chunk.Length; at += 24)
                try
                {
                    long a = BitConverter.ToInt64(chunk, at);
                    if (a != 0)
                        tokens.Add(Token(a));
                }
                catch
                {
                }
        }

        var selectedTypes = new HashSet<long>();
        foreach (Identity t in tokens)
            try
            {
                string kind = ClassName(t.Class), name = Name(t.Name);
                if (kind != "Class" && kind != "BlueprintGeneratedClass" && kind != "ScriptStruct")
                    continue;
                bool selected = IsA(t.Address, "MobCharacter") || IsA(t.Address, "BaseProjectile") || IsA(t.Address, "AIController") || Regex.IsMatch(name, "ActorRelation|Relation|Faction|Team|SWAbilitySystemComponent|AIStateComponent|BaseCharacter|GameplayTag", RegexOptions.IgnoreCase);
                if (!selected)
                    continue;
                selectedTypes.Add(t.Address);
                types.Add(new { name = name, kind = kind, lineage = AuditLineage(t.Address), properties = AuditProperties(t.Address) });
            }
            catch
            {
            }

        Identity player = Pawn();
        long image = process.MainModule.BaseAddress.ToInt64();
        foreach (Identity actor in tokens)
            try
            {
                bool mob = IsA(actor.Class, "MobCharacter"), projectile = IsA(actor.Class, "BaseProjectile");
                if (!mob && !projectile || Name(actor.Name).StartsWith("Default__"))
                    continue;
                if (!Valid(actor))
                    continue;
                List<string> tags = new List<string>(), ownedTags = new List<string>();
                string error = "", target = "none";
                try
                {
                    if (mob)
                    {
                        tags = Tags(actor);
                        ownedTags = OwnedTags(actor);
                    }
                }
                catch (Exception e)
                {
                    error = e.Message;
                }

                if (mob)
                    try
                    {
                        Identity t = Weak(actor.Address + Offset(actor, "ReplicatedCurrentTargetActor", 8));
                        target = t == null ? "none" : t.Address == player.Address ? "local-player" : ClassName(t.Class);
                    }
                    catch (Exception e)
                    {
                        error += " " + e.Message;
                    }

                bool hidden = false, colliding = false;
                try
                {
                    hidden = ReadBool(actor, "bHidden");
                    colliding = ReadBool(actor, "bActorEnableCollision");
                }
                catch
                {
                }

                actors.Add(new { objectIndex = actor.Index, serial = actor.Serial, name = Name(actor.Name), className = ClassName(actor.Class), kind = mob ? "mob-candidate" : "projectile-candidate", lineage = AuditLineage(actor.Class), replicatedTags = tags, ownedTags = ownedTags, confirmedHostile = ThreatRule.HostileTeam(ownedTags), alive = ownedTags.Contains("SW.State.Life.Alive"), target = target, hidden = hidden, colliding = colliding, readError = error });
            }
            catch
            {
            }

        using (var symbols = new LocalSymbols(M.Handle, process.MainModule.FileName, image, process.MainModule.ModuleMemorySize))
        {
            foreach (Identity t in tokens)
                try
                {
                    if (ClassName(t.Class) != "Function")
                        continue;
                    Identity owner = Token(M.Q(t.Address + 32));
                    string name = Name(t.Name), ownerName = Name(owner.Name);
                    if (!selectedTypes.Contains(owner.Address) && !Regex.IsMatch(name, "Hostile|Enemy|Relation|Faction|Attack|Projectile|Preferred.*Target|Perc[e]?ivedHostile|Montage", RegexOptions.IgnoreCase))
                        continue;
                    string qualified = ownerName + "::" + name;
                    LocalSymbols.Symbol native = symbols.Find(qualified);
                    var wrappers = new List<object>();
                    if (native != null && executable(native.Address, 128))
                        wrappers.Add(new { source = "native-symbol", pointerFieldOffset = -1, rva = "0x" + (native.Address - image).ToString("X"), bytes = BitConverter.ToString(M.Read(native.Address, 128)).Replace('-', ' ') });
                    if (native == null)
                        for (int offset = 120; offset <= 240; offset += 8)
                        {
                            long fn = M.Q(t.Address + offset);
                            if (!executable(fn, 256))
                                continue;
                            wrappers.Add(new { source = "reflected-function-pointer", pointerFieldOffset = offset, rva = "0x" + (fn - image).ToString("X"), bytes = BitConverter.ToString(M.Read(fn, 256)).Replace('-', ' ') });
                        }

                    functions.Add(new { name = qualified, reflectionAvailable = true, nativeSymbolAvailable = native != null, wrappers = wrappers });
                }
                catch
                {
                }
        }

        var combatFrame = Threats(0);
        var observations = new List<object>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < observeSeconds * 1000)
        {
            var frame = Threats(watch.ElapsedMilliseconds);
            observations.Add(new { elapsedMs = watch.ElapsedMilliseconds, frame = frame });
            System.Threading.Thread.Sleep(75);
        }

        var report = new
        {
            status = "local read-only audit; no game inputs or game function calls",
            scope = "All currently loaded objects and enemy-related reflected types/functions. Unloaded levels/assets are not enumerated.",
            gameSha256 = AdaptationRecord.Get("game.sha256"),
            objectCount = count,
            types = types,
            actors = actors,
            functions = functions,
            combatFrame = combatFrame,
            combatSamples = observations,
            lookupRecord = AdaptationRecord.Contents()
        };
        var serializer = new JavaScriptSerializer
        {
            MaxJsonLength = Int32.MaxValue
        };
        File.WriteAllText(path, serializer.Serialize(report), new UTF8Encoding(true));
    }
}
