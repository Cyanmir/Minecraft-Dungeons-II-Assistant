// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Drawing;
using System.Windows.Forms;

// 首页仅映射现有窗口状态，不额外读取游戏、握手或执行交互。
sealed partial class ToolboxForm
{
    PixelLabel homeConnection, homeRunning, headerConnection;
    PixelLabel[] homeComponents = new PixelLabel[3];
    // 每秒映射已有提示；不在首页增加游戏读取或组件握手。
    readonly Timer dashboardTimer = new Timer
    {
        Interval = 1000
    };
    // 信息卡用于连接、运行和组件状态；普通设置仍通过 Card 工厂创建 Section。
    static OreCard StatusCard(Control parent, int y, int height)
    {
        var card = new OreCard
        {
            Location = new Point(0, y),
            Size = new Size(OreMetrics.ContentWidth, height)
        };
        parent.Controls.Add(card);
        return card;
    }

    // 六项主导航之外的操作辅助保留为设置入口，不修改内部页面索引。
    void BuildDashboard()
    {
        headerConnection = (PixelLabel)LabelAt(languageButton.Parent, "", 549, 17, 100, 30);
        headerConnection.VerticalCenter = true;
        headerConnection.Wrap = false;
        headerConnection.PixelScale = .8f;
        var connection = StatusCard(pages[6], 0, 112);
        LabelAt(connection, L10n.T("游戏连接"), 24, 16, 690, 30);
        homeConnection = (PixelLabel)LabelAt(connection, "", 24, 58, 690, 36);
        homeConnection.ForeColor = OreTheme.Accent;
        // 首页只展示状态；连接与停止统一使用底部操作区，避免重复按钮。
        var running = StatusCard(pages[6], 128, 112);
        LabelAt(running, L10n.T("当前运行状态"), 24, 16, 450, 30);
        homeRunning = (PixelLabel)LabelAt(running, "", 24, 56, 690, 28);
        var components = StatusCard(pages[6], 256, 312);
        LabelAt(components, L10n.T("组件状态"), 24, 16, 690, 30);
        string[] names =
        {
            "附近交互组件",
            "战斗组件",
            "装备组件"
        };
        for (int i = 0; i < names.Length; i++)
        {
            LabelAt(components, L10n.T(names[i]), 24, 58 + i * 82, 690, 26);
            homeComponents[i] = (PixelLabel)LabelAt(components, "", 24, 88 + i * 82, 690, 48);
            homeComponents[i].ForeColor = OreTheme.Muted;
            homeComponents[i].PixelScale = .85f;
        }

        // 设置页使用普通分组，提供原有快捷辅助配置入口。
        var shortcuts = Card(pages[3], 524, 112);
        LabelAt(shortcuts, L10n.T("操作辅助"), 20, 16, 690, 28);
        ButtonAt(shortcuts, L10n.T("配置组合、跳劈和右键闪避"), 20, 56, 704).Click += delegate
        {
            AnimatePage(1);
        };
        var back = Card(pages[1], 914, 72);
        ButtonAt(back, L10n.T("返回设置"), 20, 12, 704).Click += delegate
        {
            AnimatePage(3);
        };
        dashboardTimer.Tick += delegate
        {
            RefreshDashboard();
        };
        if (interactive)
            dashboardTimer.Start();
        FormClosed += delegate
        {
            dashboardTimer.Stop();
            dashboardTimer.Dispose();
        };
    }

    // 组件对象存在也不等于动作验证成功；只展示已有运行提示，未检测时明确留空状态。
    void RefreshDashboard()
    {
        if (homeConnection == null || connectionBadge == null)
            return;
        homeConnection.Text = connectionBadge.Text;
        headerConnection.Text = reader == null ? L10n.T("未连接") : L10n.T("已连接");
        headerConnection.ForeColor = reader == null ? OreTheme.Muted : OreTheme.Accent;
        homeRunning.Text = armed || equipmentArmed ? L10n.T("运行中") : L10n.T("已暂停");
        homeRunning.ForeColor = armed || equipmentArmed ? OreTheme.Accent : OreTheme.Muted;
        homeComponents[0].Text = nearbyDirectBridge == null ? L10n.T("尚未检测；连接并开启对应功能后读取") : nearbyLootNote.Text;
        homeComponents[1].Text = combatNativeBridge == null ? L10n.T("尚未检测；连接并开启对应功能后读取") : nativeCombatNote.Text;
        homeComponents[2].Text = equipmentBridge == null ? L10n.T("尚未检测；连接并开启对应功能后读取") : equipmentState.Text;
    }
}
