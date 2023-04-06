using System;
using System.IO;

namespace Chip8Emulator
{
    internal class Chip8
    {
        private byte delayTimer;
        public byte DelayTimer
        {
            get { return delayTimer; }
            set { delayTimer = value; }
        }

        private FIXED_BYTE_ARRAY video;
        public FIXED_BYTE_ARRAY Video
        {
            get { return video; }
            set { video = value; }
        }

        private bool displayAvailable = false;
        public bool DisplayAvailable
        {
            get { return displayAvailable; }
            set { displayAvailable = value; }
        }

        private FIXED_BYTE_ARRAY registers = new FIXED_BYTE_ARRAY { b = new byte[16] };
        private FIXED_BYTE_ARRAY memory = new FIXED_BYTE_ARRAY { b = new byte[4095] };
        private uint index;
        private uint pc;
        private uint[] stack = new uint[16];
        private byte sp;
        private byte soundTimer;
        private FIXED_BYTE_ARRAY keypad = new FIXED_BYTE_ARRAY { b = new byte[16] };
        private uint opcode;
        private long progSize;
        private const uint START_ADDRESS = 0x200;
        private const int FONTSET_SIZE = 80;
        private const uint FONTSET_START_ADDRESS = 0x50;
        private const uint VIDEO_WIDTH = 64;
        private const uint VIDEO_HEIGHT = 32;
        private byte[] fontset = new byte[FONTSET_SIZE]
        {
            0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
	        0x20, 0x60, 0x20, 0x20, 0x70, // 1
	        0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
	        0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
	        0x90, 0x90, 0xF0, 0x10, 0x10, // 4
	        0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
	        0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
	        0xF0, 0x10, 0x20, 0x40, 0x40, // 7
	        0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
	        0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
	        0xF0, 0x90, 0xF0, 0x90, 0x90, // A
	        0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
	        0xF0, 0x80, 0x80, 0x80, 0xF0, // C
	        0xE0, 0x90, 0x90, 0x90, 0xE0, // D
	        0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
	        0xF0, 0x80, 0xF0, 0x80, 0x80  // F
        };

        public Chip8()
        {
            pc = START_ADDRESS;
            video = new FIXED_BYTE_ARRAY { b = new byte[VIDEO_WIDTH * VIDEO_HEIGHT] };
            for (uint i = 0; i < FONTSET_SIZE; i++)
                memory.b[FONTSET_START_ADDRESS + i] = fontset[i];
        }

        public void LoadROM(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                BinaryReader br = new BinaryReader(fs);
                progSize = new FileInfo(filePath).Length;
                byte[] rom = br.ReadBytes((int)progSize);
                for (long i = 0; i < rom.Length; i++)
                    memory.b[START_ADDRESS + i] = rom[i];
            }
        }

        public byte RandByte()
        {
            Random rnd = new Random();
            return (byte)rnd.Next(0, 255);
        }

        private void OP_00E0()
        {
            for (uint i = 0; i < video.b.Length; i++)
                video.b[i] = 0;
        }

        private void OP_00EE()
        {
            sp--;
            pc = stack[(uint)sp];
            DisplayAvailable = true;
        }

        private void OP_1nnn()
        {
            uint address = (uint)(opcode & (uint)0x0FFF);
            pc = address;
        }

        private void OP_2nnn()
        {
            uint address = (uint)(opcode & (uint)0x0FFF);
            stack[sp] = pc;
            sp++;
            pc = address;
        }

        private void OP_3xkk()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = opcode & (uint)0x00FF;
            if (registers.b[Vx] == b)
                pc += 2;
        }

        private void OP_4xkk()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = opcode & (uint)0x00FF;
            if (registers.b[Vx] != b)
                pc += 2;
        }

        private void OP_5xy0()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            if (registers.b[Vx] == registers.b[Vy])
                pc += 2;
        }

        private void OP_6xkk()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = opcode & (uint)0x00FF;
            registers.b[Vx] = (byte)b;
        }

        private void OP_7xkk()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = opcode & (uint)0x00FF;
            registers.b[Vx] += (byte)b;
        }

        private void OP_8xy0()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.b[Vx] = registers.b[Vy];
        }

        private void OP_8xy1()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.b[Vx] |= registers.b[Vy];
        }

        private void OP_8xy2()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.b[Vx] &= registers.b[Vy];
        }

        private void OP_8xy3()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.b[Vx] ^= registers.b[Vy];
        }

        private void OP_8xy4()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            uint sum = (uint)(registers.b[Vx] + registers.b[Vy]);
            if (sum > 255)
                registers.b[0xF] = 1;
            else
                registers.b[0xF] = 0;
            registers.b[Vx] = (byte)(sum & (uint)0xFF);
        }

        private void OP_8xy5()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            if (registers.b[Vx] > registers.b[Vy])
                registers.b[0xF] = 1;
            else
                registers.b[0xF] = 0;
            registers.b[Vx] -= registers.b[Vy];
        }

        private void OP_8xy6()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            registers.b[0xF] = (byte)(registers.b[Vx] & 0x1);
            registers.b[Vx] = (byte)(registers.b[Vx] >> 1);
        }

        private void OP_8xy7()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.b[Vx] = (byte)(registers.b[Vy] - registers.b[Vx]);
            if (registers.b[Vy] > registers.b[Vx])
                registers.b[0xF] = 1;
            else
                registers.b[0xF] = 0;
        }

        private void OP_8xyE()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            registers.b[0xF] = (byte)((uint)(registers.b[Vx] & (uint)0x80) >> 7);
            registers.b[Vx] = (byte)(registers.b[Vx] << 1);
        }

        private void OP_9xy0()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            if (registers.b[Vx] != registers.b[Vy])
                pc += 2;
        }

        private void OP_Annn()
        {
            uint address = (opcode & (uint)0x0FFF);
            index = (ushort)address;
        }

        private void OP_Bnnn()
        {
            uint address = (uint)(opcode & (uint)0x0FFF);
            pc = (ushort)(registers.b[0] + address);
        }

        private void OP_Cxkk()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = (uint)(opcode & (uint)0x00FF);
            registers.b[Vx] = (byte)(RandByte() & b);
        }

        private void OP_Dxyn()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            uint height = (uint)(opcode & (uint)0x000F);
            uint xPos = registers.b[Vx] % VIDEO_WIDTH;
            uint yPos = registers.b[Vy] % VIDEO_HEIGHT;
            registers.b[0xF] = 0;
            for (uint row = 0; row < height; row++)
            {
                uint spriteByte = memory.b[index + row];
                for (uint col = 0; col < 8; col++)
                {
                    uint spritePixel = (uint)(spriteByte & ((int)0x80 >> (int)col));
                    uint vp = ((yPos + row) * VIDEO_WIDTH + (xPos + col)) % (VIDEO_WIDTH * VIDEO_HEIGHT);
                    uint screenPixel = (uint)video.b[vp];
                    if (spritePixel > 0)
                    {
                        if (screenPixel == 1)
                            registers.b[0xF] = 1;
                        video.b[vp] = (byte)(video.b[vp] ^ 0xFFFFFFFF);
                    }
                }
            }
            DisplayAvailable = true;
        }

        private void OP_Ex9E()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint key = registers.b[Vx];
            if (keypad.b[key] != 0)
                pc += 2;
        }

        private void OP_ExA1()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint key = registers.b[Vx];
            if (keypad.b[key] == 0)
                pc += 2;
        }

        private void OP_Fx07()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            registers.b[Vx] = delayTimer;
        }

        private void OP_Fx0A()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            bool hit = false;
            for (uint i = 0; i < 15; i++)
                if (keypad.b[i] != 0)
                {
                    registers.b[Vx] = (byte)i;
                    hit = true;
                    break;
                }
            if (!hit)
                pc -= 2;
        }

        public uint KeyDown
        {
            set
            {
                keypad.b[value] = 1;
            }
        }

        public uint KeyUp
        {
            set
            {
                keypad.b[value] = 0;
            }
        }

        private void OP_Fx15()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            delayTimer = registers.b[Vx];
        }

        private void OP_Fx18()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            soundTimer = registers.b[Vx];
        }

        private void OP_Fx1E()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            index += registers.b[Vx];
        }

        private void OP_Fx29()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint digit = registers.b[Vx];
            index = (ushort)(FONTSET_START_ADDRESS + (5 * digit));
        }

        private void OP_Fx33()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint value = registers.b[Vx];
            memory.b[index + 2] = (byte)(value % 10);
            value /= 10;
            memory.b[index + 1] = (byte)(value % 10);
            value /= 10;
            memory.b[index] = (byte)(value % 10);
        }

        private void OP_Fx55()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            for (uint i = 0; i <= Vx; i++)
                memory.b[index + i] = registers.b[i];
        }

        private void OP_Fx65()
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            for (uint i = 0; i <= Vx; i++)
                registers.b[i] = memory.b[index + i];
        }

        public void Cycle()
        {
            opcode = ((uint)memory.b[pc] << 8) | memory.b[pc + 1];
            DisplayAvailable = false;
            pc += 2;
            CallOpcode(opcode);
            if (delayTimer > 0)
                --delayTimer;
            if (soundTimer > 0)
                --soundTimer;
        }

        private void CallOpcode(uint opcode)
        {
            string opcodeHex = opcode.ToString("X4");
            if (opcodeHex == "00E0") { OP_00E0(); return; }
            if (opcodeHex == "00EE") { OP_00EE(); return; }
            if (opcodeHex[0] == '1') { OP_1nnn(); return; }
            if (opcodeHex[0] == '2') { OP_2nnn(); return; }
            if (opcodeHex[0] == '3') { OP_3xkk(); return; }
            if (opcodeHex[0] == '4') { OP_4xkk(); return; }
            if (opcodeHex[0] == '5' && opcodeHex[3] == '0') { OP_5xy0(); return; }
            if (opcodeHex[0] == '6') { OP_6xkk(); return; }
            if (opcodeHex[0] == '7') { OP_7xkk(); return; }
            if (opcodeHex.Length == 4)
            {
                if (opcodeHex[0] == '8' && opcodeHex[3] == '0') { OP_8xy0(); return; }
                if (opcodeHex[0] == '8' && opcodeHex[3] == '1') { OP_8xy1(); return; }
                if (opcodeHex[0] == '8' && opcodeHex[3] == '2') { OP_8xy2(); return; }
                if (opcodeHex[0] == '8' && opcodeHex[3] == '3') { OP_8xy3(); return; }
                if (opcodeHex[0] == '8' && opcodeHex[3] == '4') { OP_8xy4(); return; }
                if (opcodeHex[0] == '8' && opcodeHex[3] == '5') { OP_8xy5(); return; }
                if (opcodeHex[0] == '8' && opcodeHex[3] == '6') { OP_8xy6(); return; }
                if (opcodeHex[0] == '8' && opcodeHex[3] == '7') { OP_8xy7(); return; }
                if (opcodeHex[0] == '8' && opcodeHex[3] == 'E') { OP_8xyE(); return; }
            }
            if (opcodeHex[0] == '9' && opcodeHex[3] == '0') { OP_9xy0(); return; }
            if (opcodeHex[0] == 'A') { OP_Annn(); return; }
            if (opcodeHex[0] == 'B') { OP_Bnnn(); return; }
            if (opcodeHex[0] == 'C') { OP_Cxkk(); return; }
            if (opcodeHex[0] == 'D') { OP_Dxyn(); return; }
            if (opcodeHex.Length > 2)
            {
                if (opcodeHex[0] == 'E' && opcodeHex[2] == '9' && opcodeHex[3] == 'E') { OP_Ex9E(); return; }
                if (opcodeHex[0] == 'E' && opcodeHex[2] == 'A' && opcodeHex[3] == '1') { OP_ExA1(); return; }
                if (opcodeHex[0] == 'F' && opcodeHex[2] == '0' && opcodeHex[3] == '7') { OP_Fx07(); return; }
                if (opcodeHex[0] == 'F' && opcodeHex[2] == '0' && opcodeHex[3] == 'A') { OP_Fx0A(); return; }
                if (opcodeHex[0] == 'F' && opcodeHex[2] == '1' && opcodeHex[3] == '5') { OP_Fx15(); return; }
                if (opcodeHex[0] == 'F' && opcodeHex[2] == '1' && opcodeHex[3] == '8') { OP_Fx18(); return; }
                if (opcodeHex[0] == 'F' && opcodeHex[2] == '1' && opcodeHex[3] == 'E') { OP_Fx1E(); return; }
                if (opcodeHex[0] == 'F' && opcodeHex[2] == '2' && opcodeHex[3] == '9') { OP_Fx29(); return; }
                if (opcodeHex[0] == 'F' && opcodeHex[2] == '3' && opcodeHex[3] == '3') { OP_Fx33(); return; }
                if (opcodeHex[0] == 'F' && opcodeHex[2] == '5' && opcodeHex[3] == '5') { OP_Fx55(); return; }
                if (opcodeHex[0] == 'F' && opcodeHex[2] == '6' && opcodeHex[3] == '5') { OP_Fx65(); return; }
            }
            throw new Exception("Invalid Opcode");
        }
    }
}