// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 展示资源自动更新在后台执行。
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

sealed partial class ToolboxForm
{
    CheckBox resourceAutomatic;
    Button resourceCheck;
    PixelLabel resourceStatus;
    bool resourceBusy;
    CancellationTokenSource resourceCancel;
    string resourceMessage = "展示资源尚未更新";
    object[] resourceArguments = new object[0];
    // 与本体更新分组独立，沿用同一设置页；资源不需要关闭游戏或重启工具。
    void BuildResourceSettings()
    {
        var card = Card(pages[3], 1028, 240);
        LabelAt(card, L10n.T("图标与游戏译名"), 20, 16, 704, 30);
        LabelAt(card, L10n.T("启动时自动更新展示资源"), 20, 62, 590, 28);
        resourceAutomatic = CheckAt(card, "", 660, 60, 64, settings.ResourceAutoUpdate);
        var note = (PixelLabel)LabelAt(card, L10n.T("下载原始图标、品质框与游戏译名；断网保留缓存。"), 20, 104, 704, 48);
        note.PixelScale = .8f;
        note.ForeColor = OreTheme.Muted;
        resourceStatus = (PixelLabel)LabelAt(card, "", 20, 158, 490, 60);
        resourceStatus.PixelScale = .8f;
        resourceCheck = ButtonAt(card, L10n.T("更新展示资源"), 523, 178, 201);
        resourceCheck.Click += delegate { CheckResourceUpdates(false); };
        resourceAutomatic.CheckedChanged += delegate
        {
            settings.ResourceAutoUpdate = resourceAutomatic.Checked;
            if (interactive)
                try { settings.Save(); }
                catch (Exception error) { ToolboxLog.Error("Resources.Settings", error); }
        };
        FormClosed += delegate { if (resourceCancel != null) resourceCancel.Cancel(); };
        if (!String.IsNullOrEmpty(EquipmentGamePresentation.Revision)) SetResourceStatus("展示资源版本：{0}", EquipmentGamePresentation.Revision);
        else RefreshResourceLanguage();
    }
    // 规范键和参数分别存储，语言变化不会丢失进度或重新发起下载。
    void SetResourceStatus(string message, params object[] arguments)
    {
        resourceMessage = message;
        resourceArguments = arguments;
        RefreshResourceLanguage();
    }
    void RefreshResourceLanguage()
    {
        if (resourceStatus != null) resourceStatus.Text = String.Format(L10n.T(resourceMessage), resourceArguments);
        if (resourceCheck != null) resourceCheck.Text = L10n.T("更新展示资源");
    }
    // 正常启动或用户按钮触发；预览窗口和诊断命令不联网。
    async void CheckResourceUpdates(bool startup)
    {
        if (!interactive || resourceBusy || closing || IsDisposed || startup && !settings.ResourceAutoUpdate) return;
        resourceBusy = true;
        resourceCheck.Enabled = false;
        resourceCancel = new CancellationTokenSource();
        var cancel = resourceCancel;
        SetResourceStatus("正在更新展示资源…");
        try
        {
            var catalog = await Task.Run(() => GameResources.Update(cancel.Token), cancel.Token);
            if (closing || IsDisposed) return;
            // 资源切换会释放旧缓存图像，先关闭持有旧效果图标的菜单；普通定时读数不走此路径。
            if (rerollEffectChoice != null) rerollEffectChoice.ClosePopup();
            EquipmentGamePresentation.Apply(catalog);
            RefreshEquipmentLanguage();
            SearchRerollEquipment();
            RefreshRerollDisplay();
            Invalidate(true);
            SetResourceStatus("展示资源版本：{0}", catalog.Revision);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!closing && !IsDisposed) SetResourceStatus("展示资源更新失败，已保留原资源");
            ToolboxLog.Error("Resources.Update", error);
        }
        finally
        {
            resourceBusy = false;
            if (!closing && !IsDisposed) resourceCheck.Enabled = true;
            cancel.Dispose();
            if (resourceCancel == cancel) resourceCancel = null;
        }
    }
}
