// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 中文维护说明：工具端原生战斗协议、目标筛选与请求生命周期。协议为 4；场景 epoch、序号和组件 instance 必须同时匹配。蓄力/引导需要续约及释放，停止时只结束本工具拥有的请求。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using System.Web.Script.Serialization;

// NativeCombatCatalog 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class NativeCombatCatalog
{
    static readonly HashSet<string> allowed = Load();
    // 加载本模块的配置或内嵌目录，并使用实现中的校验/回退规则。
    static HashSet<string> Load()
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("native-combat-artifacts.json"))
        using (var reader = new StreamReader(stream))
        {
            var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
            if (!CombatDefinitions.MatchesVersion((string)root["gameSHA256"]))
                throw new Exception("Native artifact catalog game mismatch");
            return new HashSet<string>(((System.Collections.IEnumerable)root["allowed"]).Cast<object>().Select(v => (string)v), StringComparer.Ordinal);
        }
    }

    // 检查法器类型是否在已适配原生生命周期目录中。
    public static bool Supports(string type)
    {
        return allowed.Contains(type);
    }
}

// NativeCombatRule 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class NativeCombatRule
{
    // 选出原生距离内可近战的已验证敌人。
    public static CombatTarget AttackTarget(CombatState state, int range, bool encounter = false)
    {
        return state != null && state.CanAttempt && state.PlayerVelocity.Valid && (encounter || state.PlayerVelocity.Length <= 30) ? state.Targets.Where(t => t.Distance <= range && Math.Abs(t.Position.Z - state.Player.Z) <= 100 && !String.IsNullOrEmpty(t.ActorName) && !String.IsNullOrEmpty(t.Type)).OrderBy(t => t.Distance).FirstOrDefault() : null;
    }

    // 选择法器可作用的真实敌对目标，保留可用性及类型限制。
    public static CombatTarget ArtifactTarget(CombatState state, bool encounter)
    {
        return state != null && state.CanAttempt ? state.Targets.Where(t => (encounter || t.TargetsPlayer) && t.Distance <= 800 && Math.Abs(t.Position.Z - state.Player.Z) <= 250 && !String.IsNullOrEmpty(t.ActorName) && !String.IsNullOrEmpty(t.Type)).OrderBy(t => t.Distance).FirstOrDefault() : null;
    }

    // 选出已勾选且冷却、灵魂和原生生命周期均可用的法器槽位。
    public static int[] ArtifactSlots(ToolboxSettings settings, CooldownInfo[] cooldowns, float souls, float[] costs)
    {
        if (cooldowns == null || cooldowns.Length < 3 || costs == null || costs.Length != 3 || float.IsNaN(souls) || float.IsInfinity(souls) || souls < 0)
            return new int[0];
        float remaining = souls;
        var slots = new List<int>();
        foreach (int i in Enumerable.Range(0, 3).OrderBy(i => i == settings.PotionSlot ? 0 : 1))
            if (settings.AutoSlots[i] && cooldowns[i] != null && cooldowns[i].Ready && !cooldowns[i].Unavailable && !float.IsNaN(costs[i]) && !float.IsInfinity(costs[i]) && costs[i] >= 0 && costs[i] <= remaining)
            {
                slots.Add(i + 1);
                remaining -= costs[i];
            }

        return slots.ToArray();
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        if (!NativeCombatCatalog.Supports("SW.Item.Artifact.DeathcapMushroom") || !NativeCombatCatalog.Supports("SW.Item.Artifact.HasteMushroom"))
            throw new Exception("Original one-shot artifact catalog missing");
        foreach (string tag in new[]
        {
            "SoulHarvester",
            "FlameSceptre",
            "CorruptedBeacon",
            "LightningRod",
            "BlizzardStaff",
            "RedstoneMines",
            "WitchesBrew",
            "Honeypot",
            "FireworkQuiver"
        }

        )
            if (!NativeCombatCatalog.Supports("SW.Item.Artifact." + tag))
                throw new Exception("Original artifact lifecycle catalog missing " + tag);
        if (NativeCombatCatalog.Supports("SW.Item.Artifact.Unknown"))
            throw new Exception("Unresearched artifact type accepted");
        var s = new CombatState
        {
            Known = true,
            Player = new ThreatVector(0, 0, 0),
            PlayerVelocity = new ThreatVector(0, 0, 0),
            Readiness = new CombatReadiness
            {
                Known = true,
                Alive = true,
                GameplayUiOnly = true
            }
        };
        s.Targets.Add(new CombatTarget { Id = "enemy", ActorName = "Enemy_C_1", Type = "SW.Mob.Zombie", Distance = 100, Position = new ThreatVector(100, 0, 0) });
        if (AttackTarget(s, 220) == null || s.CanAim)
            throw new Exception("Native attack incorrectly depends on camera/key bindings");
        s.PlayerVelocity = new ThreatVector(31, 0, 0);
        if (AttackTarget(s, 220) != null || AttackTarget(s, 220, true) == null)
            throw new Exception("Stationary/encounter movement rules failed");
        if (ArtifactTarget(s, false) != null || ArtifactTarget(s, true) == null)
            throw new Exception("Encounter artifacts require enemy aggro or retaliation ignores it");
        s.Targets[0].Distance = 900;
        if (ArtifactTarget(s, true) != null || AttackTarget(s, 220, true) != null)
            throw new Exception("Encounter attacks distant target");
        s.Targets[0].Distance = 100;
        s.PlayerVelocity = new ThreatVector(0, 0, 0);
        s.Readiness.GameplayUiOnly = false;
        if (AttackTarget(s, 220) != null || AttackTarget(s, 220, true) != null || ArtifactTarget(s, true) != null)
            throw new Exception("Native encounter accepted menu");
        var settings = new ToolboxSettings
        {
            AutoSlots = new[]
            {
                true,
                true,
                true
            }
        };
        var cd = new[]
        {
            new CooldownInfo
            {
                Known = true
            },
            new CooldownInfo
            {
                Known = true
            },
            new CooldownInfo
            {
                Known = true
            }
        };
        if (!ArtifactSlots(settings, cd, 25, new[] { 20f, 20f, 20f }).SequenceEqual(new[] { settings.PotionSlot + 1 }))
            throw new Exception("Native artifact slot soul reservation failed");
        cd[settings.PotionSlot].Locked = true;
        if (ArtifactSlots(settings, cd, 0, new[] { 20f, 20f, 20f }).Length > 0)
            throw new Exception("Unavailable native artifact slot accepted");
        var migrated = ToolboxSettings.Parse(new[] { "combatNative=1", "combatEncounter=1" });
        if (!migrated.CombatEncounter || !migrated.CombatNative || new ToolboxSettings().CombatNative || new ToolboxSettings().CombatEncounter)
            throw new Exception("Native encounter opt-in migration failed");
        return "PASS: native melee independent of cursor/camera/key bindings; stationary/menu guards, explicit artifact slots and soul budget, no potion fallback, opt-in settings.\r\n";
    }
}

// CombatBridge 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class CombatBridge
{
    const string Mod = "MCD2CombatBridge";
    readonly string epoch = Guid.NewGuid().ToString("N");
    readonly NativeBridgeChannel channel;
    string instance, lastCommand;
    int sequence;
    // 初始化 CombatBridge 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public CombatBridge(int pid)
    {
        channel = new NativeBridgeChannel(pid, Mod, "4", Parse);
        instance = channel.Instance;
    }

    // 读取本模块的当前快照；读取与执行动作分开处理。
    public NearbyLootReceipt Read()
    {
        return channel.Read();
    }

    // 解析外部数据并检查结构约束；格式或协议失配不能继续使用。
    static NearbyLootReceipt Parse(string value)
    {
        string[] p = value.Split('|');
        int n;
        double clock;
        if (p.Length == 6 && p[0] != "4")
            throw new Exception(L10n.T("原生战斗组件需要更新，请退出游戏后重新安装组件"));
        if (p.Length != 6 || p[0] != "4" || String.IsNullOrEmpty(p[1]) || !Int32.TryParse(p[3], out n) || n < 0 || !Double.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out clock) || Double.IsNaN(clock) || Double.IsInfinity(clock) || clock < 0)
            throw new Exception("Invalid native combat receipt");
        return new NearbyLootReceipt
        {
            Instance = p[1],
            Epoch = p[2],
            Sequence = n,
            Status = p[4],
            Clock = clock
        };
    }

    // 检查单个协议字段，拒绝分隔符或超长内容造成的消息歧义。
    static string Field(string v)
    {
        if (String.IsNullOrEmpty(v) || v.Length > 160 || v.Any(c => c < ' ' || c > 126 || c == '|'))
            throw new Exception("Invalid native combat identity");
        return v;
    }

    // 构造协议 4 战斗命令，携带真实玩家/目标与动作生命周期信息。
    public static string Command(NearbyLootReceipt r, string epoch, int n, string kind, CombatState state, CombatTarget target, int slot, ThreatVector direction, double range, bool dry)
    {
        if (r == null || epoch == null || epoch.Length != 32 || n <= 0 || state == null || !state.CanAttempt || !state.Player.Valid || !direction.Valid || Math.Abs(direction.Z) > .00001 || Math.Abs(direction.Length - 1) > .00001 || Double.IsNaN(range) || Double.IsInfinity(range) || range < 80 || range > 2500 || kind != "melee" && kind != "encounter" && kind != "artifact" && kind != "roll" || kind == "artifact" && (slot < 1 || slot > 3) || kind != "artifact" && slot != 0 || kind != "roll" && (target == null || !target.Position.Valid))
            throw new Exception("Invalid native combat command");
        var t = target == null ? state.Player : target.Position;
        Func<double, string> number = v => v.ToString("R", CultureInfo.InvariantCulture);
        return String.Join("|", new[] { "4", Field(r.Instance), Field(epoch), n.ToString(CultureInfo.InvariantCulture), kind, Field(state.PawnName), kind == "roll" ? "-" : Field(target.ActorName), kind == "roll" ? "-" : Field(target.Type), dry ? "1" : "0", slot.ToString(CultureInfo.InvariantCulture), number(state.Player.X), number(state.Player.Y), number(state.Player.Z), number(t.X), number(t.Y), number(t.Z), number(direction.X), number(direction.Y), number(range), number(r.Clock + .25) });
    }

    // 发送一次原生攻击、法器或闪避请求，建立对应序号归属。
    public void Dispatch(string kind, CombatState state, CombatTarget target, int slot, ThreatVector direction, double range, bool dry = false)
    {
        var r = Read();
        lastCommand = Command(r, epoch, ++sequence, kind, state, target, slot, direction, range, dry);
        Write(lastCommand);
    }

    // 检查当前动作是否仍在等待对应回执。
    public static bool Pending(string status)
    {
        return status == "Dispatched" || status == "Holding" || status == "TargetSubmitted" || status == "ReleaseRequested";
    }

    // 续约本请求拥有的蓄力/引导动作，不重新认领玩家启动的能力。
    public static string Renew(string command, NearbyLootReceipt receipt)
    {
        if (command == null || receipt == null || !Pending(receipt.Status) || Double.IsNaN(receipt.Clock) || Double.IsInfinity(receipt.Clock) || receipt.Clock < 0)
            throw new Exception("Invalid native combat renewal");
        var f = command.Split('|');
        int n;
        double expiry;
        if (f.Length != 20 || f[0] != "4" || f[1] != receipt.Instance || f[2] != receipt.Epoch || !Int32.TryParse(f[3], out n) || n != receipt.Sequence || n <= 0 || !Double.TryParse(f[19], NumberStyles.Float, CultureInfo.InvariantCulture, out expiry) || Double.IsNaN(expiry) || Double.IsInfinity(expiry) || receipt.Clock > expiry || receipt.Clock + .25 < expiry)
            throw new Exception("Expired or mismatched native combat renewal");
        f[19] = (receipt.Clock + .25).ToString("R", CultureInfo.InvariantCulture);
        return String.Join("|", f);
    }

    // 按当前请求时限续发保持命令，避免未释放的长按动作悬挂。
    public void KeepAlive(NearbyLootReceipt receipt)
    {
        lastCommand = Renew(lastCommand, receipt);
        Write(lastCommand);
    }

    // 验证当前身份、序号或基线是否仍属于同一次操作。
    public bool Matches(NearbyLootReceipt r)
    {
        return r.Instance == instance && r.Epoch == epoch && r.Sequence == sequence;
    }

    // 写入本组件的自有通信数据；不能写入角色存档。
    void Write(string value)
    {
        channel.Write(value);
    }

    // 终止本工具拥有的请求并恢复禁用命令，清理未完成状态。
    public void Stop()
    {
        lastCommand = null;
        try
        {
            channel.Stop();
        }
        catch (Exception e)
        {
            ToolboxLog.Error("Combat.NativeStop", e);
        }
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        var state = new CombatState
        {
            Known = true,
            PawnName = "Player_C_1",
            Player = new ThreatVector(1, 2, 3),
            Readiness = new CombatReadiness
            {
                Known = true,
                Alive = true,
                GameplayUiOnly = true
            }
        };
        var target = new CombatTarget
        {
            ActorName = "Enemy_C_42",
            Type = "SW.Mob.Zombie",
            Position = new ThreatVector(40, 2, 3)
        };
        var r = Parse("4|instance||0|Disabled|10.5");
        string cmd = Command(r, new string ('a', 32), 1, "melee", state, target, 0, new ThreatVector(1, 0, 0), 220, false);
        var fields = cmd.Split('|');
        if (fields.Length != 20 || fields[5] != "Player_C_1" || fields[6] != "Enemy_C_42" || fields[19] != "10.75")
            throw new Exception("Native combat identity/expiry fields");
        if (Command(r, new string ('a', 32), 2, "encounter", state, target, 0, new ThreatVector(1, 0, 0), 220, false).Split('|')[4] != "encounter")
            throw new Exception("Encounter command isolation failed");
        byte[] fixture = NativeMailboxCodec.Replace(EquipmentBridgeChecks.Fixture(Mod, NativeMailboxCodec.Encode("OFF")), NativeMailboxCodec.Encode(cmd), Mod);
        if (NativeMailboxCodec.Decode(EquipmentSaveCodec.Parse(fixture, "Request", "Command", Mod).Value) != cmd)
            throw new Exception("Native combat save round-trip");
        bool rejected = false;
        try
        {
            EquipmentSaveCodec.Parse(fixture, "Request", "Command", "MCD2NearbyLootBridge");
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Combat command accepted by collection bridge");
        foreach (var action in new[]
        {
            "artifact",
            "roll",
            "unknown"
        }

        )
        {
            rejected = false;
            try
            {
                Command(r, new string ('a', 32), 2, action, state, target, 4, new ThreatVector(1, 0, 0), 220, false);
            }
            catch
            {
                rejected = true;
            }

            if (!rejected)
                throw new Exception("Unsupported native action/slot accepted");
        }

        rejected = false;
        try
        {
            Command(r, new string ('a', 32), 2, "roll", state, null, 0, new ThreatVector(0, 0, 0), 1900, false);
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Zero native roll direction accepted");
        target.ActorName = "Enemy|injection";
        rejected = false;
        try
        {
            Command(r, new string ('a', 32), 2, "melee", state, target, 0, new ThreatVector(1, 0, 0), 220, false);
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Native field injection accepted");
        var live = Parse("4|instance|" + new string ('a', 32) + "|1|Holding|10.6");
        string renewed = Renew(cmd, live);
        if (!renewed.Split('|').Take(19).SequenceEqual(cmd.Split('|').Take(19)) || renewed.Split('|')[19] != "10.85")
            throw new Exception("Renewal changed dispatched identity or lease");
        foreach (string value in new[]
        {
            "4|instance|" + new string ('b', 32) + "|1|Holding|10.6",
            "4|instance|" + new string ('a', 32) + "|2|Holding|10.6",
            "4|instance|" + new string ('a', 32) + "|1|Holding|11",
            "4|instance|" + new string ('a', 32) + "|1|ArtifactLifecycleEnded|10.6"
        }

        )
        {
            rejected = false;
            try
            {
                Renew(cmd, Parse(value));
            }
            catch
            {
                rejected = true;
            }

            if (!rejected)
                throw new Exception("Mismatched, expired or completed action renewed");
        }

        rejected = false;
        try
        {
            Parse("1|instance||0|Disabled|10.5");
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Old combat protocol accepted");
        rejected = false;
        try
        {
            Parse("2|instance||0|Disabled|10.5");
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Pre-encounter combat protocol accepted");
        rejected = false;
        try
        {
            Parse("3|instance||0|Disabled|10.5");
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Variable-width combat protocol accepted");
        return NativeCombatRule.Test() + "PASS: immutable action renewal; mismatched epoch/sequence, expired lease, terminal receipt and old protocol rejected.\r\nPASS: independent native combat protocol 4, exact actor identity, 250ms lease, valid direction/slot boundaries, isolated save codec. No game calls or inputs.\r\n";
    }
}

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm
{
    CheckBox combatNativeEnabled;
    PixelLabel nativeCombatNote;
    CombatBridge combatNativeBridge;
    // 停止原生动作并取消未完成的本工具请求。
    void StopNativeCombat()
    {
        var bridge = combatNativeBridge;
        combatNativeBridge = null;
        if (bridge != null)
            bridge.Stop();
    }

    // 统一派发和观察原生战斗动作，响应停止和超时。
    async Task NativeCombatAction(InputRequest request, CombatState state, string kind, CombatTarget target, int slot, ThreatVector direction, double range, CancellationToken cancel)
    {
        bool sent = false;
        try
        {
            if (!settings.CombatNative || !Active(request) || cancel.IsCancellationRequested || request.Generation != generation || clock.ElapsedMilliseconds > request.Expires || !ToolboxInput.Front(reader.Pid))
                return;
            if (combatNativeBridge == null)
                combatNativeBridge = new CombatBridge(reader.Pid);
            var bridge = combatNativeBridge;
            if (!settings.CombatNative || !Active(request) || cancel.IsCancellationRequested || request.Generation != generation || clock.ElapsedMilliseconds > request.Expires || !ToolboxInput.Front(reader.Pid))
                return;
            bridge.Dispatch(kind, state, target, slot, direction, range);
            sent = true;
            if (kind == "melee" || kind == "encounter")
                lastCombatAttack = clock.ElapsedMilliseconds;
            else if (kind == "artifact")
                lastCombatArtifact = clock.ElapsedMilliseconds;
            else
            {
                lastEvade = clock.ElapsedMilliseconds;
                evadedThreats.Add(request.ThreatId);
            }

            ToolboxLog.Write("Combat.NativeRequest", "kind=" + kind + " target=" + (target == null ? "-" : target.Id) + " slot=" + slot + "; native granted ability; no cursor/key input or path");
            nativeCombatNote.Text = L10n.T("已请求原生战斗动作，等待游戏结果");
            long deadline = clock.ElapsedMilliseconds + (kind == "artifact" ? 9500 : 1000), heartbeat = 0;
            string phase = null;
            while (clock.ElapsedMilliseconds < deadline)
            {
                await Task.Delay(40, cancel);
                if (!Active(request) || request.Generation != generation || !settings.CombatNative || !ToolboxInput.Front(reader.Pid))
                {
                    bridge.Stop();
                    return;
                }

                var r = bridge.Read();
                if (!bridge.Matches(r))
                    continue;
                if (CombatBridge.Pending(r.Status))
                {
                    if (r.Status != phase)
                    {
                        phase = r.Status;
                        ToolboxLog.Write("Combat.NativePhase", "kind=" + kind + " slot=" + slot + " status=" + phase + " clock=" + r.Clock.ToString("R", CultureInfo.InvariantCulture));
                        nativeCombatNote.Text = L10n.T(phase == "Holding" ? "法器蓄力／引导中；F9 可中止" : phase == "ReleaseRequested" ? "已请求法器释放，等待原生结束" : phase == "TargetSubmitted" ? "已提交原生瞄准目标，等待游戏结果" : "已请求原生战斗动作，等待游戏结果");
                    }

                    if (kind == "artifact" && clock.ElapsedMilliseconds - heartbeat >= 80)
                    {
                        bridge.KeepAlive(r);
                        heartbeat = clock.ElapsedMilliseconds;
                    }

                    continue;
                }

                nativeCombatNote.Text = L10n.T(r.Status == "ArtifactLifecycleEnded" ? "已观察到法器原生流程结束；效果待核实" : r.Status == "AbilityObserved" ? "已观察到原生能力激活；伤害由游戏结算" : r.Status == "RollObserved" ? "已观察到闪避消耗充能并沿请求方向移动" : "原生动作未确认，查看诊断日志");
                ToolboxLog.Write("Combat.NativeResult", "kind=" + kind + " status=" + r.Status + "; activation/lifecycle evidence only; damage/hit not asserted");
                return;
            }

            nativeCombatNote.Text = L10n.T("原生动作未确认，查看诊断日志");
            ToolboxLog.Write("Combat.NativeResult", "kind=" + kind + " status=TimeoutUnconfirmed");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            nativeCombatNote.Text = L10n.T(e.Message);
            ToolboxLog.Error("Combat.Native", e);
        }
        finally
        {
            if (sent && combatNativeBridge != null)
                combatNativeBridge.Stop();
        }
    }

    // 执行一次已筛选目标的原生近战，不模拟鼠标点击。
    async Task RunNativeMelee(InputRequest request, CancellationToken cancel)
    {
        CombatState state;
        if (!(settings.CombatAttack || settings.CombatEncounter) || !ValidateCombatRequest(request, out state) || !CombatScheduling.NativeInputReady(state, settings, ToolboxInput.Held, settings.CombatEncounter))
            return;
        var target = NativeCombatRule.AttackTarget(state, settings.CombatRange, settings.CombatEncounter);
        if (target == null || target.Id != request.TargetId)
            return;
        var direction = new ThreatVector(target.Position.X - state.Player.X, target.Position.Y - state.Player.Y, 0);
        double length = direction.Length;
        if (length < 1)
            return;
        await NativeCombatAction(request, state, settings.CombatEncounter ? "encounter" : "melee", target, 0, direction * (1 / length), settings.CombatRange, cancel);
    }

    // 按预算和可用性逐槽触发法器，维护瞄准/蓄力/引导的生命周期。
    async Task RunNativeArtifacts(InputRequest request, CancellationToken cancel)
    {
        CombatState state;
        if (!settings.CombatArtifacts || !ValidateCombatRequest(request, out state) || clock.ElapsedMilliseconds - lastCombatArtifact < settings.RetrySeconds * 1000L)
            return;
        var target = NativeCombatRule.ArtifactTarget(state, settings.CombatEncounter);
        if (target == null || target.Id != request.TargetId)
            return;
        var sample = reader.Snapshot();
        UpdateArtifact();
        UpdateCooldowns();
        var inventory = reader.ReadInventoryAudit();
        var eligible = new ToolboxSettings
        {
            PotionSlot = settings.PotionSlot,
            AutoSlots = Enumerable.Range(1, 3).Select(slot => settings.AutoSlots[slot - 1] && inventory.Any(row => (Convert.ToString(row["EquippedSlot"], CultureInfo.InvariantCulture) == "SW.ItemSlot.Equipment.Artifact.Slot" + slot || Convert.ToString(row["Container"], CultureInfo.InvariantCulture) == "SW.ItemSlot.Equipment.Artifact.Slot" + slot) && NativeCombatCatalog.Supports(Convert.ToString(((Dictionary<string, object>)row["ItemData"])["TypeTag"], CultureInfo.InvariantCulture)))).ToArray()
        };
        int[] slots = NativeCombatRule.ArtifactSlots(eligible, cooldowns, sample[2], artifactCosts);
        if (slots.Length == 0)
        {
            lastCombatArtifact = clock.ElapsedMilliseconds;
            nativeCombatNote.Text = L10n.T("没有可用的原生法器；检查槽位、冷却和灵魂");
            return;
        }

        // 背包/冷却读取需要时间；派发前重新采集玩家/目标快照，不能使用过期坐标。
        if (!ValidateCombatRequest(request, out state))
            return;
        target = NativeCombatRule.ArtifactTarget(state, settings.CombatEncounter);
        if (target == null || target.Id != request.TargetId)
            return;
        var direction = new ThreatVector(target.Position.X - state.Player.X, target.Position.Y - state.Player.Y, 0);
        double length = direction.Length;
        if (length < 1)
            return;
        // 每次请求只处理一个槽位；下一槽之前重算冷却和灵魂预算。
        await NativeCombatAction(request, state, "artifact", target, slots[0], direction * (1 / length), 800, cancel);
    }

    // 执行已校验方向和落点的原生闪避动作。
    async Task RunNativeEvade(InputRequest request, CancellationToken cancel)
    {
        CombatState state;
        if (!settings.CombatEvade || !ValidateCombatRequest(request, out state) || evadeGround == null || clock.ElapsedMilliseconds - lastEvadeGround > 8000)
            return;
        var plan = EvadeGroundRule.Plan(state, reader.Threats(clock.ElapsedMilliseconds), evadeGround, evadedThreats, null, true);
        if (plan == null || plan.ThreatId != request.ThreatId || new[]
        {
            1,
            2,
            4,
            5,
            6,
            65,
            68,
            83,
            87
        }.Any(ToolboxInput.Held))
            return;
        await NativeCombatAction(request, state, "roll", null, 0, plan.Direction, state.Roll.GroundEnvelope, cancel);
    }
}
