// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 目标编辑只在独立 Ore 窗口进行。
// 模态窗口绑定一件装备，保存才入队；打开、关闭、缩放不发送任何游戏动作。
using System;
using System.Drawing;
using System.Windows.Forms;

sealed partial class ToolboxForm
{
    const int RerollTargetContentHeight = 472;
    readonly Panel rerollTargetParking = new Panel { Size = new Size(420, RerollTargetContentHeight) };
    RerollDetailsWindow rerollTargetWindow;
    EquipmentIconView rerollTargetIcon;
    PixelLabel rerollTargetStatus;

    void SetRerollTargetMessage(string reason)
    {
        if (rerollTargetStatus != null) rerollTargetStatus.Text = L10n.T(reason);
    }

    void ShowRerollTargetEditor(RerollInventoryItem item)
    {
        if (item == null || closing || RerollRunning || rerollStarting || rerollInventoryReading) return;
        if (rerollTargetWindow != null) { rerollTargetWindow.Activate(); return; }
        EditRerollGearTargets(item);
        using (var window = new RerollDetailsWindow(Font, "设置目标", 570, 668))
        {
            rerollTargetWindow = window;
            var scroll = new OreScrollPanel { BackColor = OreTheme.Background };
            var footer = new Panel { BackColor = OreTheme.Background };
            var status = new PixelLabel { Wrap = true, Text = L10n.T("选好词条后保存并加入队列") };
            var close = new OreButton { Text = L10n.T("关闭") };
            window.Content.Controls.Add(scroll);
            window.Content.Controls.Add(footer);
            scroll.Controls.Add(rerollTargetsCard);
            footer.Controls.Add(status);
            footer.Controls.Add(rerollQueueEdited);
            footer.Controls.Add(close);
            rerollTargetStatus = status;
            close.Click += delegate { window.Close(); };
            window.EditorLayout += delegate(float scale)
            {
                // 主窗口和独立窗口使用各自 DPI；固定操作留在底部，矮窗口只滚动编辑内容。
                int gap = Math.Max(8, (int)Math.Round(12 * scale));
                int footerHeight = (int)Math.Round(108 * scale);
                scroll.ScrollOffset = 0;
                scroll.DrawingScale = scale;
                scroll.BarWidth = (int)Math.Round(16 * scale);
                scroll.Bounds = new Rectangle(0, 0, window.Content.Width, Math.Max(1, window.Content.Height - footerHeight));
                footer.Bounds = new Rectangle(0, scroll.Bottom, window.Content.Width, footerHeight);
                rerollTargetsCard.Bounds = new Rectangle(0, 0, Math.Max(1, scroll.Width - scroll.BarWidth), (int)Math.Round(RerollTargetContentHeight * scale));
                ((OreCard)rerollTargetsCard).RenderScale = scale;
                LayoutRerollChildren(rerollTargetsCard, scale);
                // 装备图标始终为正方形；宽窗口只扩展名称和编辑区域，不能拉长图标的控件框。
                int iconSize = Math.Max(1, (int)Math.Round(72 * scale));
                rerollTargetIcon.Size = new Size(iconSize, iconSize);
                rerollEditingHeading.Left = rerollTargetIcon.Right + (int)Math.Round(16 * scale);
                rerollEditingHeading.Width = Math.Max(1, rerollTargetsCard.Width - rerollEditingHeading.Left - (int)Math.Round(20 * scale));
                foreach (Control control in rerollTargetsCard.Controls)
                {
                    float original;
                    if (!designTextScale.TryGetValue(control, out original)) continue;
                    var label = control as PixelLabel;
                    var button = control as OreButton;
                    if (label != null) label.PixelScale = original * scale;
                    if (button != null) button.PixelScale = original * scale;
                }
                rerollTargetsList.ItemHeight = Math.Max(24, (int)Math.Round(32 * scale));
                scroll.ContentHeight = rerollTargetsCard.Height;
                status.Bounds = new Rectangle(gap, (int)(4 * scale), Math.Max(1, footer.Width - gap * 2), (int)(44 * scale));
                status.PixelScale = .75f * scale;
                int buttonWidth = Math.Max(1, (footer.Width - gap * 3) / 2);
                rerollQueueEdited.Bounds = new Rectangle(gap, (int)(56 * scale), buttonWidth, (int)(44 * scale));
                close.Bounds = new Rectangle(gap * 2 + buttonWidth, rerollQueueEdited.Top, buttonWidth, rerollQueueEdited.Height);
                ((OreButton)rerollQueueEdited).PixelScale = close.PixelScale = .85f * scale;
            };
            window.FormClosing += delegate
            {
                // 菜单是独立顶层窗口，必须先关闭；重用控件必须在编辑窗口 Dispose 前移出。
                rerollEffectChoice.ClosePopup();
                rerollEffectCategory.ClosePopup();
                rerollTargetParking.Controls.Add(rerollTargetsCard);
                rerollTargetParking.Controls.Add(rerollQueueEdited);
            };
            try
            {
                rerollMessage = "选好词条后保存并加入队列";
                RefreshRerollTargets(); RefreshRerollDisplay();
                window.ShowDialog(this);
            }
            finally
            {
                rerollTargetParking.Controls.Add(rerollTargetsCard);
                rerollTargetParking.Controls.Add(rerollQueueEdited);
                rerollTargetStatus = null;
                rerollTargetWindow = null;
                RefreshRerollDisplay();
            }
        }
    }
}
