// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// 滚动条将拖动和翻页转换为 Changed 值；偏移限幅由所属滚动容器负责。
sealed class OreScrollBar : Control
{
    public int Maximum, PageSize, Value;
    public event Action<int> Changed;
    bool dragging, hover;
    int dragY, dragValue;
    public OreScrollBar()
    {
        Width = 16;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        MouseEnter += delegate
        {
            hover = true;
            Invalidate();
        };
        MouseLeave += delegate
        {
            hover = false;
            Invalidate();
        };
    }

    int ThumbHeight
    {
        get
        {
            return Math.Min(Height, Math.Max(30 * Width / 16, (int)(Height * PageSize / (float)Math.Max(1, PageSize + Maximum))));
        }
    }

    int ThumbY
    {
        get
        {
            return Maximum == 0 ? 0 : (int)((Height - ThumbHeight) * Value / (float)Maximum);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        float scale = OreMetrics.Scale(this);
        Color back = Parent == null ? OreTheme.Background : Parent.BackColor;
        OreRenderer.Fill(e.Graphics, ClientRectangle, back);
        // 细胶囊拇指，无轨道线；悬停或拖动时加宽并提亮，平时不抢内容视线。
        bool active = hover || dragging;
        int width = Math.Max(4, Math.Min(Width - 2, (int)Math.Round((active ? 8 : 6) * scale)));
        int inset = Math.Max(1, (int)Math.Round(2 * scale));
        if (active)
            OreRenderer.FillRound(e.Graphics, new Rectangle((Width - width) / 2, 0, width, Height), OreRenderer.Mix(back, OreTheme.Text, 4), width);
        var thumb = new Rectangle((Width - width) / 2, ThumbY + inset, width, Math.Max(width, ThumbHeight - inset * 2));
        Color color = dragging ? OreRenderer.Mix(OreTheme.Edge, OreTheme.Text, 45) : hover ? OreRenderer.Mix(OreTheme.Edge, OreTheme.Text, 30) : OreRenderer.Mix(OreTheme.Edge, OreTheme.Text, 14);
        OreRenderer.FillRound(e.Graphics, thumb, color, width);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Y >= ThumbY && e.Y <= ThumbY + ThumbHeight)
        {
            dragging = true;
            dragY = e.Y;
            dragValue = Value;
            Capture = true;
        }
        else if (Changed != null)
            Changed(Value + (e.Y < ThumbY ? -PageSize : PageSize));
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging && Changed != null)
            Changed(dragValue + (int)((e.Y - dragY) * Maximum / (float)Math.Max(1, Height - ThumbHeight)));
        base.OnMouseMove(e);
    }

    // 结束控件拖动，释放鼠标捕获。
    protected override void OnMouseUp(MouseEventArgs e)
    {
        dragging = false;
        Capture = false;
        Invalidate();
        base.OnMouseUp(e);
    }
}

// 页面、侧栏和下拉菜单共用滚动容器；布局记录逻辑坐标，滚动仅移动可视子控件。
class OreScrollPanel : Panel
{
    // 下拉弹窗不属于 ToolboxForm，显式继承触发控件的缩放；页面由主窗口决定。
    public float DrawingScale;
    readonly OreScrollBar scrollbar = new OreScrollBar();
    int offset, contentHeight = 680, barWidth = 16;
    // 窗口布局传入 DPI 后的滚动条宽度，不以页面高度猜测缩放比例。
    public int BarWidth
    {
        get
        {
            return barWidth;
        }

        set
        {
            barWidth = Math.Max(12, value);
            RefreshScrollBar();
        }
    }

    // 页面实际内容高度，单位为像素，用于滚动范围计算。
    public int ContentHeight
    {
        get
        {
            return contentHeight;
        }

        set
        {
            contentHeight = value;
            RefreshScrollBar();
        }
    }

    // 当前滚动像素偏移，限制在内容与视口差值之内。
    public int ScrollOffset
    {
        get
        {
            return offset;
        }

        set
        {
            int next = Math.Max(0, Math.Min(Math.Max(0, contentHeight - ClientSize.Height), value));
            int delta = offset - next;
            offset = next;
            foreach (Control c in Controls)
                if (c != scrollbar)
                    c.Top += delta;
            RefreshScrollBar();
            Invalidate();
        }
    }

    public OreScrollPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        Controls.Add(scrollbar);
        scrollbar.Changed += delegate (int value)
        {
            ScrollOffset = value;
        };
    }

    // 按内容高度和可见高度重算滚动条位置与范围。
    void RefreshScrollBar()
    {
        scrollbar.Maximum = Math.Max(0, contentHeight - ClientSize.Height);
        scrollbar.PageSize = ClientSize.Height;
        scrollbar.Value = offset;
        scrollbar.Visible = scrollbar.Maximum > 0;
        scrollbar.Bounds = new Rectangle(Math.Max(0, Width - barWidth), 0, barWidth, Height);
        scrollbar.BringToFront();
        scrollbar.Invalidate();
    }

    // 尺寸改变后重算显示/滚动范围，不改变业务配置。
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (scrollbar != null)
            RefreshScrollBar();
    }

    // 按滚轮增量调整列表或页面滚动，并保留边界限制。
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollOffset -= e.Delta / 120 * 48 * barWidth / 16;
        var handled = e as HandledMouseEventArgs;
        if (handled != null)
            handled.Handled = true;
        base.OnMouseWheel(e);
    }
}
