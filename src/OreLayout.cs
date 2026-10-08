// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// 保持 .NET Framework 编译兼容，DPI 使用 Windows API 并对旧系统回退。
static class OreDpi
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr window);
    // 在创建窗口前启用 DPI；受宿主已配置上下文限制时不重复覆盖。
    public static void Enable()
    {
        try
        {
            if (SetProcessDpiAwarenessContext(new IntPtr(-4)))
                return;
        }
        catch (EntryPointNotFoundException)
        {
        }

        SetProcessDPIAware();
    }

    public static float Scale(IntPtr window)
    {
        if (window != IntPtr.Zero)
            try
            {
                uint dpi = GetDpiForWindow(window);
                if (dpi > 0)
                    return dpi / 96f;
            }
            catch (EntryPointNotFoundException)
            {
            }

        using (var graphics = Graphics.FromHwnd(window))
            return graphics.DpiX / 96f;
    }
}

// 顶栏和侧栏固定逻辑比例，内容宽度随窗口增长；页面高度不足时使用已有滚动。
sealed partial class ToolboxForm
{
    // 所有自绘控件共用这一比例，不按各自行高推断字体大小。
    public float UiScale = 1;
    // 先设置容器，再设置子控件，避免依赖 Dictionary 的枚举顺序。
    static int LayoutDepth(Control control)
    {
        int depth = 0;
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
            depth++;
        return depth;
    }

    // 不拉伸字体和行高；缩放下限由可用窗口宽度/高度共同决定。
    void LayoutWindow()
    {
        CancelPageMotion();
        if (!layoutReady)
            return;
        float scale = Math.Min(interactive ? OreDpi.Scale(Handle) : 1, Math.Min(ClientSize.Width / (float)OreMetrics.DesignWidth, ClientSize.Height / 720f));
        UiScale = scale;
        int footerY = ClientSize.Height - (int)Math.Round((OreMetrics.FooterHeight + 20) * scale);
        int contentX = (int)Math.Round(OreMetrics.ContentX * scale), contentWidth = ClientSize.Width - contentX - (int)Math.Round(OreMetrics.PaddingMedium * scale);
        // 宽窗口保留适量空白，设置行不横跨整个屏幕；窄窗口保持完整可用宽度。
        int maximumContentWidth = (int)Math.Round(880 * scale);
        if (contentWidth > maximumContentWidth)
        {
            contentX += (contentWidth - maximumContentWidth) / 2;
            contentWidth = maximumContentWidth;
        }

        SuspendLayout();
        foreach (var sidebar in Controls.OfType<OreSidebar>())
        {
            sidebar.ScrollOffset = 0;
            sidebar.BarWidth = (int)Math.Round(16 * scale);
        }

        foreach (var page in pages)
        {
            var scroll = page as OreScrollPanel;
            if (scroll != null)
            {
                scroll.ScrollOffset = 0;
                scroll.BarWidth = (int)Math.Round(16 * scale);
            }
        }

        foreach (var entry in designBounds.OrderBy(x => LayoutDepth(x.Key)))
        {
            Control control = entry.Key, parent = control.Parent;
            if (control.FindForm() != this) continue;
            var surface = control as OreCard;
            if (surface != null)
                surface.RenderScale = scale;
            var r = entry.Value;
            if (parent == this)
            {
                if (control is OreSidebar)
                    control.Bounds = new Rectangle(0, (int)(OreMetrics.HeaderHeight * scale), (int)(OreMetrics.NavWidth * scale), footerY - (int)(OreMetrics.HeaderHeight * scale) - (int)(12 * scale));
                else if (pages.Contains(control))
                    control.Bounds = new Rectangle(contentX, (int)(171 * scale), contentWidth, Math.Max(100, footerY - (int)(183 * scale)));
                else if (control is OreCard)
                    control.Bounds = new Rectangle((int)(16 * scale), footerY, ClientSize.Width - (int)(32 * scale), (int)(OreMetrics.FooterHeight * scale));
                else if (r.Y == 0 && control is Panel)
                    control.Bounds = new Rectangle(0, 0, ClientSize.Width, (int)(OreMetrics.HeaderHeight * scale));
                else
                    control.Bounds = new Rectangle(contentX + (int)(20 * scale), (int)(r.Y * scale), contentWidth - (int)(40 * scale), (int)(r.Height * scale));
                continue;
            }

            Rectangle originalParent;
            float ratio = designBounds.TryGetValue(parent, out originalParent) ? parent.Width / (float)originalParent.Width : scale;
            int x = (int)Math.Round(r.X * ratio), width = (int)Math.Round(r.Width * ratio);
            // 固定尺寸的右侧输入控件对齐右边距；左侧标签和滑块使用剩余宽度。
            bool fixedWidth = control is OreNumber || control is OreSelect || control is OreToggle || control is KeyButton;
            bool headerChild = parent.Parent == this && designBounds.ContainsKey(parent) && designBounds[parent].Y == 0;
            bool footerChild = parent is OreCard && parent.Parent == this;
            if (fixedWidth && r.X >= 500 || headerChild && r.X >= 549 || footerChild && r.X >= 443)
            {
                width = (int)Math.Round(r.Width * scale);
                x = parent.Width - (int)Math.Round((originalParent.Width - r.Right) * scale) - width;
            }

            if (control is OreCard && parent is OreScrollPanel)
                width -= (int)Math.Round(20 * scale);
            control.Bounds = new Rectangle(x, (int)Math.Round(r.Y * scale), Math.Max(1, width), Math.Max(1, (int)Math.Round(r.Height * scale)));
        }

        foreach (var entry in designTextScale)
        {
            if (entry.Key.FindForm() != this) continue;
            var label = entry.Key as PixelLabel;
            var button = entry.Key as OreButton;
            if (label != null)
                label.PixelScale = entry.Value * scale;
            else if (button != null)
                // 带实验标注的导航稍收紧字号，完整显示括号文字，沿用现有按钮宽度。
                button.PixelScale = (button.Navigation ? ((button.Tag as string) == "装备整理（实验）" ? 1f : 1.1f) : entry.Value) * scale;
        }

        foreach (var page in pages.OfType<OreScrollPanel>())
            page.ContentHeight = Math.Max(page.Height, (int)Math.Round(page.Controls.Cast<Control>().Where(c => !(c is OreScrollBar)).Select(c => designBounds.ContainsKey(c) ? designBounds[c].Bottom + 8 : 0).DefaultIfEmpty(0).Max() * scale));
        foreach (var sidebar in Controls.OfType<OreSidebar>())
            sidebar.ContentHeight = Math.Max(sidebar.Height, (int)Math.Round(692 * scale));
        // 仅刷词条页覆盖通用纵向布局；其他功能保持原有坐标与滚动方式。
        LayoutRerollPage(scale);
        maximizeButton.Text = fullScreen || WindowState == FormWindowState.Maximized ? "❐" : "□";
        ResumeLayout();
        Invalidate(true);
    }
}
