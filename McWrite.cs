using System;
using System.IO;
using System.Collections.Generic;

// Writes same-length file contents back into a raw PS2 memory card image and fixes page ECC.
public static class McWrite {
    static int[] Par = new int[256]; static int[] ColMask = new int[256];
    static McWrite() {
        int[] cp = { 0x55, 0x33, 0x0F, 0x00, 0xAA, 0xCC, 0xF0 };
        for (int b = 0; b < 256; b++) { int c = 0, x = b; while (x != 0) { c ^= x & 1; x >>= 1; } Par[b] = c; }
        for (int b = 0; b < 256; b++) { int m = 0; for (int i = 0; i < cp.Length; i++) m |= Par[b & cp[i]] << i; ColMask[b] = m; }
    }
    public static byte[] Ecc128(byte[] d, int off) {
        int col = 0x77, l0 = 0x7F, l1 = 0x7F;
        for (int i = 0; i < 128; i++) { int b = d[off + i]; col ^= ColMask[b]; if (Par[b] != 0) { l0 ^= ~i; l1 ^= i; } }
        return new byte[] { (byte)col, (byte)(l0 & 0x7F), (byte)l1 };
    }
    public static void FixPageEcc(byte[] card, int page) {
        int p = page * 528;
        for (int c = 0; c < 4; c++) { var e = Ecc128(card, p + c * 128); Array.Copy(e, 0, card, p + 512 + c * 3, 3); }
    }
    // Returns number of pages (of the first n) whose stored ECC differs from computed.
    public static int VerifyEcc(string path, int maxPages) {
        var card = File.ReadAllBytes(path); int bad = 0, n = Math.Min(maxPages, card.Length / 528), checkedPages = 0;
        for (int pg = 0; pg < n; pg++) {
            int p = pg * 528; bool blank = true;
            for (int k = 0; k < 528 && blank; k++) if (card[p + k] != 0xFF) blank = false;
            if (blank) continue; checkedPages++;
            for (int c = 0; c < 4; c++) { var e = Ecc128(card, p + c * 128); if (e[0] != card[p + 512 + c * 3] || e[1] != card[p + 513 + c * 3] || e[2] != card[p + 514 + c * 3]) { bad++; break; } }
        }
        Console.WriteLine("checked " + checkedPages + " non-blank pages, bad=" + bad);
        return bad;
    }
    public static void Replace(string cardIn, string cardOut, string dirName, string fileName, byte[] data) {
        var m = new Mc(cardIn); var card = m.D;
        var root0 = m.Dir(m.RootClus, 1)[0];
        Mc.Ent dir = null; foreach (var e in m.Dir(m.RootClus, root0.Len)) if ((e.Mode & 0x8000) != 0 && e.Name == dirName) dir = e;
        if (dir == null) throw new Exception("dir not found");
        Mc.Ent file = null; foreach (var f in m.Dir(dir.Clus, dir.Len)) if ((f.Mode & 0x8000) != 0 && f.Name == fileName) file = f;
        if (file == null) throw new Exception("file not found");
        if (file.Len != data.Length) throw new Exception("length differs: " + file.Len + " vs " + data.Length);
        int pos = 0;
        foreach (var c in m.Chain(file.Clus)) {
            uint abs = c + m.AllocOff;
            for (int p = 0; p < m.Ppc && pos < data.Length; p++) {
                int page = (int)(abs * m.Ppc + p); int n = Math.Min(512, data.Length - pos);
                Array.Copy(data, pos, card, page * 528, n); pos += n;
                FixPageEcc(card, page);
            }
        }
        File.WriteAllBytes(cardOut, card);
        Console.WriteLine("wrote " + pos + " bytes of " + dirName + "/" + fileName + " -> " + cardOut);
    }
}
