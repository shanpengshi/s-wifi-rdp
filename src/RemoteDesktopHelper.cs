using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Net.NetworkInformation;

[assembly: System.Reflection.AssemblyTitle("远程桌面连接助手")]
[assembly: System.Reflection.AssemblyDescription("输入校园网 IPv4 地址，启动 Windows 远程桌面。")]
[assembly: System.Reflection.AssemblyVersion("1.3.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.3.0.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

namespace CampusRemoteDesktop
{
    public static class ConnectionSettings
    {
        public const string DefaultAddress = "";

        public static bool TryNormalizeAddress(string input, out string address)
        {
            address = null;
            string[] parts = (input ?? "").Trim().Split('.');
            if (parts.Length != 4) return false;
            string[] normalized = new string[4];
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length < 1 || parts[i].Length > 3) return false;
                foreach (char character in parts[i])
                    if (character < '0' || character > '9') return false;
                int value;
                if (!Int32.TryParse(parts[i], NumberStyles.None,
                    CultureInfo.InvariantCulture, out value) || value > 255) return false;
                normalized[i] = value.ToString(CultureInfo.InvariantCulture);
            }
            address = String.Join(".", normalized);
            return address != "0.0.0.0" && address != "255.255.255.255";
        }

        public static string SettingsPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last-ip.txt"); }
        }

        public static string LoadAddress()
        {
            try
            {
                string address;
                if (File.Exists(SettingsPath) &&
                    TryNormalizeAddress(File.ReadAllText(SettingsPath, Encoding.UTF8), out address))
                    return address;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return DefaultAddress;
        }

        public static ProcessStartInfo CreateStartInfo(string input)
        {
            string address;
            if (!TryNormalizeAddress(input, out address))
                throw new ArgumentException("请输入有效的 IPv4 地址。", "input");
            return new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mstsc.exe"),
                Arguments = "/v:" + address,
                UseShellExecute = false
            };
        }

        public static string ReadPreference(string key)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "helper-settings.ini");
                if (File.Exists(path))
                    foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                        if (line.StartsWith(key + "=", StringComparison.Ordinal)) return line.Substring(key.Length + 1);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return "";
        }

        public static void SavePreferences(bool isHost, string controller, string adapterId)
        {
            string normalized;
            if (!TryNormalizeAddress(controller, out normalized)) normalized = "";
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "helper-settings.ini"),
                "role=" + (isHost ? "host" : "controller") + Environment.NewLine +
                "controller=" + normalized + Environment.NewLine +
                "adapter=" + (adapterId ?? "") + Environment.NewLine, new UTF8Encoding(false));
        }
    }

    public sealed partial class ConnectionForm : Form
    {
        private readonly TextBox addressBox;
        private readonly TextBox controllerBox;
        private readonly Label statusLabel;
        private readonly Button connectButton;
        private readonly Button queryButton;
        private readonly ComboBox adapterBox;
        private readonly Label networkLabel;
        private readonly Label hostAddressLabel;
        private readonly Label controllerAddressLabel;
        private readonly Label footer;
        private readonly RadioButton controllerRole;
        private readonly RadioButton hostRole;
        private readonly Timer refreshTimer;
        private LocalNetworkAddress selectedAddress;
        private bool updating;
        private bool subscribed;
        private readonly Color muted = Color.FromArgb(99, 115, 136);

        public ConnectionForm() : this(ConnectionSettings.LoadAddress()) { }

        public ConnectionForm(string initialAddress) : this(initialAddress, true) { }

        public ConnectionForm(string initialAddress, bool autoDetect)
        {
            Text = "远程桌面连接助手";
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(720, 590);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            try
            {
                using (Stream iconStream = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("CampusRemoteDesktop.AppIcon"))
                using (Icon embeddedIcon = new Icon(iconStream))
                    Icon = (Icon)embeddedIcon.Clone();
            }
            catch { Icon = SystemIcons.Application; }

            Panel accent = new Panel { BackColor = Color.FromArgb(39, 99, 223), Dock = DockStyle.Top, Height = 5 };
            Controls.Add(accent);
            Label heading = MakeLabel("远程桌面连接助手", 34, 24, 650, 40, Color.FromArgb(26, 42, 66));
            heading.Font = new Font("Microsoft YaHei UI", 21F, FontStyle.Bold);
            Controls.Add(heading);
            Controls.Add(MakeLabel("一键查询本机地址，分别管理控制端与被控端。", 36, 73, 650, 25, muted));

            controllerRole = new RadioButton { Text = "本机是控制端", Location = new Point(36, 108), Size = new Size(190, 28), TabIndex = 0 };
            hostRole = new RadioButton { Text = "本机是被控端", Location = new Point(248, 108), Size = new Size(330, 28), TabIndex = 1 };
            Controls.Add(controllerRole);
            Controls.Add(hostRole);
            bool startAsHost = autoDetect && ConnectionSettings.ReadPreference("role") == "host";
            hostRole.Checked = startAsHost;
            controllerRole.Checked = !startAsHost;

            Panel networkPanel = new Panel { Location = new Point(32, 148), Size = new Size(656, 128), BackColor = Color.FromArgb(245, 248, 253) };
            networkPanel.Controls.Add(MakeLabel("本机网卡 · 多张网卡时可手动选择", 16, 9, 620, 25, Color.FromArgb(39, 54, 76)));
            adapterBox = new ComboBox
            {
                Name = "adapterBox", DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(16, 38), Size = new Size(448, 30), DropDownWidth = 624, TabIndex = 2,
                AccessibleName = "选择本机网卡"
            };
            networkPanel.Controls.Add(adapterBox);
            queryButton = MakeButton("查询并自动填入", 480, 35, 160, 35, false);
            queryButton.TabIndex = 3;
            queryButton.Click += delegate { RefreshNetwork(true); };
            networkPanel.Controls.Add(queryButton);
            networkLabel = MakeLabel("点击查询，获取 IPv4、网关、掩码和 MAC 地址。", 16, 78, 624, 44, muted);
            networkLabel.Font = new Font("Microsoft YaHei UI", 9F);
            networkPanel.Controls.Add(networkLabel);
            Controls.Add(networkPanel);

            hostAddressLabel = MakeLabel("被控电脑 IPv4（连接目标）", 34, 295, 320, 25, Color.FromArgb(39, 54, 76));
            controllerAddressLabel = MakeLabel("控制端 IPv4（本机 · 自动填入）", 368, 295, 322, 25, Color.FromArgb(39, 54, 76));
            Controls.Add(hostAddressLabel);
            Controls.Add(controllerAddressLabel);

            addressBox = new TextBox
            {
                Name = "hostAddressBox",
                Location = new Point(36, 326),
                Size = new Size(312, 37),
                Font = new Font("Segoe UI", 16F),
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 64,
                Text = initialAddress,
                TabIndex = 4,
                AccessibleName = "被控电脑 IPv4 地址"
            };
            Controls.Add(addressBox);
            string savedController;
            if (!ConnectionSettings.TryNormalizeAddress(ConnectionSettings.ReadPreference("controller"), out savedController)) savedController = "";
            controllerBox = new TextBox
            {
                Name = "controllerAddressBox", Location = new Point(370, 326), Size = new Size(314, 37),
                Font = new Font("Segoe UI", 16F), BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 64, Text = savedController, TabIndex = 5,
                AccessibleName = "控制端 IPv4 地址"
            };
            Controls.Add(controllerBox);
            Button copyInfo = MakeButton("复制本机信息", 36, 382, 312, 36, false);
            copyInfo.TabIndex = 6;
            copyInfo.Click += CopyLocalInfo;
            Controls.Add(copyInfo);
            Button copyFirewall = MakeButton("复制控制端放行命令", 370, 382, 314, 36, false);
            copyFirewall.TabIndex = 7;
            copyFirewall.Click += CopyFirewall;
            Controls.Add(copyFirewall);

            connectButton = MakeButton("连接远程桌面", 36, 437, 648, 46, true);
            connectButton.TabIndex = 8;
            connectButton.Click += Connect;
            Controls.Add(connectButton);
            AcceptButton = connectButton;

            statusLabel = MakeLabel("就绪", 34, 494, 650, 42, muted);
            statusLabel.Font = new Font("Microsoft YaHei UI", 9.5F);
            statusLabel.AccessibleName = "连接状态";
            Controls.Add(statusLabel);
            footer = MakeLabel("", 34, 543, 650, 40, muted);
            footer.Font = new Font("Microsoft YaHei UI", 9F);
            Controls.Add(footer);
            refreshTimer = new Timer { Interval = 1000 };
            refreshTimer.Tick += delegate { refreshTimer.Stop(); RefreshNetwork(false); };
            adapterBox.SelectedIndexChanged += delegate
            {
                if (!updating) SelectLocalAddress(adapterBox.SelectedItem as LocalNetworkAddress, true);
            };
            hostRole.CheckedChanged += delegate { if (!updating) ApplyRole(true); };
            InstallResponsiveLayout(autoDetect);
            ApplyRole(false);
            Shown += delegate
            {
                if (autoDetect)
                {
                    RefreshNetwork(true);
                    NetworkChange.NetworkAddressChanged += NetworkAddressChanged;
                    subscribed = true;
                }
                addressBox.Focus(); addressBox.SelectAll();
            };
            FormClosing += delegate { if (autoDetect) SaveCurrentFields(); };
            FormClosed += delegate
            {
                if (subscribed) NetworkChange.NetworkAddressChanged -= NetworkAddressChanged;
                subscribed = false;
                refreshTimer.Stop(); refreshTimer.Dispose();
            };
        }

        private Button MakeButton(string text, int x, int y, int width, int height, bool primary)
        {
            Button button = new Button
            {
                Text = text, Location = new Point(x, y), Size = new Size(width, height),
                BackColor = primary ? Color.FromArgb(39, 99, 223) : Color.FromArgb(241, 245, 252),
                ForeColor = primary ? Color.White : Color.FromArgb(39, 79, 148),
                FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", primary ? 12F : 10F, primary ? FontStyle.Bold : FontStyle.Regular),
                Cursor = Cursors.Hand, UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(28, 82, 194) : Color.FromArgb(224, 235, 252);
            return button;
        }

        private void ApplyRole(bool switched)
        {
            bool isHost = hostRole.Checked;
            hostAddressLabel.Text = isHost ? "被控电脑 IPv4（本机 · 自动填入）" : "被控电脑 IPv4（连接目标）";
            controllerAddressLabel.Text = isHost ? "控制端 IPv4（填写另一台电脑地址）" : "控制端 IPv4（本机 · 自动填入）";
            connectButton.Enabled = !isHost;
            connectButton.Text = isHost ? "被控端模式：请在另一台电脑发起连接" : "连接远程桌面";
            connectButton.BackColor = isHost ? Color.FromArgb(232, 237, 246) : Color.FromArgb(39, 99, 223);
            AcceptButton = isHost ? queryButton : connectButton;
            footer.Text = isHost ? "把本机信息发给控制端填写。放行命令需在本机的管理员 PowerShell 中执行。" :
                "查询只更新本机信息。被控电脑的新 IP 需在被控电脑查询后填写；账户密码由 Windows 处理。";
            if (switched && selectedAddress != null)
            {
                TextBox remoteBox = isHost ? controllerBox : addressBox;
                if (remoteBox.Text.Trim() == selectedAddress.Address) remoteBox.Clear();
            }
            if (selectedAddress != null) SelectLocalAddress(selectedAddress, true);
            else SetStatus(isHost ? "请查询本机 IP，并填写另一台控制端电脑的 IP" : "请查询本机 IP，再填写被控电脑地址", false);
        }

        private void RefreshNetwork(bool report)
        {
            queryButton.Enabled = false;
            try
            {
                string previousId = selectedAddress == null ? ConnectionSettings.ReadPreference("adapter") : selectedAddress.AdapterId;
                string previousAddress = selectedAddress == null ? "" : selectedAddress.Address;
                var addresses = NetworkDetails.ReadLocalAddresses();
                updating = true;
                adapterBox.Items.Clear();
                foreach (LocalNetworkAddress item in addresses) adapterBox.Items.Add(item);
                LocalNetworkAddress selected = null;
                foreach (LocalNetworkAddress item in addresses)
                    if (item.AdapterId == previousId && item.Address == previousAddress) { selected = item; break; }
                if (selected == null)
                    foreach (LocalNetworkAddress item in addresses)
                        if (item.AdapterId == previousId) { selected = item; break; }
                if (selected == null && addresses.Count > 0) selected = addresses[0];
                if (selected != null) adapterBox.SelectedItem = selected;
                updating = false;
                SelectLocalAddress(selected, report || selected == null || selected.Address != previousAddress);
            }
            catch (Exception error)
            {
                selectedAddress = null;
                (hostRole.Checked ? addressBox : controllerBox).Clear();
                networkLabel.Text = "未能读取本机网卡，请检查网络连接后重试。";
                SetStatus("查询失败：" + error.Message, true);
            }
            finally { updating = false; queryButton.Enabled = true; }
        }

        public void SelectLocalAddress(LocalNetworkAddress address, bool report)
        {
            selectedAddress = address;
            TextBox localBox = hostRole.Checked ? addressBox : controllerBox;
            if (address == null)
            {
                localBox.Clear();
                networkLabel.Text = "没有找到已连接的 IPv4 网卡，请连接校园 Wi-Fi 后重试。";
                if (report) SetStatus("未检测到可用本机 IPv4 地址", true);
                return;
            }
            localBox.Text = address.Address;
            networkLabel.Text = "网关：" + (address.Gateway.Length == 0 ? "无" : address.Gateway) +
                "    掩码：" + address.Mask + Environment.NewLine +
                "MAC：" + (address.MacAddress.Length == 0 ? "无" : address.MacAddress);
            if (report) SetStatus(address.IsVirtual ? "当前选择的是虚拟网卡；校园网直连请确认应使用的网卡" :
                "已填入" + (hostRole.Checked ? "被控端" : "控制端") + "本机 IP：" + address.Address, address.IsVirtual);
        }

        private void NetworkAddressChanged(object sender, EventArgs args)
        {
            if (IsDisposed || !IsHandleCreated || !subscribed) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (IsDisposed || !subscribed) return;
                    refreshTimer.Stop(); refreshTimer.Start();
                });
            }
            catch (InvalidOperationException) { }
        }

        private void CopyLocalInfo(object sender, EventArgs args)
        {
            RefreshNetwork(false);
            if (selectedAddress == null) return;
            try
            {
                Clipboard.SetText(NetworkDetails.Describe(selectedAddress, hostRole.Checked));
                SetStatus("已复制本机 IP、网关、掩码、MAC、电脑名和用户名", false);
            }
            catch (Exception error) { SetStatus("复制失败：" + error.Message, true); }
        }

        private void CopyFirewall(object sender, EventArgs args)
        {
            string controller;
            if (!ConnectionSettings.TryNormalizeAddress(controllerBox.Text, out controller))
            {
                SetStatus("请先查询或填写有效的控制端 IPv4 地址", true);
                controllerBox.Focus(); return;
            }
            if (hostRole.Checked && selectedAddress != null && controller == selectedAddress.Address)
            {
                SetStatus("控制端应填写另一台电脑的 IP，不能填写本机的被控端 IP", true);
                controllerBox.Focus(); return;
            }
            try
            {
                Clipboard.SetText(NetworkDetails.FirewallCommand(controller));
                SaveCurrentFields();
                SetStatus("已复制命令；请在被控电脑的管理员 PowerShell 中粘贴执行", false);
            }
            catch (Exception error) { SetStatus("复制失败：" + error.Message, true); }
        }

        private bool SaveCurrentFields()
        {
            try
            {
                string address;
                if (ConnectionSettings.TryNormalizeAddress(addressBox.Text, out address))
                    File.WriteAllText(ConnectionSettings.SettingsPath, address, new UTF8Encoding(false));
                ConnectionSettings.SavePreferences(hostRole.Checked, controllerBox.Text,
                    selectedAddress == null ? "" : selectedAddress.AdapterId);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private Label MakeLabel(string text, int x, int y, int width, int height, Color color)
        {
            return new Label
            {
                Text = text, Location = new Point(x, y), Size = new Size(width, height),
                ForeColor = color, AutoSize = false, UseMnemonic = false
            };
        }

        private void SetStatus(string text, bool error)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = error ? Color.FromArgb(184, 45, 48) : muted;
        }

        private void Connect(object sender, EventArgs eventArgs)
        {
            if (hostRole.Checked) return;
            string address;
            if (!ConnectionSettings.TryNormalizeAddress(addressBox.Text, out address))
            {
                SetStatus("请输入有效的 IPv4 地址，例如 192.0.2.10", true);
                addressBox.Focus();
                addressBox.SelectAll();
                return;
            }
            connectButton.Enabled = false;
            try
            {
                ProcessStartInfo startInfo = ConnectionSettings.CreateStartInfo(address);
                if (!File.Exists(startInfo.FileName))
                {
                    SetStatus("未找到 Windows 远程桌面程序 mstsc.exe", true);
                    return;
                }
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new IOException("未能启动远程桌面。");
                }
                addressBox.Text = address;
                bool saved = SaveCurrentFields();
                SetStatus(saved ? "已启动远程桌面，并保存地址" : "已启动远程桌面；当前目录无法保存地址", false);
            }
            catch (Exception error)
            {
                SetStatus("启动失败，请重试或检查系统远程桌面", true);
                MessageBox.Show(this, error.Message, "无法启动远程桌面", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { connectButton.Enabled = !hostRole.Checked; }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ConnectionForm());
        }
    }
}
