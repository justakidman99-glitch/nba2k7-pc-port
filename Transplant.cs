// Puts the front of each 2025-26 jersey from the user's flat photo (1920x1080, 3 rows x 10) onto the
// NBA 2K7 jersey texture. The printed number is painted out (the game draws its own numbers);
// the back and sides get the jersey's main colour, and the game's trim keeps its folds and shading.
//   transplant.exe overlay <photo> <out.png>
//   transplant.exe build <photo> <map.tsv> <dumps> <outDir>
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;

static class Transplant {
    // team, number box (x0,y0,x1,y1) in the photo
    static readonly object[][] Teams = {
        // row 1
        T("hawks", 72,140,120,185), T("celtics", 265,155,312,212), T("nets", 455,148,505,198), T("hornets_cha", 664,145,720,190),
        T("bulls", 868,140,908,185), T("cavaliers", 1055,152,1105,196), T("mavericks", 1252,138,1292,182), T("nuggets", 1452,105,1484,150),
        T("pistons", 1610,148,1650,192), T("warriors", 1800,165,1850,205),
        // row 2
        T("rockets", 110,495,152,548), T("pacers", 278,496,330,540), T("clippers", 460,510,498,555), T("lakers", 642,500,702,552),
        T("grizzlies", 840,500,892,546), T("heat", 1046,494,1102,540), T("bucks", 1245,508,1295,552), T("timberwolves", 1420,512,1460,560),
        T("pelicans", 1620,520,1650,572), T("knicks", 1798,508,1848,562),
        // row 3
        T("thunder", 80,852,116,906), T("magic", 268,864,306,916), T("sixers", 458,830,500,884), T("suns", 672,872,700,920),
        T("blazers", 870,876,908,922), T("kings", 1030,856,1086,906), T("spurs", 1262,890,1294,934), T("raptors", 1462,858,1490,894),
        T("jazz", 1600,864,1664,920), T("wizards", 1792,856,1850,912) };
    static object[] T(string n, int x0, int y0, int x1, int y1) { return new object[] { n, new Rectangle(x0, y0, x1 - x0, y1 - y0) }; }

    // Per row: top of the shoulder straps, bottom of the neck opening, hem.
    static readonly int[] Top = { 42, 398, 748 }, Neck = { 100, 455, 805 }, Hem = { 330, 690, 1045 };
    const int CellW = 192, Inset = 18;           // jersey torso sits inside each 192px column
    const int FrontW = 170, TNeck = 75;          // 2K7 texture: front panel width, bottom of its neck opening

    class Spec { public string Team; public int Col, Row; public Rectangle Num; }
    static List<Spec> Specs() {
        var l = new List<Spec>();
        for (int i = 0; i < Teams.Length; i++) l.Add(new Spec { Team = (string)Teams[i][0], Num = (Rectangle)Teams[i][1], Row = i / 10, Col = i % 10 });
        return l;
    }
    static Rectangle Front(Spec s) { int x = s.Col * CellW + Inset; return new Rectangle(x, Top[s.Row], CellW - 2 * Inset, Hem[s.Row] - Top[s.Row]); }

    static void Main(string[] a) {
        var photo = Load(a[1]);
        if (a[0] == "overlay") {
            using (var g = Graphics.FromImage(photo)) {
                foreach (var s in Specs()) {
                    var f = Front(s);
                    g.DrawRectangle(new Pen(Color.Lime, 2), f);
                    g.DrawLine(new Pen(Color.Yellow, 2), f.Left, Neck[s.Row], f.Right, Neck[s.Row]);
                    g.DrawRectangle(new Pen(Color.Magenta, 2), s.Num);
                    g.DrawString(s.Team, new Font("Segoe UI", 11f, FontStyle.Bold), Brushes.White, f.Left, f.Top - 18);
                }
            }
            photo.Save(a[2], ImageFormat.Png); return;
        }
        string map = a[2], dumps = a[3], outDir = a[4];
        Directory.CreateDirectory(outDir);
        var specs = Specs().ToDictionary(s => s.Team);
        var fronts = new Dictionary<string, Bitmap>();
        int n = 0;
        foreach (var line in File.ReadAllLines(map)) {
            var p = line.Split('\t'); if (p.Length < 3) continue;
            Spec s; if (!specs.TryGetValue(p[0], out s)) { Console.WriteLine("no jersey for " + p[0]); continue; }
            Bitmap front; if (!fronts.TryGetValue(s.Team, out front)) { front = CleanFront(photo, s); fronts[s.Team] = front; front.Save(Path.Combine(outDir, "_front-" + s.Team + ".png"), ImageFormat.Png); }
            bool shorts = p.Length > 3 && p[3] == "shorts"; // shorts: colours only, no jersey front
            using (var tex = new Bitmap(Path.Combine(dumps, p[2]))) using (var res = Make(tex, front, s, shorts)) { res.Save(Path.Combine(outDir, p[2]), ImageFormat.Png); n++; }
        }
        Console.WriteLine("jerseys written: " + n);
    }

    // Crop the jersey front and paint the number out by blending the fabric from either side of it.
    static Bitmap CleanFront(Bitmap photo, Spec s) {
        var f = Front(s);
        var b = new Bitmap(f.Width, f.Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < f.Height; y++) for (int x = 0; x < f.Width; x++) b.SetPixel(x, y, photo.GetPixel(f.X + x, f.Y + y));
        var r = new Rectangle(s.Num.X - f.X, s.Num.Y - f.Y, s.Num.Width, s.Num.Height);
        int pad = 3;
        for (int y = r.Top; y < r.Bottom; y++) {
            Color L = Avg(b, r.Left - pad - 3, y, 3), R = Avg(b, r.Right + pad, y, 3);
            Color U = Avg(b, -1, -1, 0), D = Color.Empty;
            for (int x = r.Left - pad; x < r.Right + pad; x++) {
                double t = (x - (r.Left - pad)) / (double)(r.Width + 2 * pad);
                Color top = b.GetPixel(Math.Max(0, Math.Min(b.Width - 1, x)), Math.Max(0, r.Top - pad - 1));
                Color bot = b.GetPixel(Math.Max(0, Math.Min(b.Width - 1, x)), Math.Min(b.Height - 1, r.Bottom + pad));
                double v = (y - r.Top) / (double)Math.Max(1, r.Height);
                double hr = L.R + (R.R - L.R) * t, hg = L.G + (R.G - L.G) * t, hb = L.B + (R.B - L.B) * t;
                double vr = top.R + (bot.R - top.R) * v, vg = top.G + (bot.G - top.G) * v, vb = top.B + (bot.B - top.B) * v;
                b.SetPixel(Math.Max(0, Math.Min(b.Width - 1, x)), y, Color.FromArgb(255, Cl((hr + vr) / 2), Cl((hg + vg) / 2), Cl((hb + vb) / 2)));
            }
        }
        return b;
    }
    static Color Avg(Bitmap b, int x0, int y, int w) {
        if (w == 0) return Color.Empty;
        int r = 0, g = 0, bl = 0, n = 0;
        for (int x = x0; x < x0 + w; x++) { if (x < 0 || x >= b.Width || y < 0 || y >= b.Height) continue; var c = b.GetPixel(x, y); r += c.R; g += c.G; bl += c.B; n++; }
        return n == 0 ? Color.Gray : Color.FromArgb(255, r / n, g / n, bl / n);
    }

    static Bitmap Make(Bitmap tex, Bitmap front, Spec s, bool shorts) {
        int W = tex.Width, H = tex.Height;
        var t = new Color[W, H]; int maxA = 0;
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { t[x, y] = tex.GetPixel(x, y); maxA = Math.Max(maxA, t[x, y].A); }
        var vis = new List<Color>(); for (int y = 0; y < H; y += 2) for (int x = 0; x < W; x += 2) if (t[x, y].A >= Math.Max(8, maxA * 0.15)) vis.Add(t[x, y]);
        Color tBody = Median(vis);
        var trimList = vis.Where(c => ChromaDist(c, tBody) > 60).ToList();
        Color tTrim = trimList.Count > 0 ? Median(trimList) : tBody;
        double lBody = Math.Max(40, Lum(tBody)), lTrim = Math.Max(40, Lum(tTrim));

        // Main colour = middle of the jersey front (below the lettering, number already painted out).
        int ny = Neck[s.Row] - Top[s.Row];
        var mid = new List<Color>();
        for (int y = (int)(front.Height * 0.55); y < (int)(front.Height * 0.85); y += 2)
            for (int x = (int)(front.Width * 0.25); x < (int)(front.Width * 0.75); x += 2) mid.Add(front.GetPixel(x, y));
        Color body = Median(mid);
        // Trim colour = the team's lettering colour (the strongest non-fabric colour in the band under the neck).
        var band = new List<Color>();
        for (int y = ny + 5; y < Math.Min(front.Height, ny + (int)(front.Height * 0.25)); y++)
            for (int x = (int)(front.Width * 0.12); x < (int)(front.Width * 0.88); x++) {
                var c = front.GetPixel(x, y); if (Dist(c, body) > 80) band.Add(c);
            }
        Color trim = band.Count > 40 ? Median(band.OrderByDescending(c => Dist(c, body)).Take(band.Count / 2).ToList())
                                     : (Lum(body) > 128 ? Color.FromArgb(30, 30, 30) : Color.FromArgb(235, 235, 235));

        var res = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        var oldWord = shorts ? Rectangle.Empty : new Rectangle((int)(W * 0.02), (int)(H * 0.27), (int)(W * 0.30), (int)(H * 0.30));
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
            var c = t[x, y];
            bool isTrim = !oldWord.Contains(x, y) && ChromaDist(c, tBody) > 60;
            double shade = isTrim ? Lum(c) / lTrim : Lum(c) / lBody;
            if (oldWord.Contains(x, y)) shade = 1.0;
            Color outC;
            if (!isTrim && x < FrontW && !shorts) {
                // front panel fabric: sample the photo jersey front
                double u = x / (double)FrontW, py;
                if (y < TNeck) py = (y / (double)TNeck) * ny;
                else py = ny + (y - TNeck) / (double)(H - TNeck) * (front.Height - 1 - ny);
                var pc = Bilinear(front, u * (front.Width - 1), py);
                double k = 0.85 + 0.15 * Math.Max(0.6, Math.Min(1.3, shade));   // keep a little of the game's folds
                outC = Color.FromArgb(255, Cl(pc.R * k), Cl(pc.G * k), Cl(pc.B * k));
            } else {
                Color target = isTrim ? trim : body;
                shade = Math.Max(0.55, Math.Min(1.25, shade));
                outC = Color.FromArgb(255, Cl(target.R * shade), Cl(target.G * shade), Cl(target.B * shade));
            }
            res.SetPixel(x, y, Color.FromArgb(c.A, outC.R, outC.G, outC.B)); // game's own transparency (PS2: 128 = solid)
        }
        return res;
    }

    static Color Bilinear(Bitmap b, double x, double y) {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y), x1 = Math.Min(b.Width - 1, x0 + 1), y1 = Math.Min(b.Height - 1, y0 + 1);
        x0 = Math.Max(0, Math.Min(b.Width - 1, x0)); y0 = Math.Max(0, Math.Min(b.Height - 1, y0));
        double fx = x - x0, fy = y - y0;
        Color a = b.GetPixel(x0, y0), c = b.GetPixel(x1, y0), d = b.GetPixel(x0, y1), e = b.GetPixel(x1, y1);
        Func<int, int, int, int, int> mix = (p, q, r, s2) => Cl((p * (1 - fx) + q * fx) * (1 - fy) + (r * (1 - fx) + s2 * fx) * fy);
        return Color.FromArgb(255, mix(a.R, c.R, d.R, e.R), mix(a.G, c.G, d.G, e.G), mix(a.B, c.B, d.B, e.B));
    }
    static Color Median(List<Color> px) {
        if (px.Count == 0) return Color.Gray;
        Func<Func<Color, int>, int> med = f => px.Select(f).OrderBy(v => v).ElementAt(px.Count / 2);
        return Color.FromArgb(255, med(c => c.R), med(c => c.G), med(c => c.B));
    }
    static double Dist(Color a, Color b) { int dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B; return Math.Sqrt(dr * dr + dg * dg + db * db); }
    static double Lum(Color c) { return 0.299 * c.R + 0.587 * c.G + 0.114 * c.B; }
    static double ChromaDist(Color c, Color reference) {
        double lc = Lum(c), lr = Math.Max(1, Lum(reference));
        if (lc < 35) return 0;
        double k = lr / lc, r = c.R * k - reference.R, g = c.G * k - reference.G, b = c.B * k - reference.B;
        return Math.Sqrt(r * r + g * g + b * b);
    }
    static int Cl(double v) { return v < 0 ? 0 : v > 255 ? 255 : (int)v; }

    static Bitmap Load(string path) {
        using (var fs = File.OpenRead(path)) {
            var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = new FormatConvertedBitmap(dec.Frames[0], System.Windows.Media.PixelFormats.Bgra32, null, 0);
            int w = frame.PixelWidth, h = frame.PixelHeight; var px = new byte[w * h * 4];
            frame.CopyPixels(px, w * 4, 0);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(px, 0, data.Scan0, px.Length);
            bmp.UnlockBits(data);
            return bmp;
        }
    }
}
