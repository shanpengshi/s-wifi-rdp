using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace CampusRemoteDesktop
{
    public sealed class LocalNetworkAddress
    {
        public string AdapterId;
        public string AdapterName;
        public string Description;
        public string Address;
        public string Mask;
        public string Gateway;
        public string MacAddress;
        public bool IsVirtual;
        public int Priority;
        public override string ToString()
        {
            return AdapterName + "  ·  " + Address + (IsVirtual ? "  [虚拟网卡]" : "");
        }
    }

    public static class NetworkDetails
    {
        public static List<LocalNetworkAddress> ReadLocalAddresses()
        {
            List<LocalNetworkAddress> addresses = new List<LocalNetworkAddress>();
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                try
                {
                    IPInterfaceProperties properties = adapter.GetIPProperties();
                    string gateway = "";
                    foreach (GatewayIPAddressInformation entry in properties.GatewayAddresses)
                        if (entry.Address.AddressFamily == AddressFamily.InterNetwork &&
                            entry.Address.ToString() != "0.0.0.0") { gateway = entry.Address.ToString(); break; }
                    string description = (adapter.Description + " " + adapter.Name).ToLowerInvariant();
                    bool isVirtual = description.Contains("virtual") || description.Contains("vmware") ||
                        description.Contains("vethernet") || description.Contains("hyper-v") ||
                        description.Contains("tailscale") || description.Contains("wireguard") ||
                        description.Contains("zerotier") || description.Contains("wintun") ||
                        description.Contains("tap-") || description.Contains("vpn") || description.Contains("loopback");
                    byte[] mac = adapter.GetPhysicalAddress().GetAddressBytes();
                    string macText = BitConverter.ToString(mac);
                    foreach (UnicastIPAddressInformation entry in properties.UnicastAddresses)
                    {
                        if (entry.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string address = entry.Address.ToString();
                        if (address.StartsWith("127.") || address.StartsWith("169.254.") || address == "0.0.0.0") continue;
                        int priority = gateway.Length > 0 ? 60 : 0;
                        if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) priority += 100;
                        else if (adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet) priority += 70;
                        if (address.StartsWith("10.")) priority += 25;
                        if (isVirtual) priority -= 250;
                        addresses.Add(new LocalNetworkAddress
                        {
                            AdapterId = adapter.Id, AdapterName = adapter.Name, Description = adapter.Description,
                            Address = address, Mask = entry.IPv4Mask == null ? "" : entry.IPv4Mask.ToString(),
                            Gateway = gateway, MacAddress = macText, IsVirtual = isVirtual, Priority = priority
                        });
                    }
                }
                catch (NetworkInformationException) { }
            }
            addresses.Sort(delegate(LocalNetworkAddress left, LocalNetworkAddress right)
            {
                int result = right.Priority.CompareTo(left.Priority);
                return result != 0 ? result : String.CompareOrdinal(left.AdapterName + left.Address, right.AdapterName + right.Address);
            });
            return addresses;
        }

        public static string Describe(LocalNetworkAddress address, bool isHost)
        {
            if (address == null) throw new ArgumentException("请先查询并选择本机网卡。");
            return (isHost ? "被控端（被控电脑）" : "控制端") + Environment.NewLine +
                "电脑名称：" + Environment.MachineName + Environment.NewLine +
                "Windows 用户：" + Environment.UserDomainName + "\\" + Environment.UserName + Environment.NewLine +
                "网卡：" + address.AdapterName + Environment.NewLine +
                "IPv4：" + address.Address + Environment.NewLine +
                "子网掩码：" + address.Mask + Environment.NewLine +
                "默认网关：" + address.Gateway + Environment.NewLine +
                "MAC：" + address.MacAddress + Environment.NewLine +
                (isHost ? "连接命令：mstsc /v:" + address.Address + Environment.NewLine : "");
        }

        public static string FirewallCommand(string input)
        {
            string address;
            if (!ConnectionSettings.TryNormalizeAddress(input, out address))
                throw new ArgumentException("请输入有效的控制端 IPv4 地址。");
            StringBuilder script = new StringBuilder();
            script.AppendLine("# 在被控电脑的管理员 PowerShell 中执行");
            script.AppendLine("# 仅添加/更新本工具的 RDP 放行规则，不会关闭防火墙");
            script.AppendLine("$ErrorActionPreference = 'Stop'");
            script.AppendLine("$controllerIp = '" + address + "'");
            script.AppendLine("foreach ($protocol in @('TCP', 'UDP')) {");
            script.AppendLine("    $ruleName = 'SWIFI-RDP-' + $protocol");
            script.AppendLine("    if (Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue) {");
            script.AppendLine("        Set-NetFirewallRule -Name $ruleName -Direction Inbound -Action Allow -Protocol $protocol -LocalPort 3389 -RemoteAddress $controllerIp -Profile Any -Enabled True");
            script.AppendLine("    } else {");
            script.AppendLine("        New-NetFirewallRule -Name $ruleName -DisplayName ('S-WiFi RDP ' + $protocol) -Direction Inbound -Action Allow -Protocol $protocol -LocalPort 3389 -RemoteAddress $controllerIp -Profile Any -Enabled True | Out-Null");
            script.AppendLine("    }");
            script.AppendLine("}");
            script.AppendLine("Write-Host ('已更新本机 RDP 规则，放行控制端：' + $controllerIp)");
            return script.ToString();
        }
    }
}
