using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Chip8Emulator
{
    public partial class Form1 : Form
    {
        private Chip8 chip8;

        Thread chip8_thread;
        Thread displayThread;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class FIXED_BYTE_ARRAY
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 320)]
            public byte[] @byte;
        }
        private FIXED_BYTE_ARRAY video;

        private string currentLoadedROM = @"Test.ROM";

        private bool displayRendering = false;

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
            currentLoadedROM = @"Test.ROM";
            Execute(currentLoadedROM);
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
                comboBox1.SelectedIndex = -1;
                Reset();
                currentLoadedROM = openFileDialog.FileName;
                Execute(currentLoadedROM);
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            Reset();
            Execute(currentLoadedROM);
        }

        private void Reset()
        {
            if (chip8_thread != null)
            {
                chip8.Running = false;
                chip8.Stop();
                while (chip8.Running)
                {
                    chip8.Stop();
                    chip8_thread.Abort();
                }
                while (chip8_thread.IsAlive)
                {
                    chip8_thread.Abort();
                }
            }
            panel1.BackColor = Color.Red;
            trackBar1.Value = 20000;
        }

        private void Execute(string romPath)
        {
            if(displayThread != null)
                { displayThread = null; }
            panel1.BackColor = Color.Black;
            chip8 = new Chip8();
            chip8.ShiftQuirk = checkBox3.Checked;
            chip8.VFReset = checkBox5.Checked;
            chip8.JumpQuirk = checkBox4.Checked;
            chip8.MemoryQuirk = checkBox6.Checked;
            chip8.DebugMode = checkBox2.Checked;
            chip8.LoadROM(romPath);
            // update form display in it's own thread
            displayThread = new Thread(() => DisplayLoop());
            displayThread.IsBackground = true;
            displayThread.Start();
            // start Chip8 in it's own thread
            chip8_thread = new Thread(() => chip8.Start());
            chip8_thread.IsBackground = true;
            chip8_thread.Start();
        }

        private void DisplayLoop()
        {
            panel1.BackColor = Color.Black;
            while (!chip8.Running) { }
            while (chip8.Running)
            {
                try
                {
                    if(chip8.DisplayAvailable && !displayRendering)
                        RenderScreen();
                    if (checkBox1.Checked)
                    {
                        textBox1.Invoke((MethodInvoker)(() => textBox1.Text = String.Join(Environment.NewLine, chip8.DebugMainInfo())));
                        textBox2.Invoke((MethodInvoker)(() => textBox2.Text = String.Join(Environment.NewLine, chip8.DebugStackInfo())));
                    }
                }
                catch { }
            }
            panel1.BackColor = Color.Red;
        }

        private void RenderScreen()
        {
            displayRendering = true;
            Bitmap initalBitmap = new Bitmap(64, 32);
            video = new FIXED_BYTE_ARRAY { @byte = new byte[64 * 32] };
            video.@byte = chip8.Video.@byte;       
            int cnt = 0;
            for (int y = 0; y < 32; y++)
            {
                string row = String.Empty;
                for (int x = 0; x < 64; x++)
                {
                    if (video.@byte[cnt] != 0)
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
            displayRendering = false;
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
            if (comboBox1.SelectedIndex > -1)
                if (!String.IsNullOrEmpty(comboBox1.SelectedItem.ToString()))
                    Execute(comboBox1.Text);
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
            chip8.VFReset = checkBox5.Checked;
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            chip8.MemoryQuirk = checkBox6.Checked;
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

        private void comboBox_KeyPress(object sender, KeyPressEventArgs e) { e.KeyChar = (char)Keys.None; }

        private void button5_Click(object sender, EventArgs e)
        {
            if (comboBox1.Text.Length != 0)
                comboBox1_SelectedIndexChanged(this, e);
            else
                Form1_Shown(sender, e);
        }
    }
}