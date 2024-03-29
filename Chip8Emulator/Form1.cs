﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

namespace Chip8Emulator
{
    public partial class Form1 : Form
    {
        private Chip8 chip8 = null;
        private Bitmap screen;
        private byte delay = 0;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            chip8.Stop();
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
            chip8.Stop();
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
            pictureBox1.BackgroundImage = null;
        }

        private void Execute(string romPath)
        {
            chip8 = new Chip8();
            screen = new Bitmap(64, 32, PixelFormat.Format64bppArgb);
            chip8.LoadROM(romPath);
            chip8.DelayTimer = delay;
            // start Chip8 in it's own thread
            var chip8_thread = new Thread(() => chip8.Start());
            chip8_thread.IsBackground = true;
            chip8_thread.Start();
            // update form display in it's own thread
            var displayThread = new Thread(() => DisplayLoop());
            displayThread.IsBackground = true;
            displayThread.Start();
        }

        private void DisplayLoop()
        {
            Thread.Sleep(100); // short delay to allow Chip8 thread to start
            while (chip8.Running)
            {
                if (chip8.DisplayAvailable)
                {
                    chip8.DisplayAvailable = false;
                    RenderScreen();
                    pictureBox1.Invoke((MethodInvoker)(() => pictureBox1.BackgroundImage = screen));
                    pictureBox1.Invoke((MethodInvoker)(() => pictureBox1.Refresh()));
                }
                Application.DoEvents();
            }
        }

        private void RenderScreen()
        {
            try
            {
                int cnt = 0;
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        if (chip8.Video.@byte[cnt] != 0)
                            screen.SetPixel(x, y, Color.Black);
                        else
                            screen.SetPixel(x, y, Color.White);
                        cnt++;
                    }
            }
            catch { }
        }
    }
}