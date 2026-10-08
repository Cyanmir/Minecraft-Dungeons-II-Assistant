// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 铁匠页的滑块只编辑目标/上限，不触发执行。
using System;
using System.Drawing;
using System.Windows.Forms;

sealed partial class ToolboxForm
{
    // 对数位置覆盖原有完整整数范围，低等级/小预算也容易选；同步只更新显示，不反向舍入输入。
    Action BindRerollNumberSlider(Panel parent, OreNumber number, int y)
    {
        number.MaximumInputDigits = 10;
        Func<int, int> position = value => number.Maximum <= number.Minimum ? 0 :
            (int)Math.Round(Math.Log((double)value - (double)number.Minimum + 1) / Math.Log((double)(number.Maximum - number.Minimum) + 1) * 1000);
        var slider = new OreSlider { Location = new Point(20, y + 3), Size = new Size(number.Left - 40, 39),
            Minimum = 0, Maximum = 1000, Value = position((int)number.Value), Enabled = number.Enabled && number.Maximum > number.Minimum };
        parent.Controls.Add(slider);
        bool syncing = false;
        slider.ValueChanged += delegate
        {
            if (syncing) return;
            syncing = true;
            try
            {
                // 转 int 前限幅，避免 Int32.MaxValue 预算端点受浮点误差溢出。
                int minimum = (int)number.Minimum;
                double span = (double)(number.Maximum - number.Minimum), logarithm = Math.Log(span + 1);
                double amount = Math.Exp(slider.Value / 1000d * logarithm) - 1;
                number.Value = minimum + (int)Math.Round(Math.Max(0, Math.Min(span, amount)));
            }
            finally { syncing = false; }
        };
        Action sync = delegate
        {
            if (syncing) return;
            syncing = true;
            try { slider.Value = position((int)number.Value); slider.Enabled = number.Enabled && number.Maximum > number.Minimum; }
            finally { syncing = false; }
        };
        number.ValueChanged += delegate { sync(); };
        return sync;
    }
}
