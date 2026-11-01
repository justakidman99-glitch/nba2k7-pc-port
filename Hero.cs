// Original masked-hero portrait for the custom Knicks player: navy hexagon-panel mask, orange forehead
// chevron, one narrow visor, "99" on the side, Knicks-coloured collar. Drawn at 512px, saved at 128px
// with PS2 alpha (128 = solid) for the 2K7 portrait slot, plus a full-alpha preview.
//   hero.exe <out-128.png> <preview-512.png>
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

static class Hero {
    static readonly Color Navy = Color.FromArgb(18, 40, 92), NavyDark = Color.FromArgb(10, 22, 52),
        Orange = Color.FromArgb(245, 125, 30), Visor = Color.FromArgb(200, 240, 255), Line = Color.FromArgb(70, 110, 190);

    static void Main(string[] a) {
        const int S = 512;
        using (var big = new Bitmap(S, S, PixelFormat.Format32bppArgb)) {
            using (var g = Graphics.FromImage(big)) {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
                // shoulders and collar
                using (var b = new LinearGradientBrush(new Rectangle(40, 380, 432, 132), Navy, NavyDark, 90f))
                    g.FillEllipse(b, 40, 400, 432, 260);
                using (var p = new Pen(Orange, 16)) g.DrawArc(p, 150, 360, 212, 120, 20, 140);
                // neck
                using (var b = new SolidBrush(NavyDark)) g.FillRectangle(b, 196, 330, 120, 90);
                // head
                var head = new GraphicsPath(); head.AddEllipse(116, 40, 280, 340);
                using (var b = new PathGradientBrush(head) { CenterColor = Color.FromArgb(40, 75, 150), SurroundColors = new[] { NavyDark }, CenterPoint = new PointF(230, 150) })
                    g.FillPath(b, head);
                // hexagon panel pattern, clipped to the head
                g.SetClip(head);
                using (var p = new Pen(Color.FromArgb(90, Line), 3)) {
                    double r = 26, w = Math.Sqrt(3) * r;
                    for (int row = -1; row < 16; row++) for (int col = -1; col < 14; col++) {
                        double cx = col * w + (row % 2 == 0 ? 0 : w / 2) + 100, cy = row * r * 1.5 + 30;
                        var pts = new PointF[6];
                        for (int k = 0; k < 6; k++) { double ang = Math.PI / 3 * k + Math.PI / 6; pts[k] = new PointF((float)(cx + r * Math.Cos(ang)), (float)(cy + r * Math.Sin(ang))); }
                        g.DrawPolygon(p, pts);
                    }
                }
                // forehead chevron
                using (var b = new SolidBrush(Orange))
                    g.FillPolygon(b, new[] { new PointF(256, 92), new PointF(320, 140), new PointF(300, 152), new PointF(256, 120), new PointF(212, 152), new PointF(192, 140) });
                // single narrow visor across the eyes, with a soft glow
                var visor = new GraphicsPath();
                visor.AddPolygon(new[] { new PointF(140, 200), new PointF(372, 200), new PointF(388, 214), new PointF(360, 236), new PointF(152, 236), new PointF(124, 214) });
                using (var glow = new Pen(Color.FromArgb(70, Visor), 18)) g.DrawPath(glow, visor);
                using (var b = new LinearGradientBrush(new Rectangle(124, 200, 264, 36), Color.White, Visor, 90f)) g.FillPath(b, visor);
                using (var p = new Pen(Color.FromArgb(25, 30, 45), 5)) g.DrawPath(p, visor);
                // mouth panel line and jaw seams
                using (var p = new Pen(Color.FromArgb(120, Orange), 5)) { g.DrawLine(p, 226, 318, 286, 318); }
                using (var p = new Pen(Color.FromArgb(110, 8, 16, 40), 6)) { g.DrawArc(p, 150, 150, 212, 220, 30, 120); }
                // "99" on the side of the head
                using (var f = new Font("Arial Black", 34, FontStyle.Bold)) using (var b = new SolidBrush(Orange)) {
                    var st = g.Save(); g.TranslateTransform(352, 250); g.RotateTransform(-12); g.DrawString("99", f, b, 0, 0); g.Restore(st);
                }
                g.ResetClip();
                using (var p = new Pen(Color.FromArgb(160, 6, 12, 30), 6)) g.DrawPath(p, head);
            }
            big.Save(a[1], ImageFormat.Png);
            // 128px game version, PS2 alpha scale
            using (var small = new Bitmap(128, 128, PixelFormat.Format32bppArgb)) {
                using (var g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.DrawImage(big, 0, 0, 128, 128); }
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++) { var c = small.GetPixel(x, y); small.SetPixel(x, y, Color.FromArgb((int)Math.Round(c.A * 128 / 255.0), c.R, c.G, c.B)); }
                small.Save(a[0], ImageFormat.Png);
            }
        }
        Console.WriteLine("wrote " + a[0] + " and " + a[1]);
    }
}
