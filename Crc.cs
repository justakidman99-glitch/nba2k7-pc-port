using System;
using System.IO;
using System.Collections.Generic;

public static class Crc {
    static uint[] T = Make(0xEDB88320); static uint[] TB = MakeBE(0x04C11DB7);
    static uint[] Make(uint poly) { var t = new uint[256]; for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? poly ^ (c >> 1) : c >> 1; t[i] = c; } return t; }
    static uint[] MakeBE(uint poly) { var t = new uint[256]; for (uint i = 0; i < 256; i++) { uint c = i << 24; for (int k = 0; k < 8; k++) c = (c & 0x80000000) != 0 ? (c << 1) ^ poly : c << 1; t[i] = c; } return t; }
    public static uint Crc32(byte[] b, int off, int len, uint init, bool xorOut) { uint c = init; for (int i = off; i < off + len; i++) c = T[(c ^ b[i]) & 0xFF] ^ (c >> 8); return xorOut ? ~c : c; }
    public static uint Crc32BE(byte[] b, int off, int len, uint init, bool xorOut) { uint c = init; for (int i = off; i < off + len; i++) c = TB[((c >> 24) ^ b[i]) & 0xFF] ^ (c << 8); return xorOut ? ~c : c; }
    public static uint Sum8(byte[] b, int off, int len) { uint s = 0; for (int i = off; i < off + len; i++) s += b[i]; return s; }
    public static uint Sum32(byte[] b, int off, int len) { uint s = 0; for (int i = off; i + 4 <= off + len; i += 4) s += BitConverter.ToUInt32(b, i); return s; }
    public static void Scan(string path, uint[] targets) {
        var b = File.ReadAllBytes(path);
        var set = new HashSet<uint>(targets);
        var starts = new List<int> { 0, 4, 8, 0xC, 0x10, 0x20, 0x2C, 0x30, 0x34, 0x38, 0x40 };
        foreach (int s in starts) {
            foreach (int endTrim in new[] { 0, 4, 8, 16, 32 }) {
                int len = b.Length - s - endTrim; if (len <= 0) continue;
                var results = new Dictionary<string, uint> {
                    { "crc32", Crc32(b, s, len, 0xFFFFFFFF, true) }, { "crc32-noxor", Crc32(b, s, len, 0xFFFFFFFF, false) }, { "crc32-init0", Crc32(b, s, len, 0, false) },
                    { "crc32be", Crc32BE(b, s, len, 0xFFFFFFFF, true) }, { "crc32be-noxor", Crc32BE(b, s, len, 0xFFFFFFFF, false) }, { "crc32be-init0", Crc32BE(b, s, len, 0, false) },
                    { "sum8", Sum8(b, s, len) }, { "sum32", Sum32(b, s, len) } };
                foreach (var kv in results) {
                    if (set.Contains(kv.Value)) Console.WriteLine(string.Format("MATCH {0} start=0x{1:X} trim={2} -> 0x{3:X8}", kv.Key, s, endTrim, kv.Value));
                    ushort lo = (ushort)kv.Value; if (set.Contains(lo)) Console.WriteLine(string.Format("MATCH16 {0} start=0x{1:X} trim={2} -> 0x{3:X4}", kv.Key, s, endTrim, lo));
                }
            }
        }
        Console.WriteLine("scan done");
    }
}
