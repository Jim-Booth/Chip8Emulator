using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Chip8Emulator
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class FIXED_BYTE_ARRAY
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 320)]
        public byte[] b;
    }
}
