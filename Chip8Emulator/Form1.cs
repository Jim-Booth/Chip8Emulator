using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Media;
using System.Windows.Forms;

namespace Chip8Emulator
{
    public partial class Form1 : Form
    {
        private Chip8 chip8;
        private bool running = false;
        private Bitmap screen;
        private byte delay = 0;

        public Form1()
        {
            InitializeComponent();
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            running = false;
        }

        private void keypad_KeyDown(object sender, KeyEventArgs e)
        {
            uint k = GetKeyValue(e);
            if (k != 99)
                chip8.KeyDown = k;
        }

        private void keypad_KeyUp(object sender, KeyEventArgs e)
        {
            uint k = GetKeyValue(e);
            if (k != 99)
                chip8.KeyUp = k;
        }

        private uint GetKeyValue(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.D1)
                return 1;
            if (e.KeyCode == Keys.D2)
                return 2;
            if (e.KeyCode == Keys.D3)
                return 3;
            if (e.KeyCode == Keys.D4)
                return 12;
            if (e.KeyCode == Keys.Q)
                return 4;
            if (e.KeyCode == Keys.W)
                return 5;
            if (e.KeyCode == Keys.E)
                return 6;
            if (e.KeyCode == Keys.R)
                return 13;
            if (e.KeyCode == Keys.A)
                return 7;
            if (e.KeyCode == Keys.S)
                return 8;
            if (e.KeyCode == Keys.D)
                return 9;
            if (e.KeyCode == Keys.F)
                return 14;
            if (e.KeyCode == Keys.Z)
                return 10;
            if (e.KeyCode == Keys.X)
                return 0;
            if (e.KeyCode == Keys.C)
                return 11;
            if (e.KeyCode == Keys.V)
                return 15;
            return 99;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Reset();
            Execute(@"Test.ROM");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            button2.Text = "Resume";
            running = !running;
            if (running)
            {
                button2.Text = "Pause";
                ExecuteLoop();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                Reset();
                Execute(openFileDialog.FileName);
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            Reset();
            Execute(@"Test.ROM");
        }

        private void Reset()
        {
            running = false;
            chip8 = null;
            pictureBox1.BackgroundImage = null;
        }

        private void Execute(string romPath)
        {
            chip8 = new Chip8();
            screen = new Bitmap(64, 32, PixelFormat.Format64bppArgb);
            chip8.LoadROM(romPath);
            running = true;
            ExecuteLoop();
        }

        private void ExecuteLoop()
        {
            while (running)
            {
                chip8.DelayTimer = delay;
                chip8.Cycle();
                if (chip8.DisplayAvailable)
                {
                    RenderScreen();
                    pictureBox1.BackgroundImage = screen;
                    pictureBox1.Refresh();
                    chip8.DisplayAvailable = false;
                }
                this.Refresh();
                Application.DoEvents();
            }
        }

        private void RenderScreen()
        {
            int cnt = 0;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 64; x++)
                {
                    if (chip8.Video.b[cnt] != 0)
                        screen.SetPixel(x, y, Color.Black);
                    else
                        screen.SetPixel(x, y, Color.White);
                    cnt++;
                }
        }
    }
}