using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static System.Windows.Forms.AxHost;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace Chip8Emulator
{
    internal class Chip8
    {
        private bool running = false;
        public bool Running
        {
            get { return running; }
        }

        private bool debugMode = false;
        public bool DebugMode
        {
            get { return debugMode; }
            set { debugMode = value; }
        }

        bool step = true;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class FIXED_BYTE_ARRAY
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 320)]
            public byte[] @byte;
        }
        private FIXED_BYTE_ARRAY video;
        public FIXED_BYTE_ARRAY Video
        {
            get { return video; }
            set { video = value; }
        }

        private Random RND;
        private FIXED_BYTE_ARRAY registers = new FIXED_BYTE_ARRAY { @byte = new byte[16] };
        private FIXED_BYTE_ARRAY memory = new FIXED_BYTE_ARRAY { @byte = new byte[4095] };
        private uint I;
        private uint PC;
        private uint[] STACK = new uint[16];
        private byte SP;
        private byte ST = 0;// sound timer
        private byte DT = 0;// delay timer
        private FIXED_BYTE_ARRAY keypad = new FIXED_BYTE_ARRAY { @byte = new byte[16] };
        private const uint START_ADDRESS = 0x200;
        private const int FONTSET_SIZE = 80;
        private const uint FONTSET_START_ADDRESS = 0x50;
        private const uint VIDEO_WIDTH = 64;
        private const uint VIDEO_HEIGHT = 32;
        private bool playingSound;

        private byte[] FONTS = new byte[FONTSET_SIZE]
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

        public List<string> DebugStackInfo()
        {
            List<string> stak = new List<string>();
            stak.Add("STACK");
            for (uint i = 0; i < 15; i++)
                stak.Add(STACK[i].ToString("X"));
            return stak;
        }

        public List<string> DebugMainInfo()
        {
            List<string> info = new List<string>();

            string reg = "REGISTERS\r\n01 02 02 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F\r\n";
            for (uint i = 0; i < 15; i++)
            {
                if (registers.@byte[i] < 16) reg += "0";
                reg += registers.@byte[i].ToString("X");
                if (i < 14) reg += " ";
            }
            info.Add(reg);
            info.Add("PC = " + PC);
            info.Add("OPCODE = " + ((uint)memory.@byte[PC] << 8).ToString("X4"));
            info.Add("DELAY TIMER = " + DT);
            info.Add("SOUND TIMER = " + ST);
            info.Add("INDEX = " + I);
            info.Add("RUNNING = " + running);
            return info;
        }

        public Chip8()
        {
            PC = START_ADDRESS;
            video = new FIXED_BYTE_ARRAY { @byte = new byte[VIDEO_WIDTH * VIDEO_HEIGHT] };
            for (uint i = 0; i < FONTSET_SIZE; i++)
                memory.@byte[FONTSET_START_ADDRESS + i] = FONTS[i];
        }

        public void LoadROM(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                BinaryReader br = new BinaryReader(fs);
                long progSize = new FileInfo(filePath).Length;
                byte[] rom = br.ReadBytes((int)progSize);
                for (long i = 0; i < rom.Length; i++)
                    memory.@byte[START_ADDRESS + i] = rom[i];
            }
        }

        public void Start()
        {
            running = true;
            int beat = 0;
            double timerCounter = (DateTime.Now - DateTime.MinValue).TotalMilliseconds;
            while (running)
            {
                uint opcode = ((uint)memory.@byte[PC] << 8) | memory.@byte[PC + 1];
                uint p = PC;

                // Cycle the CPU
                if (!debugMode)
                {
                    CPUCycle();
                }
                else
                {
                    while (!step) { }
                    while (p == PC)
                    {
                        CPUCycle();
                    }
                    step = false;
                }

                // update timers at 60hz
                var currentTime = (DateTime.Now - DateTime.MinValue).TotalMilliseconds;
                var milisecondsSinceLastUpdate = currentTime - timerCounter;
                if (milisecondsSinceLastUpdate > 16)
                {
                    UpdateTimers();
                    timerCounter = currentTime;
                }

                // Determine if the program has ended and set running flag or just awaiting a keypress
                beat++;
                string op = opcode.ToString("X4");
                bool awaitKey = (op[0] == 'F' && op[2] == '0' && op[3] == 'A');
                if (p != PC || awaitKey)
                    beat = 0;
                if (beat == 100)
                    running = false;
            }
            running = false;
        }

        public void Pause()
        {
            debugMode = !debugMode;
            if (!debugMode) Step();
        }

        public void Step()
        {
            step = true;
        }

        public void Stop()
        {
            running = false;
        }

        public void CPUCycle()
        {
            uint opcode = ((uint)memory.@byte[PC] << 8) | memory.@byte[PC + 1];
            PC += 2;
            CallOpcode(opcode);

            if (debugMode)
            {
                step = false;
                while (!step) { }
            }

            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 2) { }
            watch.Stop();
        }

        private void UpdateTimers()
        {
            if (DT > 0)
                DT--;
            if (ST > 0)
            {
                if (!playingSound)
                    Beep(ST);
                ST--;
            }
        }

        public void Beep(uint st)
        {
            Task.Run(() =>
            {
                playingSound = true;
                Sound.PlaySound(500, (int)(st * (1000f / 60)));
                playingSound = false;
            });
        }

        private void OP_00E0(uint opcode) // Clears the screen
        {
            video = new FIXED_BYTE_ARRAY { @byte = new byte[VIDEO_WIDTH * VIDEO_HEIGHT] };
        }

        private void OP_00EE(uint opcode) // Returns from freq subroutine
        {
            SP--;
            PC = STACK[SP];
        }

        private void OP_1nnn(uint opcode) // Jumps to address NNN
        {
            uint address = (uint)(opcode & (uint)0x0FFF);
            PC = address;
        }

        private void OP_2nnn(uint opcode) // Calls subroutine at NNN
        {
            uint address = (uint)(opcode & (uint)0x0FFF);
            STACK[SP] = PC;
            SP++;
            PC = address;
        }

        private void OP_3xnn(uint opcode) // Skips the next instruction if VX equals NN
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = opcode & (uint)0x00FF;
            if ((uint)registers.@byte[Vx] == b)
                PC += 2;
        }

        private void OP_4xnn(uint opcode) // Skips the next instruction if VX does not equal NN
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = opcode & (uint)0x00FF;
            if (registers.@byte[Vx] != b)
                PC += 2;
        }

        private void OP_5xy0(uint opcode) // Skips the next instruction if VX equals VY
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            if (registers.@byte[Vx] == registers.@byte[Vy])
                PC += 2;
        }

        private void OP_6xnn(uint opcode) // Sets VX to NN
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = opcode & (uint)0x00FF;
            registers.@byte[Vx] = (byte)b;
        }

        private void OP_7xnn(uint opcode) // Adds NN to VX (carry flag is not changed)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = opcode & (uint)0x00FF;
            registers.@byte[Vx] += (byte)b;
        }

        private void OP_8xy0(uint opcode) // Sets VX to the value of VY
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.@byte[Vx] = registers.@byte[Vy];
        }

        private void OP_8xy1(uint opcode) // Sets VX to VX or VY (bitwise OR operation)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.@byte[Vx] |= registers.@byte[Vy];
            registers.@byte[15] = 0;
        }

        private void OP_8xy2(uint opcode) // Sets VX to VX and VY. (bitwise AND operation)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.@byte[Vx] &= registers.@byte[Vy];
            registers.@byte[15] = 0;
        }

        private void OP_8xy3(uint opcode) // Sets VX to VX xor VY
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            registers.@byte[Vx] ^= registers.@byte[Vy];
            registers.@byte[15] = 0;
        }

        private void OP_8xy4(uint opcode) // Adds VY to VX. VF is set to 1 when there's an overflow, and to 0 when there is not
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            int sum = (registers.@byte[Vx] + registers.@byte[Vy]);
            registers.@byte[Vx] = (byte)((byte)sum & 0xFF);
            if (sum > 255)
                registers.@byte[15] = 1;
            else
                registers.@byte[15] = 0;
        }

        private void OP_8xy5(uint opcode) // VY is subtracted from VX. VF is set to 0 when there's an underflow, and 1 when there is not. (i.e. VF set to 1 if VX >= VY and 0 if not).
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            int sum = (registers.@byte[Vx] - registers.@byte[Vy]);
            registers.@byte[Vx] = (byte)sum;
            if (sum <= 0)
                registers.@byte[15] = 0;
            else
                registers.@byte[15] = 1;

        }

        private void OP_8xy6(uint opcode) // Stores the least significant bit of VX in VF and then shifts VX to the right by 1
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint vx = (uint)registers.@byte[Vx];
            int sum = (vx & 0x1) != 0 ? 1 : 0;
            registers.@byte[Vx] >>= 0x1;
            registers.@byte[15] = (byte)sum;
        }

        private void OP_8xy7(uint opcode) // Sets VX to VY minus VX. VF is set to 0 when there's an underflow, and 1 when there is not. (i.e. VF set to 1 if VY >= VX)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            int sum = registers.@byte[Vy] - registers.@byte[Vx];
            registers.@byte[Vx] = (byte)sum;
            if (sum <= 0)
                registers.@byte[15] = 0;
            else
                registers.@byte[15] = 1;
        }

        private void OP_8xyE(uint opcode) // Stores the most significant bit of VX in VF and then shifts VX to the left by 1
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint vx = (uint)registers.@byte[Vx];
            int sum = (vx & 0x80) == 0x80 ? 1 : 0;
            registers.@byte[Vx] <<= 0x1;
            registers.@byte[15] = (byte)sum;
        }

        private void OP_9xy0(uint opcode) // Skips the next instruction if VX does not equal VY. (Usually the next instruction is freq jump to skip freq code block)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint Vy = (opcode & (uint)0x00F0) >> 4;
            if (registers.@byte[Vx] != registers.@byte[Vy])
                PC += 2;
        }

        private void OP_Annn(uint opcode) // Sets I to the address NNN
        {
            uint address = (opcode & (uint)0x0FFF);
            I = (ushort)address;
        }

        private void OP_Bnnn(uint opcode) // Jumps to the address NNN plus V0
        {
            uint address = (uint)(opcode & (uint)0x0FFF);
            PC = (ushort)(registers.@byte[0] + address);
        }

        private void OP_Cxnn(uint opcode) // Sets VX to the result of freq bitwise and operation on freq random number (Typically: 0 to 255) and NN
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint b = (opcode & (uint)0x00FF);
            RND = new Random();
            registers.@byte[Vx] = (byte)(RND.Next(0, 255) & b);
        }

        private void OP_Dxyn(uint opcode) // Draws freq sprite at coordinate (VX, VY) that has freq width of 8 pixels and freq height of N pixels. Each row of 8 pixels is read as bit-coded starting from memory location I; I value does not change after the execution of this instruction. As described above, VF is set to 1 if any screen pixels are flipped from set to unset when the sprite is drawn, and to 0 if that does not happen
        {
            int Vx = (int)((opcode & (uint)0x0F00) >> 8);
            int Vy = (int)((opcode & (uint)0x00F0) >> 4);
            uint height = (uint)(opcode & (uint)0x000F);
            uint xPos = registers.@byte[Vx] % VIDEO_WIDTH;
            uint yPos = registers.@byte[Vy] % VIDEO_HEIGHT;
            registers.@byte[0xF] = 0;
            for (uint row = 0; row < height; row++)
            {
                uint spriteByte = memory.@byte[I + row];
                for (uint col = 0; col < 8; col++)
                {
                    if ((spriteByte & ((int)0x80 >> (int)col)) != 0)
                    {
                        uint vp = ((yPos + row) * VIDEO_WIDTH + (xPos + col)) % (VIDEO_WIDTH * VIDEO_HEIGHT);
                        if (vp != 0 && video.@byte[vp] != 0)
                        {
                            registers.@byte[0xF] = 1;
                        }
                        video.@byte[vp] ^= 1;
                    }
                }
            }
        }

        private void OP_Ex9E(uint opcode) // Skips the next instruction if the key stored in VX is pressed (usually the next instruction is freq jump to skip freq code block)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint key = registers.@byte[Vx];
            if (keypad.@byte[key] != 0)
                PC += 2;
        }

        private void OP_ExA1(uint opcode) // Skips the next instruction if the key stored in VX is not pressed (usually the next instruction is freq jump to skip freq code block)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint key = registers.@byte[Vx];
            if (keypad.@byte[key] == 0)
                PC += 2;
        }

        private void OP_Fx07(uint opcode) // Sets VX to the value of the delay timer
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            registers.@byte[Vx] = DT;
        }

        private void OP_Fx0A(uint opcode) // A key press is awaited, and then stored in VX (blocking operation, all instruction halted until next key event)
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            bool hit = false;
            for (uint i = 0; i < 15; i++)
                if (keypad.@byte[i] != 0)
                {
                    registers.@byte[Vx] = (byte)i;
                    hit = true;
                    break;
                }
            if (!hit)
                PC -= 2;
        }

        public uint KeyDown
        {
            set
            {
                keypad.@byte[value] = 1;
            }
        }

        public uint KeyUp
        {
            set
            {
                keypad.@byte[value] = 0;
            }
        }

        private void OP_Fx15(uint opcode) // Sets the delay timer to VX
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            DT = registers.@byte[Vx];
        }

        private void OP_Fx18(uint opcode) // Sets the sound timer to VX
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            ST = registers.@byte[Vx];
        }

        private void OP_Fx1E(uint opcode) // Adds VX to I. VF is not affected
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            I += registers.@byte[Vx];
        }

        private void OP_Fx29(uint opcode) // Sets I to the location of the sprite for the character in VX. Characters 0-F (in hexadecimal) are represented by freq 4x5 font
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint digit = registers.@byte[Vx];
            I = digit * 5;
        }

        private void OP_Fx33(uint opcode) // Stores the binary-coded decimal representation of VX, with the hundreds digit in memory at location in I, the tens digit at location I+1, and the ones digit at location I+2
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            uint value = registers.@byte[Vx];
            memory.@byte[I + 2] = (byte)(value % 10);
            value /= 10;
            memory.@byte[I + 1] = (byte)(value % 10);
            value /= 10;
            memory.@byte[I] = (byte)(value % 10);
        }

        private void OP_Fx55(uint opcode) // Stores from V0 to VX (including VX) in memory, starting at address I. The offset from I is increased by 1 for each value written, but I itself is left unmodified.
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            for (uint i = 0; i <= Vx; i++)
                memory.@byte[I + i] = registers.@byte[i];
        }

        private void OP_Fx65(uint opcode) // Fills from V0 to VX (including VX) with values from memory, starting at address I. The offset from I is increased by 1 for each value read, but I itself is left unmodified
        {
            uint Vx = (opcode & (uint)0x0F00) >> 8;
            for (uint i = 0; i <= Vx; i++)
                registers.@byte[i] = memory.@byte[I + i];
        }


        private void CallOpcode(uint opcode)
        {
            string opHex = opcode.ToString("X4");
            if (opHex == "00E0") { OP_00E0(opcode); return; }
            if (opHex == "00EE") { OP_00EE(opcode); return; }
            if (opHex[0] == '1') { OP_1nnn(opcode); return; }
            if (opHex[0] == '2') { OP_2nnn(opcode); return; }
            if (opHex[0] == '3') { OP_3xnn(opcode); return; }
            if (opHex[0] == '4') { OP_4xnn(opcode); return; }
            if (opHex[0] == '6') { OP_6xnn(opcode); return; }
            if (opHex[0] == '7') { OP_7xnn(opcode); return; }
            if (opHex[0] == 'A') { OP_Annn(opcode); return; }
            if (opHex[0] == 'B') { OP_Bnnn(opcode); return; }
            if (opHex[0] == 'C') { OP_Cxnn(opcode); return; }
            if (opHex[0] == 'D') { OP_Dxyn(opcode); return; }
            if (opHex[0] == '5' && opHex[3] == '0') { OP_5xy0(opcode); return; }
            if (opHex[0] == '8' && opHex[3] == '0') { OP_8xy0(opcode); return; }
            if (opHex[0] == '8' && opHex[3] == '1') { OP_8xy1(opcode); return; }
            if (opHex[0] == '8' && opHex[3] == '2') { OP_8xy2(opcode); return; }
            if (opHex[0] == '8' && opHex[3] == '3') { OP_8xy3(opcode); return; }
            if (opHex[0] == '8' && opHex[3] == '4') { OP_8xy4(opcode); return; }
            if (opHex[0] == '8' && opHex[3] == '5') { OP_8xy5(opcode); return; }
            if (opHex[0] == '8' && opHex[3] == '6') { OP_8xy6(opcode); return; }
            if (opHex[0] == '8' && opHex[3] == '7') { OP_8xy7(opcode); return; }
            if (opHex[0] == '8' && opHex[3] == 'E') { OP_8xyE(opcode); return; }
            if (opHex[0] == '9' && opHex[3] == '0') { OP_9xy0(opcode); return; }
            if (opHex[0] == 'E' && opHex[2] == '9' && opHex[3] == 'E') { OP_Ex9E(opcode); return; }
            if (opHex[0] == 'E' && opHex[2] == 'A' && opHex[3] == '1') { OP_ExA1(opcode); return; }
            if (opHex[0] == 'F' && opHex[2] == '0' && opHex[3] == '7') { OP_Fx07(opcode); return; }
            if (opHex[0] == 'F' && opHex[2] == '0' && opHex[3] == 'A') { OP_Fx0A(opcode); return; }
            if (opHex[0] == 'F' && opHex[2] == '1' && opHex[3] == '5') { OP_Fx15(opcode); return; }
            if (opHex[0] == 'F' && opHex[2] == '1' && opHex[3] == '8') { OP_Fx18(opcode); return; }
            if (opHex[0] == 'F' && opHex[2] == '1' && opHex[3] == 'E') { OP_Fx1E(opcode); return; }
            if (opHex[0] == 'F' && opHex[2] == '2' && opHex[3] == '9') { OP_Fx29(opcode); return; }
            if (opHex[0] == 'F' && opHex[2] == '3' && opHex[3] == '3') { OP_Fx33(opcode); return; }
            if (opHex[0] == 'F' && opHex[2] == '5' && opHex[3] == '5') { OP_Fx55(opcode); return; }
            if (opHex[0] == 'F' && opHex[2] == '6' && opHex[3] == '5') { OP_Fx65(opcode); return; }
            throw new Exception("Invalid Opcode");
        }
    }
}