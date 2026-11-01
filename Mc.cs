using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

// Minimal PS2 memory card (raw, with ECC) reader.
public class Mc {
    public byte[] D; public int PageLen, Ppc, RawPage, ClusterSize; public uint AllocOff, RootClus; public uint[] Ifc = new uint[32];
    public Mc(string path) {
        D = File.ReadAllBytes(path);
        PageLen = BitConverter.ToUInt16(D, 0x28); Ppc = BitConverter.ToUInt16(D, 0x2A);
        RawPage = (D.Length % 528 == 0 && D.Length / 528 * 528 == D.Length && D.Length != PageLen * (D.Length / PageLen)) ? 528 : (D.Length % 528 == 0 ? 528 : 512);
        ClusterSize = PageLen * Ppc;
        AllocOff = BitConverter.ToUInt32(D, 0x34); RootClus = BitConverter.ToUInt32(D, 0x3C);
        for (int i = 0; i < 32; i++) Ifc[i] = BitConverter.ToUInt32(D, 0x50 + 4 * i);
    }
    public byte[] ReadCluster(uint absClus) {
        var b = new byte[ClusterSize];
        for (int p = 0; p < Ppc; p++) Array.Copy(D, (absClus * Ppc + p) * RawPage, b, p * PageLen, PageLen);
        return b;
    }
    public uint Fat(uint c) {
        int per = ClusterSize / 4;
        uint fatIdx = (uint)(c / per), off = (uint)(c % per);
        uint ifcIdx = (uint)(fatIdx / per), ifcOff = (uint)(fatIdx % per);
        var ifc = ReadCluster(Ifc[ifcIdx]);
        uint fatClus = BitConverter.ToUInt32(ifc, (int)ifcOff * 4);
        var fat = ReadCluster(fatClus);
        return BitConverter.ToUInt32(fat, (int)off * 4);
    }
    public List<uint> Chain(uint c) {
        var l = new List<uint>();
        while (true) { l.Add(c); uint n = Fat(c); if (n == 0xFFFFFFFF || (n & 0x80000000) == 0) break; c = n & 0x7FFFFFFF; if (l.Count > 20000) break; }
        return l;
    }
    public byte[] ReadChain(uint c, long len) {
        var ms = new MemoryStream();
        foreach (var k in Chain(c)) ms.Write(ReadCluster(k + AllocOff), 0, ClusterSize);
        var all = ms.ToArray(); var r = new byte[Math.Min(len, all.Length)]; Array.Copy(all, r, r.Length); return r;
    }
    public class Ent { public ushort Mode; public uint Len, Clus; public string Name; }
    public List<Ent> Dir(uint clus, uint count) {
        var raw = ReadChain(clus, count * 512L); var l = new List<Ent>();
        for (int i = 0; i < count && (i + 1) * 512 <= raw.Length; i++) {
            int o = i * 512; var e = new Ent();
            e.Mode = BitConverter.ToUInt16(raw, o); e.Len = BitConverter.ToUInt32(raw, o + 4); e.Clus = BitConverter.ToUInt32(raw, o + 0x10);
            int n = 0; while (n < 32 && raw[o + 0x40 + n] != 0) n++; e.Name = Encoding.ASCII.GetString(raw, o + 0x40, n);
            l.Add(e);
        }
        return l;
    }
    public static void Extract(string card, string outDir, string prefix) {
        var m = new Mc(card);
        Console.WriteLine("pagelen=" + m.PageLen + " ppc=" + m.Ppc + " raw=" + m.RawPage + " alloc=" + m.AllocOff + " root=" + m.RootClus);
        var root0 = m.Dir(m.RootClus, 1)[0];
        foreach (var e in m.Dir(m.RootClus, root0.Len)) {
            bool exists = (e.Mode & 0x8000) != 0, isDir = (e.Mode & 0x20) != 0;
            if (!exists || !isDir || e.Name == "." || e.Name == "..") continue;
            Console.WriteLine("DIR " + e.Name + " entries=" + e.Len);
            if (!e.Name.StartsWith(prefix)) continue;
            var d = Path.Combine(outDir, e.Name); Directory.CreateDirectory(d);
            foreach (var f in m.Dir(e.Clus, e.Len)) {
                if ((f.Mode & 0x8000) == 0 || (f.Mode & 0x10) == 0) continue;
                var data = m.ReadChain(f.Clus, f.Len);
                File.WriteAllBytes(Path.Combine(d, f.Name), data);
                Console.WriteLine("  " + f.Name + " " + f.Len);
            }
        }
    }
}
