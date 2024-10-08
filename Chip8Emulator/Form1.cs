﻿using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chip8Emulator
{
    public partial class Form1 : Form
    {
        private Chip8 chip8 = null;

        public Form1()
        {
            InitializeComponent();
            SearchForCH8Roms();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            chip8.Pause();
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
            if (!String.IsNullOrEmpty(comboBox1.Text))
                comboBox1.Text = String.Empty;
            button2.Text = "Pause";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            chip8.Pause();
            if (!checkBox1.Checked)
            {
                textBox1.Text = "";
                textBox2.Text = "";
            }
            if (chip8.DebugMode)
                button2.Text = "Run";
            else
                button2.Text = "Pause";

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
            if (chip8 != null) chip8.Stop();
            comboBox1.Enabled = true;
        }

        private void Execute(string romPath)
        {
            chip8 = new Chip8();
            chip8.ShiftQuirk = checkBox3.Checked;
            chip8.LogicQuirk = checkBox5.Checked;
            chip8.JumpQuirk = checkBox4.Checked;
            chip8.LoadStoreQuirk = checkBox6.Checked;
            chip8.DebugMode = checkBox2.Checked;
            chip8.LoadROM(romPath);
            // update form display in it's own thread
            var displayThread = new Thread(() => DisplayLoop());
            displayThread.IsBackground = true;
            displayThread.Start();
            // start Chip8 in it's own thread
            var chip8_thread = new Thread(() => chip8.Start());
            chip8_thread.IsBackground = true;
            chip8_thread.Start();
        }

        private void DisplayLoop()
        {
            while (!chip8.Running) { }
            while (chip8.Running)
            {
                try
                {
                    RenderScreen();
                    if (checkBox1.Checked)
                    {
                        textBox1.Invoke((MethodInvoker)(() => textBox1.Text = String.Join(Environment.NewLine, chip8.DebugMainInfo())));
                        textBox2.Invoke((MethodInvoker)(() => textBox2.Text = String.Join(Environment.NewLine, chip8.DebugStackInfo())));
                    }
                }
                catch { }
            }
            comboBox1.Invoke((MethodInvoker)(() => comboBox1.Enabled = true));
        }

        private void RenderScreen()
        {
            Bitmap initalBitmap = new Bitmap(64, 32);
            int cnt = 0;
            for (int y = 0; y < 32; y++)
            {
                string row = String.Empty;
                for (int x = 0; x < 64; x++)
                {
                    if (chip8.Video.@byte[cnt] != 0)
                        initalBitmap.SetPixel(x, y, Color.LimeGreen);
                    else
                        initalBitmap.SetPixel(x, y, Color.Black);
                    cnt++;
                }
            }
            Rectangle outputContainerRect = new Rectangle(0, 0, 640, 320);
            Bitmap outputBitmap = new Bitmap(640, 320);
            outputBitmap.SetResolution(initalBitmap.HorizontalResolution, initalBitmap.VerticalResolution);
            using (Graphics graphics = Graphics.FromImage(outputBitmap))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (ImageAttributes wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(initalBitmap, outputContainerRect, 0, 0, initalBitmap.Width, initalBitmap.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }
            pictureBox1.Invoke((MethodInvoker)delegate { pictureBox1.Image = outputBitmap; });
        }

        private void SearchForCH8Roms()
        {
            var myFiles = Directory.EnumerateFiles(Application.StartupPath, "*.ch8");
            foreach (var file in myFiles)
            {
                comboBox1.Items.Add(Path.GetFileName(file));
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Reset();
            if (!String.IsNullOrEmpty(comboBox1.SelectedItem.ToString()))
                Execute(comboBox1.Text);
            comboBox1.Enabled = false;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            chip8.Step();
            textBox1.Invoke((MethodInvoker)(() => textBox1.Text = String.Join(Environment.NewLine, chip8.DebugMainInfo())));
            textBox2.Invoke((MethodInvoker)(() => textBox2.Text = String.Join(Environment.NewLine, chip8.DebugStackInfo())));
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            chip8.JumpQuirk = checkBox4.Checked;
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            chip8.ShiftQuirk = checkBox3.Checked;
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            chip8.LogicQuirk = checkBox5.Checked;
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            chip8.LoadStoreQuirk = checkBox6.Checked;
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            int val = trackBar1.Maximum - trackBar1.Value;
            if (chip8 != null)
            {
                if (val <= 20000)
                {
                    chip8.SimTick = val;
                }
                else
                {
                    var x = (val - 20000) * 50;
                    chip8.SimTick = x;
                }
            }
        }
    }
}