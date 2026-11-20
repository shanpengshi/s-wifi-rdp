using System;
using System.Drawing;
using System.Windows.Forms;

namespace CampusRemoteDesktop
{
    public sealed partial class ConnectionForm
    {
        private bool arranging;
        private Label titleLabel, subtitleLabel, adapterLabel;
        private Panel adapterPanel, accentPanel;
        private Button infoButton, firewallButton;

        private void InstallResponsiveLayout(bool fitScreen)
        {
            foreach (Control control in Controls)
            {
                if (control is Label && control.Text == "远程桌面连接助手") titleLabel = (Label)control;
                if (control is Label && control.Text.StartsWith("一键查询")) subtitleLabel = (Label)control;
                if (control is Panel && control.Contains(adapterBox)) adapterPanel = (Panel)control;
                else if (control is Panel) accentPanel = (Panel)control;
                if (control is Button && control.Text == "复制本机信息") infoButton = (Button)control;
                if (control is Button && control.Text == "复制控制端放行命令") firewallButton = (Button)control;
            }
            foreach (Control control in adapterPanel.Controls)
                if (control is Label && control != networkLabel) adapterLabel = (Label)control;
            accentPanel.Dock = DockStyle.None;
            AutoScroll = true;
            DoubleBuffered = true;
            MinimumSize = new Size(340, 300);
            Layout += delegate { ArrangeResponsive(); };
            foreach (Label label in new[] { statusLabel, footer, networkLabel, hostAddressLabel, controllerAddressLabel })
                label.TextChanged += delegate { PerformLayout(); };
            Shown += delegate
            {
                if (!fitScreen) return;
                Rectangle area = Screen.FromControl(this).WorkingArea;
                Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
                Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)),
                    Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
                PerformLayout();
            };
            PerformLayout();
        }

        private int LabelHeight(Label label, int width)
        {
            return TextRenderer.MeasureText(label.Text.Length == 0 ? " " : label.Text, label.Font,
                new Size(Math.Max(1, width), Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
        }

        private void ArrangeResponsive()
        {
            if (arranging || titleLabel == null || footer == null) return;
            arranging = true;
            try
            {
                // Font height follows WinForms DPI scaling and also respects larger UI fonts.
                float scale = Font.Height / 18F;
                Func<int, int> px = delegate(int n) { return Math.Max(1, (int)Math.Round(n * scale)); };
                int margin = px(24), gap = px(12);
                // Reserve a scrollbar gutter so adding a vertical scrollbar cannot cause layout oscillation.
                int width = Math.Max(1, ClientSize.Width - margin * 2 - SystemInformation.VerticalScrollBarWidth);
                int left = margin + AutoScrollPosition.X, y = px(20) + AutoScrollPosition.Y;
                bool narrow = width < px(620);
                Action<Control, int> row = delegate(Control control, int height)
                { control.SetBounds(left, y, width, height); y += height + gap; };
                accentPanel.SetBounds(0, AutoScrollPosition.Y, ClientSize.Width, px(5));
                row(titleLabel, LabelHeight(titleLabel, width));
                row(subtitleLabel, LabelHeight(subtitleLabel, width));
                int roleHeight = Math.Max(controllerRole.PreferredSize.Height, hostRole.PreferredSize.Height) + px(8);
                if (width < px(340)) { row(controllerRole, roleHeight); row(hostRole, roleHeight); }
                else
                {
                    controllerRole.SetBounds(left, y, width / 2, roleHeight);
                    hostRole.SetBounds(left + width / 2, y, width - width / 2, roleHeight);
                    y += roleHeight + gap;
                }
                int inset = px(12), inner = Math.Max(1, width - 2 * inset), ny = inset;
                int nh = LabelHeight(adapterLabel, inner);
                adapterLabel.SetBounds(inset, ny, inner, nh); ny += nh + gap;
                int buttonHeight = Math.Max(px(38), queryButton.Font.Height * 2 + px(8));
                foreach (Button button in new[] { queryButton, infoButton, firewallButton })
                    buttonHeight = Math.Max(buttonHeight, TextRenderer.MeasureText(button.Text, button.Font,
                        new Size(Math.Max(1, narrow ? inner : (inner - gap) / 2), Int32.MaxValue), TextFormatFlags.WordBreak).Height + px(20));
                int queryWidth = TextRenderer.MeasureText(queryButton.Text, queryButton.Font).Width + px(30);
                if (narrow)
                {
                    adapterBox.SetBounds(inset, ny, inner, adapterBox.PreferredHeight); ny += adapterBox.Height + gap;
                    queryButton.SetBounds(inset, ny, inner, buttonHeight); ny += buttonHeight + gap;
                }
                else
                {
                    adapterBox.SetBounds(inset, ny, Math.Max(1, inner - queryWidth - gap), adapterBox.PreferredHeight);
                    queryButton.SetBounds(inset + inner - queryWidth, ny, queryWidth, buttonHeight);
                    ny += Math.Max(buttonHeight, adapterBox.Height) + gap;
                }
                adapterBox.DropDownWidth = Math.Max(adapterBox.Width, Math.Min(px(700), Screen.FromControl(this).WorkingArea.Width - px(40)));
                nh = LabelHeight(networkLabel, inner);
                networkLabel.SetBounds(inset, ny, inner, nh); ny += nh + inset;
                row(adapterPanel, ny);
                if (narrow)
                {
                    row(hostAddressLabel, LabelHeight(hostAddressLabel, width));
                    row(addressBox, addressBox.PreferredHeight);
                    row(controllerAddressLabel, LabelHeight(controllerAddressLabel, width));
                    row(controllerBox, controllerBox.PreferredHeight);
                    row(infoButton, buttonHeight); row(firewallButton, buttonHeight);
                }
                else
                {
                    int half = (width - gap) / 2, right = left + half + gap;
                    int h = Math.Max(LabelHeight(hostAddressLabel, half), LabelHeight(controllerAddressLabel, half));
                    hostAddressLabel.SetBounds(left, y, half, h); controllerAddressLabel.SetBounds(right, y, width - half - gap, h); y += h + gap;
                    h = Math.Max(addressBox.PreferredHeight, controllerBox.PreferredHeight);
                    addressBox.SetBounds(left, y, half, h); controllerBox.SetBounds(right, y, width - half - gap, h); y += h + gap;
                    infoButton.SetBounds(left, y, half, buttonHeight); firewallButton.SetBounds(right, y, width - half - gap, buttonHeight); y += buttonHeight + gap;
                }
                int connectHeight = TextRenderer.MeasureText(connectButton.Text, connectButton.Font, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak).Height + px(24);
                row(connectButton, Math.Max(px(46), connectHeight));
                row(statusLabel, LabelHeight(statusLabel, width));
                row(footer, LabelHeight(footer, width));
                AutoScrollMinSize = new Size(0, y - AutoScrollPosition.Y + margin);
            }
            finally { arranging = false; }
        }
    }
}
