using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public static class Scan {
    // Stream a big file looking for words in UTF-16LE and ASCII; print surrounding text for each hit.
    public static void Find(string path, string[] words, int maxHits) {
        var pats = new List<KeyValuePair<string, byte[]>>();
        foreach (var w in words) { pats.Add(new KeyValuePair<string, byte[]>(w + " (u16)", Encoding.Unicode.GetBytes(w))); pats.Add(new KeyValuePair<string, byte[]>(w + " (asc)", Encoding.ASCII.GetBytes(w))); }
        const int Chunk = 16 << 20, Over = 4096;
        var buf = new byte[Chunk + Over]; long basePos = 0; int hits = 0; int carry = 0;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20)) {
            while (true) {
                int n = fs.Read(buf, carry, Chunk); if (n <= 0) break;
                int len = carry + n;
                for (int i = 0; i < len - 64; i++) {
                    foreach (var p in pats) {
                        var pb = p.Value; if (buf[i] != pb[0] || i + pb.Length > len) continue;
                        bool ok = true; for (int k = 1; k < pb.Length; k++) if (buf[i + k] != pb[k]) { ok = false; break; }
                        if (!ok) continue;
                        bool u16 = p.Key.EndsWith("(u16)");
                        int s = Math.Max(0, i - 200), e = Math.Min(len, i + 400);
                        string ctx = u16 ? Encoding.Unicode.GetString(buf, s - (s % 2 != i % 2 ? 1 : 0) < 0 ? 0 : s - (s % 2 != i % 2 ? 1 : 0), (e - s) & ~1) : Encoding.ASCII.GetString(buf, s, e - s);
                        var sb = new StringBuilder(); foreach (char c in ctx) sb.Append(c >= 32 && c < 127 ? c : '|');
                        Console.WriteLine(string.Format("{0} @{1:X}: {2}", p.Key, basePos + i - carry, sb.ToString().Replace("||", "|")));
                        if (++hits >= maxHits) return;
                    }
                }
                Array.Copy(buf, len - Over, buf, 0, Over); basePos += len - carry; carry = Over;
                basePos -= 0;
            }
        }
        Console.WriteLine("done, hits=" + hits);
    }
}
