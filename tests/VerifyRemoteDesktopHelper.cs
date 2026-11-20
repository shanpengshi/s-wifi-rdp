using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CampusRemoteDesktop;

internal static class VerifyRemoteDesktopHelper
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (ConnectionSettings.DefaultAddress != "") throw new Exception("Public build must not have a personal default IP.");
        string normalized;
        string[] valid = { "192.0.2.10", "192.0.2.20", " 192.0.2.10 ", "192.000.002.010" };
        foreach (string input in valid)
            if (!ConnectionSettings.TryNormalizeAddress(input, out normalized)) throw new Exception("Rejected valid address: " + input);
        string[] invalid = { "", " ", "192.0.2", "192.0.2.256", "192.0.2.-1", "127.1", "example.com", "::1", "192.0.2.10:3389", "192.0.2.10 /admin", "192.0.2.10 & calc", "1.2.3.4\n/v:5.6.7.8", "0.0.0.0", "255.255.255.255" };
        foreach (string input in invalid)
            if (ConnectionSettings.TryNormalizeAddress(input, out normalized)) throw new Exception("Accepted invalid address: " + input);
        var start = ConnectionSettings.CreateStartInfo(" 192.0.2.10 ");
        if (start.Arguments != "/v:192.0.2.10" || start.UseShellExecute || !File.Exists(start.FileName))
            throw new Exception("Incorrect Windows RDP launch configuration.");
        string previewPath = Path.GetFullPath(args[0]);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string firewall = NetworkDetails.FirewallCommand("192.0.2.20");
        if (!firewall.Contains("$controllerIp = '192.0.2.20'") || !firewall.Contains("@('TCP', 'UDP')") || !firewall.Contains("-RemoteAddress $controllerIp"))
            throw new Exception("Firewall command has incorrect source address or protocol scope.");
        try { NetworkDetails.FirewallCommand("192.0.2.20'; calc; '"); throw new Exception("Command injection input accepted."); }
        catch (ArgumentException) { }
        Console.WriteLine("Local IPv4 candidates found: " + NetworkDetails.ReadLocalAddresses().Count);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(previewPath), "firewall-command-qa.ps1"), firewall);
        using (var form = new ConnectionForm("192.0.2.10", false))
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000);
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();
            TextBox addressBox = (TextBox)form.Controls.Find("hostAddressBox", true)[0];
            TextBox controllerBox = (TextBox)form.Controls.Find("controllerAddressBox", true)[0];
            ComboBox adapterBox = (ComboBox)form.Controls.Find("adapterBox", true)[0];
            var sampleController = new LocalNetworkAddress { AdapterId = "sample-controller", AdapterName = "WLAN", Address = "192.0.2.20", Mask = "255.255.255.0", Gateway = "192.0.2.1", MacAddress = "02-AA-BB-CC-DD-EE" };
            adapterBox.Items.Add(sampleController);
            adapterBox.SelectedIndex = 0;
            if (controllerBox.Text != "192.0.2.20" || addressBox.Text != "192.0.2.10")
                throw new Exception("Controller mode filled the wrong field.");
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
            }
            addressBox.Text = "192.0.2.999";
            typeof(ConnectionForm).GetMethod("Connect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(form, new object[] { null, EventArgs.Empty });
            bool hasError = false;
            foreach (Control control in form.Controls)
                if (control is Label && control.Text.StartsWith("请输入有效的 IPv4")) hasError = true;
            if (!hasError) throw new Exception("Invalid input feedback not displayed.");
            addressBox.Text = "192.0.2.10";
            foreach (Control control in form.Controls)
                if (control is RadioButton && control.Text.StartsWith("本机是被控端")) ((RadioButton)control).Checked = true;
            var sampleHost = new LocalNetworkAddress { AdapterId = "sample-host", AdapterName = "WLAN", Address = "192.0.2.10", Mask = "255.255.255.0", Gateway = "192.0.2.1", MacAddress = "02-AA-BB-CC-DD-FF" };
            adapterBox.Items.Clear();
            adapterBox.Items.Add(sampleHost);
            adapterBox.SelectedIndex = 0;
            controllerBox.Text = "192.0.2.20";
            if (addressBox.Text != "192.0.2.10") throw new Exception("Host mode did not fill the local host address.");
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(Path.Combine(Path.GetDirectoryName(previewPath), "rdp-helper-host-v12.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            if (!NetworkDetails.Describe(sampleHost, true).Contains("mstsc /v:192.0.2.10")) throw new Exception("Host connection info wrong.");
            form.SelectLocalAddress(null, true);
            if (addressBox.Text != "") throw new Exception("Disconnected host kept a stale address.");
            form.SelectLocalAddress(sampleHost, true);
            form.Close();
        }
        Console.WriteLine("PASS: address validation, safe mstsc arguments, network query, role-specific autofill, offline clearing, firewall script validation, both UI views.");
        return 0;
    }
}
