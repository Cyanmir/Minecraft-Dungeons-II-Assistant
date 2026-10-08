// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// 数字输入保留编辑缓冲、提交/取消、限幅和 ValueChanged；范围修改须与对应滑块和配置一致。
sealed class OreNumber : Control
{
    bool hover;
    decimal min, max = 1000, value;
    // 编辑缓冲在 Enter 或失焦时提交；Esc 丢弃，避免未完成数字直接进入配置。
    string editing;
    public string Suffix = "";
    // 默认保持既有六位输入；大范围页面可显式放宽，不截掉百万次数或十位费用预算。
    public int MaximumInputDigits = 6;
    public event EventHandler ValueChanged;
    // 数字控件允许的最小值；与配置解析和滑块边界同步维护。
    public decimal Minimum
    {
        get
        {
            return min;
        }

        set
        {
            min = value;
            Value = this.value;
        }
    }

    // 数字控件允许的最大值；不得只放宽 UI 而绕过业务限制。
    public decimal Maximum
    {
        get
        {
            return max;
        }

        set
        {
            max = value;
            Value = this.value;
        }
    }

    // 控件当前数值；setter 保持范围限制并在变化时通知配置层。
    public decimal Value
    {
        get
        {
            return value;
        }

        set
        {
            decimal n = Math.Max(min, Math.Min(max, value));
            if (n == this.value)
                return;
            this.value = n;
            editing = null;
            Invalidate();
            if (ValueChanged != null)
                ValueChanged(this, EventArgs.Empty);
        }
    }

    public OreNumber()
    {
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
        EnabledChanged += delegate
        {
            Invalidate();
        };
        GotFocus += delegate
        {
            Invalidate();
        };
        LostFocus += delegate
        {
            Invalidate();
        };
        Size = new Size(170, 44);
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int px = OreMetrics.Pixel(this, 2, OreMetrics.ControlHeight), side = Math.Max(24, Height * 3 / 4);
        OreRenderer.Field(e.Graphics, ClientRectangle, px, hover, Focused, Enabled);
        Color color = Enabled ? OreTheme.Text : OreTheme.DisabledText;
        OreRenderer.Fill(e.Graphics, new Rectangle(side, px * 2, px, Height - px * 4), OreTheme.Edge);
        OreRenderer.Fill(e.Graphics, new Rectangle(Width - side - px, px * 2, px, Height - px * 4), OreTheme.Edge);
        float scale = OreMetrics.Scale(this);
        PixelText.DrawCjkOrBody(e.Graphics, "-", new Rectangle(0, 0, side, Height), color, scale, true);
        PixelText.DrawCjkOrBody(e.Graphics, "+", new Rectangle(Width - side, 0, side, Height), color, scale, true);
        PixelText.DrawCjkOrBody(e.Graphics, (editing ?? Value.ToString("0")) + Suffix, new Rectangle(side + px * 2, 0, Width - side * 2 - px * 4, Height), color, scale, true);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.X < Math.Max(24, Height * 3 / 4))
            Value--;
        else if (e.X > Width - Math.Max(24, Height * 3 / 4))
            Value++;
        base.OnMouseDown(e);
    }

    // 声明控件自行处理的导航键，避免 WinForms 把它当作焦点切换。
    protected override bool IsInputKey(Keys keyData)
    {
        return keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Right)
        {
            Value++;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Left)
        {
            Value--;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            Commit();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            editing = null;
            Invalidate();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    // 处理直接输入的字符，限制输入格式并保持数值边界。
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (char.IsDigit(e.KeyChar))
        {
            editing = (editing ?? "") + e.KeyChar;
            if (editing.Length > Math.Max(1, MaximumInputDigits))
                editing = editing.Substring(1);
            e.Handled = true;
            Invalidate();
        }
        else if (e.KeyChar == '\b')
        {
            editing = editing ?? Value.ToString("0");
            if (editing.Length > 0)
                editing = editing.Substring(0, editing.Length - 1);
            e.Handled = true;
            Invalidate();
        }

        base.OnKeyPress(e);
    }

    // 提交数字输入缓冲，合法值限幅后触发修改事件。
    void Commit()
    {
        decimal parsed;
        if (editing != null && decimal.TryParse(editing, out parsed))
            Value = parsed;
        editing = null;
        Invalidate();
    }

    // 失去焦点时提交数字编辑，避免半截输入直接进入设置。
    protected override void OnLostFocus(EventArgs e)
    {
        Commit();
        base.OnLostFocus(e);
    }
}

// 滑块的鼠标映射、键盘步进与绘制共用范围和手柄尺寸；数值变化仍通知原有配置层。
sealed class OreSlider : Control
{
    bool hover;
    public int Minimum = 1, Maximum = 99;
    int value;
    bool dragging;
    public event EventHandler ValueChanged;
    // 控件当前数值；setter 保持范围限制并在变化时通知配置层。
    public int Value
    {
        get
        {
            return value;
        }

        set
        {
            int n = Math.Max(Minimum, Math.Min(Maximum, value));
            if (n == this.value)
                return;
            this.value = n;
            Invalidate();
            if (ValueChanged != null)
                ValueChanged(this, EventArgs.Empty);
        }
    }

    public OreSlider()
    {
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
        EnabledChanged += delegate
        {
            Invalidate();
        };
        GotFocus += delegate
        {
            Invalidate();
        };
        LostFocus += delegate
        {
            Invalidate();
        };
        Height = 39;
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        float scale = OreMetrics.Scale(this);
        int px = Math.Max(1, (int)Math.Round(2 * scale)), thumb = ThumbSize(), half = thumb / 2;
        int x = half + (int)((Width - thumb) * (Value - Minimum) / (float)Math.Max(1, Maximum - Minimum));
        int center = Height / 2, trackHeight = Math.Max(5, (int)Math.Round(11 * scale));
        OreRenderer.Fill(e.Graphics, ClientRectangle, Parent == null ? OreTheme.Background : Parent.BackColor);
        var track = new Rectangle(half, center - trackHeight / 2, Width - thumb, trackHeight);
        OreRenderer.Fill(e.Graphics, track, OreTheme.Line);
        var inside = Rectangle.Inflate(track, -px, -px);
        OreRenderer.Fill(e.Graphics, inside, OreRenderer.Highlight(OreTheme.Edge));
        var progress = inside;
        progress.Width = Math.Max(0, x - inside.X);
        OreRenderer.Fill(e.Graphics, progress, Enabled ? OreRenderer.Highlight(OreTheme.Green) : OreTheme.Muted);
        inside.Inflate(0, -px);
        OreRenderer.Fill(e.Graphics, inside, OreTheme.Edge);
        progress = inside;
        progress.Width = Math.Max(0, x - inside.X);
        OreRenderer.Fill(e.Graphics, progress, Enabled ? OreTheme.Green : OreTheme.Muted);
        var handle = new Rectangle(x - half, (Height - thumb) / 2, thumb, thumb);
        Color face = Enabled ? hover || dragging ? OreTheme.LightHover : OreTheme.Light : OreTheme.LightPressed;
        OreRenderer.Raised(e.Graphics, handle, face, OreRenderer.Highlight(face, 40), OreTheme.Edge, px, false);
        if (Focused && ShowFocusCues)
            OreRenderer.Focus(e.Graphics, Rectangle.Inflate(handle, px * 2, px * 2), px);
    }

    // 绘制与鼠标映射共享 29px 滑块宽度，避免点选位置与显示进度偏移。
    int ThumbSize()
    {
        return Math.Min(Height, Math.Max(12, (int)Math.Round(29 * OreMetrics.Scale(this))));
    }

    // 把鼠标坐标映射到滑块值并限制边界。
    void Position(int x)
    {
        int thumb = ThumbSize();
        Value = Minimum + (int)Math.Round(Math.Max(0, Math.Min(1, (x - thumb / 2) / (float)Math.Max(1, Width - thumb))) * (Maximum - Minimum));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button != MouseButtons.Left)
            return;
        dragging = true;
        Capture = true;
        Position(e.X);
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging)
            Position(e.X);
        base.OnMouseMove(e);
    }

    // 结束控件拖动，释放鼠标捕获。
    protected override void OnMouseUp(MouseEventArgs e)
    {
        dragging = false;
        Capture = false;
        base.OnMouseUp(e);
    }

    // 切换窗口导致鼠标捕获丢失时结束拖动，避免回来后仅悬停也改变数值。
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture)
        {
            dragging = false;
            Invalidate();
        }

        base.OnMouseCaptureChanged(e);
    }

    // 声明控件自行处理的导航键，避免 WinForms 把它当作焦点切换。
    protected override bool IsInputKey(Keys keyData)
    {
        return keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Home || keyData == Keys.End || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left)
        {
            Value--;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right)
        {
            Value++;
            e.Handled = true;
        }

        if (e.KeyCode == Keys.Home)
        {
            Value = Minimum;
            e.Handled = true;
        }

        if (e.KeyCode == Keys.End)
        {
            Value = Maximum;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }
}

// 下拉选项沿用 Items 和 SelectedIndexChanged；弹窗只负责选择和显示，不改枚举顺序。
sealed class OreSelect : Control
{
    bool hover;
    Form openPopup;
    // 可选逐项图标只影响绘制，索引与原有事件/配置映射完全不变；缓存图像由调用者管理。
    public Func<int, Image> ItemIcon;
    public bool FixedTypography;
    // 项目顺序对应现有配置枚举；翻译只改显示文字，不能重新排序。
    public List<object> Items = new List<object>();
    int index = -1;
    public event EventHandler SelectedIndexChanged;
    // 下拉选项索引；索引顺序与对应枚举/配置值一致。
    public int SelectedIndex
    {
        get
        {
            return index;
        }

        set
        {
            if (value == index)
                return;
            index = value;
            Invalidate();
            if (SelectedIndexChanged != null)
                SelectedIndexChanged(this, EventArgs.Empty);
        }
    }

    public OreSelect()
    {
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
        EnabledChanged += delegate
        {
            Invalidate();
        };
        GotFocus += delegate
        {
            Invalidate();
        };
        LostFocus += delegate
        {
            Invalidate();
        };
        Height = 44;
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int px = OreMetrics.Pixel(this, 2, OreMetrics.ControlHeight);
        Color face = !Enabled ? OreTheme.LightPressed : hover ? OreTheme.Edge : OreTheme.Surface;
        OreRenderer.Fill(e.Graphics, ClientRectangle, Parent == null ? OreTheme.Card : Parent.BackColor);
        OreRenderer.Raised(e.Graphics, ClientRectangle, face, OreRenderer.Highlight(face), OreTheme.Background, px, false);
        if (Focused && ShowFocusCues)
            OreRenderer.Focus(e.Graphics, ClientRectangle, px);
        var bounds = new Rectangle(px * 8, 0, Width - px * 23, Height - px * 2);
        string text = index >= 0 && index < Items.Count ? Items[index].ToString() : L10n.T("选择槽位");
        var image = ItemIcon == null || index < 0 || index >= Items.Count ? null : ItemIcon(index);
        if (image != null)
        {
            int size = Math.Max(1, Math.Min(Height - px * 6, px * 14));
            var state = e.Graphics.Save();
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            e.Graphics.DrawImage(image, new Rectangle(bounds.Left, (Height - size) / 2 - px, size, size));
            e.Graphics.Restore(state);
            bounds.X += size + px * 3; bounds.Width -= size + px * 3;
        }
        if (FixedTypography)
            PixelText.DrawMenu(e.Graphics, text, bounds);
        else
            PixelText.DrawCjkOrBody(e.Graphics, text, bounds, Enabled ? OreTheme.Text : OreTheme.DisabledText, OreMetrics.Scale(this), false);
        int x = Width - px * 10, y = Height / 2;
        using (var pen = new Pen(Enabled ? OreTheme.Text : OreTheme.Muted, px))
            e.Graphics.DrawLines(pen, new[] { new Point(x - px * 2, y - px), new Point(x, y + px), new Point(x + px * 2, y - px) });
    }

    // 响应控件点击，切换或打开当前选项。
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Open();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
        {
            Open();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            SelectedIndex = Math.Max(0, index - 1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down)
        {
            SelectedIndex = Math.Min(Items.Count - 1, index + 1);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    void Open()
    {
        if (!Enabled || Items.Count == 0)
            return;
        ClosePopup();
        int rowHeight = Math.Max(24, (int)Math.Round(36 * OreMetrics.Scale(this))), edge = OreMetrics.Pixel(this, 2, 44);
        var popup = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            ShowInTaskbar = false,
            BackColor = OreTheme.Line,
            ClientSize = new Size(Width, Math.Min(Items.Count, 4) * rowHeight + edge * 2)
        };
        var list = new OreScrollPanel
        {
            DrawingScale = OreMetrics.Scale(this),
            BackColor = OreTheme.Field,
            Location = new Point(edge, edge),
            Size = new Size(Width - edge * 2, popup.ClientSize.Height - edge * 2),
            BarWidth = Math.Max(12, (int)(16 * OreMetrics.Scale(this)))
        };
        popup.Controls.Add(list);
        openPopup = popup;
        Point p = PointToScreen(new Point(0, Height));
        Rectangle screen = Screen.FromControl(this).WorkingArea;
        if (p.Y + popup.Height > screen.Bottom)
            p.Y = PointToScreen(Point.Empty).Y - popup.Height;
        p.X = Math.Max(screen.Left, Math.Min(p.X, screen.Right - popup.Width));
        p.Y = Math.Max(screen.Top, p.Y);
        popup.Location = p;
        for (int i = 0; i < Items.Count; i++)
        {
            int n = i;
            var b = new OreNavItem
            {
                Text = Items[i].ToString() + (i == index ? " ✓" : ""),
                ContentIcon = ItemIcon == null ? null : ItemIcon(i),
                Navigation = true,
                FixedTypography = FixedTypography,
                Selected = i == index,
                Location = new Point(0, i * rowHeight),
                Size = new Size(list.Width - (Items.Count > 4 ? list.BarWidth : 0), rowHeight),
                PixelScale = OreMetrics.Scale(this)
            };
            b.Click += delegate
            {
                popup.Close();
                SelectedIndex = n;
            };
            list.Controls.Add(b);
        }

        list.ContentHeight = Items.Count * rowHeight;
        list.ScrollOffset = Math.Max(0, (index - 3) * rowHeight);
        popup.Deactivate += delegate
        {
            popup.Close();
        };
        popup.FormClosed += delegate
        {
            if (openPopup == popup) openPopup = null;
            popup.Dispose();
        };
        popup.Show(FindForm());
    }
    // 切换装备/语言/资源前关闭旧菜单，避免旧索引选中另一效果或绘制已释放的缓存图像。
    internal void ClosePopup()
    {
        var popup = openPopup;
        openPopup = null;
        if (popup != null && !popup.IsDisposed) popup.Close();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) ClosePopup();
        base.Dispose(disposing);
    }
}
