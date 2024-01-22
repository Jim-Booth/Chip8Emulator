using System.Runtime.InteropServices;

namespace Chip8Emulator
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class FIXED_BYTE_ARRAY
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 320)]
        public byte[] b;
    }
}