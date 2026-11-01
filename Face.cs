// Puts a front-facing face photo onto an NBA 2K7 256x256 head texture.
// The face is placed by eye positions, blended in with a soft oval, and the rest of the head
// is shifted to the new skin tone. The game's own transparency is kept.
//   face.exe <head.png> <photo> <out.png> <preview.png>
//            headEyeL headEyeR headEyeY  photoEyeL photoEyeR photoEyeY  ovalCx ovalCy ovalRx ovalRy
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;

static class Face {
    static void Main(string[] a) {
        var head = new Bitmap(a[0]); var photo = Load(a[1]);
        if (a.Length > 14) { Feather = D(a[14]); Clean = a.Length > 15 && a[15] == "clean"; } // optional: feather start, "clean"
        double hl = D(a[4]), hr = D(a[5]), hy = D(a[6]), pl = D(a[7]), pr = D(a[8]), py = D(a[9]);
        double ocx = D(a[10]), ocy = D(a[11]), orx = D(a[12]), ory = D(a[13]);
        double scale = (hr - hl) / (pr - pl), hcx = (hl + hr) / 2, pcx = (pl + pr) / 2;
        int W = head.Width, H = head.Height;

        // skin tone of both faces, from the cheeks just under the eyes
        Color headSkin = Mean(head, (int)(hcx - (hr - hl) * 0.55), (int)(hy + 12), (int)((hr - hl) * 0.3), 10);
        Color photoSkin = Mean(photo, (int)(pcx - (pr - pl) * 0.55), (int)(py + 12 / scale), (int)((pr - pl) * 0.3), (int)(10 / scale));
        double gr = photoSkin.R / Math.Max(1.0, headSkin.R), gg = photoSkin.G / Math.Max(1.0, headSkin.G), gb = photoSkin.B / Math.Max(1.0, headSkin.B);
        Console.WriteLine("head skin " + headSkin + "  photo skin " + photoSkin);

        var outB = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
            var o = head.GetPixel(x, y);
            bool inset = x >= W * 0.5 && y < H * 0.25; // eye / teeth pieces keep their colours (the ear takes the new skin tone)
            Color baseC = inset ? o : Color.FromArgb(255, Cl(o.R * gr), Cl(o.G * gg), Cl(o.B * gb));
            double dx = (x - ocx) / orx, dy = (y - ocy) / ory, d = Math.Sqrt(dx * dx + dy * dy);
            double w = d < Feather ? 1 : d > 1.0 ? 0 : (1.0 - d) / (1.0 - Feather);   // soft oval edge
            w = w * w * (3 - 2 * w);                                                   // smoothstep: no visible ring
            Color c = baseC;
            if (w > 0) {
                double sx = pcx + (x + 0.5 - hcx) / scale, sy = py + (y + 0.5 - hy) / scale;
                var p = Bilinear(photo, sx, sy);
                c = Color.FromArgb(255, Cl(p.R * w + baseC.R * (1 - w)), Cl(p.G * w + baseC.G * (1 - w)), Cl(p.B * w + baseC.B * (1 - w)));
            }
            outB.SetPixel(x, y, Color.FromArgb(o.A, c.R, c.G, c.B));
        }
        if (Clean) CleanUp(outB, ocx, ocy, orx, ory);
        outB.Save(a[2], ImageFormat.Png);

        // preview: before | after at 2x, full opacity
        using (var pv = new Bitmap(W * 4 + 16, H * 2)) using (var g = Graphics.FromImage(pv)) {
            g.Clear(Color.FromArgb(40, 40, 48)); g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.DrawImage(Opaque(head), 0, 0, W * 2, H * 2); g.DrawImage(Opaque(outB), W * 2 + 16, 0, W * 2, H * 2);
            pv.Save(a[3], ImageFormat.Png);
        }
        Console.WriteLine("scale " + scale.ToString("0.000") + ", wrote " + a[2]);
    }
    static double Feather = 0.8; static bool Clean = false;

    // Inside the face oval: smooth blotchy skin (edge-preserving blur), then sharpen the features a little.
    static void CleanUp(Bitmap b, double cx, double cy, double rx, double ry) {
        int W = b.Width, H = b.Height; var src = new Color[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) src[x, y] = b.GetPixel(x, y);
        var smooth = new Color[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
            var c0 = src[x, y]; double r = 0, g = 0, bl = 0, wsum = 0;
            for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) {
                int nx = Math.Min(W - 1, Math.Max(0, x + dx)), ny = Math.Min(H - 1, Math.Max(0, y + dy)); var c = src[nx, ny];
                double diff = Math.Abs(c.R - c0.R) + Math.Abs(c.G - c0.G) + Math.Abs(c.B - c0.B);
                double wgt = Math.Exp(-(dx * dx + dy * dy) / 4.0) * Math.Exp(-diff * diff / (2 * 30.0 * 30.0)); // keeps edges, flattens blotches
                r += c.R * wgt; g += c.G * wgt; bl += c.B * wgt; wsum += wgt;
            }
            smooth[x, y] = Color.FromArgb(255, Cl(r / wsum), Cl(g / wsum), Cl(bl / wsum));
        }
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
            double dx = (x - cx) / rx, dy = (y - cy) / ry, d = Math.Sqrt(dx * dx + dy * dy); if (d >= 1.0) continue;
            double inside = d < 0.85 ? 1 : (1.0 - d) / 0.15;
            // hair and beard (dark pixels) keep their texture: only skin gets smoothed
            double lum0 = 0.299 * src[x, y].R + 0.587 * src[x, y].G + 0.114 * src[x, y].B;
            double hairKeep = lum0 < 45 ? 1 : lum0 > 75 ? 0 : (75 - lum0) / 30;
            var sm = smooth[x, y]; var so = src[x, y];
            var s = Color.FromArgb(255, Cl(sm.R + (so.R - sm.R) * hairKeep), Cl(sm.G + (so.G - sm.G) * hairKeep), Cl(sm.B + (so.B - sm.B) * hairKeep));
            double br = 0, bg = 0, bb = 0; int n = 0;
            for (int ky = -1; ky <= 1; ky++) for (int kx = -1; kx <= 1; kx++) { var c = smooth[Math.Min(W - 1, Math.Max(0, x + kx)), Math.Min(H - 1, Math.Max(0, y + ky))]; br += c.R; bg += c.G; bb += c.B; n++; }
            double k = 0.55; // unsharp amount
            double nr = s.R + k * (s.R - br / n), ng = s.G + k * (s.G - bg / n), nb = s.B + k * (s.B - bb / n);
            var o = src[x, y];
            b.SetPixel(x, y, Color.FromArgb(o.A, Cl(o.R + (nr - o.R) * inside), Cl(o.G + (ng - o.G) * inside), Cl(o.B + (nb - o.B) * inside)));
        }
    }
    static double D(string s) { return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture); }
    static int Cl(double v) { return v < 0 ? 0 : v > 255 ? 255 : (int)v; }
    static Bitmap Opaque(Bitmap b) { var o = new Bitmap(b.Width, b.Height); for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) { var c = b.GetPixel(x, y); o.SetPixel(x, y, Color.FromArgb(255, c)); } return o; }
    static Color Mean(Bitmap b, int x0, int y0, int w, int h) {
        long r = 0, g = 0, bl = 0, n = 0;
        foreach (int side in new[] { 0, 1 }) {
            int xs = side == 0 ? x0 : x0 + w * 2 + 6; // left and right cheek
            for (int y = y0; y < y0 + h; y++) for (int x = xs; x < xs + w; x++) {
                if (x < 0 || y < 0 || x >= b.Width || y >= b.Height) continue; var c = b.GetPixel(x, y); r += c.R; g += c.G; bl += c.B; n++;
            }
        }
        return n == 0 ? Color.Gray : Color.FromArgb(255, (int)(r / n), (int)(g / n), (int)(bl / n));
    }
    static Color Bilinear(Bitmap b, double x, double y) {
        x = Math.Max(0, Math.Min(b.Width - 2, x)); y = Math.Max(0, Math.Min(b.Height - 2, y));
        int x0 = (int)x, y0 = (int)y; double fx = x - x0, fy = y - y0;
        Color a = b.GetPixel(x0, y0), c = b.GetPixel(x0 + 1, y0), d = b.GetPixel(x0, y0 + 1), e = b.GetPixel(x0 + 1, y0 + 1);
        Func<int, int, int, int, int> m = (p, q, r, s) => Cl((p * (1 - fx) + q * fx) * (1 - fy) + (r * (1 - fx) + s * fx) * fy);
        return Color.FromArgb(255, m(a.R, c.R, d.R, e.R), m(a.G, c.G, d.G, e.G), m(a.B, c.B, d.B, e.B));
    }
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
