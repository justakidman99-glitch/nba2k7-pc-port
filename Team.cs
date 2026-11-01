using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public static class Team {
    public const int Base = 0x1240, Size = 452, Count = 66;
    // Print each team's strings (any self-relative pointer that resolves to text) and u16 values that look like player indices.
    public static void Dump(string path, int from, int count, bool hex) {
        var b = File.ReadAllBytes(path);
        for (int t = from; t < from + count; t++) {
            int r = Base + t * Size;
            var strs = new List<string>();
            for (int k = 0; k + 4 <= Size; k += 4) {
                uint v = BitConverter.ToUInt32(b, r + k);
                if ((v & 1) == 1 && v > 0x100 && r + k + v - 1 < b.Length && r + k + v - 1 >= 0x56000) { var s = Ros.Str(b, r + k); if (s.Length > 0 && s != "?") strs.Add(string.Format("+{0:X}:{1}", k, s)); }
            }
            Console.WriteLine(string.Format("T{0} @0x{1:X} {2}", t, r, string.Join(" | ", strs.ToArray())));
            if (hex) for (int k = 0; k < Size; k += 32) {
                var sb = new StringBuilder(); sb.AppendFormat("  +{0:X3}: ", k);
                for (int j = 0; j < 32 && k + j < Size; j++) sb.AppendFormat("{0:X2}", b[r + k + j]).Append(j % 4 == 3 ? " " : "");
                Console.WriteLine(sb);
            }
        }
    }
}
