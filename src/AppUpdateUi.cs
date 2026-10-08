// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

sealed partial class ToolboxForm
{
    CheckBox updateStartup, updateAutomatic;
    OreSelect updateChannel;
    PixelLabel updateStatus;
    Button updateCheck, updateInstall;
    CancellationTokenSource updateCancel;
    AppUpdateOffer updateOffer;
    // updateBusy 限制并发；状态和参数分开保存，切换语言时无需重发网络请求。
    bool updateBusy;
    string updateMessage = "尚未检查更新";
    object[] updateArguments = new object[0];

    // 工具更新放在操作辅助之后；组件安装位于页首，分组间保留 12 个逻辑像素。
    void BuildUpdateSettings()
    {
        var card = Card(pages[3], 648, 368);
        LabelAt(card, L10n.T("工具更新"), 20, 16, 704, 30);
        LabelAt(card, L10n.T("启动时检查更新"), 20, 60, 590, 28);
        updateStartup = CheckAt(card, "", 660, 58, 64, settings.UpdateOnStartup);
        LabelAt(card, L10n.T("更新通道"), 20, 108, 360, 28);
        updateChannel = new OreSelect { Location = new Point(423, 98), Size = new Size(301, 44) };
        updateChannel.Items.AddRange(new object[] { L10n.T("正式版"), L10n.T("Dev 版") });
        updateChannel.SelectedIndex = settings.UpdateChannel;
        card.Controls.Add(updateChannel);
        LabelAt(card, L10n.T("自动下载安装更新"), 20, 156, 590, 28);
        updateAutomatic = CheckAt(card, "", 660, 154, 64, settings.UpdateAutomatically);
        var note = (PixelLabel)LabelAt(card, L10n.T("更新后重启工具，保留配置；Dev 版来自预发布。"), 20, 204, 704, 44);
        note.ForeColor = OreTheme.Muted;
        note.PixelScale = .85f;
        updateStatus = (PixelLabel)LabelAt(card, "", 20, 254, 704, 52);
        updateStatus.PixelScale = .85f;
        updateCheck = ButtonAt(card, L10n.T("检查更新"), 20, 312, 340);
        updateInstall = ButtonAt(card, L10n.T("下载并覆盖安装"), 384, 312, 340);
        updateInstall.Enabled = false;
        updateStartup.CheckedChanged += delegate { SaveUpdateSettings(); };
        updateAutomatic.CheckedChanged += delegate { SaveUpdateSettings(); };
        updateChannel.SelectedIndexChanged += delegate
        {
            SaveUpdateSettings();
            updateOffer = null;
            SetUpdateStatus("尚未检查更新");
            if (interactive && !updateBusy)
                CheckAppUpdates(false);
        };
        updateCheck.Click += delegate { CheckAppUpdates(false); };
        updateInstall.Click += delegate { InstallAppUpdate(false); };
        FormClosed += delegate
        {
            if (updateCancel != null)
                updateCancel.Cancel();
        };
        RefreshUpdateLanguage();
    }

    void SaveUpdateSettings()
    {
        ReadUpdateSettings();
        if (!interactive)
            return;
        try { settings.Save(); }
        catch (Exception error) { ToolboxLog.Error("Update.Settings", error); }
    }
    // 把三个更新控件的值合并到既有配置，保持其余游戏设置与迁移键名稳定。
    void ReadUpdateSettings()
    {
        if (updateStartup == null)
            return;
        settings.UpdateOnStartup = updateStartup.Checked;
        settings.UpdateAutomatically = updateAutomatic.Checked;
        settings.UpdateChannel = Math.Max(0, updateChannel.SelectedIndex);
    }
    // 动态状态保留规范中文键，切换语言时重新格式化，不影响下载任务。
    void SetUpdateStatus(string message, params object[] arguments)
    {
        updateMessage = message;
        updateArguments = arguments;
        if (updateStatus != null && !IsDisposed)
            updateStatus.Text = String.Format(L10n.T(message), arguments);
        if (updateInstall != null)
            updateInstall.Enabled = !updateBusy && updateOffer != null && updateOffer.Different;
    }
    void RefreshUpdateLanguage()
    {
        if (updateChannel == null)
            return;
        int selected = updateChannel.SelectedIndex;
        updateChannel.Items[0] = L10n.T("正式版");
        updateChannel.Items[1] = L10n.T("Dev 版");
        updateChannel.SelectedIndex = selected;
        updateCheck.Text = L10n.T("检查更新");
        updateInstall.Text = L10n.T("下载并覆盖安装");
        SetUpdateStatus(updateMessage, updateArguments);
    }

    // 每次正常启动检查一次；预览和命令行诊断窗口不会联网。
    async void CheckAppUpdates(bool startup)
    {
        if (updateBusy || !interactive || IsDisposed || closing)
            return;
        updateBusy = true;
        updateCheck.Enabled = updateChannel.Enabled = false;
        updateOffer = null;
        SetUpdateStatus("正在检查 GitHub 更新…");
        updateCancel = new CancellationTokenSource();
        var token = updateCancel.Token;
        int channel = settings.UpdateChannel;
        bool automatic = false;
        try
        {
            var offer = await Task.Run(() => AppUpdater.Check(channel, token), token);
            if (closing || IsDisposed)
                return;
            updateOffer = offer;
            var current = AppUpdater.ParseVersion(AppUpdater.CurrentVersion);
            if (offer == null)
                SetUpdateStatus("所选通道暂未发布可用版本");
            else if (!offer.Different)
                SetUpdateStatus("已是所选版本：{0}", offer.Tag);
            else if (current != null && AppUpdater.CompareVersions(offer.Tag, AppUpdater.CurrentVersion) < 0)
                SetUpdateStatus("所选版本 {0} 较旧，手动安装将降级", offer.Tag);
            else
            {
                SetUpdateStatus("可安装 {0}，当前版本 {1}", offer.Tag, AppUpdater.CurrentVersion);
                // 首次同版本包差异仍可手动覆盖；自动安装只接受版本升级，避免开发构建反复重启。
                automatic = startup && settings.UpdateAutomatically && current != null && AppUpdater.CompareVersions(offer.Tag, AppUpdater.CurrentVersion) > 0;
            }
            ToolboxLog.Write("Update.Check", "channel=" + channel + " release=" + (offer == null ? "none" : offer.Tag));
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!closing && !IsDisposed)
                SetUpdateStatus("更新检查失败，请稍后重试");
            ToolboxLog.Error("Update.Check", error);
        }
        finally
        {
            updateBusy = false;
            updateCancel.Dispose();
            updateCancel = null;
            if (!IsDisposed && !closing)
            {
                updateCheck.Enabled = updateChannel.Enabled = true;
                SetUpdateStatus(updateMessage, updateArguments);
            }
        }
        if (automatic && !closing && !IsDisposed)
            InstallAppUpdate(true);
    }

    // 按用户点击或已启用的自动安装选项下载；准备成功后才暂停游戏动作并退出。
    async void InstallAppUpdate(bool automatic)
    {
        if (updateBusy || updateOffer == null || !interactive || closing)
            return;
        var offer = updateOffer;
        var current = AppUpdater.ParseVersion(AppUpdater.CurrentVersion);
        if (!automatic && current != null && AppUpdater.CompareVersions(offer.Tag, AppUpdater.CurrentVersion) < 0 && MessageBox.Show(String.Format(L10n.T("将安装较旧版本 {0}，是否继续？"), offer.Tag), L10n.T("工具更新"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        updateBusy = true;
        updateCheck.Enabled = updateChannel.Enabled = false;
        SetUpdateStatus("正在下载更新：{0}%", 0);
        updateCancel = new CancellationTokenSource();
        var token = updateCancel.Token;
        var progress = new Progress<int>(value =>
        {
            if (!closing && !IsDisposed)
                SetUpdateStatus("正在下载更新：{0}%", value);
        });
        try
        {
            string manifest = await Task.Run(() => AppUpdater.Prepare(offer, token, value => ((IProgress<int>)progress).Report(value)), token);
            if (closing || IsDisposed)
                return;
            SetUpdateStatus("下载校验完成，正在重启并安装…");
            ReadSettings();
            settings.Save();
            AppUpdater.StartInstaller(manifest);
            // 绕过窗口退出动画，仍执行原有 FormClosing 清理、OFF 请求和输入释放。
            Arm(false);
            exitReady = true;
            Close();
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!closing && !IsDisposed)
                SetUpdateStatus("更新下载或安装失败，请重试");
            ToolboxLog.Error("Update.Install", error);
        }
        finally
        {
            updateBusy = false;
            updateCancel.Dispose();
            updateCancel = null;
            if (!IsDisposed && !closing)
            {
                updateCheck.Enabled = updateChannel.Enabled = true;
                SetUpdateStatus(updateMessage, updateArguments);
            }
        }
    }
}
