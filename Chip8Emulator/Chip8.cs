using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Chip8Emulator
{
    internal class Chip8
    {
        private byte delayTimer = 0;

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

        private Random rnd = new Random();
        private FIXED_BYTE_ARRAY registers = new FIXED_BYTE_ARRAY { b = new byte[16] };
        private FIXED_BYTE_ARRAY memory = new FIXED_BYTE_ARRAY { b = new byte[4095] };
        private uint index;
        private uint pc;
        private uint[] stack = new uint[16];
        private byte sp;
        private byte soundTimer = 0;
        private bool playingSound = false;
        private uint counter = 0;
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

        private void OP_00E0() // Clears the screen
        {
            for (uint i = 0; i < video.b.Length; i++)
                video.b[i] = 0;
        }

        private void OP_00EE() // Returns from a subroutine
        {
            sp--;
            pc = stack[(uint)sp];
            DisplayAvailable = true;
        }

        private void OP_1nnn() // Jumps to address NNN
        {
            uint address = (uint)(opcode & (uint)0x0FFF);
            pc = address;
        }

        private void OP_2nnn() // Calls subroutine at NNN
        {
            uint address = (uint)(opcode & (uint)0x0FFF);
            stack[sp] = pc;
            sp++;
            pc = address;
        }

        private void OP_3xnn() // Skips the next instruction if VX equals NN
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = opcode & (uint)0x00FF;
            if (registers.b[Vx] == b)
                pc += 2;
        }

        private void OP_4xnn() // Skips the next instruction if VX does not equal NN
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = opcode & (uint)0x00FF;
            if (registers.b[Vx] != b)
                pc += 2;
        }

        private void OP_5xy0() // Skips the next instruction if VX equals VY
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            if (registers.b[Vx] == registers.b[Vy])
                pc += 2;
        }

        private void OP_6xnn() // Sets VX to NN
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = opcode & (uint)0x00FF;
            registers.b[Vx] = (byte)b;
        }

        private void OP_7xnn() // Adds NN to VX (carry flag is not changed)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = opcode & (uint)0x00FF;
            registers.b[Vx] += (byte)b;
        }

        private void OP_8xy0() // Sets VX to the value of VY
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.b[Vx] = registers.b[Vy];
        }

        private void OP_8xy1() // Sets VX to VX or VY (bitwise OR operation)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.b[Vx] |= registers.b[Vy];
        }

        private void OP_8xy2() // Sets VX to VX and VY. (bitwise AND operation)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.b[Vx] &= registers.b[Vy];
        }

        private void OP_8xy3() // Sets VX to VX xor VY
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.b[Vx] ^= registers.b[Vy];
        }

        private void OP_8xy4() // Adds VY to VX. VF is set to 1 when there's an overflow, and to 0 when there is not
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

        private void OP_8xy5() // VY is subtracted from VX. VF is set to 0 when there's an underflow, and 1 when there is not. (i.e. VF set to 1 if VX >= VY and 0 if not).
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            if (registers.b[Vx] > registers.b[Vy])
                registers.b[0xF] = 1;
            else
                registers.b[0xF] = 0;
            registers.b[Vx] -= registers.b[Vy];
        }

        private void OP_8xy6() // Stores the least significant bit of VX in VF and then shifts VX to the right by 1
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            registers.b[0xF] = (byte)(registers.b[Vx] & 0x1);
            registers.b[Vx] = (byte)(registers.b[Vx] >> 1);
            //NOP(sw, 200);
        }

        private void OP_8xy7() // Sets VX to VY minus VX. VF is set to 0 when there's an underflow, and 1 when there is not. (i.e. VF set to 1 if VY >= VX)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.b[Vx] = (byte)(registers.b[Vy] - registers.b[Vx]);
            if (registers.b[Vy] > registers.b[Vx])
                registers.b[0xF] = 1;
            else
                registers.b[0xF] = 0;
        }

        private void OP_8xyE() // Stores the most significant bit of VX in VF and then shifts VX to the left by 1
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            registers.b[0xF] = (byte)((uint)(registers.b[Vx] & (uint)0x80) >> 7);
            registers.b[Vx] = (byte)(registers.b[Vx] << 1);
        }

        private void OP_9xy0() // Skips the next instruction if VX does not equal VY. (Usually the next instruction is a jump to skip a code block)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            if (registers.b[Vx] != registers.b[Vy])
                pc += 2;
        }

        private void OP_Annn() // Sets I to the address NNN
        {
            uint address = (opcode & (uint)0x0FFF);
            index = (ushort)address;
        }

        private void OP_Bnnn() // Jumps to the address NNN plus V0
        {
            uint address = (uint)(opcode & (uint)0x0FFF);
            pc = (ushort)(registers.b[0] + address);
        }

        private void OP_Cxnn() // Sets VX to the result of a bitwise and operation on a random number (Typically: 0 to 255) and NN
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = (uint)(opcode & (uint)0x00FF);
            registers.b[Vx] = (byte)(rnd.Next(0, 255) & b);
        }

        private void OP_Dxyn() // Draws a sprite at coordinate (VX, VY) that has a width of 8 pixels and a height of N pixels. Each row of 8 pixels is read as bit-coded starting from memory location I; I value does not change after the execution of this instruction. As described above, VF is set to 1 if any screen pixels are flipped from set to unset when the sprite is drawn, and to 0 if that does not happen
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

        private void OP_Ex9E() // Skips the next instruction if the key stored in VX is pressed (usually the next instruction is a jump to skip a code block)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint key = registers.b[Vx];
            if (keypad.b[key] != 0)
                pc += 2;
        }

        private void OP_ExA1() // Skips the next instruction if the key stored in VX is not pressed (usually the next instruction is a jump to skip a code block)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint key = registers.b[Vx];
            if (keypad.b[key] == 0)
                pc += 2;
        }

        private void OP_Fx07() // Sets VX to the value of the delay timer
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            registers.b[Vx] = delayTimer;
        }

        private void OP_Fx0A() // A key press is awaited, and then stored in VX (blocking operation, all instruction halted until next key event)
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

        private void OP_Fx15() // Sets the delay timer to VX
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            delayTimer = registers.b[Vx];
        }

        private void OP_Fx18() // Sets the sound timer to VX
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            soundTimer = registers.b[Vx];
        }

        private void OP_Fx1E() // Adds VX to I. VF is not affected
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            index += registers.b[Vx];
        }

        private void OP_Fx29() // Sets I to the location of the sprite for the character in VX. Characters 0-F (in hexadecimal) are represented by a 4x5 font
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint digit = registers.b[Vx];
            index = (ushort)(FONTSET_START_ADDRESS + (5 * digit));
        }

        private void OP_Fx33() // Stores the binary-coded decimal representation of VX, with the hundreds digit in memory at location in I, the tens digit at location I+1, and the ones digit at location I+2
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint value = registers.b[Vx];
            memory.b[index + 2] = (byte)(value % 10);
            value /= 10;
            memory.b[index + 1] = (byte)(value % 10);
            value /= 10;
            memory.b[index] = (byte)(value % 10);
        }

        private void OP_Fx55() // Stores from V0 to VX (including VX) in memory, starting at address I. The offset from I is increased by 1 for each value written, but I itself is left unmodified.
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            for (uint i = 0; i <= Vx; i++)
                memory.b[index + i] = registers.b[i];
        }

        private void OP_Fx65() // Fills from V0 to VX (including VX) with values from memory, starting at address I. The offset from I is increased by 1 for each value read, but I itself is left unmodified
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            for (uint i = 0; i <= Vx; i++)
                registers.b[i] = memory.b[index + i];
        }

        public void Cycle()
        {
            var watch = Stopwatch.StartNew();

            opcode = ((uint)memory.b[pc] << 8) | memory.b[pc + 1];
            DisplayAvailable = false;
            pc += 2;
            CallOpcode(opcode);

            if (delayTimer > 0)
                delayTimer--;

            if (soundTimer > 0)
            {
                Beep(1000, soundTimer * 16);
                soundTimer = 0;
            }

            while (watch.ElapsedMilliseconds < 16) { } // throttle cycle loop to 60Hz
            watch.Stop();
        }

        private void Beep(ushort a, int b)
        {
            Task.Run(() =>
            {
                if (!playingSound)
                {
                    playingSound = true;
                    Sound.PlaySound(a, b);
                    playingSound = false;
                }
            });
        }

        private static void NOP(Stopwatch sw, int ticks = 1852)
        {
            while (sw.ElapsedTicks < ticks * 10) { }
        }

        private void CallOpcode(uint opcode)
        {
            string opcodeHex = opcode.ToString("X4");
            if (opcodeHex == "00E0") { OP_00E0(); return; }
            if (opcodeHex == "00EE") { OP_00EE(); return; }
            if (opcodeHex[0] == '1') { OP_1nnn(); return; }
            if (opcodeHex[0] == '2') { OP_2nnn(); return; }
            if (opcodeHex[0] == '3') { OP_3xnn(); return; }
            if (opcodeHex[0] == '4') { OP_4xnn(); return; }
            if (opcodeHex[0] == '5' && opcodeHex[3] == '0') { OP_5xy0(); return; }
            if (opcodeHex[0] == '6') { OP_6xnn(); return; }
            if (opcodeHex[0] == '7') { OP_7xnn(); return; }
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
            if (opcodeHex[0] == 'C') { OP_Cxnn(); return; }
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