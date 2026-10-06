// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// 状态卡片只负责背景、边框和边缘高光；Section 模式仅绘制分组细线。
class OreCard : Panel
{
    public bool Section;
    // 由窗口布局写入，分隔线和卡片边缘不随卡片内容高度变粗。
    public float RenderScale = 1;
    public OreCard()
    {
        BackColor = OreTheme.Card;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    // 根据控件当前状态绘制外观；不要在绘制阶段修改游戏或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int px = Math.Max(1, (int)Math.Round(2 * RenderScale));
        if (Section)
        {
            OreRenderer.Fill(e.Graphics, new Rectangle(px * 8, Height - px, Math.Max(0, Width - px * 16), px), OreTheme.Edge);
            return;
        }

        OreRenderer.Border(e.Graphics, ClientRectangle, OreTheme.Line, px);
        OreRenderer.Rim(e.Graphics, Rectangle.Inflate(ClientRectangle, -px, -px), BackColor, OreRenderer.Highlight(BackColor), OreTheme.Field, px);
    }
}

sealed class OreHealthBar : Control
{
    float percent;
    // 血条显示百分比，未知或越界值按控件规则处理。
    public float Percent
    {
        get
        {
            return percent;
        }

        set
        {
            percent = Math.Max(0, Math.Min(100, value));
            Invalidate();
        }
    }

    public OreHealthBar()
    {
        Height = 10;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    // 根据控件当前状态绘制外观；不要在绘制阶段修改游戏或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        OreTheme.Fill(e.Graphics, ClientRectangle, OreTheme.Line);
        OreTheme.Fill(e.Graphics, new Rectangle(2, 2, Width - 4, Height - 4), OreTheme.Field);
        OreTheme.Fill(e.Graphics, new Rectangle(2, 2, (int)((Width - 4) * Percent / 100), Height - 4), Percent < 40 ? OreTheme.Danger : OreTheme.Green);
    }
}

// 普通设置分组只有底部分隔线；继承容器接口以保持原有布局和事件绑定。
sealed class OreSection : OreCard
{
    // 设置分组使用页面背景和底部细线，不绘制状态卡片边框。
    public OreSection()
    {
        Section = true;
        BackColor = OreTheme.Background;
    }
}

// 导航使用深色背景；矮窗口可滚动访问快捷键和日志，不截断底部操作。
sealed class OreSidebar : OreScrollPanel
{
    // 侧栏沿用统一滚动容器，矮窗口仍可访问全部快捷操作。
    public OreSidebar()
    {
        BackColor = OreTheme.Card;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    // 先绘制滚动容器，再补右边界；绘制不改变滚动偏移或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        OreRenderer.Fill(e.Graphics, new Rectangle(Width - 2, 0, 2, Height), OreTheme.Field);
    }
}

// 独立分隔线可用于页面标题、设置分组或弹窗。
sealed class OreDivider : Control
{
    // 分隔线不接收焦点，默认高度由逻辑尺寸表定义。
    public OreDivider()
    {
        Height = OreMetrics.DividerHeight;
        TabStop = false;
    }

    // 只填充细分隔条，不读取配置或游戏状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        OreRenderer.Fill(e.Graphics, ClientRectangle, OreTheme.Edge);
    }
}
