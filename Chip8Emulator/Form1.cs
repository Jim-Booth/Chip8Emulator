using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

namespace Chip8Emulator
{
    public partial class Form1 : Form
    {
        private Chip8 chip8 = null;
        private byte delay = 0;

        private IntPtr displayHandle;
        public IntPtr DisplayHandle()
        {
            return displayHandle;
        }

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
            displayHandle = textBox3.Handle;
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
            chip8.DebugMode = checkBox2.Checked;
            chip8.LoadROM(romPath);
            chip8.DelayTimer = delay;
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
                if (chip8.DisplayAvailable)
                {
                    try
                    {
                        chip8.DisplayAvailable = false;
                        RenderScreen();
                        if (checkBox1.Checked)
                        {
                            textBox1.Invoke((MethodInvoker)(() => textBox1.Text = String.Join(Environment.NewLine, chip8.DebugMainInfo())));
                            textBox2.Invoke((MethodInvoker)(() => textBox2.Text = String.Join(Environment.NewLine, chip8.DebugStackInfo())));
                        }
                    }
                    catch { }
                }
               // Application.DoEvents();
            }
            comboBox1.Invoke((MethodInvoker)(() => comboBox1.Enabled = true));
        }

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hwnd, int wMsg, int wParam, int lParam);

        private const int WM_SETREDRAW = 0xB;


        private void RenderScreen()
        {
            textBox3.Invoke((MethodInvoker)(() => textBox3.Text=""));
            var handle = DisplayHandle();
            SendMessage((IntPtr)handle, WM_SETREDRAW, 0, 0);
            string pixel = "█";
            List<string> disp = new List<string>();
            string row = "";
            int cnt = 0;
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    if (chip8.Video.@byte[cnt] != 0)
                        row += pixel;
                    else
                        row += " ";
                    cnt++;
                }
                disp.Add(row);
                row = "";
            }
            textBox3.Invoke((MethodInvoker)(() => textBox3.Text = String.Join(Environment.NewLine, disp)));
            SendMessage((IntPtr)handle, WM_SETREDRAW, 1, 0);
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
    }
}