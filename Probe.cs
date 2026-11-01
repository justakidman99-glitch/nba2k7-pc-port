using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public static class Probe {
    // Find a word stored with any constant per-char offset, as 8-bit or 16-bit units.
    public static void FindShifted(string path, string word) {
        var b = File.ReadAllBytes(path);
        foreach (int unit in new[] { 1, 2 }) {
            for (int i = 0; i + word.Length * unit <= b.Length; i++) {
                int v0 = unit == 1 ? b[i] : BitConverter.ToUInt16(b, i);
                int delta = v0 - word[0]; bool ok = true;
                for (int k = 1; k < word.Length && ok; k++) {
                    int v = unit == 1 ? b[i + k * unit] : BitConverter.ToUInt16(b, i + k * unit);
                    if (v - word[k] != delta) ok = false;
                }
                if (ok) Console.WriteLine(string.Format("{0} unit={1} @0x{2:X} delta={3}", word, unit, i, delta));
            }
        }
    }
    public static void Dump(string path, int off, int len) {
        var b = File.ReadAllBytes(path);
        for (int i = off; i < off + len && i < b.Length; i += 16) {
            var sb = new StringBuilder(); var asc = new StringBuilder();
            sb.AppendFormat("{0:X6}: ", i);
            for (int k = 0; k < 16 && i + k < b.Length; k++) { sb.AppendFormat("{0:X2} ", b[i + k]); char c = (char)b[i + k]; asc.Append(c >= 32 && c < 127 ? c : '.'); }
            Console.WriteLine(sb + " " + asc);
        }
    }
}
