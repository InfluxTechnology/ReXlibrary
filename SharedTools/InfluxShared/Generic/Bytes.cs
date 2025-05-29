using System;
using System.Runtime.InteropServices;

namespace InfluxShared.Generic
{
    public static class Bytes
    {
        static Bytes()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                CopyMemory = WinCopyMemory;
            else
                CopyMemory = DotNetCopyMemory;
        }

        public static byte[] ObjectToBytes(object obj)
        {
            if (obj.GetType() == typeof(byte[]))
                return (byte[])obj;

            byte[] buffer = new byte[Marshal.SizeOf(obj)];
            GCHandle h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            IntPtr p = h.AddrOfPinnedObject();
            Marshal.StructureToPtr(obj, p, false);
            h.Free();
            return buffer;
        }

        public static byte[] ArrayToBytes(object obj, int length)
        {
            if (length == 0)
            {
                return null;
            }

            byte[] buffer = new byte[length];
            GCHandle h = GCHandle.Alloc(obj, GCHandleType.Pinned);
            IntPtr p = h.AddrOfPinnedObject();
            Marshal.Copy(p, buffer, 0, length);
            h.Free();
            return buffer;
        }

        public static byte[] EnumArrayToBytes(object obj)
        {
            if ((obj as Array).Length == 0)
                return null;

            Type elType = Enum.GetUnderlyingType(obj.GetType().GetElementType());
            int elSize = Marshal.SizeOf(elType);
            byte[] buffer = new byte[(obj as Array).Length * elSize];
            int idx = -1;
            foreach (var v in obj as Array)
                Buffer.BlockCopy(ObjectToBytes(Convert.ChangeType(v, elType)), 0, buffer, ++idx * elSize, elSize);
            return buffer;
        }

        public static byte[] BoolArrayToBytes(object obj) => Array.ConvertAll((bool[])obj, b => b ? (byte)1 : (byte)0);

        public static byte[] ArrayToBytes(object obj)
        {
            if (!obj.GetType().IsArray)
            {
                return null;
            }

            return ArrayToBytes(obj, (obj as Array).Length * Marshal.SizeOf(obj.GetType().GetElementType()));
        }

        internal delegate void OSCopyMemory(IntPtr dest, IntPtr src, uint count);
        internal static OSCopyMemory CopyMemory;

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void WinCopyMemory(IntPtr dest, IntPtr src, uint count);
        private static void DotNetCopyMemory(IntPtr dest, IntPtr src, uint count)
        {
            byte[] data = new byte[count];
            Marshal.Copy(src, data, 0, (int)count);
            Marshal.Copy(data, 0, dest, (int)count);
        }

        public static void BytesToArray(object src, object dst, uint length)
        {
            if (length == 0)
            {
                return;
            }

            GCHandle hsrc = GCHandle.Alloc(src, GCHandleType.Pinned);
            IntPtr psrc = hsrc.AddrOfPinnedObject();
            GCHandle hdst = GCHandle.Alloc(dst, GCHandleType.Pinned);
            IntPtr pdst = hdst.AddrOfPinnedObject();

            CopyMemory(pdst, psrc, length);
            hsrc.Free();
            hdst.Free();
        }

        static int GetHexVal(char hex)
        {
            int val = hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

        public static byte[] FromHexBinary(string hexBinary)
        {
            if (hexBinary.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hexBinary.Length >> 1];

            for (int i = 0; i < hexBinary.Length >> 1; ++i)
                arr[i] = (byte)((GetHexVal(hexBinary[i << 1]) << 4) + (GetHexVal(hexBinary[(i << 1) + 1])));

            return arr;
        }

        public static string ToHexBinary(object obj) => BitConverter.ToString(ArrayToBytes(obj, (obj as Array).Length)).Replace("-", string.Empty);
    }
}
