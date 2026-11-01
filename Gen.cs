using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;

// Builds a 2026-27 roster save from the original NBA 2K7 roster save.
public static class Gen {
    public class P { public string Name, First, Last, Real; public int Team, Num, Pos, Ht, Wt, Exp, Ovr; public bool TwoWay; }

    static readonly Dictionary<string, int> TeamIdx = new Dictionary<string, int> {
        {"Philadelphia 76ers",0},{"Portland Trail Blazers",1},{"Charlotte Hornets",2},{"Milwaukee Bucks",3},{"Chicago Bulls",4},{"Cleveland Cavaliers",5},
        {"Boston Celtics",6},{"LA Clippers",7},{"Memphis Grizzlies",8},{"Atlanta Hawks",9},{"Miami Heat",10},{"New Orleans Pelicans",11},{"Utah Jazz",12},
        {"Sacramento Kings",13},{"New York Knicks",14},{"Los Angeles Lakers",15},{"Orlando Magic",16},{"Dallas Mavericks",17},{"Brooklyn Nets",18},
        {"Denver Nuggets",19},{"Indiana Pacers",20},{"Detroit Pistons",21},{"Toronto Raptors",22},{"Houston Rockets",23},{"Oklahoma City Thunder",24},
        {"San Antonio Spurs",25},{"Phoenix Suns",26},{"Minnesota Timberwolves",27},{"Golden State Warriors",28},{"Washington Wizards",29} };
    static readonly int[] East = { 0, 2, 3, 4, 5, 6, 9, 10, 14, 16, 18, 20, 21, 22, 29 };
    static readonly int[] West = { 1, 7, 8, 11, 12, 13, 15, 17, 19, 23, 24, 25, 26, 27, 28 };
    static readonly int[] SkillCols = { 0, 1, 2, 3, 4, 5, 8, 9, 12, 13, 14, 15, 16, 17, 18, 20, 21, 22, 24, 25, 27, 28 };
    const int RatingOff = 0x82, ProxyCol = 28, RosterMax = 15;
    const int MaxHeight = 89; // tallest player in the original game (Yao, 7-5)
    static int appendPos;
    public static bool KeepOriginalSizes = false; // true: each team keeps its original roster size and slots

    static string Ascii(string s) {
        var d = s.Normalize(NormalizationForm.FormD); var sb = new StringBuilder();
        foreach (var c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
    static int PosCode(string p) { switch (p) { case "PG": return 0; case "SG": return 1; case "SF": return 2; case "PF": return 3; default: return 4; } }

    public static List<P> LoadPlayers(string html, string expFile) {
        var src = File.ReadAllText(html, Encoding.UTF8);
        var ovr = new Dictionary<string, int>();
        var ovrBlock = Regex.Match(src, @"const OVR = \{(.*?)\};", RegexOptions.Singleline).Groups[1].Value;
        foreach (Match m in Regex.Matches(ovrBlock, "\"([^\"]+)\":(\\d+)")) ovr[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);
        var exp = new Dictionary<string, int>();
        foreach (var part in File.ReadAllText(expFile, Encoding.UTF8).Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)) {
            var kv = part.Split('='); if (kv.Length == 2) exp[kv[0].Trim()] = int.Parse(kv[1].Trim());
        }
        var list = new List<P>();
        foreach (Match tm in Regex.Matches(src, "\\[\"([^\"]+)\",\"[^\"]+\",\"[^\"]+\",\\[(.*?)\\]\\]")) {
            string team = tm.Groups[1].Value; if (!TeamIdx.ContainsKey(team)) throw new Exception("unknown team " + team);
            foreach (Match pm in Regex.Matches(tm.Groups[2].Value, "\"([^\"]*)\"")) {
                var f = pm.Groups[1].Value.Split('|');
                var p = new P { Real = f[1], Team = TeamIdx[team], Pos = PosCode(f[2]), TwoWay = f.Length > 5 && f[5] == "tw" };
                p.Num = f[0] == "" ? 0 : (f[0] == "00" ? 0 : int.Parse(f[0]));
                var ht = f[3].Split('-'); p.Ht = int.Parse(ht[0]) * 12 + int.Parse(ht[1]); p.Wt = int.Parse(f[4]);
                if (!ovr.ContainsKey(p.Real)) throw new Exception("no OVR for " + p.Real);
                if (!exp.ContainsKey(p.Real)) throw new Exception("no exp for " + p.Real);
                p.Ovr = ovr[p.Real]; p.Exp = exp[p.Real];
                p.Name = Ascii(p.Real); int sp = p.Name.IndexOf(' ');
                if (sp < 0) { p.First = ""; p.Last = p.Name; } // one-word name: blank first name, like 2K7's own "Nene"
                else { p.First = p.Name.Substring(0, sp); p.Last = p.Name.Substring(sp + 1); }
                list.Add(p);
            }
        }
        return list;
    }

    static int Rec(int i) { return Ros.PlayerBase + i * Ros.PlayerSize; }
    static void PutPtr(byte[] b, int pos, int target) { BitConverter.GetBytes((uint)(target - pos + 1)).CopyTo(b, pos); }
    static int GetPtr(byte[] b, int pos) { uint v = BitConverter.ToUInt32(b, pos); return v == 0 ? -1 : (int)(pos + v - 1); }

    // Best five available players (at least one guard and one big), ordered PG..C into lineup slots.
    static int[] PickFive(List<P> pool, HashSet<P> used) {
        var cands = pool.Where(p => !used.Contains(p)).OrderByDescending(p => p.Ovr).ToList();
        var five = cands.Take(5).ToList();
        Func<P, bool> guard = p => p.Pos <= 1, big = p => p.Pos >= 3;
        foreach (var need in new[] { guard, big }) {
            if (five.Any(need)) continue;
            var add = cands.Skip(5).FirstOrDefault(need); if (add == null) continue;
            var drop = five.Where(p => !(guard(p) && five.Count(guard) == 1) && !(big(p) && five.Count(big) == 1)).OrderBy(p => p.Ovr).First();
            five.Remove(drop); five.Add(add);
        }
        foreach (var p in five) used.Add(p);
        var ordered = five.OrderBy(p => p.Pos).ThenByDescending(p => p.Ovr).ToList();
        var res = new int[5];
        for (int s = 0; s < 5; s++) res[s] = s < ordered.Count ? pool.IndexOf(ordered[s]) : -1;
        return res;
    }

    public static void Build(string origSave, string html, string expFile, string outSave, string report) {
        var orig = File.ReadAllBytes(origSave); var b = (byte[])orig.Clone();
        var log = new StringBuilder();
        var players = LoadPlayers(html, expFile);
        log.AppendLine("players loaded: " + players.Count);

        // Record pools
        var teamSlots = new List<int>[30];
        for (int t = 0; t < 30; t++) teamSlots[t] = Decode.RosterIdx(orig, t).ToList();
        var fa = new List<int>();
        for (int i = 628; i <= 739; i++) if (BitConverter.ToUInt16(orig, Rec(i)) == 4 && Ros.Name(orig, i).Trim().Length > 0) fa.Add(i);
        log.AppendLine("free agent records: " + fa.Count);

        // Assign players to records
        var assigned = new Dictionary<int, P>(); var onTeam = new List<P>[30]; var leftovers = new List<P>();
        int faNext = 0;
        for (int t = 0; t < 30; t++) {
            var roster = players.Where(p => p.Team == t).OrderByDescending(p => p.Ovr).ThenBy(p => p.TwoWay).ToList();
            var slots = teamSlots[t];
            int size = KeepOriginalSizes ? slots.Count : RosterMax;
            onTeam[t] = roster.Take(size).ToList(); leftovers.AddRange(roster.Skip(size));
            while (slots.Count > onTeam[t].Count) slots.RemoveAt(slots.Count - 1); // shouldn't happen (teams have 15+), kept for safety
            while (slots.Count < onTeam[t].Count) slots.Add(fa[faNext++]);
            // A 2026 player who is already in 2K7 (LeBron) keeps his own record, so he keeps his own head and face.
            var free = new List<int>(slots); var waiting = new List<P>();
            foreach (var p in onTeam[t]) {
                int own = free.FirstOrDefault(r => Ros.Name(orig, r) == p.Name);
                if (own != 0 || (free.Contains(0) && Ros.Name(orig, 0) == p.Name)) { assigned[own] = p; free.Remove(own); log.AppendLine("  kept own 2K7 record: " + p.Name + " -> slot " + own); }
                else waiting.Add(p);
            }
            for (int k = 0; k < waiting.Count; k++) assigned[free[k]] = waiting[k];
        }
        var faLeft = fa.Skip(faNext).ToList();
        var faPlayers = leftovers.OrderByDescending(p => p.Ovr).Take(faLeft.Count).ToList();
        for (int k = 0; k < faPlayers.Count; k++) assigned[faLeft[k]] = faPlayers[k];
        log.AppendLine(string.Format("team players: {0}, FA records used for teams: {1}, 2026 free agents: {2}, dropped: {3}",
            onTeam.Sum(l => l.Count), faNext, faPlayers.Count, leftovers.Count - faPlayers.Count));
        foreach (var p in leftovers.OrderByDescending(p => p.Ovr).Skip(faLeft.Count)) log.AppendLine("  dropped: " + p.Real + " (" + p.Ovr + ")");

        // Templates: original real players with ratings
        var templ = Enumerable.Range(0, 380).Concat(fa).ToList();
        var proxies = templ.Select(i => (int)orig[Rec(i) + RatingOff + ProxyCol]).OrderBy(x => x).ToArray();
        var ovrSorted = assigned.Values.Select(p => p.Ovr).OrderBy(x => x).ToArray();
        Func<int, int> mapOvr = o => {
            int lo = Array.FindIndex(ovrSorted, x => x >= o), hi = Array.FindLastIndex(ovrSorted, x => x <= o);
            double q = ((lo + hi) / 2.0) / (ovrSorted.Length - 1);
            return proxies[(int)Math.Round(q * (proxies.Length - 1))];
        };

        // Write player records
        foreach (var kv in assigned) {
            int r = Rec(kv.Key); var p = kv.Value; int target = mapOvr(p.Ovr);
            int best = -1; double bestScore = 1e9;
            foreach (var ti in templ) {
                int tr = Rec(ti); if (orig[tr + 0x34] != p.Pos) continue;
                double s = Math.Abs(orig[tr + RatingOff + ProxyCol] - target) * 3 + Math.Abs(orig[tr + 0x1D] - p.Ht);
                if (s < bestScore) { bestScore = s; best = ti; }
            }
            int tb = Rec(best); int delta = target - orig[tb + RatingOff + ProxyCol];
            for (int c = 0; c < 44; c++) b[r + RatingOff + c] = orig[tb + RatingOff + c];
            foreach (int c in SkillCols) { int ov = orig[tb + RatingOff + c]; if (ov >= 25) b[r + RatingOff + c] = (byte)Math.Max(25, Math.Min(99, ov + delta)); }

            b[r + 0x1D] = (byte)Math.Min(MaxHeight, p.Ht); BitConverter.GetBytes((ushort)p.Wt).CopyTo(b, r + 0x1E);
            b[r + 0x2F] = (byte)p.Num; b[r + 0x34] = (byte)p.Pos; b[r + 0x35] = 5; b[r + 0x36] = 5;
            int realDraft = 2026 - p.Exp;
            b[r + 0x33] = (byte)((realDraft - 20) % 100);
            b[r + 0x29] = (byte)(p.Exp + 1);
            uint bd = BitConverter.ToUInt32(orig, r + 0x24);
            uint yy = (uint)(realDraft - 19 - 20 - 1900);
            bd = (bd & ~(0x7Fu << 19)) | ((yy & 0x7F) << 19);
            BitConverter.GetBytes(bd).CopyTo(b, r + 0x24);
            int sal = p.Ovr >= 95 ? 2000 : p.Ovr >= 90 ? 1600 : p.Ovr >= 85 ? 1200 : p.Ovr >= 80 ? 800 : p.Ovr >= 75 ? 450 : p.Ovr >= 70 ? 200 : 80;
            BitConverter.GetBytes((ushort)sal).CopyTo(b, r + 0x2C);
            for (int k = 0x3C; k < 0x68; k++) b[r + k] = 0xFF; // no career history
        }

        // String space: free player-name strings referenced only by overwritten records
        var refs = new Dictionary<int, int>();
        for (int i = 0; i + 4 <= orig.Length; i++) { uint uv = BitConverter.ToUInt32(orig, i); if ((uv & 1) == 1 && uv < orig.Length) { int t = (int)(i + uv - 1); if (t < orig.Length) { int c; refs.TryGetValue(t, out c); refs[t] = c + 1; } } }
        var known = new Dictionary<int, int>();
        foreach (var rec in assigned.Keys) foreach (int off in new[] { 8, 0xC }) { int t = GetPtr(orig, Rec(rec) + off); if (t < 0) continue; int c; known.TryGetValue(t, out c); known[t] = c + 1; }
        var holes = new List<int[]>(); // [start, length]
        foreach (var kv in known) {
            if (refs[kv.Key] != kv.Value) continue;
            int s = kv.Key, e = s; while (BitConverter.ToUInt16(orig, e) != 0) e += 2; e += 2;
            holes.Add(new[] { s, e - s });
        }
        holes.Sort((x, y) => x[0].CompareTo(y[0]));
        var merged = new List<int[]>();
        foreach (var h in holes) { if (merged.Count > 0 && merged[merged.Count - 1][0] + merged[merged.Count - 1][1] == h[0]) merged[merged.Count - 1][1] += h[1]; else merged.Add(new[] { h[0], h[1] }); }
        int freeBytes = merged.Sum(h => h[1]);
        foreach (var h in merged) for (int k = 0; k < h[1]; k++) b[h[0] + k] = 0;
        var placed = new Dictionary<string, int>();
        // Reuse text that is still live in the string table (whole strings or their tails), e.g. "Williams", "Chris".
        int strStart = 0x566C0, strEnd = strStart;
        while (strEnd + 2 <= b.Length) { ushort ch = BitConverter.ToUInt16(orig, strEnd); if (ch != 0 && (ch < 0x20 || ch > 0x7E)) break; strEnd += 2; }
        for (int s0 = strStart; s0 < strEnd; ) {
            int e = s0; while (e < strEnd && BitConverter.ToUInt16(b, e) != 0) e += 2;
            if (e > s0) { var text = Encoding.Unicode.GetString(b, s0, e - s0); if (!placed.ContainsKey(text)) placed[text] = s0; }
            s0 = e + 2;
        }
        log.AppendLine(string.Format("string table 0x{0:X}-0x{1:X}, {2} reusable texts", strStart, strEnd, placed.Count));
        int reusableCount = placed.Count;
        int aSize = (int)BitConverter.ToUInt32(orig, 0x0C), bSize = (int)BitConverter.ToUInt32(orig, 0x08);
        int aEnd = 0x40 + aSize, bEnd = aEnd + bSize;
        if (aEnd != strEnd) throw new Exception(string.Format("block A end 0x{0:X} != string pool end 0x{1:X}", aEnd, strEnd));
        var blockB = new byte[bSize]; Array.Copy(orig, aEnd, blockB, 0, bSize);
        for (int k = bEnd; k < orig.Length; k++) if (orig[k] != 0) throw new Exception("slack not empty at 0x" + k.ToString("X"));
        for (int k = aEnd; k < b.Length; k++) b[k] = 0;
        appendPos = aEnd;
        Func<string, int> alloc = s => {
            int at; if (placed.TryGetValue(s, out at)) return at;
            int need = (s.Length + 1) * 2;
            foreach (var h in merged) if (h[1] >= need) {
                at = h[0]; Encoding.Unicode.GetBytes(s).CopyTo(b, at); b[at + need - 2] = 0; b[at + need - 1] = 0;
                h[0] += need; h[1] -= need; placed[s] = at; return at;
            }
            // Append to the end of block A (the string pool); block B is moved down afterwards.
            at = appendPos;
            Encoding.Unicode.GetBytes(s).CopyTo(b, at); b[at + need - 2] = 0; b[at + need - 1] = 0;
            appendPos += need; placed[s] = at; return at;
        };
        foreach (var kv in assigned.OrderByDescending(k => k.Value.Last.Length + k.Value.First.Length)) {
            int r = Rec(kv.Key);
            PutPtr(b, r + 8, alloc(kv.Value.Last)); PutPtr(b, r + 0xC, alloc(kv.Value.First));
        }

        // Team rosters and lineups
        var recOf = assigned.ToDictionary(kv => kv.Value, kv => kv.Key);
        for (int t = 0; t < 30; t++) {
            int tr = Team.Base + t * Team.Size;
            for (int k = 0; k < 18; k++) {
                int pos = tr + k * 4;
                if (k < onTeam[t].Count) PutPtr(b, pos, Rec(recOf[onTeam[t][k]])); else BitConverter.GetBytes(0u).CopyTo(b, pos);
            }
            b[tr + 0x5D] = (byte)onTeam[t].Count; // roster count must match the pointer list
            var used = new HashSet<P>();
            var start = PickFive(onTeam[t], used); var bench = PickFive(onTeam[t], used);
            for (int s = 0; s < 5; s++) if (start[s] >= 0) b[Rec(recOf[onTeam[t][start[s]]]) + 0x36] = (byte)s;
            WriteLineups(b, tr, onTeam[t], start, bench, recOf);
        }
        // All-Star teams: best players by conference
        foreach (var conf in new[] { new { T = 30, Teams = East }, new { T = 31, Teams = West } }) {
            var pool = conf.Teams.SelectMany(t => onTeam[t]).ToList(); var used = new HashSet<P>();
            var start = PickFive(pool, used); var bench = PickFive(pool, used);
            WriteLineups(b, Team.Base + conf.T * Team.Size, pool, start, bench, recOf);
        }

        // Team names
        int tm;
        tm = Team.Base + 2 * Team.Size; PutPtr(b, tm + 0x48, GetPtr(orig, Team.Base + 11 * Team.Size + 0x48)); // Bobcats -> Hornets
        tm = Team.Base + 11 * Team.Size; PutPtr(b, tm + 0x48, alloc("Pelicans"));
        tm = Team.Base + 18 * Team.Size; int bk = alloc("Brooklyn"); PutPtr(b, tm + 0x74, bk); PutPtr(b, tm + 0x7C, bk);
        tm = Team.Base + 24 * Team.Size; PutPtr(b, tm + 0x48, alloc("Thunder")); int okc = alloc("Oklahoma City"); PutPtr(b, tm + 0x74, okc); PutPtr(b, tm + 0x7C, okc);

        int leftFree = merged.Sum(h => h[1]);
        // Close block A on a 64-byte boundary, update its size, and move block B after it.
        int grow = ((appendPos - aEnd) + 63) & ~63;
        int newAEnd = aEnd + grow;
        if (newAEnd + bSize > b.Length) throw new Exception("file too small for grown block A");
        Array.Copy(blockB, 0, b, newAEnd, bSize);
        BitConverter.GetBytes((uint)(aSize + grow)).CopyTo(b, 0x0C);
        log.AppendLine(string.Format("string space: {0} bytes freed, {1} left; {2} texts reused whole; {3} bytes of new names appended; block A 0x{4:X}->0x{5:X}, block B now at 0x{5:X}-0x{6:X}",
            freeBytes, leftFree, reusableCount, appendPos - aEnd, aSize, aSize + grow, newAEnd + bSize));
        File.WriteAllBytes(outSave, b);

        // Verification pass: re-read everything from the output
        var v = File.ReadAllBytes(outSave); int errors = 0;
        for (int t = 0; t < 32; t++) {
            int tr = Team.Base + t * Team.Size;
            log.AppendLine(string.Format("== T{0} {1} {2}", t, Ros.Str(v, tr + 0x74), Ros.Str(v, tr + 0x48)));
            if (t < 30 && v[tr + 0x5D] != Decode.RosterIdx(v, t).Length) { errors++; log.AppendLine("  roster count mismatch"); }
            if (t < 30) foreach (int ix in Decode.RosterIdx(v, t)) {
                if (ix < 0) { errors++; log.AppendLine("  BAD roster pointer"); continue; }
                int r = Rec(ix); var p = assigned[ix];
                string nm = Ros.Name(v, ix); if (nm != p.First + " " + p.Last) { errors++; log.AppendLine("  NAME MISMATCH " + nm + " vs " + p.Name); }
                uint bd = BitConverter.ToUInt32(v, r + 0x24);
                log.AppendLine(string.Format("  #{0,-3} {1,-28} pos={2} role={3} {4}\"/{5}lb proxy={6} target={7} born={8} yrs={9} slot={10}",
                    v[r + 0x2F], nm, "PG SG SF PF C".Split(' ')[v[r + 0x34]], v[r + 0x36], v[r + 0x1D], BitConverter.ToUInt16(v, r + 0x1E),
                    v[r + RatingOff + ProxyCol], p.Ovr, 1900 + ((bd >> 19) & 0x7F), v[r + 0x29], ix));
            }
            var lu = new List<string>();
            for (int k = 0; k < 35; k++) { int a = GetPtr(v, tr + 0xE8 + k * 4); int ix = Decode.IndexOfRecord(a); if (ix < 0) { errors++; lu.Add("BAD"); } else if (k < 10) lu.Add(Ros.Name(v, ix)); }
            log.AppendLine("  lineup1: " + string.Join(", ", lu.Take(5).ToArray()) + " | lineup2: " + string.Join(", ", lu.Skip(5).Take(5).ToArray()));
        }
        log.AppendLine("== free agents");
        foreach (var i in faLeft) log.AppendLine("  " + Ros.Name(v, i) + " " + (assigned.ContainsKey(i) ? assigned[i].Ovr.ToString() : "(old)"));
        // every player and team name pointer must land inside block A, at the start of a string
        int vAEnd = 0x40 + (int)BitConverter.ToUInt32(v, 0x0C);
        Action<int, string> checkStr = (pos, what) => {
            int a = GetPtr(v, pos); if (a < 0) return;
            if (a < 0x566C0 || a >= vAEnd) { errors++; log.AppendLine("pointer outside string pool: " + what); }
            else if (BitConverter.ToUInt16(v, a - 2) != 0) { errors++; log.AppendLine("pointer into middle of a string: " + what); }
        };
        for (int i = 0; i < Ros.PlayerCount; i++) foreach (int off in new[] { 8, 0xC }) checkStr(Rec(i) + off, "player " + i);
        for (int t = 0; t < Team.Count; t++) foreach (int off in new[] { 0x48, 0x4C, 0x50, 0x74, 0x78, 0x7C }) checkStr(Team.Base + t * Team.Size + off, "team " + t);
        var vb = new byte[BitConverter.ToUInt32(v, 0x08)]; Array.Copy(v, vAEnd, vb, 0, vb.Length);
        var ob = new byte[BitConverter.ToUInt32(orig, 0x08)]; Array.Copy(orig, 0x40 + (int)BitConverter.ToUInt32(orig, 0x0C), ob, 0, ob.Length);
        if (Convert.ToBase64String(vb) != Convert.ToBase64String(ob)) { errors++; log.AppendLine("block B changed"); }
        for (int k = vAEnd + vb.Length; k < v.Length; k++) if (v[k] != 0) { errors++; log.AppendLine("slack not zero"); break; }
        if (v.Length != orig.Length) { errors++; log.AppendLine("file length changed"); }
        log.AppendLine("legend check: " + Ros.Name(v, 441) + ", " + Ros.Name(v, 418) + ", created slot: " + Ros.Name(v, 500));
        log.AppendLine("ERRORS: " + errors);
        File.WriteAllText(report, log.ToString(), Encoding.UTF8);
        Console.WriteLine(log.ToString().Split('\n').Take(8).Aggregate((a, c) => a + "\n" + c));
        Console.WriteLine("ERRORS: " + errors);
    }

    static void WriteLineups(byte[] b, int tr, List<P> pool, int[] start, int[] bench, Dictionary<P, int> recOf) {
        for (int L = 0; L < 7; L++) {
            var five = L == 1 ? bench : start;
            for (int s = 0; s < 5; s++) {
                int pi = five[s] >= 0 ? five[s] : start[s];
                PutPtr(b, tr + 0xE8 + (L * 5 + s) * 4, Rec(recOf[pool[pi]]));
            }
        }
    }
}
