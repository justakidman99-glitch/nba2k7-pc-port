// Recolours NBA 2K7 jersey textures to the 2026 jerseys in the user's photo and swaps in the new chest wordmark.
// The 2K7 texture keeps its folds and shading; only colours and lettering change.
//   jersey.exe overlay <photo> <out.png>                 - draw sampling boxes on the photo (for checking)
//   jersey.exe build <photo> <map.tsv> <dumps> <outDir>  - write one replacement per game texture
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;

static class Jersey {
    // Photo layout: 3 rows x 10 jerseys, left to right.
    static readonly string[][] Rows = {
        new[] { "pelicans", "pacers", "hawks", "cavaliers", "grizzlies", "rockets", "pistons", "blazers", "bulls", "sixers" },
        new[] { "bucks", "celtics", "mavericks", "raptors", "spurs", "jazz", "magic", "suns", "lakers", "kings" },
        new[] { "thunder", "clippers", "wizards", "timberwolves", "nuggets", "hornets_cha", "nets", "warriors", "heat", "knicks" } };
    const double X0 = 62, DX = 83.6;
    static readonly int[] ChestY = { 62, 213, 363 }, BellyY = { 122, 272, 422 };

    class Spot { public string Team; public int Cx, Chest, Belly; }
    static IEnumerable<Spot> Spots() {
        for (int r = 0; r < 3; r++) for (int i = 0; i < 10; i++)
            yield return new Spot { Team = Rows[r][i], Cx = (int)Math.Round(X0 + i * DX), Chest = ChestY[r], Belly = BellyY[r] };
    }
    static Rectangle BodyBox(Spot s) { return new Rectangle(s.Cx - 16, s.Belly - 8, 32, 16); }
    static Rectangle WordBox(Spot s) { return new Rectangle(s.Cx - 30, s.Chest - 13, 60, 24); }
    static Rectangle JerseyBox(Spot s) { return new Rectangle(s.Cx - 34, s.Chest - 40, 68, 120); }

    static void Main(string[] a) {
        var photo = Load(a[1]);
        if (a[0] == "overlay") {
            using (var g = Graphics.FromImage(photo)) {
                foreach (var s in Spots()) {
                    g.DrawRectangle(Pens.Lime, BodyBox(s)); g.DrawRectangle(Pens.Yellow, WordBox(s)); g.DrawRectangle(Pens.Magenta, JerseyBox(s));
                    g.DrawString(s.Team, new Font("Segoe UI", 7f), Brushes.Cyan, s.Cx - 30, s.Chest - 52);
                }
            }
            photo.Save(a[2], ImageFormat.Png);
            return;
        }
        // build
        string mapFile = a[2], dumps = a[3], outDir = a[4];
        Directory.CreateDirectory(outDir);
        var spots = Spots().ToDictionary(s => s.Team);
        int n = 0;
        foreach (var line in File.ReadAllLines(mapFile)) {
            var p = line.Split('\t'); if (p.Length < 3) continue;
            string team = p[0], file = p[2]; Spot s;
            if (!spots.TryGetValue(team, out s)) { Console.WriteLine("no photo jersey for " + team); continue; }
            using (var tex = new Bitmap(Path.Combine(dumps, file))) using (var res = Make(photo, s, tex)) {
                res.Save(Path.Combine(outDir, file), ImageFormat.Png); n++;
            }
        }
        Console.WriteLine("jerseys written: " + n);
    }

    static Color Median(Bitmap b, IEnumerable<Point> pts) {
        var px = pts.Where(p => p.X >= 0 && p.Y >= 0 && p.X < b.Width && p.Y < b.Height).Select(p => b.GetPixel(p.X, p.Y)).ToList();
        if (px.Count == 0) return Color.Gray;
        Func<Func<Color, int>, int> med = f => px.Select(f).OrderBy(v => v).ElementAt(px.Count / 2);
        return Color.FromArgb(255, med(c => c.R), med(c => c.G), med(c => c.B));
    }
    static IEnumerable<Point> Pts(Rectangle r) { for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) yield return new Point(x, y); }
    static double Dist(Color a, Color b) { int dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B; return Math.Sqrt(dr * dr + dg * dg + db * db); }
    static double Lum(Color c) { return 0.299 * c.R + 0.587 * c.G + 0.114 * c.B; }
    // Colour difference with brightness taken out, so folds and shadows in the fabric count as the same colour.
    static double ChromaDist(Color c, Color reference) {
        double lc = Lum(c), lr = Math.Max(1, Lum(reference));
        if (lc < 35) return Lum(reference) < 60 ? 0 : 0; // very dark pixels are shadow, not trim
        double k = lr / lc;
        double r = c.R * k - reference.R, g = c.G * k - reference.G, b = c.B * k - reference.B;
        return Math.Sqrt(r * r + g * g + b * b);
    }
    static double Sat(Color c) { int mx = Math.Max(c.R, Math.Max(c.G, c.B)), mn = Math.Min(c.R, Math.Min(c.G, c.B)); return mx == 0 ? 0 : (mx - mn) / (double)mx; }
    static int Cl(double v) { return v < 0 ? 0 : v > 255 ? 255 : (int)v; }

    static Bitmap Make(Bitmap photo, Spot s, Bitmap tex) {
        // New colours from the photo
        Color body = Median(photo, Pts(BodyBox(s)));
        var bg = Color.FromArgb(38, 34, 32); // dark wall behind the jerseys
        var jerseyPx = Pts(JerseyBox(s)).Where(p => p.X >= 0 && p.Y >= 0 && p.X < photo.Width && p.Y < photo.Height)
            .Select(p => photo.GetPixel(p.X, p.Y)).Where(c => Dist(c, body) > 70 && Dist(c, bg) > 40).ToList();
        Color trim = jerseyPx.Count > 30
            ? jerseyPx.OrderByDescending(c => Sat(c) * 2 + Dist(c, body) / 255.0).Take(Math.Max(10, jerseyPx.Count / 4)).Aggregate(new int[3], (acc, c) => { acc[0] += c.R; acc[1] += c.G; acc[2] += c.B; return acc; }, acc => Color.FromArgb(255, acc[0] / Math.Max(10, jerseyPx.Count / 4), acc[1] / Math.Max(10, jerseyPx.Count / 4), acc[2] / Math.Max(10, jerseyPx.Count / 4)))
            : (Lum(body) > 128 ? Color.FromArgb(30, 30, 30) : Color.FromArgb(235, 235, 235));

        // Template: its main fabric colour and its trim colour
        int W = tex.Width, H = tex.Height;
        var tpx = new Color[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) tpx[x, y] = tex.GetPixel(x, y);
        // PS2 alpha: 128 means solid, so "solid enough" is relative to the texture's own highest alpha
        int maxA = 0; for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) maxA = Math.Max(maxA, tpx[x, y].A);
        var opaque = new List<Color>(); for (int y = 0; y < H; y += 2) for (int x = 0; x < W; x += 2) if (tpx[x, y].A >= Math.Max(8, maxA * 0.15)) opaque.Add(tpx[x, y]); // some textures store the fabric itself nearly transparent
        Color tBody = Median(opaque);
        var tTrimList = opaque.Where(c => ChromaDist(c, tBody) > 60).ToList();
        Color tTrim = tTrimList.Count > 0 ? Median(tTrimList) : tBody;
        double lBody = Math.Max(40, Lum(tBody)), lTrim = Math.Max(40, Lum(tTrim));

        // Old chest wordmark area on the front panel (left side of the texture)
        var word = new Rectangle((int)(W * 0.02), (int)(H * 0.27), (int)(W * 0.30), (int)(H * 0.30));
        var res = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
            var c = tpx[x, y];
            bool inWord = word.Contains(x, y);
            bool isTrim = !inWord && ChromaDist(c, tBody) > 60;
            double shade;
            Color target;
            if (isTrim) { shade = Lum(c) / lTrim; target = trim; }
            else { shade = inWord ? FabricShade(tpx, x, y, tBody, lBody) : Lum(c) / lBody; target = body; }
            shade = Math.Max(0.55, Math.Min(1.25, shade));
            res.SetPixel(x, y, Color.FromArgb(c.A, Cl(target.R * shade), Cl(target.G * shade), Cl(target.B * shade)));
        }

        // New wordmark: chest crop from the photo with the jersey colour keyed out
        var wb = WordBox(s);
        using (var crop = new Bitmap(wb.Width, wb.Height, PixelFormat.Format32bppArgb)) {
            for (int y = 0; y < wb.Height; y++) for (int x = 0; x < wb.Width; x++) {
                var c = photo.GetPixel(Math.Min(photo.Width - 1, wb.X + x), Math.Min(photo.Height - 1, wb.Y + y));
                double d = Math.Min(Dist(c, body), Dist(c, bg) * 1.4); // jersey colour or the dark wall = background
                int alpha = d < 45 ? 0 : d < 80 ? (int)((d - 45) / 35 * 255) : 255;
                crop.SetPixel(x, y, Color.FromArgb(alpha, c));
            }
            using (var g = Graphics.FromImage(res)) {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingMode = CompositingMode.SourceOver;
                float sc = Math.Min((float)word.Width / crop.Width, (float)word.Height / crop.Height);
                int dw = (int)(crop.Width * sc), dh = (int)(crop.Height * sc);
                g.DrawImage(crop, new Rectangle(word.X + (word.Width - dw) / 2, word.Y + (word.Height - dh) / 2, dw, dh));
            }
        }
        // keep the game's own transparency for every pixel (PS2: 128 = solid)
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { var c = res.GetPixel(x, y); res.SetPixel(x, y, Color.FromArgb(tpx[x, y].A, c.R, c.G, c.B)); }
        return res;
    }

    // Shading under the old wordmark: borrow brightness from fabric just above/below it.
    static double FabricShade(Color[,] t, int x, int y, Color tBody, double lBody) {
        int H = t.GetLength(1);
        for (int d = 1; d < 60; d++) {
            foreach (int yy in new[] { y - d, y + d }) {
                if (yy < 0 || yy >= H) continue;
                var c = t[x, yy]; if (c.A > 40 && Dist(c, tBody) < 50) return Lum(c) / lBody;
            }
        }
        return 1.0;
    }
    static Color Median(List<Color> px) {
        if (px.Count == 0) return Color.Gray;
        Func<Func<Color, int>, int> med = f => px.Select(f).OrderBy(v => v).ElementAt(px.Count / 2);
        return Color.FromArgb(255, med(c => c.R), med(c => c.G), med(c => c.B));
    }

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
