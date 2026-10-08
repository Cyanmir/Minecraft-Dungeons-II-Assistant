// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 分类只取对应版本模板的直接 Traits，属于浏览辅助，不用于可达性、最高等级或互斥判定。
// 预算编辑范围按当前原生余额设置，正在执行时冻结整批预算，不能随余额下降改写运行配置。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static class RerollDisplayCategories
{
    static readonly Dictionary<string, int[]> groups = Read();
    static Dictionary<string, int[]> Read()
    {
        using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("reroll-display-categories.json"))
        using (var reader = new StreamReader(stream))
        {
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
            return ((Dictionary<string, object>)data["categories"]).ToDictionary(pair => pair.Key,
                // JSON 数组兼容 ArrayList，不强转为 object[]。
                pair => ((System.Collections.IEnumerable)pair.Value).Cast<object>().Select(Convert.ToInt32).ToArray(), StringComparer.Ordinal);
        }
    }
    internal static bool Contains(string type, int category)
    {
        int[] value;
        return category == 0 || groups.TryGetValue(type, out value) && value.Contains(category) ||
            category == 5 && !groups.ContainsKey(type);
    }
}
sealed partial class ToolboxForm
{
    void BuildRerollEffectFilter()
    {
        rerollEffectCategory = new OreSelect { Location = new Point(20, 228), Size = new Size(164, 36) };
        rerollTargetsCard.Controls.Add(rerollEffectCategory);
        RefreshRerollEffectCategories();
        rerollEffectSearch = new RerollSearchBox { HintKey = "搜索词条", Location = new Point(194, 232), Size = new Size(206, 28), BackColor = OreTheme.Field, ForeColor = OreTheme.Text, BorderStyle = BorderStyle.FixedSingle };
        rerollTargetsCard.Controls.Add(rerollEffectSearch);
        rerollEffectCategory.SelectedIndexChanged += delegate { if (!rerollConfigLoading) RefreshRerollTargets(); };
        rerollEffectSearch.TextChanged += delegate { if (!rerollConfigLoading) RefreshRerollTargets(); };
    }
    void RefreshRerollEffectCategories()
    {
        int index = Math.Max(0, rerollEffectCategory.SelectedIndex);
        ReplaceRerollOptions(rerollEffectCategory, new object[] { L10n.T("全部词条"), L10n.T("近战词条"), L10n.T("远程词条"), L10n.T("防具词条"), L10n.T("法器词条"), L10n.T("其他词条") });
        rerollEffectCategory.SelectedIndex = index;
        var search = rerollEffectSearch as RerollSearchBox; if (search != null) search.RefreshHint();
    }
    // 观察每秒更新，但相同菜单内容不能重建或收起，否则用户无法完成选择。
    // 只有选项实际变化时关闭旧菜单，防止旧索引对应到另一个效果。
    static void ReplaceRerollOptions(OreSelect control, object[] options)
    {
        if (control.Items.SequenceEqual(options)) return;
        control.ClosePopup();
        control.Items.Clear(); control.Items.AddRange(options);
        control.Invalidate();
    }
    bool VisibleRerollEffect(string type)
    {
        string query = rerollEffectSearch == null ? "" : rerollEffectSearch.Text.Trim();
        return RerollDisplayCategories.Contains(type, rerollEffectCategory.SelectedIndex) &&
            (query.Length == 0 || RerollEffectName(type).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
    }
    void RefreshRerollBudgetControls()
    {
        if (rerollConfiguration == null || rerollSnapshot == null || rerollSnapshot.State != "Selected" || RerollRunning || rerollStarting) return;
        bool changed = false;
        rerollConfigLoading = true;
        try
        {
            for (int i = 0; i < 3; i++)
            {
                var number = rerollBudgetNumbers[i]; if (number == null) continue;
                number.Maximum = rerollSnapshot.Balance[i];
                number.Value = Math.Min(rerollConfiguration.Budgets[i], (int)number.Maximum);
                if (rerollConfiguration.Budgets[i] != (int)number.Value) { rerollConfiguration.Budgets[i] = (int)number.Value; changed = true; }
                if (rerollBudgetSync[i] != null) rerollBudgetSync[i]();
            }
        }
        finally { rerollConfigLoading = false; }
        if (changed) SaveRerollConfiguration();
    }
    static string RerollCurrencyText(int[] values)
    {
        var keys = new[] { "Currency_Emerald", "Currency_SpringStone", "Currency_EnchantmentPoint" };
        return String.Join(" / ", Enumerable.Range(0, 3).Select(i => EquipmentGamePresentation.Term(keys[i]) + " " + values[i]));
    }
}
