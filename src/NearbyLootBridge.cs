// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 中文维护说明：工具端收集协议 6：目标身份、场景、范围和一次请求回执。食物效果、装备入包 UID、宝箱 Open 标签和罐子掉落分别判断；请求已发送或实体消失不足以证明完整结果。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;

// 协议 6 的收集回执快照；Instance/Epoch/Sequence 对应一次请求，Status 是独立结果证据。
sealed class NearbyLootReceipt
{
    public string Instance, Epoch, Status;
    public int Sequence;
    public double Clock;
    // 解析外部数据并检查结构约束；格式或协议失配不能继续使用。
    public static NearbyLootReceipt Parse(string value)
    {
        string[] p = value.Split('|');
        int sequence;
        double clock;
        if (p.Length == 6 && p[0] != "6")
            throw new Exception(L10n.T("直接收集组件需要更新，请退出游戏后重新安装组件"));
        if (p.Length != 6 || p[0] != "6" || String.IsNullOrEmpty(p[1]) || !Int32.TryParse(p[3], out sequence) || sequence < 0 || !Double.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out clock) || Double.IsNaN(clock) || Double.IsInfinity(clock) || clock < 0)
            throw new Exception("Invalid nearby collection receipt");
        return new NearbyLootReceipt
        {
            Instance = p[1],
            Epoch = p[2],
            Sequence = sequence,
            Status = p[4],
            Clock = clock
        };
    }
}

// NearbyLootBridge 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class NearbyLootBridge
{
    const string Mod = "MCD2NearbyLootBridge";
    readonly string epoch = Guid.NewGuid().ToString("N"), instance;
    readonly NativeBridgeChannel channel;
    int sequence;
    // 初始化 NearbyLootBridge 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public NearbyLootBridge(int pid)
    {
        channel = new NativeBridgeChannel(pid, Mod, "6", NearbyLootReceipt.Parse);
        instance = channel.Instance;
    }

    // 读取本模块的当前快照；读取与执行动作分开处理。
    public NearbyLootReceipt Read()
    {
        return channel.Read();
    }

    // 检查单个协议字段，拒绝分隔符或超长内容造成的消息歧义。
    static string Field(string value)
    {
        if (String.IsNullOrEmpty(value) || value.Length > 160 || value.Any(c => c < ' ' || c > 126 || c == '|'))
            throw new Exception("Invalid nearby actor identity");
        return value;
    }

    // 限制直接收集可处理的已核实目标类型。
    static bool Supported(NearbyLootTarget t)
    {
        return t != null && NearbyLootCatalog.Contains(t.Type) && (t.Kind == "item" && !NearbyLootCatalog.Book(t.Type) && !NearbyLootCatalog.Food(t.Type) && !NearbyLootCatalog.Tnt(t.Type) && !NearbyLootCatalog.EmeraldCurrency(t.Type) || t.Kind == "book" && NearbyLootCatalog.Book(t.Type) || t.Kind == "chest" && t.Type.StartsWith("SW.LootActor.", StringComparison.Ordinal) && t.Type.Contains("Chest") || t.Kind == "food" && NearbyLootCatalog.Food(t.Type) || t.Kind == "tnt" && NearbyLootCatalog.Tnt(t.Type) || t.Kind == "pot" && NearbyLootCatalog.EmeraldPot(t.Type));
    }

    // 按目标类型判断回执是否包含所需成功证据，Dispatched 不能当成功。
    public static bool Confirmed(NearbyLootTarget t, string status)
    {
        return t != null && (t.Kind == "item" && status == "PickedUp" || t.Kind == "book" && status == "BookPickedUp" || t.Kind == "chest" && status == "Opened" || t.Kind == "food" && status == "Consumed" || t.Kind == "tnt" && status == "Carried" || t.Kind == "pot" && status == "BrokenWithDrops");
    }

    // 编码协议、场景、玩家、目标身份/位置、类型及期限。
    public static string Command(NearbyLootReceipt receipt, string epoch, int sequence, NearbyLootTarget target, string pawn, bool dry, bool allowMoving = false, int intervalMs = 500, int foodIntervalMs = 1000)
    {
        if (!Supported(target) || !target.Position.Valid || String.IsNullOrEmpty(target.Id) || epoch == null || epoch.Length != 32 || sequence <= 0)
            throw new Exception("Invalid direct collection target");
        if (intervalMs != NearbyLootTiming.Clamp(intervalMs) || foodIntervalMs != NearbyLootTiming.Clamp(foodIntervalMs))
            throw new Exception("Invalid direct collection interval");
        Func<double, string> number = v => v.ToString("R", CultureInfo.InvariantCulture);
        return "6|" + Field(receipt.Instance) + "|" + Field(epoch) + "|" + sequence + "|" + target.Kind + "|" + Field(target.ActorName) + "|" + Field(target.Type) + "|" + Field(pawn) + "|" + number(target.Position.X) + "|" + number(target.Position.Y) + "|" + number(target.Position.Z) + "|" + number(receipt.Clock + .6) + "|" + (dry ? "1" : "0") + "|" + (allowMoving ? "1" : "0") + "|" + intervalMs + "|" + foodIntervalMs;
    }

    // 向独立收集槽发送一次预览或真实原生交互请求。
    public void Dispatch(NearbyLootTarget target, string pawn, bool dry = false, bool allowMoving = false, int intervalMs = 500, int foodIntervalMs = 1000)
    {
        var receipt = Read();
        sequence++;
        Write(Command(receipt, epoch, sequence, target, pawn, dry, allowMoving, intervalMs, foodIntervalMs));
    }

    // 验证当前身份、序号或基线是否仍属于同一次操作。
    public bool Matches(NearbyLootReceipt receipt)
    {
        return receipt.Instance == instance && receipt.Epoch == epoch && receipt.Sequence == sequence;
    }

    // 写入本组件的自有通信数据；不能写入角色存档。
    void Write(string command)
    {
        channel.Write(command);
    }

    // 终止本工具拥有的请求并恢复禁用命令，清理未完成状态。
    public void Stop()
    {
        try
        {
            channel.Stop();
        }
        catch (Exception e)
        {
            ToolboxLog.Error("Loot.DirectStop", e);
        }
    }

    // 读取同类型装备 UID 集合，提供装备入包前后比较证据。
    static List<Dictionary<string, string>> InventoryProof(HealthReader reader)
    {
        var result = new List<Dictionary<string, string>>();
        foreach (var row in reader.ReadInventoryAudit())
        {
            var data = row["ItemData"] as Dictionary<string, object>;
            if (data == null || !(data["TypeTag"] is string) || !(data["SessionUID"] is string))
                throw new Exception("Inventory proof fields unavailable");
            result.Add(new Dictionary<string, string> { { "type", (string)data["TypeTag"] }, { "uid", (string)data["SessionUID"] } });
        }

        return result;
    }

    // 先预览再单次请求，现场核对回执、实体、背包/效果及鼠标/角色位置。
    public static void CheckLive(string path, bool execute, string requestedKind = null)
    {
        using (var reader = new HealthReader())
        {
            if (requestedKind != null && !new[]
            {
                "item",
                "book",
                "chest",
                "food",
                "tnt",
                "pot"
            }.Contains(requestedKind))
                throw new Exception("Unknown diagnostic target kind");
            if (execute)
                WaitForIdle(reader.Pid);
            var state = reader.ReadNearbyLoot();
            var rule = new NearbyLootRule();
            if (requestedKind != null)
                state.Targets = state.Targets.Where(t => t.Kind == requestedKind).ToList();
            var target = rule.Select(state, true, true, true, 0, true, true);
            var bridge = new NearbyLootBridge(reader.Pid);
            var initial = bridge.Read();
            NearbyLootReceipt result = initial;
            bool acknowledged = false;
            List<Dictionary<string, string>> inventoryBefore = null, inventoryAfter = null;
            string inventoryError = null;
            double? throwableBefore = null, throwableAfter = null;
            string throwableError = null;
            bool dispatched = false;
            PointProof cursor = new PointProof();
            cursor.CaptureBefore();
            try
            {
                if (target == null)
                    throw new Exception("No eligible nearby target (" + (requestedKind ?? "any") + "); component loaded with status=" + initial.Status);
                if (!state.Standing.Supported || !state.Combat.CanAttempt)
                    throw new Exception("Player not ready for stationary direct collection");
                if (execute && (!ToolboxInput.Front(reader.Pid) || Enumerable.Range(1, 254).Any(ToolboxInput.Held)))
                    throw new Exception("Game must remain foreground with controls released for one-shot test");
                if (execute && (target.Kind == "item" || target.Kind == "book"))
                    inventoryBefore = InventoryProof(reader);
                if (execute && target.Kind == "tnt")
                    throwableBefore = reader.ReadThrowableCount();
                bridge.Dispatch(target, state.PawnName, !execute);
                dispatched = true;
                var watch = Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < 6000)
                {
                    Thread.Sleep(100);
                    if (execute && (!ToolboxInput.Front(reader.Pid) || Enumerable.Range(1, 254).Any(ToolboxInput.Held)))
                        throw new Exception("Foreground or manual input changed during direct collection test");
                    result = bridge.Read();
                    if (!bridge.Matches(result))
                        continue;
                    acknowledged = true;
                    if (result.Status == "Dispatched")
                        continue;
                    if (!execute && result.Status == "PreviewReady" || execute && Confirmed(target, result.Status))
                        break;
                    throw new Exception("Native direct collection refused/unconfirmed: " + result.Status);
                }

                if (!acknowledged || execute && !Confirmed(target, result.Status) || !execute && result.Status != "PreviewReady")
                    throw new Exception("Native direct collection acknowledgement timed out");
            }
            finally
            {
                cursor.CaptureAfter();
                bridge.Stop();
                NearbyLootState after = null;
                string afterError = null;
                try
                {
                    after = reader.ReadNearbyLoot();
                }
                catch (Exception e)
                {
                    afterError = e.Message;
                }

                if (inventoryBefore != null)
                    try
                    {
                        inventoryAfter = InventoryProof(reader);
                    }
                    catch (Exception e)
                    {
                        inventoryError = e.Message;
                    }

                if (throwableBefore != null)
                    try
                    {
                        throwableAfter = reader.ReadThrowableCount();
                    }
                    catch (Exception e)
                    {
                        throwableError = e.Message;
                    }

                var added = inventoryBefore == null || inventoryAfter == null ? null : inventoryAfter.Where(i => i["type"] == target.Type && !inventoryBefore.Any(b => b["uid"] == i["uid"])).ToArray();
                var targetAfter = after == null || target == null ? null : after.Targets.FirstOrDefault(t => t.Id == target.Id);
                bool sameSession = after != null && after.Known && state.Known && after.Combat != null && state.Combat != null && after.Combat.Session == state.Combat.Session && after.PawnName == state.PawnName;
                bool actionVerified = sameSession && Confirmed(target, result.Status) && (target.Kind == "item" ? targetAfter == null && added != null && added.Length > 0 : target.Kind == "chest" ? targetAfter != null && targetAfter.Opened == true : target.Kind == "tnt" ? targetAfter == null && throwableAfter > throwableBefore : target.Kind == "pot" ? targetAfter == null || targetAfter.Tags != null && targetAfter.Tags.Contains("SW.State.Life.Dead") : targetAfter == null);
                bool testPassed = execute ? dispatched && actionVerified && cursor.Known && cursor.Before == cursor.After && (after.Combat.Player - state.Combat.Player).Length <= .001 : acknowledged && result.Status == "PreviewReady" && sameSession;
                File.WriteAllText(path, new JavaScriptSerializer().Serialize(new { protocol = 6, componentLive = true, instance = initial.Instance, execute = execute, acknowledged = acknowledged, status = result.Status, target = target, session = state.Combat == null ? null : state.Combat.Session, mouseInputSent = false, toolCursorMovement = false, cursorBefore = cursor.Before, cursorAfter = cursor.After, cursorUnchanged = cursor.Known && cursor.Before == cursor.After, nativeInteractionRoute = "original target interaction event; granted native activation task / native PlayerMelee event", playerBefore = state.Combat == null ? (ThreatVector? )null : state.Combat.Player, playerAfter = after == null || after.Combat == null ? (ThreatVector? )null : after.Combat.Player, playerUnchanged = sameSession && (after.Combat.Player - state.Combat.Player).Length <= .001, targetAfter = targetAfter, afterReadError = afterError, inventoryBefore = inventoryBefore, inventoryAfter = inventoryAfter, newSameTypeUids = added, inventoryReadError = inventoryError, equipmentVerified = execute && sameSession && target != null && target.Kind == "item" && result.Status == "PickedUp" && targetAfter == null && added != null && added.Length > 0, chestVerified = execute && sameSession && target != null && target.Kind == "chest" && result.Status == "Opened" && targetAfter != null && targetAfter.Opened == true, foodVerified = execute && sameSession && target != null && target.Kind == "food" && result.Status == "Consumed" && targetAfter == null, tntVerified = execute && sameSession && target != null && target.Kind == "tnt" && result.Status == "Carried" && targetAfter == null && throwableAfter > throwableBefore, potVerified = execute && sameSession && target != null && target.Kind == "pot" && result.Status == "BrokenWithDrops" && (targetAfter == null || targetAfter.Tags != null && targetAfter.Tags.Contains("SW.State.Life.Dead")), throwableCountBefore = throwableBefore, throwableCountAfter = throwableAfter, throwableReadError = throwableError, nativeFoodOutcomeChecked = result.Status == "Consumed", nativeEmeraldDropObserved = result.Status == "BrokenWithDrops", testPassed = testPassed, normalNativeActorInteraction = execute && dispatched, characterSaveEdited = false, githubUpload = false }), new UTF8Encoding(true));
                if (execute && Confirmed(target, result.Status) && !testPassed)
                    throw new Exception("Native action reported success but independent action/cursor/player proof failed; inspect report");
            }
        }
    }

    // 等到游戏前台且人工输入稳定松开；条件不满足不发真实请求。
    static void WaitForIdle(int pid)
    {
        var watch = Stopwatch.StartNew();
        long stable = 0;
        System.Drawing.Point previous = new System.Drawing.Point();
        bool known = false;
        while (watch.ElapsedMilliseconds < 30000)
        {
            if (ToolboxInput.Held(27))
                throw new OperationCanceledException("Esc stopped one-shot test before dispatch");
            System.Drawing.Point p;
            bool idle = ToolboxInput.Front(pid) && !Enumerable.Range(1, 254).Any(ToolboxInput.Held) && ToolboxInput.CombatCursor(out p);
            if (!idle)
            {
                stable = watch.ElapsedMilliseconds;
                known = false;
            }
            else
            {
                ToolboxInput.CombatCursor(out p);
                if (!known || p != previous)
                {
                    previous = p;
                    stable = watch.ElapsedMilliseconds;
                    known = true;
                }
                else if (watch.ElapsedMilliseconds - stable >= 1200)
                    return;
            }

            Thread.Sleep(100);
        }

        throw new Exception("Game must be foreground with controls and cursor stationary; no request sent");
    }

    // PointProof 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
    sealed class PointProof
    {
        public System.Drawing.Point Before, After;
        public bool Known;
        // 保存测试前鼠标与角色坐标，用于证明未自动移动。
        public void CaptureBefore()
        {
            Known = ToolboxInput.CombatCursor(out Before);
        }

        // 读取测试后鼠标与角色坐标，与前值独立比较。
        public void CaptureAfter()
        {
            Known = ToolboxInput.CombatCursor(out After) && Known;
        }
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        var receipt = NearbyLootReceipt.Parse("6|instance@1.2||0|Disabled|10.5");
        var target = new NearbyLootTarget
        {
            Id = "1:2",
            ActorName = "BP_Item_C_42",
            Type = "SW.Item.Sword",
            Kind = "item",
            Position = new ThreatVector(1.25, 2.5, 3.75)
        };
        string cmd = Command(receipt, new string ('a', 32), 1, target, "Player_C_1", false);
        string[] fields = cmd.Split('|');
        if (fields.Length != 16 || fields[5] != "BP_Item_C_42" || fields[11] != "11.1" || fields[12] != "0" || fields[13] != "0" || fields[14] != "500" || fields[15] != "1000")
            throw new Exception("Direct pickup identity / lease serialization");
        string moving = Command(receipt, new string ('a', 32), 1, target, "Player_C_1", false, true, 250, 1000);
        if (!moving.EndsWith("|0|1|250|1000"))
            throw new Exception("Moving flag / independent interval serialization failed");
        foreach (int invalid in new[]
        {
            99,
            30001
        }

        )
        {
            bool invalidInterval = false;
            try
            {
                Command(receipt, new string ('a', 32), 1, target, "Player_C_1", false, true, invalid, 1000);
            }
            catch
            {
                invalidInterval = true;
            }

            if (!invalidInterval)
                throw new Exception("Out-of-bounds native interval accepted");
        }

        var save = NativeMailboxCodec.Replace(EquipmentBridgeChecks.Fixture(Mod, NativeMailboxCodec.Encode("OFF")), NativeMailboxCodec.Encode(cmd), Mod);
        if (NativeMailboxCodec.Decode(EquipmentSaveCodec.Parse(save, "Request", "Command", Mod).Value) != cmd)
            throw new Exception("Direct component save round trip");
        bool foreign = false;
        try
        {
            EquipmentSaveCodec.Parse(save, "Request", "Command");
        }
        catch
        {
            foreign = true;
        }

        if (!foreign)
            throw new Exception("Direct command accepted by equipment component codec");
        bool rejected = false;
        target.ActorName = "BP|Other";
        try
        {
            Command(receipt, new string ('a', 32), 1, target, "Player_C_1", false);
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Direct collection field injection accepted");
        rejected = false;
        try
        {
            NearbyLootReceipt.Parse("6|instance||0|Ready|NaN");
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Invalid native clock accepted");
        rejected = false;
        try
        {
            NearbyLootReceipt.Parse("1|instance||0|Ready|10.5");
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Obsolete click-dispatch component protocol accepted");
        rejected = false;
        try
        {
            NearbyLootReceipt.Parse("2|instance||0|Ready|10.5");
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Obsolete direct-RPC component protocol accepted");
        rejected = false;
        try
        {
            NearbyLootReceipt.Parse("3|instance||0|Ready|10.5");
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Obsolete stationary component protocol accepted");
        rejected = false;
        try
        {
            NearbyLootReceipt.Parse("4|instance||0|Ready|10.5");
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Pre-book collection protocol accepted");
        rejected = false;
        try
        {
            NearbyLootReceipt.Parse("5|instance||0|Ready|10.5");
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Variable-width collection protocol accepted");
        target.ActorName = "BP_Book_C_1";
        target.Kind = "book";
        target.Type = "SW.Item.EnchantmentBook.PotionSharing";
        if (Command(receipt, new string ('a', 32), 2, target, "Player_C_1", false).Split('|')[4] != "book" || !Confirmed(target, "BookPickedUp") || Confirmed(target, "PickedUp") || Confirmed(target, "Dispatched"))
            throw new Exception("Book pickup result isolation failed");
        target.Type = "SW.Item.EnchantmentBook.Base";
        rejected = false;
        try
        {
            Command(receipt, new string ('a', 32), 2, target, "Player_C_1", false);
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Abstract book command accepted");
        target.Type = "SW.Item.EnchantmentBook.Unknown";
        rejected = false;
        try
        {
            Command(receipt, new string ('a', 32), 2, target, "Player_C_1", false);
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Unknown book command accepted");
        target.Type = "SW.Item.Sword";
        target.ActorName = "BP_Item_C_42";
        target.Kind = "food";
        rejected = false;
        try
        {
            Command(receipt, new string ('a', 32), 1, target, "Player_C_1", false);
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Direct collection accepted unsupported food");
        foreach (var pair in new[]
        {
            new[]
            {
                "food",
                "SW.Item.Consumable.Food.Apple",
                "Consumed"
            },
            new[]
            {
                "tnt",
                "SW.Item.Consumable.Throwable.TNT",
                "Carried"
            },
            new[]
            {
                "pot",
                "SW.LootActor.Pot.Small.Emerald",
                "BrokenWithDrops"
            },
            new[]
            {
                "pot",
                "SW.LootActor.Pot.Large.Emerald",
                "BrokenWithDrops"
            }
        }

        )
        {
            target.Kind = pair[0];
            target.Type = pair[1];
            string command = Command(receipt, new string ('a', 32), 2, target, "Player_C_1", true);
            if (command.Split('|')[4] != pair[0] || !Confirmed(target, pair[2]) || Confirmed(target, "Dispatched") || Confirmed(target, "Opened"))
                throw new Exception("Per-kind native route/result distinction failed");
        }

        target.Type = "SW.LootActor.Pot.Large.SpringStone";
        rejected = false;
        try
        {
            Command(receipt, new string ('a', 32), 3, target, "Player_C_1", false);
        }
        catch
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Non-emerald pot accepted");
        target.Kind = "food";
        target.Type = "SW.Item.Consumable.Food.Carrot";
        if (Confirmed(target, "ConsumedUnconfirmed") || Confirmed(target, "Dispatched"))
            throw new Exception("Food disappearance claimed as a confirmed effect");
        target.Kind = "pot";
        target.Type = "SW.LootActor.Pot.Small.Emerald";
        if (Confirmed(target, "BrokenUnconfirmed"))
            throw new Exception("Pot destruction claimed as confirmed drops");
        return NearbyLootTiming.Test() + "PASS: protocol 6, enchantment-book result and old protocol rejection, moving flag / independent interval serialization, equipment/chest/food/TNT/pot result distinctions including unconfirmed consumption, exact identity, native clock expiry and save codec guards. No cursor, game input or file writes.\r\n";
    }
}

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm
{
    NearbyLootBridge nearbyDirectBridge;
    // 关闭当前直接收集请求并释放工具端通道。
    void StopDirectLoot()
    {
        nearbyObservation.Cancel();
        var bridge = nearbyDirectBridge;
        nearbyDirectBridge = null;
        if (bridge != null)
            bridge.Stop();
    }

    // 执行已筛选目标的原生交互，并异步观察对应结果与取消信号。
    async Task RunDirectLoot(InputRequest request, NearbyLootState state, NearbyLootTarget target, CancellationToken cancel)
    {
        if (!Active(request) || cancel.IsCancellationRequested || request.Generation != generation || clock.ElapsedMilliseconds > request.Expires)
            return;
        bool sent = false, failed = false, allowMoving = settings.NearbyAllowMoving;
        try
        {
            if (nearbyDirectBridge == null)
                nearbyDirectBridge = new NearbyLootBridge(reader.Pid);
            if (!Active(request) || cancel.IsCancellationRequested || request.Generation != generation || clock.ElapsedMilliseconds > request.Expires || !ToolboxInput.Front(reader.Pid) || !NearbyInputReady(clock.ElapsedMilliseconds))
                return;
            System.Drawing.Point cursorBefore;
            if (!ToolboxInput.CombatCursor(out cursorBefore))
                return;
            var bridge = nearbyDirectBridge;
            bridge.Dispatch(target, state.PawnName, false, allowMoving, settings.NearbyIntervalMs, settings.NearbyFoodIntervalMs);
            sent = true;
            nearbyLootTiming.Attempt(target.Kind, clock.ElapsedMilliseconds);
            long deadline = clock.ElapsedMilliseconds + 6500;
            nearbyLootNote.Text = L10n.T("正在直接收集附近目标，不移动鼠标");
            ToolboxLog.Write("Loot.DirectRequest", "session=" + state.Combat.Session + " target=" + target.Id + " actor=" + target.ActorName + " type=" + target.Type + " allowManualMovement=" + allowMoving + " intervalMs=" + settings.NearbyIntervalMs + " foodIntervalMs=" + settings.NearbyFoodIntervalMs + "; no tool cursor movement; original interaction event and " + (target.Kind == "pot" ? "native PlayerMelee event" : "authoritative granted GA_Interact request"));
            while (clock.ElapsedMilliseconds < deadline)
            {
                await Task.Delay(100, cancel);
                if (!Active(request) || cancel.IsCancellationRequested || request.Generation != generation)
                    return;
                var receipt = bridge.Read();
                if (!bridge.Matches(receipt))
                    continue;
                if (receipt.Status == "Dispatched")
                {
                    nearbyLootRule.Attempt(target);
                    continue;
                }

                if (NearbyLootBridge.Confirmed(target, receipt.Status))
                {
                    nearbyLootRule.Attempt(target);
                    System.Drawing.Point cursorAfter;
                    var after = reader.ReadNearbyLoot();
                    if (!ToolboxInput.CombatCursor(out cursorAfter) || !after.Known || after.Combat.Session != state.Combat.Session || (!allowMoving && (cursorAfter != cursorBefore || (after.Combat.Player - state.Combat.Player).Length > .001)))
                    {
                        Arm(false);
                        nearbyLootNote.Text = L10n.T("鼠标或角色位置发生变化，已停止附近交互");
                        ToolboxLog.Write("Loot.DirectProofFailed", "target=" + target.Id + " native=" + receipt.Status);
                        return;
                    }

                    nearbyLootNote.Text = L10n.T(receipt.Status == "BookPickedUp" ? "游戏已确认附魔书拾取" : receipt.Status == "PickedUp" ? "游戏已确认装备拾取" : receipt.Status == "Opened" ? "游戏已确认宝箱开启" : receipt.Status == "Consumed" ? "游戏已确认食物消耗与原生效果" : receipt.Status == "Carried" ? "游戏已确认 TNT 拾取与携带" : "游戏已确认绿宝石罐破坏与掉落");
                    ToolboxLog.Write("Loot.DirectResult", "target=" + target.Id + " state=" + receipt.Status + "; allowManualMovement=" + allowMoving + " cursorUnchanged=" + (cursorAfter == cursorBefore) + " playerUnchanged=" + ((after.Combat.Player - state.Combat.Player).Length <= .001));
                    return;
                }

                if (receipt.Status == "ConsumedUnconfirmed" || receipt.Status == "BrokenUnconfirmed")
                {
                    nearbyLootRule.Attempt(target);
                    nearbyLootNote.Text = L10n.T(receipt.Status == "ConsumedUnconfirmed" ? "食物实体已消耗，原生效果未确认" : "罐子已破坏，掉落结果未确认");
                    ToolboxLog.Write("Loot.DirectObserved", "target=" + target.Id + " state=" + receipt.Status + "; observed entity end only, not verified success");
                    return;
                }

                nearbyLootRule.Defer(target, clock.ElapsedMilliseconds, receipt.Status == "Throttled" ? 100 : 2000);
                if (receipt.Status == "Unconfirmed" || receipt.Status == "AlreadyAttempted" || receipt.Status == "TargetOutOfRange")
                    nearbyLootRule.Attempt(target);
                nearbyLootNote.Text = L10n.T("直接收集未完成，详见日志");
                ToolboxLog.Limited("Loot.DirectBlocked", "target=" + target.Id + " state=" + receipt.Status);
                return;
            }

            nearbyLootRule.Attempt(target);
            ToolboxLog.Limited("Loot.DirectBlocked", "target=" + target.Id + " native acknowledgement timeout");
        }
        catch (OperationCanceledException)
        {
            failed = true;
            if (sent)
                nearbyLootRule.Attempt(target);
            throw;
        }
        catch (Exception e)
        {
            failed = true;
            if (sent)
                nearbyLootRule.Attempt(target);
            else
                nearbyLootRule.Defer(target, clock.ElapsedMilliseconds);
            nearbyLootNote.Text = e.Message;
            ToolboxLog.Limited("Loot.DirectUnavailable", e.Message);
        }
        // 同一场景复用已核实通道，但每次动作仍恢复 OFF；F9、取消、场景/传输错误会丢弃通道并重新握手。
        finally
        {
            if (failed || cancel.IsCancellationRequested)
                StopDirectLoot();
            else if (nearbyDirectBridge != null)
                nearbyDirectBridge.Stop();
        }
    }
}
