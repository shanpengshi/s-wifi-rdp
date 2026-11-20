using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CampusRemoteDesktop;

internal static class LayoutVerification
{
    private static void Collect(Control root, Dictionary<Control, Font> fonts)
    {
        fonts.Add(root, root.Font);
        foreach (Control c in root.Controls) Collect(c, fonts);
    }
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        int cases = 0;
        foreach (float scale in new[] { 1F, 1.25F, 1.5F, 1.75F, 2F, 2.5F, 3F })
        foreach (Size size in new[] { new Size(360, 480), new Size(720, 590), new Size(1280, 720), new Size(1920, 1080) })
        using (ConnectionForm form = new ConnectionForm("192.0.2.10", false))
        {
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.ShowInTaskbar = false;
            form.Show(); Application.DoEvents();
            Dictionary<Control, Font> fonts = new Dictionary<Control, Font>(); Collect(form, fonts);
            form.SuspendLayout();
            foreach (KeyValuePair<Control, Font> item in fonts)
                item.Key.Font = new Font(item.Value.FontFamily, item.Value.SizeInPoints * scale, item.Value.Style);
            form.ClientSize = size;
            form.ResumeLayout(true); form.PerformLayout(); Application.DoEvents();
            foreach (Control c in form.Controls)
            {
                if (c.Width > form.ClientSize.Width || c.Left < 0 || c.Right > form.ClientSize.Width)
                    throw new Exception("Horizontal overflow at scale " + scale + ": " + c.Text);
                Label label = c as Label;
                Button button = c as Button;
                if (button != null && TextRenderer.MeasureText(button.Text, button.Font, new Size(button.Width, Int32.MaxValue), TextFormatFlags.WordBreak).Height > button.Height)
                    throw new Exception("Clipped button at scale " + scale + ": " + button.Text);
                if (label != null && TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height > label.Height)
                    throw new Exception("Clipped label at scale " + scale + ": " + label.Text);
                foreach (Control other in form.Controls)
                    if (c != other && c.Bounds.IntersectsWith(other.Bounds)) throw new Exception("Overlapping controls: " + c.Text + " / " + other.Text);
            }
            Control host = form.Controls.Find("hostAddressBox", true)[0];
            Control controller = form.Controls.Find("controllerAddressBox", true)[0];
            if (size.Width == 360 && controller.Top <= host.Top) throw new Exception("Narrow mode did not stack fields.");
            form.AutoScrollPosition = new Point(0, form.AutoScrollMinSize.Height);
            Application.DoEvents();
            foreach (Control c in form.Controls)
                if (c.AccessibleName == "连接状态" && c.Bottom > form.ClientSize.Height) throw new Exception("Status cannot be reached by scrolling.");
            form.AutoScrollPosition = Point.Empty;
            if ((scale == 1F && size.Width == 720) || (scale == 2F && size.Width == 1280) || (scale == 1F && size.Width == 360))
                using (Bitmap image = new Bitmap(form.Width, form.Height))
                { form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(Path.Combine(args[0], "layout-" + (int)(scale * 100) + "-" + size.Width + ".png")); }
            form.Close(); cases++;
        }
        Console.WriteLine("PASS: " + cases + " layout scenarios (100%-300% font-scale simulation, 360-1920px windows), wrapping, overlap, stacking and scroll reachability.");
    }
}
