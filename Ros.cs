using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public static class Ros {
    public const int PlayerBase = 0x9D48, PlayerSize = 0xB0, PlayerCount = 754;
    public static string Str(byte[] b, int ptrPos) {
        uint v = BitConverter.ToUInt32(b, ptrPos); if (v == 0) return "";
        int p = (int)(ptrPos + v - 1); if (p < 0 || p >= b.Length) return "?";
        var sb = new StringBuilder(); while (p + 1 < b.Length) { char c = (char)BitConverter.ToUInt16(b, p); if (c == 0) break; sb.Append(c); p += 2; if (sb.Length > 40) break; }
        return sb.ToString();
    }
    public static string Name(byte[] b, int i) { int r = PlayerBase + i * PlayerSize; return Str(b, r + 0xC) + " " + Str(b, r + 8); }
    public static void DumpPlayers(string path, int from, int count) {
        var b = File.ReadAllBytes(path);
        for (int i = from; i < from + count; i++) {
            int r = PlayerBase + i * PlayerSize;
            Console.WriteLine(string.Format("#{0} @0x{1:X} {2}", i, r, Name(b, i)));
            for (int k = 0; k < PlayerSize; k += 16) {
                var sb = new StringBuilder(); sb.AppendFormat("  +{0:X2}: ", k);
                for (int j = 0; j < 16; j++) sb.AppendFormat("{0:X2} ", b[r + k + j]);
                Console.WriteLine(sb);
            }
        }
    }
    public static void ListNames(string path) {
        var b = File.ReadAllBytes(path);
        for (int i = 0; i < PlayerCount; i++) Console.WriteLine(i + "\t" + Name(b, i));
    }
}
