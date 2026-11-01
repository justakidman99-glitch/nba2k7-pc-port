using System;
using System.IO;
using System.Collections.Generic;

public static class Ptr {
    // Report positions i whose u32 value v satisfies i + v + k == target (self-relative) or v + k == target (absolute), |k| <= 8.
    public static void Find(string path, int target) {
        var b = File.ReadAllBytes(path);
        for (int i = 0; i + 4 <= b.Length; i++) {
            long v = BitConverter.ToUInt32(b, i);
            for (int k = -8; k <= 8; k++) {
                if (i + v + k == target) Console.WriteLine(string.Format("self  @0x{0:X} v=0x{1:X} k={2}", i, v, k));
                if (v + k == target && v > 0x1000) Console.WriteLine(string.Format("abs   @0x{0:X} v=0x{1:X} k={2}", i, v, k));
            }
        }
    }
}
