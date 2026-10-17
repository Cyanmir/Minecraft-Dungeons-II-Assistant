// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// 卡片只负责圆角背景、细描边和顶部微光；Section 标志保留给布局与旧调用识别分组。
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int line = Math.Max(1, (int)Math.Round(RenderScale)), radius = Math.Max(4, (int)Math.Round(10 * RenderScale));
        // 先铺父容器底色，圆角外侧与页面融合；分组和状态卡共用同一卡片外观。
        OreRenderer.Fill(e.Graphics, ClientRectangle, Parent == null ? OreTheme.Background : Parent.BackColor);
        OreRenderer.Plate(e.Graphics, ClientRectangle, BackColor, OreTheme.Outline, radius, line);
        // 顶部 1px 微光让卡片在深色底上有轻微的抬起感。
        using (var sheen = new SolidBrush(Color.FromArgb(10, Color.White)))
            e.Graphics.FillRectangle(sheen, new Rectangle(radius, line, Math.Max(0, Width - radius * 2), line));
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

    protected override void OnPaint(PaintEventArgs e)
    {
        // 胶囊形进度：深槽 + 渐变填充，低于 40% 转为红色警示。
        Color back = Parent == null ? OreTheme.Card : Parent.BackColor;
        OreRenderer.Fill(e.Graphics, ClientRectangle, back);
        int radius = Height / 2;
        OreRenderer.Plate(e.Graphics, ClientRectangle, OreTheme.Field, OreTheme.Line, radius, 1);
        int width = (int)Math.Round((Width - 2) * Percent / 100);
        if (width <= 0)
            return;
        Color fill = Percent < 40 ? OreTheme.Danger : OreTheme.Green;
        var bar = new Rectangle(1, 1, Math.Max(Height - 2, width), Height - 2);
        var state = e.Graphics.Save();
        e.Graphics.SetClip(new Rectangle(1, 1, width, Height - 2));
        OreRenderer.FillRoundGradient(e.Graphics, bar, OreRenderer.Highlight(fill, 22), fill, radius - 1);
        e.Graphics.Restore(state);
    }
}

// 普通设置分组与状态卡同为圆角卡片；继承容器接口以保持原有布局和事件绑定。
sealed class OreSection : OreCard
{
    public OreSection()
    {
        Section = true;
        BackColor = OreTheme.Card;
    }
}

// 导航使用深色背景；矮窗口可滚动访问快捷键和日志，不截断底部操作。
sealed class OreSidebar : OreScrollPanel
{
    // 侧栏沿用统一滚动容器，矮窗口仍可访问全部快捷操作。
    public OreSidebar()
    {
        BackColor = OreTheme.Field;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    // 先绘制滚动容器，再补 1px 右边界；绘制不改变滚动偏移或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int line = Math.Max(1, (int)Math.Round(OreMetrics.Scale(this)));
        OreRenderer.Fill(e.Graphics, new Rectangle(Width - line, 0, line, Height), OreTheme.Line);
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
        OreRenderer.Fill(e.Graphics, ClientRectangle, Parent == null ? OreTheme.Background : Parent.BackColor);
        OreRenderer.Fill(e.Graphics, new Rectangle(0, (Height - 1) / 2, Width, 1), OreTheme.Line);
    }
}
