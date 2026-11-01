using System;
using System.IO;
using System.Collections.Generic;

public static class Decode {
    // Brute-force a bitfield layout for birthdate in the u32 at +0x24.
    public static void BirthBits(string path, int[] idx, int[] y, int[] m, int[] d) {
        var b = File.ReadAllBytes(path);
        var vals = new uint[idx.Length];
        for (int i = 0; i < idx.Length; i++) vals[i] = BitConverter.ToUInt32(b, Ros.PlayerBase + idx[i] * Ros.PlayerSize + 0x24);
        for (int dOff = 0; dOff < 28; dOff++)
        for (int mOff = 0; mOff < 28; mOff++)
        for (int yOff = 0; yOff < 26; yOff++)
        for (int yBits = 7; yBits <= 12; yBits++) {
            if (yOff + yBits > 32) continue;
            bool ok = true; int yBase = int.MinValue;
            for (int i = 0; i < idx.Length && ok; i++) {
                uint v = vals[i];
                int dd = (int)((v >> dOff) & 31), mm = (int)((v >> mOff) & 15), yy = (int)((v >> yOff) & ((1u << yBits) - 1));
                if (dd != d[i]) { ok = false; break; }
                if (mm != m[i] && mm != m[i] - 1) { ok = false; break; }
                int baseHere = y[i] - yy; if (yBase == int.MinValue) yBase = baseHere; else if (baseHere != yBase) ok = false;
            }
            if (ok) Console.WriteLine(string.Format("day@{0} month@{1} (0- or 1-based) year@{2}/{3}bits base={4}", dOff, mOff, yOff, yBits, yBase));
        }
        Console.WriteLine("birth scan done");
    }
    public static int IndexOfRecord(int addr) { int o = addr - Ros.PlayerBase; return (o >= 0 && o % Ros.PlayerSize == 0) ? o / Ros.PlayerSize : -1; }
    // Decode team pointer lists as player indices.
    public static void TeamLists(string path, int team) {
        var b = File.ReadAllBytes(path); int r = Team.Base + team * Team.Size;
        Console.Write("roster (+0x00..+0x47): ");
        for (int k = 0; k < 0x48; k += 4) { uint v = BitConverter.ToUInt32(b, r + k); if (v == 0) { Console.Write("- "); continue; } int a = (int)(r + k + v - 1); int ix = IndexOfRecord(a); Console.Write(ix >= 0 ? ix + " " : string.Format("[0x{0:X}] ", a)); }
        Console.WriteLine();
        Console.Write("list (+0xC0..+0x177): ");
        for (int k = 0xC0; k < 0x178; k += 4) { uint v = BitConverter.ToUInt32(b, r + k); if (v == 0) { Console.Write("- "); continue; } int a = (int)(r + k + v - 1); int ix = IndexOfRecord(a); Console.Write(ix >= 0 ? ix + " " : string.Format("[0x{0:X}] ", a)); }
        Console.WriteLine();
    }
    public static int[] RosterIdx(byte[] b, int team) {
        int r = Team.Base + team * Team.Size; var l = new List<int>();
        for (int k = 0; k < 0x48; k += 4) { uint v = BitConverter.ToUInt32(b, r + k); if (v == 0) continue; l.Add(IndexOfRecord((int)(r + k + v - 1))); }
        return l.ToArray();
    }
}
