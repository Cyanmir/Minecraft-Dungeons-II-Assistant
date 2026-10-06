// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：创建自动战斗与装备整理页面，并绑定装备保护规则。增加控件时同时检查设置读取、Changed/Save、语言刷新与页面尺寸；整理预览只显示计划，不执行出售。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
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
        var encounter = Card(pages[4], 0, 120);
        LabelAt(encounter, L10n.T("遇到敌人自动攻击"), 20, 16, 600, 30);
        combatEncounterEnabled = CheckAt(encounter, "", 660, 14, 64, settings.CombatEncounter);
        LabelAt(encounter, L10n.T("需原生模式；遇敌主动近战，允许手动行走，不寻路"), 20, 58, 704, 28).ForeColor = OreTheme.Muted;
        LabelAt(encounter, L10n.T("法器仍由下方开关与勾选槽位控制，无需等敌人先出手"), 20, 87, 704, 28).ForeColor = OreTheme.Muted;
        var attack = Card(pages[4], 132, 158);
        LabelAt(attack, L10n.T("附近敌人原地近战"), 20, 16, 600, 30);
        combatAttackEnabled = CheckAt(attack, "", 660, 14, 64, settings.CombatAttack);
        LabelAt(attack, L10n.T("组件原生近战或按键模式，不追赶远处敌人"), 20, 58, 704, 36).ForeColor = OreTheme.Muted;
        LabelAt(attack, L10n.T("附近判定范围"), 20, 112, 460, 28);
        combatRange = Number(attack, 523, 104, 80, 500, settings.CombatRange, 201);
        var proactive = Card(pages[4], 302, 128);
        LabelAt(proactive, L10n.T("战斗提前使用法器"), 20, 16, 600, 30);
        combatArtifactsEnabled = CheckAt(proactive, "", 660, 14, 64, settings.CombatArtifacts);
        LabelAt(proactive, L10n.T("遇敌攻击开启后主动使用，否则等待敌人攻击你"), 20, 58, 704, 31).ForeColor = OreTheme.Muted;
        LabelAt(proactive, L10n.T("药水仍只在低血量时使用"), 20, 91, 704, 27).ForeColor = OreTheme.Muted;
        var threatCard = Card(pages[4], 442, 148);
        LabelAt(threatCard, L10n.T("攻击预警（实验）"), 20, 15, 580, 28);
        threatEnabled = CheckAt(threatCard, "", 660, 14, 64, settings.ThreatEnabled);
        var threatHelp = (PixelLabel)LabelAt(threatCard, L10n.T("攻击动作或直线弹道接近时，提前使用已选法器"), 20, 51, 704, 28);
        threatHelp.PixelScale = .85f;
        threatHelp.ForeColor = OreTheme.Muted;
        var threatPotion = (PixelLabel)LabelAt(threatCard, L10n.T("药水仍受血量阈值限制；仅在游戏前台生效"), 20, 83, 704, 27);
        threatPotion.PixelScale = .85f;
        threatPotion.ForeColor = OreTheme.Muted;
        threatNote = (PixelLabel)LabelAt(threatCard, L10n.T("等待攻击预警数据"), 20, 116, 704, 24);
        threatNote.PixelScale = .8f;
        threatNote.ForeColor = OreTheme.Muted;
        var evade = Card(pages[4], 602, 158);
        LabelAt(evade, L10n.T("攻击自动闪避（实验）"), 20, 16, 600, 30);
        combatEvadeEnabled = CheckAt(evade, "", 660, 14, 64, settings.CombatEvade);
        LabelAt(evade, L10n.T("核对原生充能、危险和方向上的地面"), 20, 58, 704, 34).ForeColor = OreTheme.Muted;
        evadeRuntimeNote = (PixelLabel)LabelAt(evade, L10n.T("核对弹道与近战骨骼轨迹；未覆盖全部 Boss 攻击"), 20, 105, 704, 34);
        evadeRuntimeNote.ForeColor = OreTheme.Muted;
        evadeRuntimeNote.PixelScale = .85f;
        var runtime = Card(pages[4], 772, 148);
        LabelAt(runtime, L10n.T("战斗状态"), 20, 16, 704, 30);
        combatRuntimeNote = (PixelLabel)LabelAt(runtime, L10n.T("勾选功能后按 F8 开始，F9 随时停止"), 20, 55, 704, 38);
        combatRuntimeNote.PixelScale = .85f;
        LabelAt(runtime, L10n.T("仅游戏前台执行；打开菜单或手动操作时暂停近战"), 20, 103, 704, 31).ForeColor = OreTheme.Muted;
        var native = Card(pages[4], 932, 208);
        LabelAt(native, L10n.T("原生战斗组件（实验）"), 20, 16, 600, 30);
        combatNativeEnabled = CheckAt(native, "", 660, 14, 64, settings.CombatNative);
        LabelAt(native, L10n.T("近战、各类法器与安全方向翻滚；鼠标不动"), 20, 58, 704, 32).ForeColor = OreTheme.Muted;
        LabelAt(native, L10n.T("法器支持蓄力、限时引导和目标瞄准；待人工测试"), 20, 95, 704, 32).ForeColor = OreTheme.Muted;
        nativeCombatNote = (PixelLabel)LabelAt(native, L10n.T("勾选原生模式后，组件缺失时暂停战斗动作"), 20, 130, 704, 30);
        nativeCombatNote.PixelScale = .8f;
        var installCombat = ButtonAt(native, L10n.T("安装 / 更新原生战斗组件"), 20, 166, 704);
        installCombat.Click += delegate
        {
            InstallCombatComponent();
        };
        var chest = Card(pages[4], 1152, 120);
        LabelAt(chest, L10n.T("附近宝箱交互（实验）"), 20, 16, 600, 30);
        nearbyChestsEnabled = CheckAt(chest, "", 660, 14, 64, settings.NearbyChests);
        LabelAt(chest, L10n.T("已有宝箱使用原生交互；每个目标只请求一次"), 20, 58, 704, 46).ForeColor = OreTheme.Muted;
        var loot = Card(pages[4], 1284, 120);
        LabelAt(loot, L10n.T("附近装备 / 附魔书 / TNT 拾取（实验）"), 20, 16, 600, 30);
        nearbyItemsEnabled = CheckAt(loot, "", 660, 14, 64, settings.NearbyItems);
        LabelAt(loot, L10n.T("拾取附近装备和附魔书；TNT 仅拾取携带"), 20, 58, 704, 46).ForeColor = OreTheme.Muted;
        var pot = Card(pages[4], 1416, 120);
        LabelAt(pot, L10n.T("附近绿宝石罐（实验）"), 20, 16, 600, 30);
        nearbyPotsEnabled = CheckAt(pot, "", 660, 14, 64, settings.NearbyPots);
        LabelAt(pot, L10n.T("原生近战破坏大小罐；携带 TNT 时跳过；鼠标不动"), 20, 58, 704, 46).ForeColor = OreTheme.Muted;
        var food = Card(pages[4], 1548, 120);
        LabelAt(food, L10n.T("附近食物自动食用（实验）"), 20, 16, 600, 30);
        nearbyFoodEnabled = CheckAt(food, "", 660, 14, 64, settings.NearbyFood);
        LabelAt(food, L10n.T("只处理已核实食物，核对消耗与原生效果；鼠标不动"), 20, 58, 704, 46).ForeColor = OreTheme.Muted;
        var direct = Card(pages[4], 1680, 520);
        LabelAt(direct, L10n.T("附近原生交互（实验）"), 20, 16, 600, 30);
        nearbyDirectEnabled = CheckAt(direct, "", 660, 14, 64, settings.NearbyDirect);
        LabelAt(direct, L10n.T("需要蓝图加载器及直接收集组件；不移动鼠标，不需选中"), 20, 58, 704, 44).ForeColor = OreTheme.Muted;
        LabelAt(direct, L10n.T("移动交互和间隔可自定义；需更新直接收集组件"), 20, 104, 704, 31).ForeColor = OreTheme.Muted;
        LabelAt(direct, L10n.T("允许手动移动时交互"), 20, 150, 600, 30);
        nearbyMovingEnabled = CheckAt(direct, "", 660, 148, 64, settings.NearbyAllowMoving);
        LabelAt(direct, L10n.T("通用交互间隔"), 20, 202, 704, 30);
        nearbyInterval = Number(direct, 523, 239, 100, 30000, settings.NearbyIntervalMs, 201);
        nearbyInterval.Suffix = "ms";
        nearbyIntervalSlider = new OreSlider
        {
            Location = new Point(20, 242),
            Size = new Size(482, 39),
            Minimum = 0,
            Maximum = 1000,
            Value = NearbyIntervalScale.Position(settings.NearbyIntervalMs)
        };
        direct.Controls.Add(nearbyIntervalSlider);
        LabelAt(direct, L10n.T("食物食用间隔"), 20, 298, 704, 30);
        nearbyFoodInterval = Number(direct, 523, 335, 100, 30000, settings.NearbyFoodIntervalMs, 201);
        nearbyFoodInterval.Suffix = "ms";
        nearbyFoodIntervalSlider = new OreSlider
        {
            Location = new Point(20, 338),
            Size = new Size(482, 39),
            Minimum = 0,
            Maximum = 1000,
            Value = NearbyIntervalScale.Position(settings.NearbyFoodIntervalMs)
        };
        direct.Controls.Add(nearbyFoodIntervalSlider);
        LabelAt(direct, L10n.T("拖动滑块或点击数字输入；1000 ms = 1 秒"), 20, 392, 704, 30).ForeColor = OreTheme.Muted;
        LabelAt(direct, L10n.T("最小尝试间隔；原生动作未结束时等待"), 20, 426, 704, 30).ForeColor = OreTheme.Muted;
        var installNearby = ButtonAt(direct, L10n.T("安装 / 更新直接收集组件"), 20, 465, 704);
        installNearby.Click += delegate
        {
            InstallNearbyComponent();
        };
        var progress = Card(pages[4], 2212, 184);
        LabelAt(progress, L10n.T("附近目标状态"), 20, 16, 704, 30);
        nearbyLootNote = (PixelLabel)LabelAt(progress, L10n.T("勾选附近交互后开始检测\n不寻路；成功结果仍需游戏确认"), 20, 59, 704, 76);
        nearbyLootNote.PixelScale = .85f;
        LabelAt(progress, L10n.T("F9 停止所有自动操作"), 20, 141, 704, 28).ForeColor = OreTheme.Muted;
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
            Changed();
        };
        var enabledCard = Card(pages[5], 0, 276);
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
        equipmentState = (PixelLabel)LabelAt(enabledCard, L10n.T("整理已关闭；启用时记录已有物品"), 20, 111, 704, 45);
        equipmentState.PixelScale = .85f;
        equipmentState.ForeColor = OreTheme.Muted;
        var dependency = (PixelLabel)LabelAt(enabledCard, L10n.T("仅自动出售需 Blueprint Loader（蓝图加载器）和装备回收组件"), 20, 160, 704, 44);
        dependency.PixelScale = .8f;
        dependency.ForeColor = OreTheme.Muted;
        var install = ButtonAt(enabledCard, L10n.T("安装 / 更新装备回收组件"), 20, 215, 704);
        install.Click += delegate
        {
            InstallEquipmentComponent();
        };
        equipmentEnabled.CheckedChanged += delegate
        {
            SetEquipmentEnabled(equipmentEnabled.Checked);
        };
        BuildEquipmentVisual();
        var filters = Card(pages[5], 728, 480);
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

        var protect = Card(pages[5], 1220, 190);
        LabelAt(protect, L10n.T("已装备、锁定、附魔装备始终保留"), 20, 15, 704, 31);
        keepUpgrades = CheckAt(protect, L10n.T("保留更好的装备"), 20, 59, 338, equipmentPolicy.KeepUpgrades);
        keepMerchant = CheckAt(protect, L10n.T("保留商店购买"), 380, 59, 338, equipmentPolicy.KeepMerchant);
        keepQuest = CheckAt(protect, L10n.T("保留任务奖励"), 20, 103, 338, equipmentPolicy.KeepQuest);
        sellDuplicates = CheckAt(protect, L10n.T("清理较差的重复装备"), 380, 103, 338, equipmentPolicy.Duplicates);
        var safe = (PixelLabel)LabelAt(protect, L10n.T("未确认的属性与来源也会保留"), 20, 151, 704, 28);
        safe.ForeColor = OreTheme.Muted;
        safe.PixelScale = .85f;
        var preview = Card(pages[5], 1422, 420);
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

    // 创建与品质枚举一致的选择控件。
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
            var items = reader.ReadInventoryAudit().Select(EquipmentItem.Decode).ToList();
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
                    return fresh.ReadInventoryAudit().Select(EquipmentItem.Decode).ToList();
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
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
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

    // 离线检查保护控件、语言与计划展示绑定。
    void TestEquipmentUi()
    {
        if (equipmentEnabled.Checked || equipmentPolicy.ToggleKey != 0x76 || equipmentToggleKey.KeyCode != 0x76)
            throw new Exception("Equipment startup / default hotkey failed");
        if (normalRarity.Any(c => c.SelectedIndex != 1) || stormRarity.Any(c => c.SelectedIndex != 0) || !keepUpgrades.Checked || !keepMerchant.Checked || !keepQuest.Checked || sellDuplicates.Checked || researchBusy || equipmentPlan != null)
            throw new Exception("Equipment defaults or preview-only startup failed");
        normalRarity[0].SelectedIndex = 3;
        stormRarity[0].SelectedIndex = 2;
        sellDuplicates.Checked = true;
        if (equipmentPolicy.Normal[0] != EquipmentRarity.Special || equipmentPolicy.Storm[0] != EquipmentRarity.Rare || !equipmentPolicy.Duplicates || equipmentPolicy.Normal[1] != EquipmentRarity.Common || armed)
            throw new Exception("Independent equipment policy UI binding failed");
        var copy = new JavaScriptSerializer().Deserialize<EquipmentPolicy>(new JavaScriptSerializer().Serialize(equipmentPolicy));
        if (copy.Normal[0] != EquipmentRarity.Special || copy.Storm[0] != EquipmentRarity.Rare || !copy.KeepMerchant || !copy.Duplicates)
            throw new Exception("Equipment policy serialization failed");
        normalRarity[0].SelectedIndex = 1;
        stormRarity[0].SelectedIndex = 0;
        sellDuplicates.Checked = false;
        for (int lang = 0; lang < 6; lang++)
        {
            SetLanguage(lang);
            if (equipmentCategoryTexts[0].Text != EquipmentCategoryCaption(EquipmentCategory.Melee) || (string)normalRarity[0].Items[1] != EquipmentRarityCaption(EquipmentRarity.Common))
                throw new Exception("Official game equipment language switching failed");
            if (normalRarity[0].SelectedIndex != 1 || stormRarity[0].SelectedIndex != 0 || !equipmentPolicy.KeepMerchant || armed)
                throw new Exception("Language switching changed equipment policy");
        }

        SetLanguage(0);
    }

    // 导出只读计划到用户指定路径。
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
}
