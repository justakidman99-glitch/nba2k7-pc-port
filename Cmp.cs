using System;
using System.IO;
using System.Text;

public static class Cmp {
    public static void Table(string path, int[] idx, int from, int to) {
        var b = File.ReadAllBytes(path);
        var h = new StringBuilder("name                    ");
        for (int k = from; k < to; k++) h.AppendFormat("{0:X2} ", k);
        Console.WriteLine(h);
        foreach (int i in idx) {
            int r = Ros.PlayerBase + i * Ros.PlayerSize;
            var sb = new StringBuilder(); string n = Ros.Name(b, i); if (n.Length > 23) n = n.Substring(0, 23);
            sb.Append(n.PadRight(24));
            for (int k = from; k < to; k++) sb.AppendFormat("{0,2} ", b[r + k].ToString("X2"));
            Console.WriteLine(sb);
        }
    }
    public static void TableDec(string path, int[] idx, int from, int to) {
        var b = File.ReadAllBytes(path);
        var h = new StringBuilder("name                    ");
        for (int k = from; k < to; k++) h.AppendFormat("{0,3}", (k - from));
        Console.WriteLine(h);
        foreach (int i in idx) {
            int r = Ros.PlayerBase + i * Ros.PlayerSize;
            var sb = new StringBuilder(); string n = Ros.Name(b, i); if (n.Length > 23) n = n.Substring(0, 23);
            sb.Append(n.PadRight(24));
            for (int k = from; k < to; k++) sb.AppendFormat("{0,3}", b[r + k]);
            Console.WriteLine(sb);
        }
    }
}
