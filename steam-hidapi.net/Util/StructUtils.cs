using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace steam_hidapi.net.Util
{
    internal static class StructUtils
    {
        public static T ToStructure<T>(this byte[] bytes) where T : struct
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        public static byte[] ToBytes<T>(this T structure) where T : struct
        {
            int size = Marshal.SizeOf(structure);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(structure, ptr, false);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        public static bool EqualsWithValues<TKey, TValue>(this Dictionary<TKey, TValue> obj1, Dictionary<TKey, TValue> obj2)
        {
            bool equal = false;
            if (obj1.Count == obj2.Count) // Require equal count.
            {
                equal = true;
                foreach (var pair in obj1)
                {
                    TValue value;
                    if (obj2.TryGetValue(pair.Key, out value))
                    {
                        // Require value be equal.
                        if (!value.Equals(pair.Value))
                        {
                            equal = false;
                            break;
                        }
                    }
                    else
                    {
                        // Require key be present.
                        equal = false;
                        break;
                    }
                }
            }

            return equal;
        }
    }
}