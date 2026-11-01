// Makes an NBA 2K7 menu portrait (128x128) from a photo: placed by eye positions, plain studio
// background removed, the subject kept inside its own panel of the photo.
//   portrait.exe <2k7 portrait.png> <photo> <out.png> <preview.png> eyeL eyeR eyeY panelX0 panelX1 [bgBlack]
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

static class Portrait {
    // where the eyes sit in a 2K7 portrait
    const double TEyeL = 50, TEyeR = 83, TEyeY = 50;

    static void Main(string[] a) {
        var orig = new Bitmap(a[0]); var photo = Load(a[1]);
        double el = D(a[4]), er = D(a[5]), ey = D(a[6]); int p0 = (int)D(a[7]), p1 = (int)D(a[8]);
        bool blackBg = a.Length > 9 && a[9] == "black";
        double scale = (TEyeR - TEyeL) / (er - el), cx = (el + er) / 2, tcx = (TEyeL + TEyeR) / 2;
        int W = orig.Width, H = orig.Height;
        int maxA = 0; for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) maxA = Math.Max(maxA, orig.GetPixel(x, y).A);

        // background mask on the photo panel: flood from the panel edges through plain studio colour
        int PW = p1 - p0, PH = photo.Height; var bg = new bool[PW, PH];
        Color key = blackBg ? Color.FromArgb(10, 10, 10) : photo.GetPixel(p0 + 3, Math.Min(PH - 1, 60));
        var q = new Queue<Point>();
        for (int y = 0; y < PH; y++) { q.Enqueue(new Point(0, y)); q.Enqueue(new Point(PW - 1, y)); }
        for (int x = 0; x < PW; x++) q.Enqueue(new Point(x, 0));
        while (q.Count > 0) {
            var p = q.Dequeue(); if (p.X < 0 || p.Y < 0 || p.X >= PW || p.Y >= PH || bg[p.X, p.Y]) continue;
            var c = photo.GetPixel(p0 + p.X, p.Y);
            if (!IsBg(c, key, blackBg)) continue;
            bg[p.X, p.Y] = true;
            q.Enqueue(new Point(p.X + 1, p.Y)); q.Enqueue(new Point(p.X - 1, p.Y)); q.Enqueue(new Point(p.X, p.Y + 1)); q.Enqueue(new Point(p.X, p.Y - 1));
        }

        var outB = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
            double sx = cx + (x + 0.5 - tcx) / scale, sy = ey + (y + 0.5 - TEyeY) / scale;
            // average a few photo pixels for a smooth downscale, with alpha from the background mask
            double r = 0, g = 0, b = 0, cov = 0; int n = 0;
            for (int j = 0; j < 3; j++) for (int i = 0; i < 3; i++) {
                int px = (int)(sx + (i - 1) / (2 * scale)), py = (int)(sy + (j - 1) / (2 * scale)); n++;
                if (px < p0 || px >= p1 || py < 0 || py >= PH) continue;
                if (bg[px - p0, py]) continue;
                var c = photo.GetPixel(px, py); r += c.R; g += c.G; b += c.B; cov++;
            }
            if (cov == 0) { outB.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0)); continue; }
            int alpha = (int)Math.Round(maxA * cov / n);
            outB.SetPixel(x, y, Color.FromArgb(alpha, Cl(r / cov), Cl(g / cov), Cl(b / cov)));
        }
        outB.Save(a[2], ImageFormat.Png);

        // preview: old | new, 3x, on a dark card background like a modern player card
        using (var pv = new Bitmap(W * 6 + 30, H * 3 + 10)) using (var gr = Graphics.FromImage(pv)) {
            gr.Clear(Color.FromArgb(24, 30, 26)); gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            gr.DrawImage(Full(orig), 5, 5, W * 3, H * 3); gr.DrawImage(Full(outB), W * 3 + 25, 5, W * 3, H * 3);
            pv.Save(a[3], ImageFormat.Png);
        }
        Console.WriteLine("scale " + scale.ToString("0.000") + ", wrote " + a[2]);
    }
    static bool IsBg(Color c, Color key, bool black) {
        if (black) return c.R + c.G + c.B < 75;
        int mx = Math.Max(c.R, Math.Max(c.G, c.B)), mn = Math.Min(c.R, Math.Min(c.G, c.B));
        int d = Math.Abs(c.R - key.R) + Math.Abs(c.G - key.G) + Math.Abs(c.B - key.B);
        return (mx - mn) < 18 && d < 60;
    }
    static Bitmap Full(Bitmap b) { var o = new Bitmap(b.Width, b.Height); for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) { var c = b.GetPixel(x, y); o.SetPixel(x, y, Color.FromArgb(Math.Min(255, c.A * 2), c.R, c.G, c.B)); } return o; }
    static double D(string s) { return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture); }
    static int Cl(double v) { return v < 0 ? 0 : v > 255 ? 255 : (int)v; }
    static Bitmap Load(string path) {
        if (!path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) return new Bitmap(path);
        using (var fs = File.OpenRead(path)) {
            var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var f = new FormatConvertedBitmap(dec.Frames[0], System.Windows.Media.PixelFormats.Bgra32, null, 0);
            int w = f.PixelWidth, h = f.PixelHeight; var px = new byte[w * h * 4]; f.CopyPixels(px, w * 4, 0);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(px, 0, data.Scan0, px.Length); bmp.UnlockBits(data);
            return bmp;
        }
    }
}
