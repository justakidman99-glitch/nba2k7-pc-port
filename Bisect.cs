using System;
using System.IO;

// Builds test saves that apply only one group of changes from a full build onto the original save.
// The full build must use the same record slots as the original (KeepOriginalSizes build).
public static class Bisect {
    static void CopyField(byte[] dst, byte[] src, int from, int to) {
        for (int i = 0; i < 380; i++) Copy(dst, src, Ros.PlayerBase + i * Ros.PlayerSize, from, to);
        for (int i = 628; i <= 739; i++) Copy(dst, src, Ros.PlayerBase + i * Ros.PlayerSize, from, to);
    }
    static void Copy(byte[] dst, byte[] src, int rec, int from, int to) { for (int k = from; k < to; k++) dst[rec + k] = src[rec + k]; }

    public static byte[] Make(byte[] orig, byte[] full, string group) {
        var b = (byte[])orig.Clone();
        switch (group) {
            case "names": {
                // grown string pool + moved block B + header size + name pointers
                int oA = 0x40 + (int)BitConverter.ToUInt32(orig, 0x0C), fA = 0x40 + (int)BitConverter.ToUInt32(full, 0x0C);
                Array.Copy(full, oA, b, oA, b.Length - oA);
                BitConverter.GetBytes(BitConverter.ToUInt32(full, 0x0C)).CopyTo(b, 0x0C);
                CopyField(b, full, 0x08, 0x10);
                break;
            }
            case "body": CopyField(b, full, 0x1D, 0x20); CopyField(b, full, 0x2F, 0x30); CopyField(b, full, 0x34, 0x37); break;
            case "ratings": CopyField(b, full, 0x82, 0xAE); break;
            case "history": CopyField(b, full, 0x3C, 0x68); break;
            case "career": CopyField(b, full, 0x24, 0x28); CopyField(b, full, 0x29, 0x2A); CopyField(b, full, 0x2C, 0x2E); CopyField(b, full, 0x33, 0x34); break;
            case "lineups":
                for (int t = 0; t < 32; t++) { int tr = Team.Base + t * Team.Size; Array.Copy(full, tr + 0xE8, b, tr + 0xE8, 35 * 4); }
                break;
            default: throw new Exception("unknown group " + group);
        }
        return b;
    }
}
