// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Windows.Forms;

// 所有尺寸均为 96 DPI 的逻辑单位；窗口负责一次缩放，控件绘制不重复乘系统 DPI。
static class OreMetrics
{
    // 窗口基准、侧栏、顶底栏与设置行的尺寸，均为 96 DPI 逻辑像素。
    public const int DesignWidth = 1024, DesignHeight = 860;
    public const int NavWidth = 240, HeaderHeight = 64, FooterHeight = 124;
    public const int RowHeight = 88, ControlHeight = 44, NavItemHeight = 48;
    public const int PaddingSmall = 8, PaddingMedium = 16, PaddingLarge = 24;
    public const int DividerHeight = 2, ContentX = 264, ContentWidth = 744;
    // 页面内共享窗口缩放比例；复选框的行高不同，也不能把图标和字体缩小。
    public static float Scale(Control control)
    {
        var window = control.FindForm() as ToolboxForm;
        if (window != null)
            return window.UiScale;
        for (Control parent = control; parent != null; parent = parent.Parent)
        {
            var scroll = parent as OreScrollPanel;
            if (scroll != null && scroll.DrawingScale > 0)
                return scroll.DrawingScale;
        }

        return OreDpi.Scale(control.IsHandleCreated ? control.Handle : IntPtr.Zero);
    }

    // 普通控件和弹窗共用逻辑像素比例；保留 referenceHeight 参数兼容既有调用。
    public static int Pixel(Control control, int logical, int referenceHeight)
    {
        return Math.Max(1, (int)Math.Round(logical * Scale(control)));
    }
}
