// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm
{
    EquipmentPolicy equipmentPolicy;
    InventoryBaseline inventoryBaseline = new InventoryBaseline();
    PixelLabel[] equipmentCategoryTexts = new PixelLabel[7];
    OreSelect[] normalRarity = new OreSelect[7], stormRarity = new OreSelect[7];
    CheckBox keepUpgrades, keepMerchant, keepQuest, sellDuplicates;
    PixelLabel equipmentResult;
    Button previewNewEquipment, previewAllEquipment, sellExistingButton;
    bool equipmentUpdating, researchBusy;
    List<EquipmentDecision> equipmentPlan;
    static readonly string[] categoryLabels =
    {
        "近战武器",
        "远程武器",
        "头盔",
        "胸甲",
        "护腿",
        "靴子",
        "法器"
    };
    static readonly string[] rarityLabels =
    {
        "关闭",
        "普通",
        "稀有",
        "特殊",
        "独特"
    };
    // 创建原生战斗、附近交互与装备整理页面，控件值通过统一设置路径保存。
    void BuildAutomationPages()
    {
        equipmentPolicy = interactive ? EquipmentPolicy.Load() : new EquipmentPolicy();
        // 页面按用途拆分；常用目标开关以两列展示，安装入口仅保留在设置。
        var encounter = Card(pages[4], 0, 100);
        LabelAt(encounter, L10n.T("遇到敌人自动攻击"), 20, 16, 600, 30);
        combatEncounterEnabled = CheckAt(encounter, "", 660, 14, 64, settings.CombatEncounter);
        LabelAt(encounter, L10n.T("主动近战，可手动移动；不会追赶远处敌人"), 20, 58, 704, 30).ForeColor = OreTheme.Muted;
        var attack = Card(pages[4], 112, 196);
        LabelAt(attack, L10n.T("站定时自动近战"), 20, 16, 600, 30);
        combatAttackEnabled = CheckAt(attack, "", 660, 14, 64, settings.CombatAttack);
        LabelAt(attack, L10n.T("目标判定范围（80–500 游戏单位）"), 20, 58, 704, 30);
        combatRange = Number(attack, 523, 96, 80, 500, settings.CombatRange, 201);
        combatRangeSlider = new OreSlider
        {
            Location = new Point(20, 99),
            Size = new Size(482, 39),
            Minimum = 80,
            Maximum = 500,
            Value = settings.CombatRange
        };
        attack.Controls.Add(combatRangeSlider);
        combatRangeNote = (PixelLabel)LabelAt(attack, L10n.T("游戏近战范围待连接读取；工具判定上限 500\n数值为游戏单位，不扩大原生攻击范围"), 20, 146, 704, 42);
        combatRangeNote.PixelScale = .75f;
        combatRangeNote.ForeColor = OreTheme.Muted;
        var proactive = Card(pages[4], 320, 100);
        LabelAt(proactive, L10n.T("战斗使用法器"), 20, 16, 600, 30);
        combatArtifactsEnabled = CheckAt(proactive, "", 660, 14, 64, settings.CombatArtifacts);
        LabelAt(proactive, L10n.T("使用自动恢复页已选槽位；药水遵循血量阈值"), 20, 58, 704, 30).ForeColor = OreTheme.Muted;
        var threatCard = Card(pages[4], 432, 132);
        LabelAt(threatCard, L10n.T("攻击预警（实验）"), 20, 15, 580, 28);
        threatEnabled = CheckAt(threatCard, "", 660, 14, 64, settings.ThreatEnabled);
        LabelAt(threatCard, L10n.T("危险接近时提前使用已选法器"), 20, 53, 704, 28).ForeColor = OreTheme.Muted;
        threatNote = (PixelLabel)LabelAt(threatCard, L10n.T("等待攻击预警数据"), 20, 92, 704, 27);
        threatNote.PixelScale = .8f;
        threatNote.ForeColor = OreTheme.Muted;
        var evade = Card(pages[4], 576, 132);
        LabelAt(evade, L10n.T("攻击自动闪避（实验）"), 20, 16, 600, 30);
        combatEvadeEnabled = CheckAt(evade, "", 660, 14, 64, settings.CombatEvade);
        LabelAt(evade, L10n.T("核对充能、危险与地面；未覆盖全部 Boss 攻击"), 20, 55, 704, 30).ForeColor = OreTheme.Muted;
        evadeRuntimeNote = (PixelLabel)LabelAt(evade, L10n.T("等待闪避数据"), 20, 96, 704, 26);
        evadeRuntimeNote.ForeColor = OreTheme.Muted;
        evadeRuntimeNote.PixelScale = .85f;
        var runtime = StatusCard(pages[4], 720, 116);
        LabelAt(runtime, L10n.T("战斗状态"), 20, 16, 704, 30);
        combatRuntimeNote = (PixelLabel)LabelAt(runtime, L10n.T("开启功能后按 F8 开始，F9 停止"), 20, 56, 704, 44);
        combatRuntimeNote.PixelScale = .85f;
        var master = Card(pages[2], 0, 76);
        LabelAt(master, L10n.T("自动拾取与交互"), 20, 18, 600, 30);
        nearbyDirectEnabled = CheckAt(master, "", 660, 18, 64, settings.NearbyDirect);
        nearbyChestsEnabled = LootTypeCard(0, 88, "已有宝箱", "打开附近未开启的宝箱", settings.NearbyChests);
        nearbyItemsEnabled = LootTypeCard(378, 88, "装备 / 书 / TNT", "装备、附魔书及 TNT 拾取", settings.NearbyItems);
        nearbyFoodEnabled = LootTypeCard(0, 216, "自动食用", "已核实食物，使用原生效果", settings.NearbyFood);
        nearbyPotsEnabled = LootTypeCard(378, 216, "绿宝石罐", "破坏大小罐；携带 TNT 时跳过", settings.NearbyPots);
        var direct = Card(pages[2], 344, 336);
        LabelAt(direct, L10n.T("允许手动移动时交互"), 20, 16, 600, 30);
        nearbyMovingEnabled = CheckAt(direct, "", 660, 14, 64, settings.NearbyAllowMoving);
        LabelAt(direct, L10n.T("通用交互间隔"), 20, 65, 704, 30);
        nearbyInterval = Number(direct, 523, 103, 100, 30000, settings.NearbyIntervalMs, 201);
        nearbyInterval.Suffix = "ms";
        nearbyIntervalSlider = new OreSlider
        {
            Location = new Point(20, 106),
            Size = new Size(482, 39),
            Minimum = 0,
            Maximum = 1000,
            Value = NearbyIntervalScale.Position(settings.NearbyIntervalMs)
        };
        direct.Controls.Add(nearbyIntervalSlider);
        LabelAt(direct, L10n.T("食物食用间隔"), 20, 160, 704, 30);
        nearbyFoodInterval = Number(direct, 523, 198, 100, 30000, settings.NearbyFoodIntervalMs, 201);
        nearbyFoodInterval.Suffix = "ms";
        nearbyFoodIntervalSlider = new OreSlider
        {
            Location = new Point(20, 201),
            Size = new Size(482, 39),
            Minimum = 0,
            Maximum = 1000,
            Value = NearbyIntervalScale.Position(settings.NearbyFoodIntervalMs)
        };
        direct.Controls.Add(nearbyFoodIntervalSlider);
        LabelAt(direct, L10n.T("滑块或直接输入数字；1000 ms = 1 秒"), 20, 256, 704, 28).ForeColor = OreTheme.Muted;
        LabelAt(direct, L10n.T("保持原生范围；动作未结束时等待，不自动寻路"), 20, 294, 704, 28).ForeColor = OreTheme.Muted;
        var progress = StatusCard(pages[2], 692, 140);
        LabelAt(progress, L10n.T("拾取状态"), 20, 16, 704, 30);
        nearbyLootNote = (PixelLabel)LabelAt(progress, L10n.T("开启目标类型后按 F8 开始"), 20, 58, 704, 68);
        nearbyLootNote.PixelScale = .85f;
        // 组件安装是首次使用入口，固定在设置页顶部。
        var components = StatusCard(pages[3], 0, 220);
        LabelAt(components, L10n.T("全部游戏组件"), 20, 16, 704, 30);
        LabelAt(components, L10n.T("收集、战斗、装备回收统一安装；自动识别游戏目录"), 20, 58, 704, 28).ForeColor = OreTheme.Muted;
        componentInstallNote = (PixelLabel)LabelAt(components, L10n.T("首次使用或更新时，请保存进度并完全退出游戏。"), 20, 98, 704, 60);
        componentInstallNote.PixelScale = .85f;
        componentInstallButton = ButtonAt(components, L10n.T("安装 / 更新全部组件"), 20, 164, 704);
        componentInstallButton.Click += delegate
        {
            InstallAllComponents();
        };
        var native = Card(pages[3], 360, 148);
        LabelAt(native, L10n.T("前台也使用原生战斗"), 20, 16, 600, 30);
        combatNativeEnabled = CheckAt(native, "", 660, 14, 64, settings.CombatNative);
        LabelAt(native, L10n.T("原生模式不移动鼠标；组件缺失时等待"), 20, 56, 704, 30).ForeColor = OreTheme.Muted;
        nativeCombatNote = (PixelLabel)LabelAt(native, L10n.T("组件缺失时暂停对应动作"), 20, 98, 704, 40);
        nativeCombatNote.PixelScale = .8f;
        nearbyChestsEnabled.CheckedChanged += delegate
        {
            Changed();
        };
        nearbyItemsEnabled.CheckedChanged += delegate
        {
            Changed();
        };
        nearbyPotsEnabled.CheckedChanged += delegate
        {
            Changed();
        };
        nearbyFoodEnabled.CheckedChanged += delegate
        {
            Changed();
        };
        nearbyDirectEnabled.CheckedChanged += delegate
        {
            Changed();
        };
        nearbyMovingEnabled.CheckedChanged += delegate
        {
            Changed();
        };
        nearbyInterval.ValueChanged += delegate
        {
            NearbyTimingChanged(false, false);
        };
        nearbyFoodInterval.ValueChanged += delegate
        {
            NearbyTimingChanged(true, false);
        };
        nearbyIntervalSlider.ValueChanged += delegate
        {
            NearbyTimingChanged(false, true);
        };
        nearbyFoodIntervalSlider.ValueChanged += delegate
        {
            NearbyTimingChanged(true, true);
        };
        combatEncounterEnabled.CheckedChanged += delegate
        {
            StopNativeCombat();
            Changed();
        };
        combatNativeEnabled.CheckedChanged += delegate
        {
            StopNativeCombat();
            Changed();
        };
        combatEvadeEnabled.CheckedChanged += delegate
        {
            Changed();
        };
        combatAttackEnabled.CheckedChanged += delegate
        {
            Changed();
        };
        combatArtifactsEnabled.CheckedChanged += delegate
        {
            Changed();
        };
        combatRange.ValueChanged += delegate
        {
            CombatRangeChanged(false);
        };
        combatRangeSlider.ValueChanged += delegate
        {
            CombatRangeChanged(true);
        };
        var enabledCard = Card(pages[5], 0, 244);
        LabelAt(enabledCard, L10n.T("新拾取装备整理"), 20, 16, 600, 30);
        equipmentEnabled = CheckAt(enabledCard, "", 660, 14, 64, false);
        LabelAt(enabledCard, L10n.T("启停快捷键"), 20, 64, 460, 30);
        equipmentToggleKey = KeyAt(enabledCard, equipmentPolicy.ToggleKey, 523, 64 - 6, 201);
        equipmentToggleKey.BeforeChange = delegate
        {
            StopEquipment();
        };
        equipmentToggleKey.Changed += delegate
        {
            EquipmentHotkeyChanged();
        };
        equipmentState = (PixelLabel)LabelAt(enabledCard, L10n.T("整理已关闭；启用时记录已有物品"), 20, 102, 704, 35);
        equipmentState.PixelScale = .85f;
        equipmentState.ForeColor = OreTheme.Muted;
        var dependency = (PixelLabel)LabelAt(enabledCard, L10n.T("仅自动出售需 Blueprint Loader（蓝图加载器）和装备回收组件"), 20, 144, 704, 44);
        dependency.PixelScale = .8f;
        dependency.ForeColor = OreTheme.Muted;
        LabelAt(enabledCard, L10n.T("安装或更新请前往「设置」"), 20, 198, 704, 35).ForeColor = OreTheme.Muted;
        equipmentEnabled.CheckedChanged += delegate
        {
            SetEquipmentEnabled(equipmentEnabled.Checked);
        };
        BuildEquipmentVisual();
        var filters = Card(pages[5], 256, 480);
        LabelAt(filters, L10n.T("分类稀有度上限"), 20, 15, 704, 30);
        LabelAt(filters, L10n.T("装备类别"), 20, 56, 234, 28);
        LabelAt(filters, L10n.T("普通装备"), 276, 56, 212, 28);
        LabelAt(filters, L10n.T("灵魂风暴装备"), 510, 56, 214, 28);
        for (int i = 0; i < 7; i++)
        {
            int at = 92 + i * 52;
            equipmentCategoryTexts[i] = (PixelLabel)LabelAt(filters, EquipmentCategoryCaption((EquipmentCategory)i), 20, at + 8, 234, 28);
            normalRarity[i] = RaritySelect(filters, 276, at, (int)equipmentPolicy.Normal[i]);
            stormRarity[i] = RaritySelect(filters, 510, at, (int)equipmentPolicy.Storm[i]);
        }

        var protect = Card(pages[5], 748, 190);
        LabelAt(protect, L10n.T("已装备、锁定、附魔装备始终保留"), 20, 15, 704, 31);
        keepUpgrades = CheckAt(protect, L10n.T("保留更好的装备"), 20, 59, 338, equipmentPolicy.KeepUpgrades);
        keepMerchant = CheckAt(protect, L10n.T("保留商店购买"), 380, 59, 338, equipmentPolicy.KeepMerchant);
        keepQuest = CheckAt(protect, L10n.T("保留任务奖励"), 20, 103, 338, equipmentPolicy.KeepQuest);
        sellDuplicates = CheckAt(protect, L10n.T("清理较差的重复装备"), 380, 103, 338, equipmentPolicy.Duplicates);
        var safe = (PixelLabel)LabelAt(protect, L10n.T("未确认的属性与来源也会保留"), 20, 151, 704, 28);
        safe.ForeColor = OreTheme.Muted;
        safe.PixelScale = .85f;
        var preview = StatusCard(pages[5], 950, 420);
        LabelAt(preview, L10n.T("待卖清单预览"), 20, 16, 704, 30);
        LabelAt(preview, L10n.T("新拾取模式忽略建立基线时已有的全部物品"), 20, 55, 704, 39).ForeColor = OreTheme.Muted;
        previewNewEquipment = ButtonAt(preview, L10n.T("预览新拾取"), 20, 103, 338);
        previewAllEquipment = ButtonAt(preview, L10n.T("预览全部已有"), 380, 103, 344);
        previewNewEquipment.Click += async delegate
        {
            await PreviewEquipment(false);
        };
        previewAllEquipment.Click += async delegate
        {
            await PreviewEquipment(true);
        };
        equipmentResult = (PixelLabel)LabelAt(preview, L10n.T("尚未生成清单；当前不会出售装备"), 20, 163, 704, 74);
        equipmentResult.PixelScale = .9f;
        equipmentResult.ForeColor = OreTheme.Muted;
        var detail = ButtonAt(preview, L10n.T("查看清单与保留原因"), 20, 246, 338);
        detail.Click += delegate
        {
            ShowEquipmentPlan();
        };
        var export = ButtonAt(preview, L10n.T("导出待卖清单"), 380, 246, 344);
        export.Click += delegate
        {
            ExportEquipmentPlan();
        };
        sellExistingButton = ButtonAt(preview, L10n.T("回收符合规则的已有装备"), 20, 305, 704);
        sellExistingButton.Click += delegate
        {
            SellExistingEquipment();
        };
        LabelAt(preview, L10n.T("持续自动出售只处理新拾取物品；F9 停止整理"), 20, 362, 704, 38).ForeColor = OreTheme.Muted;
        foreach (var c in new[]
        {
            keepUpgrades,
            keepMerchant,
            keepQuest,
            sellDuplicates
        }

        )
            c.CheckedChanged += delegate
            {
                EquipmentPolicyChanged();
            };
        foreach (var c in normalRarity.Concat(stormRarity))
            c.SelectedIndexChanged += delegate
            {
                EquipmentPolicyChanged();
            };
    }

    OreSelect RaritySelect(Control parent, int x, int y, int value)
    {
        var select = new OreSelect
        {
            Location = new Point(x, y),
            Size = new Size(214, 40)
        };
        for (int i = 0; i < rarityLabels.Length; i++)
            select.Items.Add(EquipmentRarityCaption((EquipmentRarity)i));
        select.SelectedIndex = value;
        parent.Controls.Add(select);
        return select;
    }

    // 刷新整理页控件标题与选项，不改保存的保护策略。
    void RefreshEquipmentLanguage()
    {
        if (equipmentPolicy == null)
            return;
        equipmentUpdating = true;
        try
        {
            for (int i = 0; i < 7; i++)
                equipmentCategoryTexts[i].Text = EquipmentCategoryCaption((EquipmentCategory)i);
            foreach (var c in normalRarity.Concat(stormRarity))
                for (int i = 0; i < 5; i++)
                    c.Items[i] = EquipmentRarityCaption((EquipmentRarity)i);
        }
        finally
        {
            equipmentUpdating = false;
        }

        RefreshEquipmentVisualLanguage();
    }

    // 采集 UI 保护规则并更新预览；启用状态按现有流程重置。
    void EquipmentPolicyChanged()
    {
        if (equipmentUpdating || equipmentPolicy == null)
            return;
        StopEquipment();
        Arm(false);
        for (int i = 0; i < 7; i++)
        {
            equipmentPolicy.Normal[i] = (EquipmentRarity)normalRarity[i].SelectedIndex;
            equipmentPolicy.Storm[i] = (EquipmentRarity)stormRarity[i].SelectedIndex;
        }

        equipmentPolicy.KeepUpgrades = keepUpgrades.Checked;
        equipmentPolicy.KeepMerchant = keepMerchant.Checked;
        equipmentPolicy.KeepQuest = keepQuest.Checked;
        equipmentPolicy.Duplicates = sellDuplicates.Checked;
        equipmentPlan = null;
        equipmentVisualGeneration++;
        equipmentVisual.Clear();
        RefreshEquipmentVisual();
        equipmentResult.Text = L10n.T("规则已更新，请重新生成清单");
        if (interactive)
            try
            {
                equipmentPolicy.Save();
            }
            catch (Exception e)
            {
                ToolboxLog.Error("Equipment.Settings", e);
            }
    }

    // 采集当前角色/关卡的已有装备基线。
    void InitializeEquipmentBaseline()
    {
        inventoryBaseline.Reset();
        if (reader == null)
            return;
        try
        {
            var items = reader.ReadInventory().Select(EquipmentItem.Decode).ToList();
            inventoryBaseline.Capture(reader.InventorySession(), items);
            ToolboxLog.Write("Equipment.Baseline", "Captured " + items.Count + " existing records; no selling");
        }
        catch (Exception e)
        {
            ToolboxLog.Error("Equipment.Baseline", e);
        }
    }

    // 切换诊断/安装操作占用状态，避免同时执行自动动作。
    void ResearchBusy(bool value)
    {
        researchBusy = value;
        previewNewEquipment.Enabled = previewAllEquipment.Enabled = sellExistingButton.Enabled = equipmentVisualPreview.Enabled = !value;
    }

    // 只读生成整理计划，不发送实际回收指令。
    async Task PreviewEquipment(bool all)
    {
        if (researchBusy)
            return;
        StopEquipment();
        Arm(false);
        ResearchBusy(true);
        try
        {
            string session = null;
            var items = await Task.Run(delegate
            {
                using (var fresh = new HealthReader())
                {
                    session = fresh.InventorySession();
                    return fresh.ReadInventory().Select(EquipmentItem.Decode).ToList();
                }
            });
            if (closing || IsDisposed)
                return;
            if (!inventoryBaseline.Matches(session))
                inventoryBaseline.Capture(session, items);
            equipmentPlan = equipmentPolicy.Plan(items, inventoryBaseline.Existing, all);
            equipmentVisualGeneration++;
            equipmentVisual.Reset(equipmentPlan, false);
            equipmentVisualList.ResetScroll();
            RefreshEquipmentVisual();
            int ready = equipmentPlan.Count(d => d.Sell);
            equipmentResult.Text = String.Format(L10n.T("读取 {0} 件装备 · 待卖 {1} 件 · 保留 {2} 件\n仅预览，未出售任何物品"), equipmentPlan.Count(d => d.Item.Category != EquipmentCategory.Unsupported), ready, equipmentPlan.Count(d => d.Item.Category != EquipmentCategory.Unsupported && !d.Sell));
            ToolboxLog.Write("Equipment.Preview", "scope=" + (all ? "all" : "new") + " eligible=" + ready + "; no selling");
        }
        catch (Exception e)
        {
            if (!closing)
            {
                equipmentPlan = null;
                equipmentVisualGeneration++;
                equipmentVisual.Clear();
                equipmentVisual.Note = "清单读取失败，保持全部物品";
                RefreshEquipmentVisual();
                equipmentResult.Text = L10n.T("清单读取失败，保持全部物品") + "\n" + e.Message;
            }

            ToolboxLog.Error("Equipment.Preview", e);
        }
        finally
        {
            if (!IsDisposed)
                ResearchBusy(false);
        }
    }

    // 显示计划中的可回收与受保护物品及原因。
    void ShowEquipmentPlan()
    {
        if (equipmentPlan == null)
        {
            equipmentResult.Text = L10n.T("请先生成待卖清单");
            return;
        }

        using (var f = new Form
        {
            Text = L10n.T("待卖清单预览"),
            Size = new Size(940, 700),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = OreTheme.Background,
            Font = Font
        }

        )
        {
            var list = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = OreTheme.Background,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                GridColor = OreTheme.Edge,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = OreTheme.Card,
                    ForeColor = OreTheme.Text,
                    SelectionBackColor = OreTheme.Green,
                    SelectionForeColor = OreTheme.Text
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = OreTheme.Surface,
                    ForeColor = OreTheme.Text,
                    SelectionBackColor = OreTheme.Surface,
                    SelectionForeColor = OreTheme.Text
                }
            };
            f.Controls.Add(list);
            var icons = new DataGridViewImageColumn
            {
                HeaderText = "",
                Name = "icon",
                ImageLayout = DataGridViewImageCellLayout.Zoom,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = 54
            };
            list.Columns.Add(icons);
            list.RowTemplate.Height = 52;
            list.Columns.Add("type", L10n.T("装备类别"));
            list.Columns.Add("item", L10n.T("物品"));
            list.Columns.Add("rarity", EquipmentGamePresentation.Term("inventory_sort_rarity"));
            list.Columns.Add("power", EquipmentGamePresentation.Term("header_power"));
            list.Columns.Add("decision", L10n.T("判定"));
            foreach (var d in equipmentPlan.Where(x => x.Item.Category != EquipmentCategory.Unsupported).OrderByDescending(x => x.Sell))
                list.Rows.Add(EquipmentGamePresentation.Icon(d.Item), EquipmentCategoryCaption(d.Item.Category), EquipmentGamePresentation.Name(d.Item), EquipmentRarityCaption(d.Item.Rarity), d.Item.Power, d.Sell ? L10n.T(equipmentVisual.Live ? "符合回收规则" : "符合规则（未出售）") : EquipmentReasonCaption(d.Reason));
            f.ShowDialog(this);
        }
    }

    // 把规则原因转换为当前语言说明。
    static string ReasonCaption(string reason)
    {
        switch (reason)
        {
            case "Equipped":
                return "已装备";
            case "Locked":
                return "已锁定";
            case "Enchanted":
                return "已附魔";
            case "Existing":
                return "已有物品";
            case "Upgrade":
                return "优于已装备物品";
            case "UpgradeUnknown":
                return "缺少同类装备对照";
            case "SourceProtection":
                return "来源保护";
            case "OtherProtection":
            case "DataUnknown":
                return "属性未确认，保留";
            case "BestDuplicate":
                return "最佳重复装备";
            case "StormKept":
                return "保留灵魂风暴装备";
            default:
                return "超出稀有度上限";
        }
    }

    void ExportEquipmentPlan()
    {
        if (equipmentPlan == null)
        {
            equipmentResult.Text = L10n.T("请先生成待卖清单");
            return;
        }

        using (var d = new SaveFileDialog
        {
            Title = L10n.T("导出待卖清单"),
            Filter = "JSON (*.json)|*.json",
            FileName = "MCD2_equipment_preview_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json",
            OverwritePrompt = true
        }

        )
            if (d.ShowDialog(this) == DialogResult.OK)
                try
                {
                    File.WriteAllText(d.FileName, new JavaScriptSerializer().Serialize(new { previewOnly = true, policy = equipmentPolicy, decisions = equipmentPlan }), new UTF8Encoding(true));
                }
                catch (Exception e)
                {
                    ToolboxLog.Error("Equipment.Export", e);
                    equipmentResult.Text = e.Message;
                }
    }

    // 两列拾取开关的统一尺寸，缩放时仍由 RecordLayout 处理。
    CheckBox LootTypeCard(int x, int y, string title, string help, bool enabled)
    {
        var card = Card(pages[2], y, 116);
        card.Left = x;
        card.Width = 366;
        LabelAt(card, L10n.T(title), 16, 15, 272, 30);
        var toggle = CheckAt(card, "", 282, 14, 64, enabled);
        var note = (PixelLabel)LabelAt(card, L10n.T(help), 16, 58, 334, 44);
        note.PixelScale = .8f;
        note.ForeColor = OreTheme.Muted;
        return toggle;
    }

    // 统计控件树中的实际安装按钮，翻译后的标题通过规范键还原。
    static int CountInstallButtons(Control parent)
    {
        return parent.Controls.Cast<Control>().Sum(c => (c is Button && L10n.Canonical(c.Text) == "安装 / 更新全部组件" ? 1 : 0) + CountInstallButtons(c));
    }
}
