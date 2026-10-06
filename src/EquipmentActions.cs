// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 装备整理开关、热键、基线与执行调度。
using System;
using System.Linq;
using System.Windows.Forms;

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm
{
    CheckBox equipmentEnabled;
    KeyButton equipmentToggleKey;
    PixelLabel equipmentState;
    bool equipmentArmed, equipmentSwitching, equipmentHotkeyRegistered;
    long lastEquipmentRead = -10000, equipmentStarted;
    EquipmentBridge equipmentBridge;
    bool sellExisting;
    int lastEquipmentSold = -1;
    int acceptedEquipmentKey = 0x76;
    // 注册装备整理快捷键并报告冲突，默认接受键为 F7。
    void RegisterEquipmentHotkey()
    {
        if (equipmentPolicy == null || !interactive)
            return;
        ToolboxInput.UnregisterHotKey(Handle, 10);
        equipmentHotkeyRegistered = ToolboxInput.RegisterHotKey(Handle, 10, 0x4000, (uint)equipmentPolicy.ToggleKey);
        acceptedEquipmentKey = equipmentPolicy.ToggleKey;
        if (!equipmentHotkeyRegistered && equipmentState != null)
            equipmentState.Text = L10n.T("整理快捷键已被占用，可重新绑定或使用开关");
        ToolboxLog.Write("Equipment.Hotkey", "key=" + ToolboxInput.Name(equipmentPolicy.ToggleKey) + " registered=" + equipmentHotkeyRegistered);
    }

    // 核对整理键与游戏、药水、槽位及工具触发键的冲突。
    bool EquipmentKeyConflict(int key)
    {
        if (!ToolboxInput.Allowed(key) || key == 9 || key == 27 || key == 0x7A)
            return true;
        if (key == settings.ComboTrigger || key == settings.JumpTrigger || key == settings.AttackTrigger || key == settings.JumpKey || key == settings.DodgeKey || key == settings.PotionKey || settings.Slots.Contains(key))
            return true;
        if (reader != null && reader.AllKeyboardBindings().Values.Any(v => GameActionBindings.KeyboardCode(v) == key))
            return true;
        return false;
    }

    // 先停止整理，再验证并持久化新键，失败恢复已接受键。
    void EquipmentHotkeyChanged()
    {
        StopEquipment();
        int key = equipmentToggleKey.KeyCode;
        try
        {
            if (EquipmentKeyConflict(key))
            {
                equipmentToggleKey.KeyCode = acceptedEquipmentKey;
                equipmentToggleKey.RefreshText();
                equipmentState.Text = L10n.T("整理快捷键与游戏或工具键位冲突，请重新绑定");
                return;
            }

            equipmentPolicy.ToggleKey = key;
            RegisterEquipmentHotkey();
            acceptedEquipmentKey = key;
            if (interactive)
                equipmentPolicy.Save();
            if (!interactive || equipmentHotkeyRegistered)
                equipmentState.Text = L10n.T("整理已关闭；启用时记录已有物品");
        }
        catch (Exception e)
        {
            equipmentState.Text = L10n.T("整理键位设置失败") + "\n" + e.Message;
            ToolboxLog.Error("Equipment.Hotkey", e);
        }
    }

    // 切换整理状态，避免控件回调造成重复启停。
    void ToggleEquipment()
    {
        if (equipmentEnabled != null)
        {
            sellExisting = false;
            equipmentEnabled.Checked = !equipmentEnabled.Checked;
        }
    }

    // 停止回收组件、清除启用状态并刷新界面。
    void StopEquipment()
    {
        if (equipmentArmed)
        {
            equipmentVisual.Note = "整理已停止";
            RefreshEquipmentVisual();
            ReleaseEquipmentVisualReader();
        }

        equipmentArmed = false;
        equipmentBridge = null;
        sellExisting = false;
        if (interactive)
            try
            {
                EquipmentBridge.Stop();
            }
            catch (Exception e)
            {
                ToolboxLog.Error("Equipment.Stop", e);
            }

        if (equipmentEnabled == null)
            return;
        equipmentSwitching = true;
        try
        {
            equipmentEnabled.Checked = false;
        }
        finally
        {
            equipmentSwitching = false;
        }

        if (equipmentState != null)
            equipmentState.Text = L10n.T("整理已关闭；启用时记录已有物品");
    }

    // 启用时记录背包基线和当前会话，建立独立回收请求。
    void SetEquipmentEnabled(bool on)
    {
        if (equipmentSwitching)
            return;
        if (!on)
        {
            StopEquipment();
            ToolboxLog.Change("Equipment.Enabled", "false");
            return;
        }

        try
        {
            if (reader == null || connecting || closing || exportingLogs || researchBusy)
                throw new Exception(L10n.T("请先连接游戏，再启用整理"));
            if (EquipmentKeyConflict(equipmentPolicy.ToggleKey))
                throw new Exception(L10n.T("整理快捷键与游戏或工具键位冲突，请重新绑定"));
            string session = reader.InventorySession();
            var items = reader.ReadInventory().Select(EquipmentItem.Decode).ToList();
            if (reader.InventorySession() != session)
                throw new Exception("Inventory session changed");
            inventoryBaseline.Capture(session, items);
            equipmentPlan = equipmentPolicy.Plan(items, inventoryBaseline.Existing, sellExisting);
            equipmentVisualGeneration++;
            equipmentVisualIncludeExisting = sellExisting;
            equipmentVisualFinalRead = false;
            equipmentVisual.Reset(equipmentPlan, true);
            equipmentVisualList.ResetScroll();
            lastEquipmentVisualRead = -10000;
            RefreshEquipmentVisual();
            equipmentBridge = new EquipmentBridge(reader.Pid, equipmentPolicy, sellExisting);
            equipmentBridge.Pulse(false);
            lastEquipmentRead = -10000;
            lastEquipmentSold = -1;
            equipmentStarted = Environment.TickCount & Int32.MaxValue;
            equipmentArmed = true;
            equipmentState.Text = L10n.T(sellExisting ? "正在等待游戏端确认回收已有装备" : "正在等待游戏端确认，已有装备保留");
            ToolboxLog.Change("Equipment.Enabled", "true baseline=" + items.Count + "; native bridge requested");
        }
        catch (Exception e)
        {
            StopEquipment();
            equipmentState.Text = e.Message;
            ToolboxLog.Error("Equipment.Enable", e);
        }
    }

    // 按刷新间隔读取回执，验证会话/序号后更新计数；换场景立即停止。
    void PollEquipment(long now)
    {
        if (!equipmentArmed || reader == null || connecting || closing || exportingLogs || researchBusy || now - lastEquipmentRead < 250)
            return;
        lastEquipmentRead = now;
        try
        {
            string session = reader.InventorySession();
            if (!inventoryBaseline.Matches(session))
            {
                StopEquipment();
                equipmentState.Text = L10n.T("角色或场景已变化，请重新启用整理");
                return;
            }

            var state = equipmentBridge.Read();
            if (equipmentBridge.Acknowledged(state))
            {
                equipmentVisual.Receipt(state.Sold, state.Eligible, state.Status);
                RefreshEquipmentVisual();
                if (state.Sold != lastEquipmentSold)
                {
                    lastEquipmentSold = state.Sold;
                    ToolboxLog.Write("Equipment.Receipt", "mode=" + (sellExisting ? "existing" : "new") + " salvaged=" + state.Sold + " pending=" + state.Eligible + " state=" + state.Status);
                }

                if (state.Status == "Completed")
                {
                    StopEquipment();
                    equipmentVisual.Note = "已有装备整理完成";
                    equipmentVisualFinalRead = true;
                    PollEquipmentVisual(now);
                    RefreshEquipmentVisual();
                    equipmentState.Text = String.Format(L10n.T("已有装备整理完成 · 已回收 {0} 件"), state.Sold);
                    return;
                }

                if (state.Status != "Monitoring" && state.Status != "AwaitingReceipt" && state.Status != "NativeRejected")
                    throw new Exception(L10n.T("游戏端整理已停止，请重新启用") + ": " + state.Status);
                if ((DateTime.UtcNow - System.IO.File.GetLastWriteTimeUtc(EquipmentBridge.ReceiptPath)).TotalSeconds > 2.5)
                    throw new Exception(L10n.T("游戏端响应超时，整理已停止"));
                equipmentState.Text = String.Format(L10n.T(sellExisting ? "正在回收已有装备 · 已回收 {0} 件 · 待处理 {1} 件" : "自动出售已启用 · 已回收 {0} 件 · 待处理 {1} 件"), state.Sold, state.Eligible);
                if (state.Status == "NativeRejected")
                    equipmentState.Text = L10n.T("游戏拒绝回收此物品，已保留并跳过");
            }
            else if ((Environment.TickCount & Int32.MaxValue) - equipmentStarted > 4000)
                throw new Exception(L10n.T("游戏端响应超时，整理已停止"));
            equipmentBridge.Pulse(false);
            PollEquipmentVisual(now);
        }
        catch (Exception e)
        {
            StopEquipment();
            equipmentState.Text = L10n.T("整理已停止") + "\n" + e.Message;
            ToolboxLog.Error("Equipment.Monitor", e);
        }
    }

    // 经已有的交互流程请求回收已有装备，沿用全部保护规则。
    async void SellExistingEquipment()
    {
        if (researchBusy)
            return;
        StopEquipment();
        Arm(false);
        await PreviewEquipment(true);
        if (equipmentPlan == null || !equipmentPlan.Any(d => d.Sell))
            return;
        int count = equipmentPlan.Count(d => d.Sell);
        if (MessageBox.Show(this, String.Format(L10n.T("按当前规则回收已有装备和法器，待回收 {0} 件。已装备、锁定、附魔及受保护物品会保留。继续吗？"), count), L10n.T("回收符合规则的已有装备"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;
        sellExisting = true;
        equipmentEnabled.Checked = true;
    }

}
