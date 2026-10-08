// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 工具端原生战斗协议、目标筛选与请求生命周期。
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

static class NativeCombatCatalog
{
    static readonly HashSet<string> allowed = Load();
    static readonly HashSet<string> selfAllowed = LoadSelf();
    static HashSet<string> LoadSelf()
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("native-combat-artifacts.json"))
        using (var reader = new StreamReader(stream))
        {
            var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
            return new HashSet<string>(((System.Collections.IEnumerable)root["lifecycles"]).Cast<Dictionary<string, object>>().Where(row => !(bool)row["targeting"] && allowed.Contains((string)row["type"])).Select(row => (string)row["type"]), StringComparer.Ordinal);
        }
    }

    public static bool SupportsSelf(string type)
    {
        return selfAllowed.Contains(type);
    }

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

}

sealed class CombatBridge
{
    const string Mod = "MCD2CombatBridge";
    readonly string epoch = Guid.NewGuid().ToString("N");
    readonly NativeBridgeChannel channel;
    string instance, lastCommand;
    int sequence;
    public CombatBridge(int pid)
    {
        channel = new NativeBridgeChannel(pid, Mod, "5", Parse);
        instance = channel.Instance;
    }

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
        if (p.Length == 6 && p[0] != "5")
            throw new Exception(L10n.T("原生战斗组件需要更新，请退出游戏后重新安装组件"));
        if (p.Length != 6 || p[0] != "5" || String.IsNullOrEmpty(p[1]) || !Int32.TryParse(p[3], out n) || n < 0 || !Double.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out clock) || Double.IsNaN(clock) || Double.IsInfinity(clock) || clock < 0)
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

    static string Field(string v)
    {
        if (String.IsNullOrEmpty(v) || v.Length > 160 || v.Any(c => c < ' ' || c > 126 || c == '|'))
            throw new Exception("Invalid native combat identity");
        return v;
    }

    // 构造协议 5 战斗命令，携带真实玩家/目标与动作生命周期信息。
    public static string Command(NearbyLootReceipt r, string epoch, int n, string kind, CombatState state, CombatTarget target, int slot, ThreatVector direction, double range, bool dry)
    {
        bool self = kind == "potion" || kind == "selfartifact" || kind == "roll";
        bool artifact = kind == "artifact" || kind == "selfartifact";
        // 药水的 range 字段专用于血量百分比阈值，其他动作仍表示范围；固定字段宽度保持不变。
        if (r == null || epoch == null || epoch.Length != 32 || n <= 0 || state == null || !state.CanAttempt || !state.Player.Valid || !direction.Valid || Math.Abs(direction.Z) > .00001 || Math.Abs(direction.Length - 1) > .00001 || Double.IsNaN(range) || Double.IsInfinity(range) || (kind == "potion" ? range < 1 || range > 99 : range < 80 || range > 2500) || kind != "melee" && kind != "encounter" && kind != "artifact" && kind != "selfartifact" && kind != "potion" && kind != "roll" || artifact && (slot < 1 || slot > 3) || !artifact && slot != 0 || !self && (target == null || !target.Position.Valid) || self && target != null)
            throw new Exception("Invalid native combat command");
        var t = target == null ? state.Player : target.Position;
        Func<double, string> number = v => v.ToString("R", CultureInfo.InvariantCulture);
        return String.Join("|", new[] { "5", Field(r.Instance), Field(epoch), n.ToString(CultureInfo.InvariantCulture), kind, Field(state.PawnName), self ? "-" : Field(target.ActorName), self ? "-" : Field(target.Type), dry ? "1" : "0", slot.ToString(CultureInfo.InvariantCulture), number(state.Player.X), number(state.Player.Y), number(state.Player.Z), number(t.X), number(t.Y), number(t.Z), number(direction.X), number(direction.Y), number(range), number(r.Clock + .25) });
    }

    // 发送一次原生攻击、法器或闪避请求，建立对应序号归属。
    public void Dispatch(string kind, CombatState state, CombatTarget target, int slot, ThreatVector direction, double range, bool dry = false)
    {
        var r = Read();
        lastCommand = Command(r, epoch, ++sequence, kind, state, target, slot, direction, range, dry);
        Write(lastCommand);
    }

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
        if (f.Length != 20 || f[0] != "5" || f[1] != receipt.Instance || f[2] != receipt.Epoch || !Int32.TryParse(f[3], out n) || n != receipt.Sequence || n <= 0 || !Double.TryParse(f[19], NumberStyles.Float, CultureInfo.InvariantCulture, out expiry) || Double.IsNaN(expiry) || Double.IsInfinity(expiry) || receipt.Clock > expiry || receipt.Clock + .25 < expiry)
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

}

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm
{
    CheckBox combatNativeEnabled;
    PixelLabel nativeCombatNote;
    CombatBridge combatNativeBridge;
    // 手柄恢复始终走组件；其他请求仍按用户原生模式和前后台作用域决定。
    bool NativeRequestRoute(InputRequest request)
    {
        // 前台键鼠组合是一键同时按下全部当前游戏槽位键，与自动战斗的原生模式独立。
        // 后台和手柄保留组件逐槽执行，绝不向其他应用发送全局按键。
        if (request.Hotbar && !request.Controller && GameForeground()) return false;
        return UseNativeCombat || request.Controller && (request.Healing || request.Threat || request.Hotbar);
    }

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
            if (!NativeRequestRoute(request) || !Active(request) || cancel.IsCancellationRequested || request.Generation != generation || clock.ElapsedMilliseconds > request.Expires)
                return;
            if (combatNativeBridge == null)
                combatNativeBridge = new CombatBridge(reader.Pid);
            var bridge = combatNativeBridge;
            if (!NativeRequestRoute(request) || !Active(request) || cancel.IsCancellationRequested || request.Generation != generation || clock.ElapsedMilliseconds > request.Expires)
                return;
            bridge.Dispatch(kind, state, target, slot, direction, range);
            sent = true;
            if (kind == "melee" || kind == "encounter")
                lastCombatAttack = clock.ElapsedMilliseconds;
            else if (kind == "artifact" || kind == "selfartifact")
                lastCombatArtifact = clock.ElapsedMilliseconds;
            else if (kind == "roll")
            {
                lastEvade = clock.ElapsedMilliseconds;
                evadedThreats.Add(request.ThreatId);
            }

            ToolboxLog.Write("Combat.NativeRequest", "kind=" + kind + " target=" + (target == null ? "-" : target.Id) + " slot=" + slot + "; native granted ability; no cursor/key input or path");
            nativeCombatNote.Text = L10n.T("已请求原生战斗动作，等待游戏结果");
            if (request.Healing || request.Threat)
            {
                healRule.Last = clock.ElapsedMilliseconds;
                if (request.Threat)
                    consumedThreats.Add(request.ThreatId);
            }

            long deadline = clock.ElapsedMilliseconds + (kind == "artifact" || kind == "selfartifact" ? 9500 : kind == "potion" ? 1600 : 1000), heartbeat = 0;
            string phase = null;
            while (clock.ElapsedMilliseconds < deadline)
            {
                await Task.Delay(40, cancel);
                if (!Active(request) || request.Generation != generation || !NativeRequestRoute(request))
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

                    if ((kind == "artifact" || kind == "selfartifact") && clock.ElapsedMilliseconds - heartbeat >= 80)
                    {
                        bridge.KeepAlive(r);
                        heartbeat = clock.ElapsedMilliseconds;
                    }

                    continue;
                }

                // 药水结果仅确认充能消耗；实际回血仍由独立读数观察，不把请求发送当作成功。
                nativeCombatNote.Text = L10n.T(r.Status == "PotionConsumed" ? "已观察到药水充能消耗；回血由游戏结算" : r.Status == "ArtifactLifecycleEnded" ? "法器流程已结束" : r.Status == "AbilityObserved" ? "已观察到原生能力激活；伤害由游戏结算" : r.Status == "RollObserved" ? "已观察到闪避消耗充能并沿请求方向移动" : "原生动作未确认，查看诊断日志");
                ToolboxLog.Write("Combat.NativeResult", "kind=" + kind + " status=" + r.Status + "; activation/lifecycle/consumption evidence only; damage/healing not asserted");
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

    // 原生恢复使用真实药水能力及已勾选法器，逐槽重读血量、冷却、灵魂和装备，绝不直接改资源。
    async Task RunNativeRecovery(InputRequest request, CancellationToken cancel)
    {
        var owner = reader;
        int[] actions = request.Keys.Select(key => key == settings.PotionKey ? 3 : Array.IndexOf(settings.Slots, key)).Where(slot => slot >= 0).Distinct().OrderBy(slot => slot == 3 ? -1 : slot).ToArray();
        float beforeHealth = hp;
        var before = (CooldownInfo[])cooldowns.Clone();
        var observed = new List<int>();
        foreach (int action in actions)
        {
            if (!Active(request) || reader != owner || request.Generation != generation || cancel.IsCancellationRequested || (request.Hotbar ? !settings.ComboEnabled : !settings.HealEnabled))
                break;
            var sample = reader.Snapshot();
            // Last 是整个恢复请求的重试节流，不能阻断同一请求内后续已选槽位；血量资格仍逐槽重查。
            if (request.Healing && !(sample[0] > 0 && sample[1] > 0 && sample[0] < sample[1] * settings.Threshold / 100f))
                break;
            UpdateArtifact();
            UpdateCooldowns();
            bool potion;
            int[] eligible;
            if (request.Hotbar)
            {
                // 人工组合使用 ComboSlots，不借用自动恢复的 AutoSlots；成本逐槽读取，空自动选择不阻断组合。
                eligible = settings.ComboKeys();
                if (action == 3 || !settings.ComboSlots[action])
                    continue;
                // 组合费用由原生能力核对，不依赖 UI 费用文字。
                // 一次触发遍历全部勾选槽位，各槽仅请求一次，不因一个不可用槽位中断其余槽位。
            }
            else if (request.Threat)
            {
                if (!settings.ThreatEnabled)
                    break;
                var threats = reader.Threats(clock.ElapsedMilliseconds);
                if (!threats.Known || !threats.Threats.Any(threat => threat.Id == request.ThreatId))
                    break;
                eligible = ThreatRule.Select(settings, cooldowns, sample[2], artifactCosts, sample[0], sample[1]);
            }
            else
                eligible = RecoveryRule.Select(settings, cooldowns, sample[2], artifactCosts, out potion);
            if (!eligible.Contains(action == 3 ? settings.PotionKey : settings.Slots[action]))
                continue;
            string type = null;
            if (action != 3)
            {
                var equipped = reader.ReadInventory().Where(row => Convert.ToString(row["EquippedSlot"], CultureInfo.InvariantCulture) == "SW.ItemSlot.Equipment.Artifact.Slot" + (action + 1) || Convert.ToString(row["Container"], CultureInfo.InvariantCulture) == "SW.ItemSlot.Equipment.Artifact.Slot" + (action + 1)).ToArray();
                if (equipped.Length != 1)
                    continue;
                type = Convert.ToString(((Dictionary<string, object>)equipped[0]["ItemData"])["TypeTag"], CultureInfo.InvariantCulture);
                if (!NativeCombatCatalog.Supports(type))
                {
                    ToolboxLog.Limited("Recovery.NativeBlocked", "artifact lifecycle not adapted: " + type);
                    continue;
                }
            }

            // 在背包与遥测读取之后采集场景，避免用旧玩家坐标派发恢复请求。
            var state = reader.ReadCombatState();
            if (!state.CanAttempt)
            {
                ToolboxLog.Limited("Recovery.NativeBlocked", "player or gameplay UI unavailable");
                break;
            }

            var target = action == 3 || NativeCombatCatalog.SupportsSelf(type) ? null : NativeCombatRule.ArtifactTarget(state, true);
            if (action != 3 && !NativeCombatCatalog.SupportsSelf(type) && target == null)
            {
                ToolboxLog.Limited("Recovery.NativeBlocked", "aimed artifact requires eligible enemy: " + type);
                continue;
            }

            var direction = target == null ? new ThreatVector(1, 0, 0) : new ThreatVector(target.Position.X - state.Player.X, target.Position.Y - state.Player.Y, 0);
            if (direction.Length < 1)
                continue;
            // 每个后续槽位有独立的新鲜快照与短派发期限，不能续用前一个槽位的旧坐标。
            request.Expires = clock.ElapsedMilliseconds + 1200;
            observed.Add(action);
            await NativeCombatAction(request, state, action == 3 ? "potion" : target == null ? "selfartifact" : "artifact", target, action == 3 ? 0 : action + 1, direction * (1 / direction.Length), action == 3 ? settings.Threshold : 800, cancel);
            if (action == 3)
                break;
        }

        if (observed.Count > 0)
            ObserveRecoveryOutput(owner, request.Generation, observed.ToArray(), beforeHealth, before);
    }

    // 执行一次已筛选目标的原生近战，不模拟鼠标点击。
    async Task RunNativeMelee(InputRequest request, CancellationToken cancel)
    {
        CombatState state;
        if (!(settings.CombatAttack || settings.CombatEncounter) || !ValidateCombatRequest(request, out state) || !CombatScheduling.NativeInputReady(state, settings, GameInputHeld, settings.CombatEncounter))
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
        var inventory = reader.ReadInventory();
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
        }.Any(GameInputHeld))
            return;
        await NativeCombatAction(request, state, "roll", null, 0, plan.Direction, state.Roll.GroundEnvelope, cancel);
    }
}
