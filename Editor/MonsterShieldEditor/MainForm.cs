/**    
	MonsterShield Prop Controller Editor software
    Copyright (C) 2015  Jason LeSueur Tatum

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
**/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;



/// Revisions
/// 1.0.0.5a
///     * Fixed filters on the File->Open dialog
///     * Fixed not showing the last loaded file in the File->Open dialog
///     * Added Computer Audio Offset adjustment setting to MP3 tab.  This allows you to tweak the playback from your computer so that it 
///       matches the timing from the MP3 player on the MonsterShield.  Note that there is still a problem with the audio getting delayed a 
///       fraction of a second on the computer the first time an animation is triggered after it is selected.  This is because the MP3 file is
///       being cached before playback begins, and this takes up a certain amount of time.  Subsequent triggers are okay because the MP3 file already
///       got cached.  We are still investigating ways to address this.  No matter what, the audio from the actual MP3 module should be considered
///       the actual timing.  It is the computer audio that is off, not the MP3 module.
///     * Added refresh rate adjustment so that the editor performs better on slower computers and/or uses less CPU during playback of animations.
///     * Completely redesigned memory footprint and Monster file to work with new firmware memory layout.
///     * Fixed issues with initial directory settings in Open and Save dialogs.
///     * Changed .MOS (Monster file) format to include all MonsterShield configuration settings.  
///     * If a MonsterShield is connected when a .MOS file is opened, the settings contained in the .MOS file will be sent to the MonsterShield.
///       This allows for turn-key sharing of Monster files.
///       
///    KNOWN ISSUES:
///     * First time the computer plays an MP3 file, the timing is way off.  Subsequent plays of that MP3 file are okay until
///       a different MP3 file is selected when switching tracks.  Has to do with Windows Media Player loading & caching.
/// 
/// 1.0.0.4
///     * Included Arduino drivers with MonsterShield Editor to ease connecting MonsterShield hardware to computer.
///     * Fixed bug where animation changes on current slot were not saved when clicking the Save or Save As menu options unless you selected another slot before saving.
///     * Mouse wheel now scrolls in & out of animation window.
///     * Com ports listed in the Options menu are now automatically refreshed every time the menu is displayed.  
///       It's now no longer necessary to restart the software if you plug in a MonsterShield after the software was restarted.
///     * Added keyboard shortcuts to menu options: New (Ctrl+N), Open (Ctrl+O), Save (Ctrl+S), Cut (Ctrl+X), Copy (Ctrl+C), Paste (Ctrl+V), and Select All (Ctrl+A).  
/// 
/// 1.0.0.3
///     * Enabled the ability to change number of slots and slots / per command
///     * Fixed crash bug when exceeding 4096 command buffer
///     * Fixed bug when trying to display data that exceeds 4096 commands.
///     * Added slot mode:  2 slots / 8064 events per slot
///     * Fixed bug in parsing slot number from trigger commands that caused it
///       to somtimes parse as 0.  This fixed some issues with ambient mode.
///     * Fixed some issues with ambient mode handling and interaction.
///     
/// TODO:
///     * fix the fact that Arduino code allows you to load in more data than you are supposed to be able to.
///     * fix code to not allow Editor to send more than max data based on current slot mode.


namespace MonsterShieldEditor
{
    public partial class MainForm : Form
    {

        private int trackleft = 100;
        private int tracktop = 25;
        private int tracktop_nowaveform = 25;
        private int tracktop_waveform = 100;
        private int trackheight = 20;
        private int trackspacing = 2;

        private int toptrackview = 0;

        //private string ComPort = "COM12";
        private bool InterfaceAutomation = false;
        private bool HighPrecision = false;
        private string ComPort = "COM7";
        private string filename = "untitled.mos";
        string RxString;
        string cmdBuffer;
        int selectedSequence = 0;
        private bool [] relayStates = new bool[16];
        private long LeftAnimPos = 0;   // Position of left edge of animation window in the animation.
        private long selectLeft = 0;
        private long selectRight = 0;
        private long CursorPos = 0;
        private short CursorRelay = -1;

        private bool KeyStateShift = false;
        private bool PlayOnline = false;
        private bool DownloadAll = false;
        private int CurrentDownloadSlot = 0;
        private int NumberOfSlots = 15;
        double audioOffset = -0.25;

        private int zoom = 1;
        const int ZOOM_MAX = 8;
        const int MAX_COMMANDS_PER_SECOND = 100;
        
        //const int MAX_PLAYBACK_BUFFER = 32768*4;
        const int MAX_PLAYBACK_BUFFER = 32768/2; // Changed on 9/8/2013
        //const int MAX_PLAYBACK_BUFFER = 22 * 60 * 500; // minutes * seconds * 1/1000 of a second  (16 minutes)
        //const int MAX_PLAYBACK_BUFFER = 11 * 60 * 100; // minutes * seconds * 1/1000 of a second  (16 minutes)
        //const int MAX_PLAYBACK_BUFFER = 30 * 60 * 10000; // minutes * seconds * 1/1000 of a second  (16 minutes)

        const int MAX_COMMANDS = 8064;
        const int TOOLMODE_DRAW = 0;
        const int TOOLMODE_SELECT = 1;

        //private bool[] relayStates = new bool[16];



        private enum MouseModes
        {
            MouseModeNone,
            MouseModeAdjustEnd,
            MouseModeDraw
        }

        private enum PlaybackMode
        {
            EditMode,
            PlayMode
        }

        private int ToolMode = TOOLMODE_DRAW;
        private MouseModes currentMouseMode = MouseModes.MouseModeNone;
        private PlaybackMode currentPlaybackMode = PlaybackMode.EditMode;


        private MonsterFile ourMonsterFile = new MonsterFile();

        AnimationSlot2 currentSlot;

        //private List<AnimationSlot> slots = new List<AnimationSlot>(15);

        //private byte[] commands = new byte[MAX_COMMANDS]; // each byte represents 25 ms, or 1/40 of a second.
        //private byte[] delays = new byte[MAX_COMMANDS];
        private short[] sequenceLengths = new short[15];

        private byte[] playbackBuffer = new byte[MAX_PLAYBACK_BUFFER];
        private byte[] copyBuffer1 = new byte[32768 / 2];
        private byte[] copyBuffer2 = new byte[32768 / 2];
        private long copyBufferLength = 0;

        private short downloadPointer = 0;
        private long AnimationEndMarker = 0;

        private Stopwatch watch = new Stopwatch();
        private int prevMouseX = 0;

        private ProgressForm frmProgress;


        private Font rulerFont = new Font("Arial", 8);
        private Font relayFont = new Font("Courier New", 8);

        private string FilePath = "";
        private string MusicPath = "";

        private bool RenderPulses = true;  // This flashes a box on the screen once per second.
        private bool PulseToggle = false;
        private int lastSecond = 0;

        private int refreshinterval = 25;

        public MainForm()
        {
            InitializeComponent();

            
           // enhancedWaveViewer1.Left = trackleft;

            for (int i = 0; i < 16; i++)
            {
                relayStates[i] = false;
            }


            InitNewAnimations();

            for (int i = 0; i < MAX_PLAYBACK_BUFFER; i++)
            {
                playbackBuffer[i] = 0x0F;
            }



            SetTitle();

            EnumerateComPorts();



            SetSequence(0);

            try
            {
                // Get the connection string from the registry.
                if (Application.UserAppDataRegistry.GetValue("ComPort") != null)
                {
                    ComPort = Application.UserAppDataRegistry.GetValue("ComPort").ToString();
                    serialPort1.PortName = ComPort;
                    lblStatusComPort.Text = ComPort;
                }
                else
                {
                    ComPort = "UNDEFINED";
                    serialPort1.PortName = ComPort;
                    lblStatusComPort.Text = ComPort;
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                ComPort = "UNDEFINED";
                serialPort1.PortName = ComPort;
                lblStatusComPort.Text = ComPort;
            }


            try
            {
                if (Application.UserAppDataRegistry.GetValue("FilePath") != null)
                {
                    FilePath = Application.UserAppDataRegistry.GetValue("FilePath").ToString();
                }
            }
            catch (Exception ex)
            {
                FilePath = "untitled.mos";
            }


            try
            {
                if (Application.UserAppDataRegistry.GetValue("MusicPath") != null)
                {
                    MusicPath = Application.UserAppDataRegistry.GetValue("MusicPath").ToString();
                }
            }
            catch (Exception ex)
            {
            }



            try
            {
                if (Application.UserAppDataRegistry.GetValue("AudioOffset") != null)
                {
                    audioOffset = Convert.ToDouble(Application.UserAppDataRegistry.GetValue("AudioOffset"));
                }
            }
            catch (Exception ex)
            {
                audioOffset = -0.361f;
            }
            numericUpDown1.Value = Convert.ToDecimal(audioOffset);

            int rr = 8;
            try
            {
                if (Application.UserAppDataRegistry.GetValue("RefreshRate") != null)
                {
                    rr = Convert.ToInt16(Application.UserAppDataRegistry.GetValue("RefreshRate"));
                }
            }
            catch (Exception ex)
            {
                rr = 8;
            }
            trackBar1.Value = rr;
            ConvertRefreshToInterval(rr);
           


            SetOnlineUI(false);

            cboSlotCount.Items.Clear();
            for (int i = 15; i > 0; i--)
            {
                cboSlotCount.Items.Add(i.ToString());
            }
            cboSlotCount.SelectedIndex = 0;
            comboBoxPlaybackMode.SelectedIndex = 0;

            ourMonsterFile.setTotalEventsPerSlot(15);


            ProcessNewZoomFactor();
            SetEndMarkerLabel();

            this.MouseWheel += new MouseEventHandler(MainForm_MouseWheel);

            enhancedWaveViewer1.Visible = false;
            tracktop = tracktop_nowaveform;


            try
            {
                if (Application.UserAppDataRegistry.GetValue("FilePath") != null)
                {
                    filename = Application.UserAppDataRegistry.GetValue("FilePath").ToString();
                    if (System.IO.File.Exists(filename))
                    {
                        LoadAnimationSlots(filename);
                    }
                }
            }
            catch (Exception ex)
            {
            }



        }

        void MainForm_MouseWheel(object sender, MouseEventArgs e)
        {
            //throw new NotImplementedException();
            if (e.Delta > 0)
            {
                zoom += 1;
                if (zoom > ZOOM_MAX) zoom = ZOOM_MAX;
                ProcessNewZoomFactor();
                pictureBox1.Refresh();
            }
            else if (e.Delta < 0)
            {
                zoom -= 1;
                if (zoom < 1) zoom = 1;
                ProcessNewZoomFactor();
                pictureBox1.Refresh();
            }
        }



        public string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        private void SetTitle()
        {
            this.Text = string.Format("{0} [BETA] {1} - {2}", AssemblyTitle, AssemblyVersion, filename);
        }

        private bool isConnected()
        {
            return isConnected(false);
        }

        private bool isConnected(bool ConnectNow)
        {
            if (ComPort == "UNDEFINED")
                return false;

            if (serialPort1.IsOpen)
            {
                return true;
            }
            else
            {

                if (ConnectNow)
                {

                    // Not connected, try to connect.
                    try
                    {
                        serialPort1.PortName = ComPort;
                        serialPort1.Open();
                    }
                    catch (Exception ex)
                    {
                        lblStatusConnection.Text = "Disconnected";
                        lblStatusBaud.Text = "--";
                        lblStatusComPort.Text = "--";
                        lblStatusMessage.Text = ex.Message;
                        lblStatusMessage.ForeColor = Color.Red;
                        btnConnect.Text = "Connect";
                    }

                    if (serialPort1.IsOpen)
                    {
                        textBox1.Text = "Connected on port " + serialPort1.PortName + " at " + serialPort1.BaudRate + " baud.\r\n";
                        lblStatusMessage.Text = "Succesfully connected to MonsterShield!";
                        lblStatusMessage.ForeColor = Color.Black;
                        lblStatusConnection.Text = "Connected ";
                        lblStatusComPort.Text = serialPort1.PortName;
                        lblStatusBaud.Text = serialPort1.BaudRate.ToString();
                        serialPort1.WriteLine("@V");   // Request firmware version
                        serialPort1.WriteLine("@E");   // Request EEPROM memory installed.
                        serialPort1.WriteLine("@O0");
                        serialPort1.WriteLine("@Z0");
                        serialPort1.WriteLine("@Z1");
                        serialPort1.WriteLine("@Z2");
                        serialPort1.WriteLine("@Z3");
                        serialPort1.WriteLine("@Y0");
                        btnConnect.Text = "Disconnect";
                        SetOnlineUI(true);
                        return true;
                    }
                    else
                    {
                        btnConnect.Text = "Connect";
                        textBox1.Text = "Failed to connect to MonsterShield on port " + serialPort1.PortName + "!\r\n";
                        SetOnlineUI(false);
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen == true)
            {
                try
                {
                    serialPort1.Close();
                }
                catch
                {
                }

                SetOnlineUI(false);
                lblStatusConnection.Text = "Disconnected";
                lblStatusBaud.Text = "--";
                lblStatusComPort.Text = "--";
                lblStatusMessage.Text = "";
                btnConnect.Text = "Connect";
            }
            else
            {
                if (ComPort == "UNDEFINED")
                {
                    MessageBox.Show("Please select a COM port from the \"Options -> Com Port\" menu first!", "COM port not set!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                isConnected(true);
            }
            

            //serialPort1.Open();
            //if (serialPort1.IsOpen)
            //{
            //    //buttonStart.Enabled = false;
            //    //buttonStop.Enabled = true;
            //    textBox1.ReadOnly = false;
            //    textBox1.Text = "Connected on port " + serialPort1.PortName + " at " + serialPort1.BaudRate + " baud.\r\n";
            //}
        }

        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            RxString = serialPort1.ReadExisting();
            this.Invoke(new EventHandler(DisplayText));
            
            
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (serialPort1.IsOpen) serialPort1.Close();
            Application.UserAppDataRegistry.SetValue("AudioOffset", audioOffset);
        }

        private void DisplayText(object sender, EventArgs e)
        {
            textBox1.AppendText(RxString);
            cmdBuffer = cmdBuffer + RxString;
            while (cmdBuffer.Contains("\r\n"))
            {
                // We found a command!

                // Select selected from MonsterShield
                if (cmdBuffer.StartsWith("$S"))
                {
                    int ival;
                    if (cmdBuffer.Length == 5)
                    {
                        string value = cmdBuffer.Substring(2, 1);
                        if (int.TryParse(value, out ival))
                        {
                            SetSequence(ival);
                        }
                    }
                    else if (cmdBuffer.Length == 6)
                    {
                        string value = cmdBuffer.Substring(2, 2);
                        if (int.TryParse(value, out ival))
                        {
                            SetSequence(ival);
                        }
                    }
                }


                // Sequence enabled from MonsterShield
                if (cmdBuffer.StartsWith("$+"))
                {
                    int ival = -1;
                    if (cmdBuffer.Length == 5)
                    {
                        string value = cmdBuffer.Substring(2, 1);
                        if (int.TryParse(value, out ival))
                        {
                            //SetSequence(ival);
                        }
                    }
                    else if (cmdBuffer.Length == 6)
                    {
                        string value = cmdBuffer.Substring(2, 2);
                        if (int.TryParse(value, out ival))
                        {
                            //SetSequence(ival);
                        }
                    }

                    if (ival > -1)
                    {
                        SetSequenceEnabled(ival, true);
                    }
                }

                // Sequence disabled from MonsterShield
                if (cmdBuffer.StartsWith("$-"))
                {
                    int ival = -1;
                    if (cmdBuffer.Length == 5)
                    {
                        string value = cmdBuffer.Substring(2, 1);
                        if (int.TryParse(value, out ival))
                        {
                            //SetSequence(ival);
                        }
                    }
                    else if (cmdBuffer.Length == 6)
                    {
                        string value = cmdBuffer.Substring(2, 2);
                        if (int.TryParse(value, out ival))
                        {
                            //SetSequence(ival);
                        }
                    }

                    if (ival > -1)
                    {
                        SetSequenceEnabled(ival, false);
                    }

                }












                if (cmdBuffer.StartsWith("$P"))
                {
                    int ival;
                    if (cmdBuffer.Length == 5)
                    {
                        string value = cmdBuffer.Substring(2, 1);
                        if (int.TryParse(value, out ival))
                        {
                            //SetSequence(ival);
                            if (ival >= 0 && ival < 3)
                            {
                                InterfaceAutomation = true; // Set this so that we don't send a serial command back down.
                                comboBoxPlaybackMode.SelectedIndex = ival;
                            }
                        }
                    }
                }
                else if (cmdBuffer.StartsWith("$00"))
                {
                    // Playback has completed!
                    StopPlayback();
                    currentPlaybackMode = PlaybackMode.EditMode;
                    SetPlaybackModeUI(false);

                }
                else if (cmdBuffer.StartsWith("$SLOTCNT="))
                {
                    int ival;
                    int x = cmdBuffer.IndexOf("\r\n") - 9;
                    if (x < 1) x = 1;
                    if (int.TryParse(cmdBuffer.Substring(9, x), out ival))
                    {
                        if (ival >= 0 && ival <= cboSlotCount.Items.Count - 1)
                        {
                            //InterfaceAutomation = true;
                            cboSlotCount.SelectedIndex = 15 - ival;
                            ConfigureSlots();
                        }
                    }

                }
                else if (cmdBuffer.StartsWith("$EEPROM"))
                {
                    if (cmdBuffer.IndexOf("1=") == 7)
                    {
                        // EEPROM 1 slot
                        if (cmdBuffer.Substring(9, 1) == "1")
                            chkEEPROM1.Checked = true;
                        else
                            chkEEPROM1.Checked = false;
                    }
                    else if (cmdBuffer.IndexOf("2=") == 7)
                    {
                        // EEPROM 2 slot
                        if (cmdBuffer.Substring(9, 1) == "1")
                            chkEEPROM2.Checked = true;
                        else
                            chkEEPROM2.Checked = false;
                    }
                }
                else if (cmdBuffer.StartsWith("$TRIG"))
                {
                    if (cmdBuffer.IndexOf("=") == 6)
                    {
                        
                        int trigger = int.Parse(cmdBuffer.Substring(5, 1));

                        string []values = cmdBuffer.Substring(7).Split(',');
                        ourMonsterFile.TriggerSensitivity[trigger] = int.Parse(values[0]);
                        ourMonsterFile.TriggerThreshold[trigger] = int.Parse(values[1]);
                        ourMonsterFile.TriggerCooldown[trigger] = int.Parse(values[2]);

                        if (values[3] == "1")
                            ourMonsterFile.TriggerOnHigh[trigger] = true;
                        else
                            ourMonsterFile.TriggerOnHigh[trigger] = false;

                        if (int.Parse(values[4]) == 1)
                            ourMonsterFile.TriggerIgnoreUntilReset[trigger] = true;
                        else
                            ourMonsterFile.TriggerIgnoreUntilReset[trigger] = false;
                        RefreshTriggerInfo();

                    }


                    /*
                    if (cmdBuffer.StartsWith("$TRIG.THR="))
                    {
                        int ival;
                        int x = cmdBuffer.IndexOf("\r\n") - 10;
                        if (x < 1) x = 1;
                        if (int.TryParse(cmdBuffer.Substring(10, x), out ival))
                        {
                            if (ival < 100) ival = 100;
                            hScrollTriggerThreshold.Value = ival;
                        }
                    }
                    else if (cmdBuffer.StartsWith("$TRIG.SEN="))
                    {
                        int ival;
                        int x = cmdBuffer.IndexOf("\r\n") - 10;
                        if (x < 1) x = 1;
                        if (int.TryParse(cmdBuffer.Substring(10, x), out ival))
                        {
                            if (ival > 255) ival = 255;
                            if (ival < 0) ival = 0;
                            hScrollTriggerSensitivity.Value = ival;
                        }
                    }
                    else if (cmdBuffer.StartsWith("$TRIG.COO="))
                    {
                        int ival;
                        int x = cmdBuffer.IndexOf("\r\n") - 10;
                        if (x < 1) x = 1;
                        if (int.TryParse(cmdBuffer.Substring(10, x), out ival))
                        {
                            if (ival > 255) ival = 255;
                            if (ival < 0) ival = 0;
                            hScrollTriggerCooldown.Value = ival;
                        }
                    }
                    else if (cmdBuffer.StartsWith("$TRIG.VOL="))
                    {
                        int ival;
                        int x = cmdBuffer.IndexOf("\r\n") - 10;
                        if (x < 1) x = 1;
                        if (int.TryParse(cmdBuffer.Substring(10, x), out ival))
                        {
                            if (ival > 1) ival = 1;
                            if (ival < 0) ival = 0;
                            if (ival == 1)
                            {
                                rdoHigh.Checked = true;
                            }
                            else
                            {
                                rdoLow.Checked = true;
                            }
                        }
                    }
                    else if (cmdBuffer.StartsWith("$TRIG.RST="))
                    {
                        int ival;
                        int x = cmdBuffer.IndexOf("\r\n") - 10;
                        if (x < 1) x = 1;
                        if (int.TryParse(cmdBuffer.Substring(10, x), out ival))
                        {
                            if (ival > 1) ival = 1;
                            if (ival < 0) ival = 0;
                            if (ival == 1)
                            {
                                chkResetOnVoltage.Checked = true;
                            }
                            else
                            {
                                chkResetOnVoltage.Checked = false;
                            }
                        }
                    }
                     * */

                }
                else if (cmdBuffer.StartsWith("$T"))
                {
                    // Find out which sequence was triggered

                    int i = cmdBuffer.IndexOf('\r');
                    string strCount = cmdBuffer.Substring(2, i - 2);
                    int ival = 0;
                    int.TryParse(strCount, out ival);

                    SetSequence(ival);


                    // Playback was triggered!
                    hScrollBar1.Value = 0;
                    LeftAnimPos = 0;
                    timer1.Interval = refreshinterval;
                    timer1.Start();
                    watch.Reset();
                    watch.Start();
                    SetPlaybackModeUI(true);
                    currentPlaybackMode = PlaybackMode.PlayMode;

                }
                else if (cmdBuffer.StartsWith("$R"))
                {
                    //byte ival;


                    //byte cmd1 = (byte)cmdBuffer[2];
                    //byte cmd2 = (byte)cmdBuffer[3];

                    cmdBuffer = cmdBuffer.Substring(2);
                    string strcmd1 = cmdBuffer.Substring(0, 2);
                    string strcmd2 = cmdBuffer.Substring(2, 2);

                    byte cmd1 = byte.Parse(strcmd1, System.Globalization.NumberStyles.AllowHexSpecifier);
                    byte cmd2 = byte.Parse(strcmd2, System.Globalization.NumberStyles.AllowHexSpecifier);


                    /*


                    if ((cmd1 & 0x01) == 0x01)
                        SetRelayState(0, false);
                    else
                        SetRelayState(0, true);

                    if ((cmd1 & 0x02) == 0x02)
                        SetRelayState(1, false);
                    else
                        SetRelayState(1, true);

                    if ((cmd1 & 0x04) == 0x04)
                        SetRelayState(2, false);
                    else
                        SetRelayState(2, true);

                    if ((cmd1 & 0x08) == 0x08)
                        SetRelayState(3, false);
                    else
                        SetRelayState(3, true);
                    */

                    for (int i = 0; i < 8; i++)
                    {
                        if ((cmd1 & (0x01 << i)) == (0x01 << i))
                        {
                            relayStates[i] = false;
                        }
                        else
                        {
                            relayStates[i] = true;
                        }
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        if ((cmd2 & (0x01 << i)) == (0x01 << i))
                        {
                            relayStates[i + 8] = false;
                        }
                        else
                        {
                            relayStates[i + 8] = true;
                        }
                    }



                    //pictureBox1.Refresh();


                    /*
                    string value = cmdBuffer.Substring(2, 1);

                    byte c = (byte)value[0];
                    if (c >= 65)
                    {
                        c = (byte)(c - 0x37);
                    }

                    if ((c & 0x01) == 0x01)
                    {
                        SetRelayState(btnRelay0, false);
                    }
                    else
                    {
                        SetRelayState(btnRelay0, true);
                    }

                    if ((c & 0x02) == 0x02)
                    {
                        SetRelayState(btnRelay1, false);
                    }
                    else
                    {
                        SetRelayState(btnRelay1, true);
                    }

                    if ((c & 0x04) == 0x04)
                    {
                        SetRelayState(btnRelay2, false);
                    }
                    else
                    {
                        SetRelayState(btnRelay2, true);
                    }

                    if ((c & 0x08) == 0x08)
                    {
                        SetRelayState(btnRelay3, false);
                    }
                    else
                    {
                        SetRelayState(btnRelay3, true);
                    }

                     * */


                }
                else if (cmdBuffer.StartsWith("$O"))
                {
                    chk0.Checked = false;
                    chk1.Checked = false;
                    chk2.Checked = false;
                    chk3.Checked = false;
                    chk4.Checked = false;
                    chk5.Checked = false;
                    chk6.Checked = false;
                    chk7.Checked = false;
                    chk8.Checked = false;
                    chk9.Checked = false;
                    chkA.Checked = false;
                    chkB.Checked = false;
                    chkC.Checked = false;
                    chkD.Checked = false;
                    chkE.Checked = false;

                    // Parse each enabled/disabled flag
                    if (cmdBuffer.Substring(2, 1) == "1") chk0.Checked = true;
                    if (cmdBuffer.Substring(3, 1) == "1") chk1.Checked = true;
                    if (cmdBuffer.Substring(4, 1) == "1") chk2.Checked = true;
                    if (cmdBuffer.Substring(5, 1) == "1") chk3.Checked = true;
                    if (cmdBuffer.Substring(6, 1) == "1") chk4.Checked = true;
                    if (cmdBuffer.Substring(7, 1) == "1") chk5.Checked = true;
                    if (cmdBuffer.Substring(8, 1) == "1") chk6.Checked = true;
                    if (cmdBuffer.Substring(9, 1) == "1") chk7.Checked = true;
                    if (cmdBuffer.Substring(10, 1) == "1") chk8.Checked = true;
                    if (cmdBuffer.Substring(11, 1) == "1") chk9.Checked = true;
                    if (cmdBuffer.Substring(12, 1) == "1") chkA.Checked = true;
                    if (cmdBuffer.Substring(13, 1) == "1") chkB.Checked = true;
                    if (cmdBuffer.Substring(14, 1) == "1") chkC.Checked = true;
                    if (cmdBuffer.Substring(15, 1) == "1") chkD.Checked = true;
                    if (cmdBuffer.Substring(16, 1) == "1") chkE.Checked = true;

                    LoadSlotsFromCheckboxes();

                    //for (int i = 0; i < 15; i++)
                    //{
                    //    if (cmdBuffer.Substring(i + 3, 1) == '1')
                    //    {

                    //    }
                    //}
                }
                else if (cmdBuffer.StartsWith("$NUMCMD="))
                {
                    int i = cmdBuffer.IndexOf('\r');
                    string strCount = cmdBuffer.Substring(8, i - 8);
                    short cmdCount = 0;
                    short.TryParse(strCount, out cmdCount);
                    ourMonsterFile.slots[CurrentDownloadSlot].AnimationCommandLength = cmdCount;
                    ourMonsterFile.slots[CurrentDownloadSlot].AnimationEnd = cmdCount;
                }
                else if (cmdBuffer.StartsWith("$BEGIN!"))
                {
                    this.Enabled = false;
                    ourMonsterFile.slots[CurrentDownloadSlot].Init();
                    if (frmProgress == null || frmProgress.IsDisposed)
                    {
                        frmProgress = new ProgressForm();

                    }
                    frmProgress.ProgressValue = 0;
                    frmProgress.ProgressMax = ourMonsterFile.slots[CurrentDownloadSlot].AnimationCommandLength;
                    frmProgress.MessageLabel = string.Format("Downloading slot {0} from MonsterShield...", CurrentDownloadSlot);
                    frmProgress.Show();




                    //for (int i = 0; i < MAX_PLAYBACK_BUFFER; i++)
                    //{
                    //    playbackBuffer[i] = 0x0F;
                    //}

                    textBox1.AppendText("Beginning download...\r\n");
                    downloadPointer = 0;
                }
                else if (cmdBuffer.StartsWith("$D"))
                {
                    cmdBuffer = cmdBuffer.Substring(2);
                    // There should be 32 events (2 bytes for each event) followed by a '<' character.
                    //Console.WriteLine(cmdBuffer);
                    for (int i = 0; i < 32; i++)
                    {
                        if (downloadPointer < ourMonsterFile.slots[CurrentDownloadSlot].AnimationCommandLength)
                        {
                            string strcmd1 = cmdBuffer.Substring(0, 2);
                            string strcmd2 = cmdBuffer.Substring(2, 2);
                            ourMonsterFile.slots[CurrentDownloadSlot].cmd1[downloadPointer] = byte.Parse(strcmd1, System.Globalization.NumberStyles.AllowHexSpecifier);
                            ourMonsterFile.slots[CurrentDownloadSlot].cmd2[downloadPointer] = byte.Parse(strcmd2, System.Globalization.NumberStyles.AllowHexSpecifier);
                        }
                        else
                        {
                            ourMonsterFile.slots[CurrentDownloadSlot].cmd1[downloadPointer] = 0xFF;
                            ourMonsterFile.slots[CurrentDownloadSlot].cmd2[downloadPointer] = 0xFF;
                        }


                        downloadPointer += 1;

                        if ((downloadPointer % 10) == 0)
                            frmProgress.ProgressValue = downloadPointer;

                        cmdBuffer = cmdBuffer.Substring(4);
                    }

                    /*
                    if (cmdBuffer.Length >= 5)
                    {
                        cmdBuffer = cmdBuffer.Substring(2);
                        while (cmdBuffer[0] != '\r')
                        {
                            string strcmd1 = cmdBuffer.Substring(0, 2);
                            string strcmd2 = cmdBuffer.Substring(2, 2);
                            string strtiming = cmdBuffer.Substring(4, 4);
                            //string strtiming2 = cmdBuffer.Substring(6, 2);

                            if (downloadPointer < ourMonsterFile.slots[CurrentDownloadSlot].timing.Length - 2)
                            {
                                ourMonsterFile.slots[CurrentDownloadSlot].cmd1[downloadPointer] = byte.Parse(strcmd1, System.Globalization.NumberStyles.AllowHexSpecifier);
                                ourMonsterFile.slots[CurrentDownloadSlot].cmd2[downloadPointer] = byte.Parse(strcmd2, System.Globalization.NumberStyles.AllowHexSpecifier);
                                ourMonsterFile.slots[CurrentDownloadSlot].timing[downloadPointer] = ushort.Parse(strtiming, System.Globalization.NumberStyles.AllowHexSpecifier);
                                if (HighPrecision)
                                {
                                    ourMonsterFile.slots[CurrentDownloadSlot].timing[downloadPointer] = (ushort)(ourMonsterFile.slots[CurrentDownloadSlot].timing[downloadPointer] / 10);
                                }
                                else
                                {
                                    ourMonsterFile.slots[CurrentDownloadSlot].timing[downloadPointer] = (ushort)(ourMonsterFile.slots[CurrentDownloadSlot].timing[downloadPointer] / 10);
                                }
                                downloadPointer += 1;
                            }

                            if ((downloadPointer % 10) == 0)
                                frmProgress.ProgressValue = downloadPointer;

                            cmdBuffer = cmdBuffer.Substring(8);
                        }
                    }
                     * */
                }
                else if (cmdBuffer.StartsWith("$END!"))
                {
                    textBox1.AppendText("Download complete!\r\n");
                    //ourMonsterFile.slots[CurrentDownloadSlot].AnimationCommandLength = downloadPointer;

                    if (!DownloadAll)
                    {
                        frmProgress.Close();
                        frmProgress.Dispose();
                        this.Enabled = true;
                        this.Activate();
                    }

                    // Find animation end marker:
                    //int pos = 0;
                    //for (int i = 0; i < ourMonsterFile.slots[CurrentDownloadSlot].AnimationCommandLength; i++)
                    //{
                    //    int j = 0;
                    //    do
                    //    {
                    //        pos += 1;
                    //        j += 1;

                    //    } while (j < ourMonsterFile.slots[CurrentDownloadSlot].timing[i + 1]);
                    //}
                    //ourMonsterFile.slots[CurrentDownloadSlot].AnimationEnd = pos;
                    if (ourMonsterFile.slots[CurrentDownloadSlot].AnimationCommandLength > 0)
                    {
                        //ourMonsterFile.slots[CurrentDownloadSlot].AnimationEnd = ourMonsterFile.slots[CurrentDownloadSlot].timing[ourMonsterFile.slots[CurrentDownloadSlot].AnimationCommandLength - 1] * 10;
                        ourMonsterFile.slots[CurrentDownloadSlot].AnimationEnd = ourMonsterFile.slots[CurrentDownloadSlot].AnimationCommandLength;

                    }
                    if (DownloadAll)
                    {
                        CurrentDownloadSlot += 1;
                        if (CurrentDownloadSlot < NumberOfSlots)
                        {
                            if (isConnected())
                            {

                                string data = "@D" + GetSequenceChar(CurrentDownloadSlot);
                                serialPort1.WriteLine(data);
                            }
                        }
                        else
                        {
                            frmProgress.Close();
                            frmProgress.Dispose();
                            this.Enabled = true;
                            this.Activate();
                            DisplayAnimationSlot(ourMonsterFile.slots[0]);
                            SelectSequence(0);
                        }
                    }
                    else
                    {
                        DisplayAnimationSlot(ourMonsterFile.slots[CurrentDownloadSlot]);
                    }



                }


                cmdBuffer = cmdBuffer.Substring(cmdBuffer.IndexOf("\r\n") + 2);
            }

           
        }

        /// <summary>
        /// Converts the command table to the playback buffer so that it can be rendered on screen.
        /// </summary>
        /// <param name="slot"></param>
        private void DisplayAnimationSlot(AnimationSlot2 slot)
        {
            currentSlot = slot;
            AnimationEndMarker = ourMonsterFile.slots[selectedSequence].AnimationEnd;
            /*
            int pos = 0;
            byte prevvalue;

            for (int i = 0; i < MAX_PLAYBACK_BUFFER; i++)
            {
                playbackBuffer[i] = 0x0F;
            }

            for (int i = 0; i < slot.AnimationCommandLength; i++)
            {


                prevvalue = slot.cmd1[i];
                int start = 0;
                int stop = 0;
                if (!HighPrecision)
                {
                    start = slot.timing[i] * 10;
                    stop = slot.timing[i + 1] * 10;
                }
                else
                {
                    start = (slot.cmd2[i] << 16) + slot.timing[i];
                    stop = (slot.cmd2[i+1] << 16) + slot.timing[i + 1];
                }

                //int start = slot.timing[i] * 10;
                //int stop = slot.timing[i + 1] * 10;




                for (int j = start; j < stop; j++)
                {
                    playbackBuffer[j] = prevvalue;
                }



                //int j = 0;
                //do
                //{
                //    if (pos < playbackBuffer.Length)
                //        playbackBuffer[pos] = prevvalue;
                //    pos += 1;
                //    j += 1;

                //} while (i+1 < slot.timing.Length-1 && j < slot.timing[i+1]);

            }

            //slot.AnimationEnd = pos;
            AnimationEndMarker = ourMonsterFile.slots[selectedSequence].AnimationEnd;
            SetEndMarkerLabel();

             * 
             * 
             * */


            LeftAnimPos = 0;
            hScrollBar1.Value = 0;
            hScrollBar1.Maximum = currentSlot.cmd1.Length;
            pictureBox1.Refresh();
        }

        /// <summary>
        /// Converts the playback buffer (which can be displayed on the screen) to the 
        /// command buffer.
        /// </summary>
        /// <param name="slot"></param>
        private void SaveAnimationToSlot(AnimationSlot2 slot)
        {
            //slot.Init();

            currentSlot.cmd1.CopyTo(slot.cmd1, 0);
            currentSlot.cmd2.CopyTo(slot.cmd2, 0);

            slot.AnimationCommandLength = currentSlot.AnimationCommandLength;
            slot.AnimationEnd = AnimationEndMarker;

            /*
            //byte delayCount = 0;
            //byte prevByte = playbackBuffer[0];
            byte prevByte = 255;

            //slot.commands[0] = 0x0F;
            slot.cmd1[0] = playbackBuffer[0];
            slot.timing[0] = 0x00;
            short cmdCount = 1;

            if (AnimationEndMarker > playbackBuffer.Length)
                AnimationEndMarker = playbackBuffer.Length-1;

            int timeindex = 0;
            int i = 0;
            for (i = 0; i <= AnimationEndMarker; i++)
            {

                //if (playbackBuffer[i] != prevByte || delayCount == 255)
                if (playbackBuffer[i] != prevByte)
                {
                    slot.cmd1[cmdCount] = playbackBuffer[i];
                    timeindex = (short)i;
                    if (!HighPrecision)
                    {
                        timeindex = (short)(timeindex / 10);
                    }
                    slot.timing[cmdCount] = (ushort)timeindex;
                    if (HighPrecision)
                    {
                        slot.cmd2[cmdCount] = Convert.ToByte(timeindex << 8 >> 24);
                    }

                    //delayCount = 1;
                    cmdCount += 1;

                    if (cmdCount > slot.cmd1.Length - 1)
                    {
                        // We went past the buffer.  Gotta truncate (clip) the rest of the animation.  Sorry.
                        cmdCount = (short)(slot.cmd1.Length - 1);
                        break;
                    }
                }
                prevByte = playbackBuffer[i];
            }
            slot.cmd1[cmdCount] = playbackBuffer[AnimationEndMarker]; // 06/27/2012 was AnimationEndMarker - 1.  Removed "- 1".
            //slot.timing[cmdCount] = (short)(i / 10);
            //slot.timing[cmdCount] = (short)(i);
            timeindex = (short)i;
            if (!HighPrecision)
            {
                timeindex = (short)(timeindex / 10);
            }
            slot.timing[cmdCount] = (ushort)timeindex;
            if (HighPrecision)
            {
                slot.cmd2[cmdCount] = Convert.ToByte(timeindex << 8 >> 24);
            }



            cmdCount += 1;
* */
            //slot.AnimationCommandLength = cmdCount;
            //slot.AnimationEnd = AnimationEndMarker;
            //Console.WriteLine("Command length: {0}", cmdCount);

            // Show what we saved
            //Console.WriteLine("Output from SaveAnimationToSlot:");
            //for (int i = 0; i < slot.AnimationCommandLength; i++)
            //{
            //    Console.WriteLine("{0} : {1:X2}{2:X2}", i, slot.commands[i], slot.delays[i]);
            //}
            //Console.WriteLine("Finished.");
             

        }


        private void SetSequence(int id)
        {
            selectedSequence = id;

            if (id == 0)
            {
                btnSel0.BackColor = Color.Red;
                btnSel0.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSel0.FlatStyle = FlatStyle.System;
                btnSel0.BackColor = Control.DefaultBackColor;
            }

            if (id == 1)
            {
                btnSel1.BackColor = Color.Red;
                btnSel1.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSel1.FlatStyle = FlatStyle.System;
                btnSel1.BackColor = Control.DefaultBackColor;
            }

            if (id == 2)
            {
                btnSel2.BackColor = Color.Red;
                btnSel2.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSel2.FlatStyle = FlatStyle.System;
                btnSel2.BackColor = Control.DefaultBackColor;
            }

            if (id == 3)
            {
                btnSel3.BackColor = Color.Red;
                btnSel3.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSel3.FlatStyle = FlatStyle.System;
                btnSel3.BackColor = Control.DefaultBackColor;
            }

            if (id == 4)
            {
                btnSel4.BackColor = Color.Red;
                btnSel4.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSel4.FlatStyle = FlatStyle.System;
                btnSel4.BackColor = Control.DefaultBackColor;
            }

            if (id == 5)
            {
                btnSel5.BackColor = Color.Red;
                btnSel5.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSel5.FlatStyle = FlatStyle.System;
                btnSel5.BackColor = Control.DefaultBackColor;
            }

            if (id == 6)
            {
                btnSel6.BackColor = Color.Red;
                btnSel6.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSel6.FlatStyle = FlatStyle.System;
                btnSel6.BackColor = Control.DefaultBackColor;
            }

            if (id == 7)
            {
                btnSel7.BackColor = Color.Red;
                btnSel7.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSel7.FlatStyle = FlatStyle.System;
                btnSel7.BackColor = Control.DefaultBackColor;
            }
            if (id == 8)
            {
                btnSel8.BackColor = Color.Red;
                btnSel8.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSel8.FlatStyle = FlatStyle.System;
                btnSel8.BackColor = Control.DefaultBackColor;
            }

            if (id == 9)
            {
                btnSel9.BackColor = Color.Red;
                btnSel9.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSel9.FlatStyle = FlatStyle.System;
                btnSel9.BackColor = Control.DefaultBackColor;
            }

            if (id == 10)
            {
                btnSelA.BackColor = Color.Red;
                btnSelA.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSelA.FlatStyle = FlatStyle.System;
                btnSelA.BackColor = Control.DefaultBackColor;
            }

            if (id == 11)
            {
                btnSelB.BackColor = Color.Red;
                btnSelB.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSelB.FlatStyle = FlatStyle.System;
                btnSelB.BackColor = Control.DefaultBackColor;
            }

            if (id == 12)
            {
                btnSelC.BackColor = Color.Red;
                btnSelC.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSelC.FlatStyle = FlatStyle.System;
                btnSelC.BackColor = Control.DefaultBackColor;
            }

            if (id == 13)
            {
                btnSelD.BackColor = Color.Red;
                btnSelD.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSelD.FlatStyle = FlatStyle.System;
                btnSelD.BackColor = Control.DefaultBackColor;
            }

            if (id == 14)
            {
                btnSelE.BackColor = Color.Red;
                btnSelE.FlatStyle = FlatStyle.Standard;
            }
            else
            {
                btnSelE.FlatStyle = FlatStyle.System;
                btnSelE.BackColor = Control.DefaultBackColor;
            }

            if (id < 15)
                DisplayAnimationSlot(ourMonsterFile.slots[id]);
        }

        private void SelectSequence(int id)
        {
            SaveAnimationToSlot(ourMonsterFile.slots[selectedSequence]);


            //if (System.IO.File.Exists(ourMonsterFile.slots[id].MP3File))
            //{
            //    enhancedWaveViewer1.Visible = true;
            //    enhancedWaveViewer1.WaveStream = new NAudio.Wave.Mp3FileReader(ourMonsterFile.slots[id].MP3File);
            //    enhancedWaveViewer1.SamplesPerPixel = enhancedWaveViewer1.WaveStream.WaveFormat.SampleRate / 20;

            //    tracktop = tracktop_waveform;
            //}
            //else
            //{
                enhancedWaveViewer1.Visible = false;
                tracktop = tracktop_nowaveform;
            //}

            DisplayAnimationSlot(ourMonsterFile.slots[id]);
            SetSequence(id);

            if (isConnected())
            {
                string data = "@S" + GetSequenceChar(id);
                serialPort1.WriteLine(data);
            }

            if (!string.IsNullOrEmpty(ourMonsterFile.slots[id].MP3File))
            {
                //axWindowsMediaPlayer1.URL = slots[id].MP3File;
                if (System.IO.File.Exists(ourMonsterFile.slots[id].MP3File))
                {
                    axWindowsMediaPlayer1.URL = ourMonsterFile.slots[id].MP3File;
                    axWindowsMediaPlayer1.Ctlcontrols.currentPosition = 0.0f;
                    //axWindowsMediaPlayer1.Ctlcontrols.play();
                    axWindowsMediaPlayer1.Ctlcontrols.stop();

                }
            }
        }

        private void btnSel0_Click(object sender, EventArgs e)
        {
            SelectSequence(0);
        }

        private void btnSel1_Click(object sender, EventArgs e)
        {
            SelectSequence(1);
        }

        private void btnSel2_Click(object sender, EventArgs e)
        {
            SelectSequence(2);
        }

        private void btnSel3_Click(object sender, EventArgs e)
        {
            SelectSequence(3);
        }

        private void btnSel4_Click(object sender, EventArgs e)
        {
            SelectSequence(4);
        }

        private void btnSel5_Click(object sender, EventArgs e)
        {
            SelectSequence(5);
        }

        private void btnSel6_Click(object sender, EventArgs e)
        {
            SelectSequence(6);
        }

        private void btnSel7_Click(object sender, EventArgs e)
        {
            SelectSequence(7);
        }

        private void btnSel8_Click(object sender, EventArgs e)
        {
            SelectSequence(8);
        }

        private void btnSel9_Click(object sender, EventArgs e)
        {
            SelectSequence(9);
        }

        private void btnSelA_Click(object sender, EventArgs e)
        {
            SelectSequence(10);
        }

        private void btnSelB_Click(object sender, EventArgs e)
        {
            SelectSequence(11);
        }

        private void btnSelC_Click(object sender, EventArgs e)
        {
            SelectSequence(12);
        }

        private void btnSelD_Click(object sender, EventArgs e)
        {
            SelectSequence(13);
        }

        private void btnSelE_Click(object sender, EventArgs e)
        {
            SelectSequence(14);
        }



        private char GetSequenceChar(int id)
        {
            char c = 'a';
            if (id < 10)
            {
                c = (char)(id+0x30);
            }
            else
            {
                switch (id)
                {
                    case 10:
                        c = 'a';
                        break;

                    case 11:
                        c = 'b';
                        break;

                    case 12:
                        c = 'c';
                        break;

                    case 13:
                        c = 'd';
                        break;

                    case 14:
                        c = 'e';
                        break;
                }
            }

            return c;

        }

        private void btnTrigger_Click(object sender, EventArgs e)
        {
            //double offset = double.Parse(txtOffset.Text);
            double offset = audioOffset;
            Stopwatch offsetwatch = new Stopwatch();
            lastSecond = 0;
            PulseToggle = true;
            offsetwatch.Reset();
            offsetwatch.Start();
            if (isConnected())
            {
                string data = "@T" + GetSequenceChar(selectedSequence);
                serialPort1.WriteLine(data);

                PlayOnline = true;
            }
            else
            {
                PlayOnline = false;
            }
            hScrollBar1.Value = 0;
            LeftAnimPos = 0;
            timer1.Interval = refreshinterval;
            timer1.Start();
            watch.Reset();
            watch.Start();
            currentPlaybackMode = PlaybackMode.PlayMode;
            SetPlaybackModeUI(true);
            axWindowsMediaPlayer1.uiMode = "invisible";
            if (System.IO.File.Exists(ourMonsterFile.slots[selectedSequence].MP3File))
            {
                axWindowsMediaPlayer1.URL = ourMonsterFile.slots[selectedSequence].MP3File;
                axWindowsMediaPlayer1.Ctlcontrols.currentPosition = offsetwatch.Elapsed.TotalSeconds - offset;
                axWindowsMediaPlayer1.Ctlcontrols.play();
                
            }
            offsetwatch.Stop();

        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        

        private void UpdateRelays(int relay)
        {
            byte accum1 = 0x00;
            byte accum2 = 0x00;
            for (int i = 0; i < 8; i++)
            {
                if (relayStates[i])
                    accum1 = (byte)(accum1 | (0x01 << i));
            }
            for (int i = 8; i < 16; i++)
            {
                if (relayStates[i])
                    accum2 = (byte)(accum2 | (0x01 << i-8));
            }

            /*
            bool state = relayStates[relay];


            int i = relay;
            if (state == false)
            {
                i += 4;
            }
             * 
             * */

            if (isConnected())
            {
                string data = string.Format("@R{0:X2}{1:X2}",0xFF - accum1, 0xFF - accum2);
                serialPort1.WriteLine(data);
            }

        }






        private void SendRelay(int relay, bool value)
        {
            relayStates[relay] = value;
            UpdateRelays(relay);
            pictureBox1.Refresh();
        }


        private void SetRelayState(int relay, bool value)
        {
            relayStates[relay] = value;
            
        }


        private void SetRelayState(object sender, bool value)
        {
            Button relay = sender as Button;
            int i = int.Parse(relay.Tag.ToString());
            relayStates[i] = value;
            if (value)
            {
                relay.FlatStyle = FlatStyle.Flat;
                relay.BackColor = Color.Red;
            }
            else
            {
                relay.FlatStyle = FlatStyle.Standard;
                relay.BackColor = Control.DefaultBackColor;
            }
        }

        private void btnRelay0_MouseDown(object sender, MouseEventArgs e)
        {
            //SendRelay(sender, true);
        }

        private void btnRelay0_MouseUp(object sender, MouseEventArgs e)
        {
            //SendRelay(sender, false);
        }

        private void btnRelay1_MouseDown(object sender, MouseEventArgs e)
        {
            //SendRelay(sender, true);
        }

        private void btnRelay1_MouseUp(object sender, MouseEventArgs e)
        {
            //SendRelay(sender, false);
        }

        private void btnRelay2_MouseDown(object sender, MouseEventArgs e)
        {
            //SendRelay(sender, true);
        }

        private void btnRelay2_MouseUp(object sender, MouseEventArgs e)
        {
            //SendRelay(sender, false);
        }

        private void btnRelay3_MouseDown(object sender, MouseEventArgs e)
        {
            //SendRelay(sender, true);
        }

        private void btnRelay3_MouseUp(object sender, MouseEventArgs e)
        {
            //SendRelay(sender, false);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.D1:
                    if (relayStates[0] == false) SendRelay(0, true);
                    break;

                case Keys.D2:
                    if (relayStates[1] == false) SendRelay(1, true);
                    break;

                case Keys.D3:
                    if (relayStates[2] == false) SendRelay(2, true);
                    break;

                case Keys.D4:
                    if (relayStates[3] == false) SendRelay(3, true);
                    break;

                case Keys.D5:
                    if (relayStates[4] == false) SendRelay(4, true);
                    break;

                case Keys.D6:
                    if (relayStates[5] == false) SendRelay(5, true);
                    break;

                case Keys.D7:
                    if (relayStates[6] == false) SendRelay(6, true);
                    break;

                case Keys.D8:
                    if (relayStates[7] == false) SendRelay(7, true);
                    break;

                case Keys.D9:
                    if (relayStates[8] == false) SendRelay(8, true);
                    break;

                case Keys.D0:
                    if (relayStates[9] == false) SendRelay(9, true);
                    break;

                case Keys.Q:
                    if (relayStates[10] == false) SendRelay(10, true);
                    break;

                case Keys.W:
                    if (relayStates[11] == false) SendRelay(11, true);
                    break;

                case Keys.E:
                    if (relayStates[12] == false) SendRelay(12, true);
                    break;

                case Keys.R:
                    if (relayStates[13] == false) SendRelay(13, true);
                    break;

                case Keys.T:
                    if (relayStates[14] == false) SendRelay(14, true);
                    break;

                case Keys.Y:
                    if (relayStates[15] == false) SendRelay(15, true);
                    break;

                case Keys.ShiftKey:
                    KeyStateShift = true;
                    break;
            }
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.D1:
                    if (relayStates[0] == true) SendRelay(0, false);
                    break;

                case Keys.D2:
                    if (relayStates[1] == true) SendRelay(1, false);
                    break;

                case Keys.D3:
                    if (relayStates[2] == true) SendRelay(2, false);
                    break;

                case Keys.D4:
                    if (relayStates[3] == true) SendRelay(3, false);
                    break;

                case Keys.D5:
                    if (relayStates[4] == true) SendRelay(4, false);
                    break;

                case Keys.D6:
                    if (relayStates[5] == true) SendRelay(5, false);
                    break;

                case Keys.D7:
                    if (relayStates[6] == true) SendRelay(6, false);
                    break;

                case Keys.D8:
                    if (relayStates[7] == true) SendRelay(7, false);
                    break;

                case Keys.D9:
                    if (relayStates[8] == true) SendRelay(8, false);
                    break;

                case Keys.D0:
                    if (relayStates[9] == true) SendRelay(9, false);
                    break;

                case Keys.Q:
                    if (relayStates[10] == true) SendRelay(10, false);
                    break;

                case Keys.W:
                    if (relayStates[11] == true) SendRelay(11, false);
                    break;

                case Keys.E:
                    if (relayStates[12] == true) SendRelay(12, false);
                    break;

                case Keys.R:
                    if (relayStates[13] == true) SendRelay(13, false);
                    break;

                case Keys.T:
                    if (relayStates[14] == true) SendRelay(14, false);
                    break;

                case Keys.Y:
                    if (relayStates[15] == true) SendRelay(15, false);
                    break;

                case Keys.ShiftKey:
                    KeyStateShift = false;
                    break;
            }
        }

        private void btnGetFlags_Click(object sender, EventArgs e)
        {
            if (isConnected())
            {
                string data = "@O0";
                serialPort1.WriteLine(data);
            }
        }

        private void SetSlotEnable(object sender)
        {
            string data = "";
            CheckBox chk = sender as CheckBox;

            int s = 0;
            int.TryParse(chk.Tag.ToString(), System.Globalization.NumberStyles.AllowHexSpecifier, null ,out s);


            if (chk.Checked)
            {
                data = "@+";
            }
            else
            {
                data = "@-";
            }

            if (isConnected())
            {
                data = data + chk.Tag.ToString();
                serialPort1.WriteLine(data);
            }
        }

        private void chk0_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chk1_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chk2_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chk3_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chk4_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chk5_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chk6_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chk7_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chk8_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chk9_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chkA_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chkB_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chkC_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chkD_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void chkE_Click(object sender, EventArgs e)
        {
            SetSlotEnable(sender);
        }

        private void statusStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }



        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            PictureBox pb = sender as PictureBox;

            string textValue;

            int x1, y1, x2, y2, w, h, sx1, sx2 = 0;

            int skipfactor = zoom;  // skipfactor indicates how many samples to skip over when zoomed in.
                                    // We don't really skip over, we just average those samples together.
                                    // ...Sort of like a psuedo FFT.

            Graphics g = e.Graphics;
            SolidBrush brushBlack = new SolidBrush(Color.Black);
            SolidBrush brushScopeBackground = new SolidBrush(Color.Black);
            SolidBrush brushGray = new SolidBrush(Color.LightGray);
            SolidBrush brushSelectionBackground = new SolidBrush(Color.FromArgb(75,75,100));
            SolidBrush brushScaleText = new SolidBrush(Color.White);
            SolidBrush brushEndMarker = new SolidBrush(Color.Red);
            SolidBrush brushRelay = new SolidBrush(Color.LightGreen);
            
            Pen penBlack = new Pen(Color.Black);
            Pen penSample = new Pen(Color.LightGreen);
            Pen penEndMarker = new Pen(Color.Red);
            Pen penPosition = new Pen(Color.Aquamarine);
            penPosition.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
            penPosition.DashPattern = new float[] { 3.0f, 3.0f, 3.0f, 3.0f };

            w = pb.Width;
            h = pb.Height;



            int renderRows = (pb.Height - tracktop) / (trackheight + trackspacing);
            int maxRenderRow = toptrackview + renderRows;
            if (maxRenderRow > 16) maxRenderRow = 16;

            // Render grid lines
            /////////////////////////////////////////////////
           
            // Calculate how many samples we can display on the pictureBox, 
            // taking into account the space taken up by the relay buttons.
            // We intend to have 1 grid-line per second.
            int AnimWidth = w - trackleft;

            // find out how many grid lines we can fit in current window
            int gridcount = (AnimWidth / MAX_COMMANDS_PER_SECOND) * skipfactor;

 
            x1 = trackleft;
            int gridY1 = tracktop - 10;
            int gridY2 = tracktop;

            // Render track backgrounds
            /////////////////////////////////////////////////////
            x1 = trackleft;
            x2 = w - trackleft - 10;


            // Calculate user selection range within the tracks
            if (selectLeft <= selectRight)
            {
                sx1 = trackleft + (int)(selectLeft - LeftAnimPos) * skipfactor;
                sx2 = (int)(selectRight - selectLeft) * skipfactor;
            }
            else if (selectRight < selectLeft)
            {
                sx1 = trackleft + (int)(selectRight - LeftAnimPos) * skipfactor;
                sx2 = (int)(selectLeft - selectRight) * skipfactor;
            }
            else
            {
                sx1 = trackleft;
                sx2 = 1;
            }
            // If we don't have more anything selected, at least show something.
            if (sx2 < 2) sx2 = 2;

            // Render relay indicators
            for (int i = toptrackview; i < maxRenderRow; i++)
            {
                y1 = tracktop + ((trackheight + trackspacing) * (i - toptrackview));
                
                // Render normal background
                g.FillRectangle(brushScopeBackground, x1, y1, x2, trackheight);

                // Render selection range color
                g.FillRectangle(brushSelectionBackground, sx1, y1, sx2, trackheight);

                // Render relay box
                if (relayStates[i] == true)
                {
                    g.FillRectangle(Brushes.Red, 2, y1, trackleft - 4, trackheight);
                }
                else
                {
                    g.FillRectangle(Brushes.LightGray, 2, y1, trackleft - 4, trackheight);
                }
                string mylabel = ourMonsterFile.outputName[i];
                if (mylabel.Length > 12) mylabel = mylabel.Substring(0, 12);
                g.DrawString(String.Format("{0}:{1}", (i + 1).ToString(), mylabel), relayFont, Brushes.Black, new PointF(4.0f, y1));
            }

          

            // Render track data as a line graph
            //////////////////////////////////////////////
          
            // Each pixel represents 25 ms or 1/40 of a second.
            x1 = trackleft;

            int numSegments = AnimWidth / skipfactor;

            // Draw the sequence end marker
            //////////////////////////////////////
            x2 = x1;
            y1 = (tracktop + ((trackheight + trackspacing) * (CursorRelay)));
            for (int i = (int)LeftAnimPos; i < (LeftAnimPos + numSegments); i++)
            {
                if (i == AnimationEndMarker)
                {
                    // Draw the end marker box before the text so that it appears behind.
                    g.FillRectangle(brushEndMarker, x2 - 6, tracktop - 30, 12, 20);
                    g.DrawLine(penEndMarker, x2, tracktop - 10, x2, ((tracktop + trackheight)*16) + 5);
                }

                if (i == CursorPos)
                {
                    g.DrawLine(penPosition, x2, y1, x2, y1 + trackheight);
                }
                x2 += skipfactor;
            }

            if (RenderPulses && PulseToggle)
            {
                g.FillRectangle(brushEndMarker, 65, tracktop - 25, 24, 20);
            }


            x1 = trackleft;
            int mymod = 40;
            int gridmod = 20;
            if (zoom > 2)
            {
                mymod = 20;
                gridmod = 10;
            }
            if (zoom > 4) gridmod = 5;
          
            for (int i = (int)LeftAnimPos; i < (LeftAnimPos + numSegments); i++)
            {
                x2 = x1 + skipfactor;

                //Show grid line every 1 second
                if ((i % gridmod) == 0)
                {
                    g.DrawLine(penBlack, x1, gridY1, x1, gridY2);
                }

                if ((i % mymod) == 0)
                {
                    //long seconds = (LeftAnimPos + i) / 20;
                    long seconds = i / 20;
                    TimeSpan userTime = TimeSpan.FromSeconds(seconds);
                    textValue = String.Format("{0}:{1:00}", userTime.Minutes, userTime.Seconds);

                    g.DrawString(textValue, rulerFont, brushScaleText, new PointF(x1 - 10.0f, tracktop-24.0F));

                }
                int skip = skipfactor - 1;
                if (skip < 1) skip = 1;
                byte cmd1 = 0xFF;
                byte cmd2 = 0xFF;
                byte cmd = 0xFF;
                if (i < currentSlot.cmd1.Length)
                {
                     cmd1 = currentSlot.cmd1[i];
                     cmd2 = currentSlot.cmd2[i];
                     cmd = cmd1;
                }

                    for (int r = toptrackview; r < maxRenderRow; r++)
                    {
                        int shiftr = r;
                        if (r > 7)
                        {
                            cmd = cmd2;
                            shiftr = r-8;
                        }

                        y1 = tracktop + ((trackheight + trackspacing) * (r-toptrackview)) + 5;

                        if (i > ourMonsterFile.TotalEventsPerSlot)
                        {
                            // End of slot!
                            g.FillRectangle(brushEndMarker, x1, y1, skipfactor, trackheight - 10);
                        }
                        else
                        {

                            if ((cmd & (0x01 << shiftr)) == (0x01 << shiftr))
                            {
                                // Relay should be OFF
                            }
                            else
                            {
                                // Relay should be ON
                                g.FillRectangle(brushRelay, x1, y1, skip, trackheight - 10);
                                //g.FillRectangle(brushRelay, x1, y1, 5, trackheight - 10);
                            }
                        }
                    }
               
                x1 += skipfactor;
                
            } // for loop for rendering samples.
        }



        private void MainForm_Resize(object sender, EventArgs e)
        {

            pictureBox1.Refresh();
            vScrollBar1.Left = pictureBox1.Width - vScrollBar1.Width;
            vScrollBar1.Top = 0;
            vScrollBar1.Height = hScrollBar1.Top;
            //enhancedWaveViewer1.Width = pictureBox1.Width - trackleft - vScrollBar1.Width;
        }

        private void hScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            //int AnimWidth = pictureBox1.Width - (btnRelay0.Left + btnRelay0.Width + 5);

            LeftAnimPos = hScrollBar1.Value;
            //LeftAnimPos = hScrollBar1.Value * zoom;
            //enhancedWaveViewer1.StartPosition = hScrollBar1.Value * zoom * enhancedWaveViewer1.SamplesPerPixel;
            //enhancedWaveViewer1.StartPosition = hScrollBar1.Value * (256);
            /*

            //LeftAnimPos = hScrollBar1.Value;
            LeftAnimPos = hScrollBar1.Value*(zoom);
            //Console.WriteLine(LeftAnimPos.ToString());
            if (LeftAnimPos > (playbackBuffer.Length - (AnimWidth * zoom)))
            {
                //LeftAnimPos = playbackBuffer.Length;
                LeftAnimPos = playbackBuffer.Length - (AnimWidth * zoom);
            }
            */


            pictureBox1.Refresh();
            //enhancedWaveViewer1.Refresh();
            //TimeSpan span = new TimeSpan(0, 0, 0, 0, (int)LeftAnimPos * 10);
            //toolStripStatusPosition.Text = string.Format("{0:00}:{1:00}.{2:0##}",span.Minutes, span.Seconds, span.Milliseconds);
        }

        private void ProcessNewZoomFactor()
        {
            int AnimWidth = pictureBox1.Width - trackleft;

            //hScrollBar1.Maximum = ((playbackBuffer.Length - (AnimWidth*zoom)) / (zoom));
            //hScrollBar1.Maximum = currentSlot.cmd1.Length / zoom;
            //hScrollBar1.Maximum = ((currentSlot.cmd1.Length - (AnimWidth * zoom)) / zoom);


            // New code 9/8/2013
            int numSegments = AnimWidth / zoom;
            //hScrollBar1.Maximum = 14 * 60 * 20;
            hScrollBar1.Maximum = ourMonsterFile.TotalEventsPerSlot;
            hScrollBar1.SmallChange = (9-zoom);
            hScrollBar1.LargeChange = (9-zoom) * 2;

            //hScrollBar1.Maximum = currentSlot.cmd1.Length / numSegments;
            /*
            switch (zoom)
            {
                case 1:
                    hScrollBar1.Minimum = 0;
                    hScrollBar1.SmallChange = 40;
                    hScrollBar1.LargeChange = 20;
                    hScrollBar1.Maximum = currentSlot.cmd1.Length / numSegments;
                    break;

                case 2:
                    hScrollBar1.Minimum = 0;
                    hScrollBar1.SmallChange = 20;
                    hScrollBar1.LargeChange = 10;
                    hScrollBar1.Maximum = 14 * 60 * 20;
                    break;

                case 3:
                    hScrollBar1.Minimum = 0;
                    hScrollBar1.SmallChange = 5;
                    hScrollBar1.LargeChange = 10;
                    hScrollBar1.Maximum = 14 * 60 * 20;
                    break;

                case 4:
                    break;

                case 5:
                    break;
                    
                case 6:
                    break;

                case 8:
                    hScrollBar1.Minimum = 0;
                    hScrollBar1.SmallChange = 1;
                    hScrollBar1.LargeChange = 5;
                    hScrollBar1.Maximum = 14 * 60 * 20;
                    break;

                default:
                    break;
            }

            */

                    //hScrollBar1.Minimum = 0;
                    //hScrollBar1.SmallChange = numSegments / 40;
                    //hScrollBar1.LargeChange = numSegments / 20;
                    //hScrollBar1.Maximum = 14 * 60 * 200;


            //hScrollBar1.Maximum = currentSlot.cmd1.Length - (AnimWidth/zoom);
            //step 1
            //hScrollBar1.Maximum = currentSlot.cmd1.Length / numSegments;
            //hScrollBar1.Maximum = currentSlot.cmd1.Length - (numSegments*zoom);
            //hScrollBar1.Maximum = (currentSlot.cmd1.Length / numSegments);
            //hScrollBar1.Maximum = numSegments;
            //hScrollBar1.Maximum = currentSlot.cmd1.Length;


            //step 2
            //hScrollBar1.Maximum += hScrollBar1.Width;
            //hScrollBar1.Maximum += numSegments/zoom;

            //step 3
            //hScrollBar1.Maximum += hScrollBar1.LargeChange;



            LeftAnimPos = hScrollBar1.Value;

            /*

            //hScrollBar1.Maximum = 11 * 60 * 100;
            hScrollBar1.SmallChange = zoom * 2;
            hScrollBar1.LargeChange = zoom * 4;

            int newvalue = (int)LeftAnimPos;
            if (newvalue > hScrollBar1.Maximum) newvalue = hScrollBar1.Maximum;
            hScrollBar1.Value = newvalue;


            LeftAnimPos = hScrollBar1.Value * (zoom);
            if (LeftAnimPos > (playbackBuffer.Length - (AnimWidth * zoom)))
            {
                LeftAnimPos = playbackBuffer.Length - (AnimWidth * zoom);
            }
            */

            toolStripStatusZoom.Text = string.Format("Zoom: {0}%", zoom * 100);

            pictureBox1.Refresh();
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            DownloadAll = false;
            CurrentDownloadSlot = selectedSequence;
            if (isConnected())
            {
                string data = "@D" + GetSequenceChar(CurrentDownloadSlot);
                serialPort1.WriteLine(data);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            LeftAnimPos = (long)(double)(watch.ElapsedMilliseconds) / 50;
            //enhancedWaveViewer1.StartPosition = (enhancedWaveViewer1.WaveStream.WaveFormat.SampleRate / 20) * (watch.ElapsedMilliseconds / 50);
            
            
            TimeSpan span = new TimeSpan(0,0,0,0,(int)watch.ElapsedMilliseconds);
            toolStripStatusPosition.Text = string.Format("Pos: {0:00}:{1:00}.{2:0##}", span.Minutes, span.Seconds, span.Milliseconds);
            if (lastSecond != span.Seconds)
            {
                lastSecond = span.Seconds;
                PulseToggle = !PulseToggle;
            }
            if (!PlayOnline)
            {
                for (int i = 0; i < 8; i++)
                {
                    if ((currentSlot.cmd1[LeftAnimPos] & (0x01 << i)) == (0x01 << i))
                    {
                        relayStates[i] = false;
                    }
                    else
                    {
                        relayStates[i] = true;
                    }
                }
                for (int i = 0; i < 8; i++)
                {
                    if ((currentSlot.cmd2[LeftAnimPos] & (0x01 << i)) == (0x01 << i))
                    {
                        relayStates[i+8] = false;
                    }
                    else
                    {
                        relayStates[i+8] = true;
                    }
                }

                /*


                // Check if we need to do anything with the relays
                if ((playbackBuffer[LeftAnimPos] & 0x01) == 0x01)
                {
                    SetRelayState(btnRelay0, false);
                }
                else
                {
                    SetRelayState(btnRelay0, true);
                }

                if ((playbackBuffer[LeftAnimPos] & 0x02) == 0x02)
                {
                    SetRelayState(btnRelay1, false);
                }
                else
                {
                    SetRelayState(btnRelay1, true);
                }

                if ((playbackBuffer[LeftAnimPos] & 0x04) == 0x04)
                {
                    SetRelayState(btnRelay2, false);
                }
                else
                {
                    SetRelayState(btnRelay2, true);
                }

                if ((playbackBuffer[LeftAnimPos] & 0x08) == 0x08)
                {
                    SetRelayState(btnRelay3, false);
                }
                else
                {
                    SetRelayState(btnRelay3, true);
                }
                */

                // If we are in offline mode, we need to stop ourselves since the MonsterShield won't be there
                // to tell us when to stop.
                if (LeftAnimPos >= AnimationEndMarker)
                {
                    // STOP!!!
                    StopPlayback();
                    SetPlaybackModeUI(false);
                    currentPlaybackMode = PlaybackMode.EditMode;
                }
            }


            pictureBox1.Refresh();
            //enhancedWaveViewer1.Refresh();
        }

        private void HandleMouseDrawing(MouseEventArgs e)
        {
            int xstart = trackleft;
            //int ScopePixelWidth = pictureBox1.Width - xstart;

            bool relayNewState;
            if (e.Button == MouseButtons.Left)
            {
                relayNewState = true;
            }
            else
            {
                relayNewState = false;
            }
           
            // Make sure we were in the track
            if (e.X >= trackleft)
            {
                //long pos1 = (e.X - xstart + LeftAnimPos)*zoom;
                //long pos2 = (prevMouseX - xstart + LeftAnimPos)*zoom;
                //long pos1 = (zoom * (e.X - xstart)) + LeftAnimPos;
                long pos1 = LeftAnimPos + ((e.X - xstart) / zoom);
                //long pos2 = (zoom * (prevMouseX - xstart)) + LeftAnimPos;
                long pos2 = LeftAnimPos + ((prevMouseX - xstart) / zoom);
                //pos1 = pos1 + (AnimWidth/zoom);
                //pos2 = pos2 + (AnimWidth/zoom);

                CursorPos = pos1;

                // Scale by zoom factor
                //pos1 = pos1 + (AnimWidth - LeftAnimPos)/zoom;
                //pos2 = pos2 + (AnimWidth - LeftAnimPos)/zoom;

                int relay = -1;
                int renderRows = (pictureBox1.Height - tracktop) / (trackheight + trackspacing);
                int maxRow = toptrackview + renderRows;
                if (maxRow > 16) maxRow = 16;

                // find out which track we are drawing in
                for (int i = toptrackview; i < maxRow; i++)
                {
                    if (e.Y >= (tracktop + ((trackheight + trackspacing) * (i-toptrackview))) && e.Y < (tracktop + ((trackheight + trackspacing) * (i-toptrackview))) + trackheight)
                    {
                        relay = i;
                        if (relay > 15) relay = 15;
                    }
                }


                // Are we clicking on the scale to change the Animation End Marker?
                //if (e.Y >= btnRelay0.Top - 40 && e.Y <= btnRelay0.Top-10)
                if (currentMouseMode == MouseModes.MouseModeAdjustEnd)
                {
                    //slots[selectedSequence].AnimationEnd = pos1;
                    AnimationEndMarker = pos1;
                    if (AnimationEndMarker >= playbackBuffer.Length) AnimationEndMarker = playbackBuffer.Length;
                    prevMouseX = e.X;
                    SetEndMarkerLabel();
                    pictureBox1.Refresh(); // redraw the screen.
                }
                else
                {

                    if (currentMouseMode == MouseModes.MouseModeDraw )
                    {
                        // If Shift key is being held, draw in ALL relays
                        if (KeyStateShift)
                        {
                            relay = 255;
                        }

                        // bounds checking:
                        if (pos1 >= 0 && pos1 < playbackBuffer.Length)
                        {

                            if (pos1 > pos2)
                            {
                                // Go backward
                                for (long pos = pos1; pos >= pos2; pos--)
                                {
                                    DrawInBuffer(pos, relay, relayNewState);
                                    //Console.WriteLine("B");
                                }


                            }
                            else
                            {
                                // Go forward
                                //for (long pos = pos1 - 1; pos <= pos2; pos++)  // Commented out 8/13/2013  -- why are we subtracting 1 from pos1???
                                for (long pos = pos1; pos <= pos2; pos++)
                                {
                                    DrawInBuffer(pos, relay, relayNewState);
                                    //Console.WriteLine("F");
                                }
                            }


                        }
                        prevMouseX = e.X;
                        pictureBox1.Refresh(); // redraw the screen.
                    }
                }
            }



            
        }

        private void DrawInBuffer(long pos, int relay, bool relayOn)
        {
            if (pos >= 0 && pos < currentSlot.cmd1.Length)
            {
                if (relayOn == true)
                {
                    if (relay == 255)
                    {
                        currentSlot.cmd1[pos] = 0x00;
                        currentSlot.cmd2[pos] = 0x00;
                        //Debug.Assert(false);
                    }
                    else
                    {
                        if (relay < 8)
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] & (0xFF - (0x01 << relay)));
                        else
                            currentSlot.cmd2[pos] = (byte)(currentSlot.cmd2[pos] & (0xFF - (0x01 << (relay-8))));
                    }

                    /*

                    switch (relay)
                    {
                        case 0:
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] & 0x0E);
                            break;

                        case 1:
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] & 0x0D);
                            break;

                        case 2:
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] & 0x0B);
                            break;

                        case 3:
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] & 0x07);
                            break;

                        case 255:
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] & 0x0E);
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] & 0x0D);
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] & 0x0B);
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] & 0x07);
                            break;
                    }
                     * */
                }
                else
                {
                    if (relay == 255)
                    {
                        currentSlot.cmd1[pos] = 0xFF;
                        currentSlot.cmd2[pos] = 0xFF;
                        //Debug.Assert(false);
                    }
                    else
                    {
                        //Debug.Assert(relay < 14);
                        if (relay < 8)
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] | (0x01 << relay));
                        else
                            currentSlot.cmd2[pos] = (byte)(currentSlot.cmd2[pos] | (0x01 << (relay-8)));
                    }


                    /*
                    switch (relay)
                    {
                        case 0:
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] | 0x01);
                            break;

                        case 1:
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] | 0x02);
                            break;

                        case 2:
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] | 0x04);
                            break;

                        case 3:
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] | 0x08);
                            break;

                        case 255:
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] | 0x01);
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] | 0x02);
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] | 0x04);
                            currentSlot.cmd1[pos] = (byte)(currentSlot.cmd1[pos] | 0x08);
                            break;
                    }
                    */
                }
            }
        }


        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (currentPlaybackMode != PlaybackMode.EditMode) return;

            prevMouseX = e.X;
            int xstart = trackleft;
            switch (ToolMode)
            {
                case TOOLMODE_DRAW:
                    if (e.Y >= 0 && e.Y <= tracktop - 10)
                    {
                        currentMouseMode = MouseModes.MouseModeAdjustEnd;
                    }
                    else
                    {
                        currentMouseMode = MouseModes.MouseModeDraw;
                    }
                    HandleMouseDrawing(e);
                    break;

                case TOOLMODE_SELECT:
                    if (e.X >= xstart)
                    {
                        
                        //long pos1 = (zoom * (e.X - xstart)) + LeftAnimPos;
                        //long pos2 = (zoom * (prevMouseX - xstart)) + LeftAnimPos;


                        //selectLeft = (zoom * (e.X - xstart)) + LeftAnimPos;
                        selectLeft = LeftAnimPos + ((e.X - xstart) / zoom); 
                        selectRight = selectLeft;
                        //selectLeft = LeftAnimPos + e.X - (btnRelay0.Left + btnRelay0.Width + 5);
                    }
                    pictureBox1.Refresh();
                    break;
            }
            
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (currentPlaybackMode != PlaybackMode.EditMode) return;

            int xstart = trackleft;

            if (e.X >= trackleft)
            {
                //long pos1 = (e.X - xstart + LeftAnimPos)*zoom;
                //long pos2 = (prevMouseX - xstart + LeftAnimPos)*zoom;
                //long pos1 = CursorPos = (zoom * (e.X - xstart)) + LeftAnimPos;
                long pos1 = CursorPos = LeftAnimPos + ((e.X - xstart) / zoom); 

                TimeSpan span = new TimeSpan(0, 0, 0, 0, (int)pos1*50);
                toolStripStatusPosition.Text = string.Format("Pos: {0:00}:{1:00}.{2:0##}", span.Minutes, span.Seconds, span.Milliseconds);

                CursorRelay = -1;
                for (int i = 0; i < 16; i++)
                {
                    if (e.Y >= (tracktop + ((trackheight + trackspacing) * (i))) && e.Y < (tracktop + ((trackheight + trackspacing) * (i))) + trackheight)
                    {
                        CursorRelay = (short)i;
                        if (KeyStateShift) CursorRelay = 255;
                    }
                }

                pictureBox1.Refresh();
            }

            if (e.Button != MouseButtons.None)
            {
                switch (ToolMode)
                {
                    case TOOLMODE_DRAW:
                        HandleMouseDrawing(e);
                        break;

                    case TOOLMODE_SELECT:
                        if (e.X >= xstart)
                        {
                            //long newvalue = LeftAnimPos + e.X - (btnRelay0.Left + btnRelay0.Width + 5);
                            //long newvalue = (zoom * (e.X - xstart)) + LeftAnimPos;
                            long newvalue = LeftAnimPos + ((e.X - xstart) / zoom); 
                            selectRight = newvalue;
                            //if (newvalue < selectLeft)
                            //{
                            //    //selectRight = selectLeft;
                            //    selectLeft = newvalue;
                            //}
                            //else
                            //{
                            //    selectRight = newvalue;
                            //}
                            //selectRight = LeftAnimPos + e.X - (btnRelay0.Left + btnRelay0.Width + 5);
                        }
                        pictureBox1.Refresh();
                        break;
                }
            }
        }

        private void btnZoomIn_Click(object sender, EventArgs e)
        {
            zoom += 1;
            if (zoom > ZOOM_MAX) zoom = ZOOM_MAX;
            ProcessNewZoomFactor();
            pictureBox1.Refresh();
        }

        private void btnZoomOut_Click(object sender, EventArgs e)
        {
            zoom -= 1;
            if (zoom < 1) zoom = 1;
            ProcessNewZoomFactor();
            pictureBox1.Refresh();
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            btnEdit.BackColor = Color.CornflowerBlue;
            btnSel.BackColor = Control.DefaultBackColor;
            ToolMode = TOOLMODE_DRAW;
            pictureBox1.Cursor = Cursors.Cross;
        }

        private void btnSel_Click(object sender, EventArgs e)
        {
            btnEdit.BackColor = Control.DefaultBackColor;
            btnSel.BackColor = Color.CornflowerBlue;
            ToolMode = TOOLMODE_SELECT;
            pictureBox1.Cursor = Cursors.IBeam;
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (currentPlaybackMode != PlaybackMode.EditMode) return;

            int xstart = trackleft;

            if (e.X < trackleft)
            {
                if (e.Button == MouseButtons.Right)
                {
                    int relay = -1;
                    int renderRows = (pictureBox1.Height - tracktop) / (trackheight + trackspacing);
                    int maxRow = toptrackview + renderRows;
                    if (maxRow > 16) maxRow = 16;

                    // find out which track we are drawing in
                    for (int i = toptrackview; i < maxRow; i++)
                    {
                        if (e.Y >= (tracktop + ((trackheight + trackspacing) * (i - toptrackview))) && e.Y < (tracktop + ((trackheight + trackspacing) * (i - toptrackview))) + trackheight)
                        {
                            relay = i;
                            if (relay > 15) relay = 15;
                        }
                    }

                    ShowInputDialog(ref ourMonsterFile.outputName[relay]);
                }
            }


            switch (ToolMode)
            {
                case TOOLMODE_DRAW:
                    //HandleMouseDrawing(e);
                    currentMouseMode = MouseModes.MouseModeNone;
                    break;

                case TOOLMODE_SELECT:
                    if (e.X >= xstart)
                    {
                        //selectRight = LeftAnimPos + e.X - (btnRelay0.Left + btnRelay0.Width + 5);
                        //long newvalue = (zoom * (e.X - xstart)) + LeftAnimPos;
                        long newvalue = LeftAnimPos + ((e.X - xstart) / zoom); 
                        selectRight = newvalue;

                        //// If we didn't move the mouse at all, let's make it so at least 1 pixel is shown.
                        //if (selectRight == selectLeft)
                        //{
                        //    selectRight += 1;
                        //}

                        //if (newvalue <= selectLeft)
                        //{
                        //    //selectRight = selectLeft;
                        //    selectLeft = newvalue;
                        //}
                        //else
                        //{
                        //    selectRight = newvalue;
                        //}

                        if (selectLeft > selectRight)
                        {
                            newvalue = selectLeft;
                            selectLeft = selectRight;
                            selectRight = newvalue;
                        }

                        pictureBox1.Refresh();
                    }
                    break;
            }
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            // Rebuild command table
            SaveAnimationToSlot(ourMonsterFile.slots[selectedSequence]);

            SendAnimation(selectedSequence);

            /*
    
            if (isConnected())
            {
                frmProgress = new ProgressForm();
                frmProgress.ProgressValue = 0;
                frmProgress.ProgressMax = ourMonsterFile.slots[selectedSequence].AnimationCommandLength;
                frmProgress.MessageLabel = string.Format("Uploading slot {0} to MonsterShield...", selectedSequence);
                frmProgress.Show();
                this.Enabled = false;

                string data = "@U" + GetSequenceChar(selectedSequence);
                SendSerialCommand(data);

                //short seqlen = ourMonsterFile.slots[selectedSequence].AnimationCommandLength;
                long seqlen = ourMonsterFile.slots[selectedSequence].AnimationEnd;

                SendSerialCommand(String.Format("{0:X4}", seqlen));

                // Write data
                for (int i = 0; i < seqlen; i++)
                {
                    serialPort1.Write(String.Format("{0:X2}", ourMonsterFile.slots[selectedSequence].cmd1[i]));
                    //System.Threading.Thread.Sleep(5);
                    serialPort1.Write(String.Format("{0:X2}", ourMonsterFile.slots[selectedSequence].cmd2[i]));
                    System.Threading.Thread.Sleep(5);
                    //serialPort1.Write(String.Format("{0:X4}", ourMonsterFile.slots[selectedSequence].timing[i]*10));
                    //System.Threading.Thread.Sleep(10);
                    //frmProgress.ProgressValue += 1;
                    if ((i % 10) == 0)
                        frmProgress.ProgressValue = i;
                }

                this.Enabled = true;
                this.Activate();
                frmProgress.Close();
            
            }
             * */
        }

        private void SendAnimation(int slot)
        {
            if (isConnected())
            {
                //short seqlen = ourMonsterFile.slots[selectedSequence].AnimationCommandLength;
                long seqlen = ourMonsterFile.slots[slot].AnimationEnd;

                frmProgress = new ProgressForm();
                frmProgress.ProgressValue = 0;
                frmProgress.ProgressMax = (int)seqlen;
                frmProgress.MessageLabel = string.Format("Uploading slot {0} to MonsterShield...", slot);
                frmProgress.Show();
                this.Enabled = false;

                string data = "@U" + GetSequenceChar(slot);
                SendSerialCommand(data);



                SendSerialCommand(String.Format("{0:X4}", seqlen));

                // Write data
                for (int i = 0; i < seqlen; i++)
                {
                    serialPort1.Write(String.Format("{0:X2}", ourMonsterFile.slots[slot].cmd1[i]));
                    //System.Threading.Thread.Sleep(5);
                    serialPort1.Write(String.Format("{0:X2}", ourMonsterFile.slots[slot].cmd2[i]));
                    System.Threading.Thread.Sleep(5);
                    //serialPort1.Write(String.Format("{0:X4}", ourMonsterFile.slots[selectedSequence].timing[i]*10));
                    //System.Threading.Thread.Sleep(10);
                    //frmProgress.ProgressValue += 1;
                    if ((i % 10) == 0)
                        frmProgress.ProgressValue = i;
                }
                frmProgress.ProgressValue = frmProgress.ProgressMax;
                this.Enabled = true;
                this.Activate();
                frmProgress.Close();

            }
        }



        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (!string.IsNullOrEmpty(FilePath))
            {
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(FilePath);
            }
            dialog.FileName = FilePath;
            dialog.Filter = "Animations (.mos)|*.mos|All Files (*.*)|*.*";
            dialog.FilterIndex = 1;

            DialogResult result = dialog.ShowDialog();

            switch (result)
            {
                case DialogResult.OK:
                    filename = dialog.FileName;
                    LoadAnimationSlots(filename);
                    //Application.UserAppDataRegistry.SetValue("FilePath", System.IO.Path.GetDirectoryName(dialog.FileName));
                    Application.UserAppDataRegistry.SetValue("FilePath", dialog.FileName);
                    //FilePath = System.IO.Path.GetDirectoryName(dialog.FileName);
                    FilePath = dialog.FileName;
                    break;
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Save stuff!
            SaveAnimationToSlot(ourMonsterFile.slots[selectedSequence]);
            SaveAnimationSlots();
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Save stuff!
            SaveAnimationToSlot(ourMonsterFile.slots[selectedSequence]);
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.FileName = filename;


            if (!string.IsNullOrEmpty(FilePath))
            {
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(FilePath);
            }
            //dialog.FileName = FilePath;
            dialog.Filter = "Animations (.mos)|*.mos|All Files (*.*)|*.*";
            dialog.FilterIndex = 1;





            DialogResult result = dialog.ShowDialog();

            switch (result)
            {
                case DialogResult.OK:
                    filename = dialog.FileName;
                    SaveAnimationSlots();
                    Application.UserAppDataRegistry.SetValue("FilePath", dialog.FileName);
                    FilePath = System.IO.Path.GetDirectoryName(dialog.FileName);
                    break;

            }
        }



        private void SaveAnimationSlots()
        {
            try
            {
                ourMonsterFile.SaveFile(filename);
                lblStatusMessage.ForeColor = Color.Black;
                lblStatusMessage.Text = string.Format("Succesfully saved {0}.", filename);
                SetTitle();
                Application.UserAppDataRegistry.SetValue("FilePath", filename);
            }
            catch(Exception ex)
            {
                lblStatusMessage.ForeColor = Color.Red;
                lblStatusMessage.Text = string.Format("Error saving file: {0}", ex.Message);
            }

        }

        private void LoadAnimationSlots(string filename)
        {
            try
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(MonsterFile));
                TextReader textReader = new StreamReader(filename);

                // 2014-08-19 First try the v1.5 file format
                try
                {
                    ourMonsterFile = (MonsterFile)deserializer.Deserialize(textReader);
                    textReader.Close();
                }
                catch (Exception ex)
                {
                    textReader.Close();
                    


                    try
                    {

                        // Okay, let's see if it's the old stuff.
                        deserializer = new XmlSerializer(typeof(List<AnimationSlot>));
                        textReader = new StreamReader(filename);
                        List<AnimationSlot> oldSlots = (List<AnimationSlot>)deserializer.Deserialize(textReader);
                        textReader.Close();

                        // Assuming we got here, we need to convert the old file format to the new file format!
                        MessageBox.Show("The file \"" + filename + "\" was created with an old version of the editor.  We will convert it to the new format.  Please be aware that some truncation may occur due to differences in capabilities between firmware.");
                        ourMonsterFile.ConvertOldFormat(oldSlots);



                    }
                    catch (Exception ex2)
                    {
                        MessageBox.Show("Sorry, we couldn't load \"" + filename + "\" for some reason: " + ex2.Message);
                    }
                }

                // Configure settings
                cboSlotCount.SelectedIndex = ourMonsterFile.SlotSetting;
                ConfigureSlots();
                comboBoxPlaybackMode.SelectedIndex = ourMonsterFile.PlaybackMode;
                chkAmbient.Checked = ourMonsterFile.AmbientMode;
                chkEEPROM1.Checked = ourMonsterFile.eeprom1;
                chkEEPROM2.Checked = ourMonsterFile.eeprom2;
                try
                {
                    //TODO 8/15/2013 FIX!
                    //hScrollTriggerThreshold.Value = ourMonsterFile.TriggerThreshold;
                    //hScrollTriggerSensitivity.Value = ourMonsterFile.TriggerSensitivity;
                    //hScrollTriggerCooldown.Value = ourMonsterFile.TriggerCooldown;
                }
                catch
                {
                }
                //TODO 8/15/2013 FIX!
                //chkResetOnVoltage.Checked = ourMonsterFile.TriggerIgnoreUntilReset;
                //rdoHigh.Checked = ourMonsterFile.TriggerOnHigh;
                //rdoLow.Checked = !ourMonsterFile.TriggerOnHigh;


                // If we are connected to MonsterShield, set its configuration values from our file.
                if (serialPort1.IsOpen)
                {
                    string data = "@X" + cboSlotCount.SelectedIndex;
                    if (SendSerialCommand(data) == false)
                    {
                    }
                    System.Threading.Thread.Sleep(5);

                    SendSerialCommand(String.Format("@Z1{0:X4}", hScrollTriggerThreshold.Value));
                    System.Threading.Thread.Sleep(5);
                    SendSerialCommand(String.Format("@Z2{0:X2}", hScrollTriggerSensitivity.Value));
                    System.Threading.Thread.Sleep(5);
                    SendSerialCommand(String.Format("@Z3{0:X2}", hScrollTriggerCooldown.Value));
                    System.Threading.Thread.Sleep(5);
                    
                    //TODO FIX! 8/15/2013
                    /*
                    if (ourMonsterFile.TriggerOnHigh)
                    {
                        SendSerialCommand("@Z41");
                    }
                    else
                    {
                        SendSerialCommand("@Z40");
                    }
                    
                    System.Threading.Thread.Sleep(5);

                    if (ourMonsterFile.TriggerIgnoreUntilReset)
                    {
                        SendSerialCommand("@Z51");
                    }
                    else
                    {
                        SendSerialCommand("@Z50");
                    }
                     * */

                    System.Threading.Thread.Sleep(5);

                    if (ourMonsterFile.AmbientMode)
                    {
                        SendSerialCommand("@A1");
                    }
                    else
                    {
                        SendSerialCommand("@A0");
                    }

                    System.Threading.Thread.Sleep(5);
                    SendSerialCommand(string.Format("@P{0}", comboBoxPlaybackMode.SelectedIndex));

                }


                lblStatusMessage.ForeColor = Color.Black;
                lblStatusMessage.Text = string.Format("Succesfully loaded {0}.", filename);
                DisplayAnimationSlot(ourMonsterFile.slots[0]);
                SetTitle();
                LoadCheckboxesFromSlots();
                ProcessNewZoomFactor();
            }
            catch (Exception ex)
            {
                lblStatusMessage.ForeColor = Color.Red;
                lblStatusMessage.Text = string.Format("Error opening file: {0}", ex.Message);
            }

            pictureBox1.Refresh();
        }

        private void LoadCheckboxesFromSlots()
        {
            chk0.Checked = ourMonsterFile.slots[0].Enabled;
            chk1.Checked = ourMonsterFile.slots[1].Enabled;
            chk2.Checked = ourMonsterFile.slots[2].Enabled;
            chk3.Checked = ourMonsterFile.slots[3].Enabled;
            chk4.Checked = ourMonsterFile.slots[4].Enabled;
            chk5.Checked = ourMonsterFile.slots[5].Enabled;
            chk6.Checked = ourMonsterFile.slots[6].Enabled;
            chk7.Checked = ourMonsterFile.slots[7].Enabled;
            chk8.Checked = ourMonsterFile.slots[8].Enabled;
            chk9.Checked = ourMonsterFile.slots[9].Enabled;
            chkA.Checked = ourMonsterFile.slots[10].Enabled;
            chkB.Checked = ourMonsterFile.slots[11].Enabled;
            chkC.Checked = ourMonsterFile.slots[12].Enabled;
            chkD.Checked = ourMonsterFile.slots[13].Enabled;
            chkE.Checked = ourMonsterFile.slots[14].Enabled;
        }

        private void LoadSlotsFromCheckboxes()
        {
            ourMonsterFile.slots[0].Enabled = chk0.Checked;
            ourMonsterFile.slots[1].Enabled = chk1.Checked;
            ourMonsterFile.slots[2].Enabled = chk2.Checked;
            ourMonsterFile.slots[3].Enabled = chk3.Checked;
            ourMonsterFile.slots[4].Enabled = chk4.Checked;
            ourMonsterFile.slots[5].Enabled = chk5.Checked;
            ourMonsterFile.slots[6].Enabled = chk6.Checked;
            ourMonsterFile.slots[7].Enabled = chk7.Checked;
            ourMonsterFile.slots[8].Enabled = chk8.Checked;
            ourMonsterFile.slots[9].Enabled = chk9.Checked;
            ourMonsterFile.slots[10].Enabled = chkA.Checked;
            ourMonsterFile.slots[11].Enabled = chkB.Checked;
            ourMonsterFile.slots[12].Enabled = chkC.Checked;
            ourMonsterFile.slots[13].Enabled = chkD.Checked;
            ourMonsterFile.slots[14].Enabled = chkE.Checked;

        }

        private void ConfigureSlots()
        {
            int slotMode = 15 - cboSlotCount.SelectedIndex;
            NumberOfSlots = slotMode;

            btnSel0.Visible = true;
            btnSel1.Visible = true;
            btnSel2.Visible = true;
            btnSel3.Visible = true;
            btnSel4.Visible = true;
            btnSel5.Visible = true;
            btnSel6.Visible = true;
            btnSel7.Visible = true;
            btnSel8.Visible = true;
            btnSel9.Visible = true;
            btnSelA.Visible = true;
            btnSelB.Visible = true;
            btnSelC.Visible = true;
            btnSelD.Visible = true;
            btnSelE.Visible = true;
            
            chk0.Visible = true;
            chk1.Visible = true;
            chk2.Visible = true;
            chk3.Visible = true;
            chk4.Visible = true;
            chk5.Visible = true;
            chk6.Visible = true;
            chk7.Visible = true;
            chk8.Visible = true;
            chk9.Visible = true;
            chkA.Visible = true;
            chkB.Visible = true;
            chkC.Visible = true;
            chkD.Visible = true;
            chkE.Visible = true;

            //NumberOfSlots = 15;

            if (NumberOfSlots < 15)
            {
                btnSelE.Visible = false;
                chkE.Visible = false;
            }

            if (NumberOfSlots < 14)
            {
                btnSelD.Visible = false;
                chkD.Visible = false;
            }

            if (NumberOfSlots < 13)
            {
                btnSelC.Visible = false;
                chkC.Visible = false;
            }

            if (NumberOfSlots < 12)
            {
                btnSelB.Visible = false;
                chkB.Visible = false;
            }

            if (NumberOfSlots < 11)
            {
                btnSelA.Visible = false;
                chkA.Visible = false;
            }

            if (NumberOfSlots < 10)
            {
                btnSel9.Visible = false;
                chk9.Visible = false;
            }

            if (NumberOfSlots < 9)
            {
                btnSel8.Visible = false;
                chk8.Visible = false;
            }

            if (NumberOfSlots < 8)
            {
                btnSel7.Visible = false;
                chk7.Visible = false;
            }

            if (NumberOfSlots < 7)
            {
                btnSel6.Visible = false;
                chk6.Visible = false;
            }

            if (NumberOfSlots < 6)
            {
                btnSel5.Visible = false;
                chk5.Visible = false;
            }

            if (NumberOfSlots < 5)
            {
                btnSel4.Visible = false;
                chk4.Visible = false;
            }

            if (NumberOfSlots < 4)
            {
                btnSel3.Visible = false;
                chk3.Visible = false;
            }

            if (NumberOfSlots < 3)
            {
                btnSel2.Visible = false;
                chk2.Visible = false;
            }

            if (NumberOfSlots < 2)
            {
                btnSel1.Visible = false;
                chk1.Visible = false;
            }

            ourMonsterFile.setTotalEventsPerSlot(NumberOfSlots);
            ProcessNewZoomFactor();
        }



        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            filename = "untitled.mos";
            InitNewAnimations();
            DisplayAnimationSlot(ourMonsterFile.slots[0]);
            SetTitle();
            
        }

        private void InitNewAnimations()
        {
            for (int i = 0; i < 16; i++)
            {
                ourMonsterFile.outputName[i] = "Output";
            }

            ourMonsterFile.slots.Clear();
            for (int i = 0; i < 15; i++)
            {
                AnimationSlot2 anim = new AnimationSlot2();
                

                // fake some data
                for (int j = 0; j < anim.cmd1.Length; j++)
                {
                    anim.cmd1[j] = 0xFF;
                    anim.cmd2[j] = 0xFF;
                }

                ourMonsterFile.slots.Add(anim);
            }
            LoadCheckboxesFromSlots();
        }

        private void ClipboardCut()
        {
            if (selectLeft >= 0 && selectRight >= 0)
            {
                
                if (selectLeft < selectRight)
                {
                    long j = 0;
                    long offset = selectRight;
                    for (long i = selectLeft; i < MAX_PLAYBACK_BUFFER - (selectRight - selectLeft) - 1; i++)
                    {
                        copyBuffer1[j] = currentSlot.cmd1[i];
                        copyBuffer2[j] = currentSlot.cmd2[i];

                        currentSlot.cmd1[i] = currentSlot.cmd1[offset + 1];
                        currentSlot.cmd2[i] = currentSlot.cmd2[offset + 1];
                        //playbackBuffer[i] = playbackBuffer[offset+1];
                        
                        j += 1;
                        offset += 1;
                    }
                    copyBufferLength = selectRight - selectLeft;
                }
                
                selectLeft = selectRight = 0;
            }
            
            pictureBox1.Refresh();
        }

        private void ClipboardPaste()
        {
            if (selectLeft >= 0)
            {

                // Step 1:  Copy from the rear of the playback buffer
                //long offset = selectRight - selectLeft;
                for (long k = MAX_PLAYBACK_BUFFER - 1; k >= selectLeft; k--)
                {
                    if (k - copyBufferLength >= 0)
                    {
                        currentSlot.cmd1[k] = currentSlot.cmd1[k - copyBufferLength];
                        currentSlot.cmd2[k] = currentSlot.cmd2[k - copyBufferLength];
                        //playbackBuffer[k] = playbackBuffer[k - copyBufferLength];
                    }
                        
                }


                // Step 2:  Copy from our copyBuffer
                long j = 0;
                for (long i = selectLeft; i < selectLeft + copyBufferLength; i++)
                {
                    //playbackBuffer[i] = copyBuffer[j];
                    currentSlot.cmd1[i] = copyBuffer1[j];
                    currentSlot.cmd2[i] = copyBuffer2[j];

                    j += 1;
                }
                selectRight = selectLeft + copyBufferLength;

            }

            pictureBox1.Refresh();
        }

        private void ClipboardCopy()
        {
            if (selectLeft >= 0 && selectRight >= 0)
            {

                if (selectLeft < selectRight)
                {
                    long j = 0;
                    for (long i = selectLeft; i < selectRight; i++)
                    {
                        copyBuffer1[j] = currentSlot.cmd1[i];
                        copyBuffer2[j] = currentSlot.cmd2[i];
                        //copyBuffer[j] = playbackBuffer[i];
                        j += 1;
                    }
                    copyBufferLength = j;
                }

                selectLeft = selectRight = 0;
            }

            pictureBox1.Refresh();
        }

        private void SelectAll()
        {
            selectLeft = 0;
            selectRight = playbackBuffer.Length - 1;
            pictureBox1.Refresh();
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClipboardCut();   
        }

        private void comPortToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            foreach (ToolStripMenuItem item in comPortToolStripMenuItem.DropDownItems)
            {
                item.Checked = false;
            }

            ComPort = e.ClickedItem.Text;
            //serialPort1.PortName = ComPort;
            lblStatusComPort.Text = serialPort1.PortName;
            e.ClickedItem.Select();

            try
            {
                // Save the selected Com Port.
                Application.UserAppDataRegistry.SetValue("ComPort",ComPort);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


            if (serialPort1.IsOpen == true)
            {
                try
                {
                    serialPort1.Close();
                }
                catch
                {
                }
            }
            isConnected();

        }

        private void btnStop_Click(object sender, EventArgs e)
        {

            SendSerialCommand("@*");

            StopPlayback();
            SetPlaybackModeUI(false);
            currentPlaybackMode = PlaybackMode.EditMode;
            pictureBox1.Refresh();

        }


        private void StopPlayback()
        {
            timer1.Enabled = false;
            watch.Stop();
            axWindowsMediaPlayer1.Ctlcontrols.stop();
            hScrollBar1.Value = 0;
            LeftAnimPos = 0;
            pictureBox1.Refresh();

            for (int i = 0; i < 16; i++)
            {
                relayStates[i] = false;
            }

            SetPlaybackModeUI(false);
        }

        private void SetPlaybackModeUI(bool play)
        {
            if (play)
            {
                btnConnect.Enabled = false;
                btnDownload.Enabled = false;
                btnDownloadAll.Enabled = false;
                btnUpload.Enabled = false;
                btnUploadAll.Enabled = false;
                btnSel0.Enabled = false;
                btnSel1.Enabled = false;
                btnSel2.Enabled = false;
                btnSel3.Enabled = false;
                btnSel4.Enabled = false;
                btnSel5.Enabled = false;
                btnSel6.Enabled = false;
                btnSel7.Enabled = false;
                btnSel8.Enabled = false;
                btnSel9.Enabled = false;
                btnSelA.Enabled = false;
                btnSelB.Enabled = false;
                btnSelC.Enabled = false;
                btnSelD.Enabled = false;
                btnSelE.Enabled = false;
                chk0.Enabled = false;
                chk1.Enabled = false;
                chk2.Enabled = false;
                chk3.Enabled = false;
                chk4.Enabled = false;
                chk5.Enabled = false;
                chk6.Enabled = false;
                chk7.Enabled = false;
                chk8.Enabled = false;
                chk9.Enabled = false;
                chkA.Enabled = false;
                chkB.Enabled = false;
                chkC.Enabled = false;
                chkD.Enabled = false;
                chkE.Enabled = false;
                cboSlotCount.Enabled = false;
                comboBoxPlaybackMode.Enabled = false;
                btnRelay0.Enabled = false;
                btnRelay1.Enabled = false;
                btnRelay2.Enabled = false;
                btnRelay3.Enabled = false;
            }
            else
            {
                if (PlayOnline == true)
                {
                    btnDownload.Enabled = true;
                    btnDownloadAll.Enabled = true;
                    btnUpload.Enabled = true;
                    btnUploadAll.Enabled = true;
                }
                btnConnect.Enabled = true;

                btnSel0.Enabled = true;
                btnSel1.Enabled = true;
                btnSel2.Enabled = true;
                btnSel3.Enabled = true;
                btnSel4.Enabled = true;
                btnSel5.Enabled = true;
                btnSel6.Enabled = true;
                btnSel7.Enabled = true;
                btnSel8.Enabled = true;
                btnSel9.Enabled = true;
                btnSelA.Enabled = true;
                btnSelB.Enabled = true;
                btnSelC.Enabled = true;
                btnSelD.Enabled = true;
                btnSelE.Enabled = true;
                chk0.Enabled = true;
                chk1.Enabled = true;
                chk2.Enabled = true;
                chk3.Enabled = true;
                chk4.Enabled = true;
                chk5.Enabled = true;
                chk6.Enabled = true;
                chk7.Enabled = true;
                chk8.Enabled = true;
                chk9.Enabled = true;
                chkA.Enabled = true;
                chkB.Enabled = true;
                chkC.Enabled = true;
                chkD.Enabled = true;
                chkE.Enabled = true;
                cboSlotCount.Enabled = true;
                comboBoxPlaybackMode.Enabled = true;
                btnRelay0.Enabled = true;
                btnRelay1.Enabled = true;
                btnRelay2.Enabled = true;
                btnRelay3.Enabled = true;
                btnTrigger.Enabled = true;
                btnStop.Enabled = true;
             
            }
        }

        private void btnRelay0_Click(object sender, EventArgs e)
        {

        }

        private void btnRelay1_Click(object sender, EventArgs e)
        {

        }

        private void comboBoxPlaybackMode_SelectedIndexChanged(object sender, EventArgs e)
        {

            if (comboBoxPlaybackMode.SelectedIndex >= 0 && comboBoxPlaybackMode.SelectedIndex < 3)
            {
                if (InterfaceAutomation == false)
                    SendSerialCommand(string.Format("@P{0}", comboBoxPlaybackMode.SelectedIndex));
            }
            InterfaceAutomation = false;
            ourMonsterFile.PlaybackMode = comboBoxPlaybackMode.SelectedIndex;
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClipboardCopy();
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClipboardPaste();
        }

        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (selectLeft >= 0 && selectRight >= 0)
            {
                for (long i = selectLeft; i < selectRight; i++)
                {
                    currentSlot.cmd1[i] = 0xFF;
                    currentSlot.cmd2[i] = 0xFF;
                    //playbackBuffer[i] = 0x0F;
                }
            }

            pictureBox1.Refresh();
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectAll();
        }

        private void SetSequenceEnabled(int sequenceNo, bool value)
        {
            switch (sequenceNo)
            {
                case 0:
                    chk0.Checked = value;
                    break;

                case 1:
                    chk1.Checked = value;
                    break;

                case 2:
                    chk2.Checked = value;
                    break;

                case 3:
                    chk3.Checked = value;
                    break;

                case 4:
                    chk4.Checked = value;
                    break;

                case 5:
                    chk5.Checked = value;
                    break;

                case 6:
                    chk6.Checked = value;
                    break;

                case 7:
                    chk7.Checked = value;
                    break;

                case 8:
                    chk8.Checked = value;
                    break;

                case 9:
                    chk9.Checked = value;
                    break;

                case 10:
                    chkA.Checked = value;
                    break;

                case 11:
                    chkB.Checked = value;
                    break;

                case 12:
                    chkC.Checked = value;
                    break;

                case 13:
                    chkD.Checked = value;
                    break;

                case 14:
                    chkE.Checked = value;
                    break;
            }
        }

        private void SetEndMarkerLabel()
        {
            TimeSpan span = new TimeSpan(0, 0, 0, 0, (int)AnimationEndMarker * 50);
            toolStripStatusEndMarker.Text = string.Format("End: {0:00}:{1:00}.{2:0##}", span.Minutes, span.Seconds, span.Milliseconds);
        }

        private void btnDownloadAll_Click(object sender, EventArgs e)
        {
            SelectSequence(0);
            DownloadAll = true;
            CurrentDownloadSlot = 0;

            string data = "@D" + GetSequenceChar(CurrentDownloadSlot);
            SendSerialCommand(data);

        }

        private void SetOnlineUI(bool online)
        {
            if (online)
            {
                btnDownload.Enabled = true;
                btnDownloadAll.Enabled = true;
                btnUpload.Enabled = true;
                btnUploadAll.Enabled = true;
            }
            else
            {
                btnDownload.Enabled = false;
                btnDownloadAll.Enabled = false;
                btnUpload.Enabled = false;
                btnUploadAll.Enabled = false;
            }
        }

        private void btnUploadAll_Click(object sender, EventArgs e)
        {
            // Rebuild command table
            SaveAnimationToSlot(ourMonsterFile.slots[selectedSequence]);


            if (isConnected())
            {
                frmProgress = new ProgressForm();

                frmProgress.Show();
                this.Enabled = false;

                int numSlots = 15;
                switch (cboSlotCount.SelectedIndex)
                {
                    case 0:
                        numSlots = 15;
                        break;

                    case 1:
                        numSlots = 10;
                        break;

                    case 2:
                        numSlots = 6;
                        break;

                    case 3:
                        numSlots = 5;
                        break;

                    case 4:
                        numSlots = 3;
                        break;

                    case 5:
                        numSlots = 2;
                        break;

                    case 6:
                        numSlots = 1;
                        break;

                }


                for (int slot = 0; slot < numSlots; slot++)
                {
                    frmProgress.ProgressValue = 0;
                    frmProgress.ProgressMax = ourMonsterFile.slots[slot].AnimationCommandLength;
                    frmProgress.MessageLabel = string.Format("Uploading slot {0} to MonsterShield...", slot);

                    string data = "@U" + GetSequenceChar(slot);
                    serialPort1.WriteLine(data);

                    short seqlen = ourMonsterFile.slots[slot].AnimationCommandLength;

                    serialPort1.WriteLine(String.Format("{0:X4}", seqlen));

                    // Write data
                    for (int i = 0; i < seqlen; i++)
                    {
                        serialPort1.Write(String.Format("{0:X2}", ourMonsterFile.slots[slot].cmd1[i]));
                        System.Threading.Thread.Sleep(5);
                        //serialPort1.Write(String.Format("{0:X2}", ourMonsterFile.slots[slot].timing[i]));
                        //System.Threading.Thread.Sleep(5);
                        //frmProgress.ProgressValue += 1;
                        if ((i % 10) == 0)
                            frmProgress.ProgressValue = i;
                    }

                    System.Threading.Thread.Sleep(100);

                }

                this.Enabled = true;
                this.Activate();
                frmProgress.Close();

            }
        }

        private void hScrollTriggerThreshold_ValueChanged(object sender, EventArgs e)
        {
            lblTriggerThreshold.Text = string.Format("{0:F3} volts", (0.0049f * (float)hScrollTriggerThreshold.Value));
            //ourMonsterFile.TriggerThreshold = hScrollTriggerThreshold.Value;
        }

        private void hScrollTriggerSensitivity_ValueChanged(object sender, EventArgs e)
        {
            lblTriggerSensitivity.Text = string.Format("{0} ms", (hScrollTriggerSensitivity.Value*10));
            //ourMonsterFile.TriggerSensitivity = hScrollTriggerSensitivity.Value;
        }

        private void hScrollTriggerCooldown_ValueChanged(object sender, EventArgs e)
        {
            lblTriggerCooldown.Text = string.Format("{0} seconds", hScrollTriggerCooldown.Value);
            //ourMonsterFile.TriggerCooldown = hScrollTriggerCooldown.Value;
        }

        private bool SendSerialCommand(string text)
        {
            bool rc = isConnected();
            if (rc)
            {
                serialPort1.WriteLine(text);
                textBox1.AppendText("sent:" + text + "\r\n");
            }

            return rc;
        }

        private void SendTriggerInfo(int trigger)
        {
            int voltage = 0;
            int onreset = 0;

            if (ourMonsterFile.TriggerOnHigh[trigger] == true) voltage = 1;
            if (ourMonsterFile.TriggerIgnoreUntilReset[trigger] == true) onreset = 1;

            SendSerialCommand(String.Format("@z{0},{1:X4},{2:X2},{3:X2},{4},{5}", 
                trigger, 
                ourMonsterFile.TriggerThreshold[trigger],
                ourMonsterFile.TriggerSensitivity[trigger],
                ourMonsterFile.TriggerCooldown[trigger],
                voltage,
                onreset));
        }

        private void hScrollTriggerThreshold_Scroll(object sender, ScrollEventArgs e)
        {
            if (e.Type == ScrollEventType.EndScroll)
            {
                int trigger = cboTrigger.SelectedIndex;
                ourMonsterFile.TriggerThreshold[trigger] = hScrollTriggerThreshold.Value;
                SendTriggerInfo(trigger);
                //SendSerialCommand(String.Format("@Z1{0:X4}", hScrollTriggerThreshold.Value));
                //ourMonsterFile.TriggerThreshold = hScrollTriggerThreshold.Value;
            }
        }

        private void hScrollTriggerSensitivity_Scroll(object sender, ScrollEventArgs e)
        {
            if (e.Type == ScrollEventType.EndScroll)
            {
                int trigger = cboTrigger.SelectedIndex;
                ourMonsterFile.TriggerSensitivity[trigger] = hScrollTriggerSensitivity.Value;
                SendTriggerInfo(trigger);
                //SendSerialCommand(String.Format("@Z2{0:X2}", hScrollTriggerSensitivity.Value));
                //ourMonsterFile.TriggerSensitivity = hScrollTriggerSensitivity.Value;
            }
        }

        private void hScrollTriggerCooldown_Scroll(object sender, ScrollEventArgs e)
        {
            if (e.Type == ScrollEventType.EndScroll)
            {
                int trigger = cboTrigger.SelectedIndex;
                ourMonsterFile.TriggerCooldown[trigger] = hScrollTriggerCooldown.Value;
                SendTriggerInfo(trigger);
                //SendSerialCommand(String.Format("@Z3{0:X2}", hScrollTriggerCooldown.Value));
                //ourMonsterFile.TriggerCooldown = hScrollTriggerCooldown.Value;
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox1 frmAbout = new AboutBox1();
            frmAbout.ShowDialog();
            frmAbout.Dispose();
        }

        private bool IsNewVersionAvailable()
        {
            bool newer = false;

            try
            {
                System.Net.WebRequest request = System.Net.WebRequest.Create("http://www.hauntsoft.com/versioninfo/mse.xml");
                request.Timeout = 600;
                System.Net.WebResponse response = request.GetResponse();

                if (response != null)
                {
                    XmlDocument doc = new XmlDocument();
                    StreamReader reader = new StreamReader(response.GetResponseStream());
                    string data = reader.ReadToEnd();

                    doc.LoadXml(data);

                    int major = 0;
                    int majorrevision = 0;
                    int minor = 0;
                    int revision = 0;

                    XmlNode node = doc.SelectSingleNode("version/major");
                    if (node != null)
                    {
                        major = int.Parse(node.InnerText);
                    }

                    node = doc.SelectSingleNode("version/majorrevision");
                    if (node != null)
                    {
                        majorrevision = int.Parse(node.InnerText);
                    }

                    node = doc.SelectSingleNode("version/minor");
                    if (node != null)
                    {
                        minor = int.Parse(node.InnerText);
                    }

                    node = doc.SelectSingleNode("version/revision");
                    if (node != null)
                    {
                        revision = int.Parse(node.InnerText);
                    }

                    Version ver = Assembly.GetExecutingAssembly().GetName().Version;



                    if (ver.Major < major)
                    {
                        newer = true;
                    }
                    else if (ver.Major == major)
                    {
                        if (ver.MajorRevision < majorrevision)
                        {
                            newer = true;
                        }
                        else if (ver.MajorRevision == majorrevision)
                        {
                            if (ver.Minor < minor)
                            {
                                newer = true;
                            }
                            else if (ver.Minor == minor)
                            {
                                if (ver.MinorRevision < revision)
                                {
                                    newer = true;
                                }
                            }
                        }
                        
                    }

                    /*
                    if (ver.Major > major)
                    {
                        newer = false;
                    }
                    else if (ver.Major == major)
                    {
                        if (ver.MajorRevision > majorrevision)
                        {
                            newer = false;
                        }
                        else if (ver.MajorRevision == majorrevision)
                        {
                            if (ver.Minor > minor)
                            {
                                newer = false;
                            }
                            else if (ver.Minor == minor)
                            {
                                if (ver.MinorRevision > revision)
                                {
                                    newer = false;
                                }

                            }
                        }
                    }
                     * */

                    if (newer)
                    {
                        // Show dialog!
                        NewVersion frmVersion = new NewVersion();
                        frmVersion.SetVersion(string.Format("{0}.{1}.{2}.{3}", major,majorrevision,minor,revision));
                        frmVersion.ShowDialog();
                        if (frmVersion.DialogResult == DialogResult.Yes)
                        {
                            // Shutdown app!
                            frmVersion.Dispose();
                            //this.Close();
                            return true;

                        }
                    }
                    
                   
                }

            }
            catch (Exception ex)
            {
                // Oops, couldn't connect or something.
                Console.WriteLine("Error:  Couldn't get XML version file from hauntsoft server: {0}", ex.Message);
            }

            return false;
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {

            if (IsNewVersionAvailable())
            {

                Application.Exit();
                this.Close();
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void btnRelay2_Click(object sender, EventArgs e)
        {

        }

        private void btnRelay3_Click(object sender, EventArgs e)
        {

        }

        private void btnSelectMp3_Click(object sender, EventArgs e)
        {
            MP3Files mp3 = new MP3Files();
            mp3.SetMP3Files(ourMonsterFile.slots);
            mp3.MusicPath = MusicPath;
            mp3.ShowDialog();
            mp3.GetMP3Files(ourMonsterFile.slots);
            MusicPath = mp3.MusicPath;
            Application.UserAppDataRegistry.SetValue("FilePath", MusicPath);
        }

        private void btnCopyMp3_Click(object sender, EventArgs e)
        {
            CopyMP3 mp3 = new CopyMP3();
            mp3.slots = ourMonsterFile.slots;
            if (mp3.ShowDialog() == DialogResult.OK)
            {
                
            }

        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
         
        }

        private void cboSlotCount_SelectedIndexChanged(object sender, EventArgs e)
        {
            
        }

        private void cboSlotCount_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (cboSlotCount.SelectedIndex != -1)
            {
                if (serialPort1.IsOpen == true)
                {

                    // Since we are online with a MonsterShield, we need to 
                    // warn user that this will format all animations.
                    if (MessageBox.Show("This operation will erase all animation data stored on the MonsterShield.  Are you Sure?", "WARNING", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) == DialogResult.Cancel)
                    {
                        SendSerialCommand("@Y0");
                        return;
                    }

                }

                string data = string.Format("@X{0:x1}",(15 - cboSlotCount.SelectedIndex));
                if (SendSerialCommand(data) == false)
                {
                    // We are offline, so WE need to configure the slots.
                    ConfigureSlots();
                }
                else
                {
                    // We are online.
                }

                ourMonsterFile.SlotSetting = cboSlotCount.SelectedIndex;

            }

        }

        private void chkAmbient_CheckedChanged(object sender, EventArgs e)
        {
            if (chkAmbient.Checked == true)
            {
                SendSerialCommand("@A1");
            }
            else
            {
                SendSerialCommand("@A0");
            }
            ourMonsterFile.AmbientMode = chkAmbient.Checked;
        }

        private void rdoHigh_CheckedChanged(object sender, EventArgs e)
        {
            
        }

        private void rdoLow_CheckedChanged(object sender, EventArgs e)
        {
            
        }

        private void rdoHigh_Click(object sender, EventArgs e)
        {
            int trigger = cboTrigger.SelectedIndex;
            ourMonsterFile.TriggerOnHigh[trigger] = true;
            SendTriggerInfo(trigger);
            //SendSerialCommand("@Z41");
        }

        private void rdoLow_Click(object sender, EventArgs e)
        {
            int trigger = cboTrigger.SelectedIndex;
            ourMonsterFile.TriggerOnHigh[trigger] = false;
            SendTriggerInfo(trigger);
            //SendSerialCommand("@Z40");
        }

        private void chkResetOnVoltage_Click(object sender, EventArgs e)
        {
            int trigger = cboTrigger.SelectedIndex;
            if (chkResetOnVoltage.Checked == true)
            {
                ourMonsterFile.TriggerIgnoreUntilReset[trigger] = true;
                SendTriggerInfo(trigger);
                //SendSerialCommand("@Z51");
            }
            else
            {
                ourMonsterFile.TriggerIgnoreUntilReset[trigger] = false;
                SendTriggerInfo(trigger);
                //SendSerialCommand("@Z50");
            }
            //ourMonsterFile.TriggerIgnoreUntilReset = chkResetOnVoltage.Checked;
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EnumerateComPorts();
        }

        private void EnumerateComPorts()
        {
            comPortToolStripMenuItem.DropDownItems.Clear();
            foreach (string port in System.IO.Ports.SerialPort.GetPortNames())
            {
                ToolStripMenuItem mymenuitem = new ToolStripMenuItem(port);
                mymenuitem.CheckOnClick = true;
                if (ComPort == port)
                {
                    mymenuitem.Checked = true;
                }
                comPortToolStripMenuItem.DropDownItems.Add(mymenuitem);
            }
        }

        private void secondPulsesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GeneratePulses(2, 200);
        }

        private void GeneratePulses(int milliseconds, short count)
        {
            bool toggle = false;
            int timing = 0;
            for (int i = 0; i < count; i++)
            {
                if (toggle)
                    ourMonsterFile.slots[selectedSequence].cmd1[i] = 0xFF;
                else
                    ourMonsterFile.slots[selectedSequence].cmd1[i] = 0xE0;

                ourMonsterFile.slots[selectedSequence].cmd2[i] = 0x00;
                timing += milliseconds;
                //ourMonsterFile.slots[selectedSequence].timing[i] = (short)(timing / 10);
                //ourMonsterFile.slots[selectedSequence].timing[i] = (ushort)(timing);
                toggle = !toggle;
            }
            ourMonsterFile.slots[selectedSequence].AnimationCommandLength = (short)(count);
            ourMonsterFile.slots[selectedSequence].AnimationEnd = count * timing;

            DisplayAnimationSlot(ourMonsterFile.slots[selectedSequence]);
        }



        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            audioOffset = Convert.ToDouble(numericUpDown1.Value);
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            ConvertRefreshToInterval(trackBar1.Value);
        }

        private void ConvertRefreshToInterval(int value)
        {
            switch (value)
            {
                case 10:
                    refreshinterval = 15;
                    break;

                case 9:
                    refreshinterval = 20;
                    break;

                case 8:
                    refreshinterval = 25;
                    break;

                case 7:
                    refreshinterval = 35;
                    break;

                case 6:
                    refreshinterval = 45;
                    break;

                case 5:
                    refreshinterval = 55;
                    break;

                case 4:
                    refreshinterval = 65;
                    break;

                case 3:
                    refreshinterval = 75;
                    break;

                case 2:
                    refreshinterval = 85;
                    break;

                case 1:
                    refreshinterval = 100;
                    break;

                case 0:
                    refreshinterval = 125;
                    break;

            }

            timer1.Interval = refreshinterval;
            Application.UserAppDataRegistry.SetValue("RefreshRate", trackBar1.Value);
        }

        private void generateAnimationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GeneratePulses(10, 100);
        }

        private void chk0_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void pictureBox1_Resize(object sender, EventArgs e)
        {

        }

        private void splitContainer2_SplitterMoved(object sender, SplitterEventArgs e)
        {
            vScrollBar1.Left = pictureBox1.Width - vScrollBar1.Width;
            vScrollBar1.Top = 0;
            vScrollBar1.Height = hScrollBar1.Top;
        }

        private void vScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            toptrackview = vScrollBar1.Value;
            pictureBox1.Refresh();
        }

        private void chkEEPROM1_CheckedChanged(object sender, EventArgs e)
        {
            ourMonsterFile.eeprom1 = chkEEPROM1.Checked;
            ourMonsterFile.setTotalEventsPerSlot(15 - cboSlotCount.SelectedIndex);
            ProcessNewZoomFactor();
            pictureBox1.Refresh();
        }

        private void chkEEPROM2_CheckedChanged(object sender, EventArgs e)
        {
            ourMonsterFile.eeprom2 = chkEEPROM2.Checked;
            ourMonsterFile.setTotalEventsPerSlot(15 - cboSlotCount.SelectedIndex);
            ProcessNewZoomFactor();
            pictureBox1.Refresh();

        }

        private void chkResetOnVoltage_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void cboTrigger_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshTriggerInfo();
        }

        private void RefreshTriggerInfo()
        {
            if (cboTrigger.SelectedIndex == -1) cboTrigger.SelectedIndex = 0;
            int trigger = cboTrigger.SelectedIndex;
            if (ourMonsterFile.TriggerThreshold[trigger] < 100) ourMonsterFile.TriggerThreshold[trigger] = 100;
            hScrollTriggerThreshold.Value = ourMonsterFile.TriggerThreshold[trigger];

            if (ourMonsterFile.TriggerSensitivity[trigger] < 0) ourMonsterFile.TriggerSensitivity[trigger] = 0;
            if (ourMonsterFile.TriggerSensitivity[trigger] > 255) ourMonsterFile.TriggerSensitivity[trigger] = 255;
            hScrollTriggerSensitivity.Value = ourMonsterFile.TriggerSensitivity[trigger];

            if (ourMonsterFile.TriggerCooldown[trigger] < 0) ourMonsterFile.TriggerCooldown[trigger] = 0;
            if (ourMonsterFile.TriggerCooldown[trigger] > 255) ourMonsterFile.TriggerCooldown[trigger] = 255;
            hScrollTriggerCooldown.Value = ourMonsterFile.TriggerCooldown[trigger];

            if (ourMonsterFile.TriggerOnHigh[trigger] == true)
            {
                rdoHigh.Checked = true;
                rdoLow.Checked = false;
            }
            else
            {
                rdoHigh.Checked = false;
                rdoLow.Checked = true;
            }

            if (ourMonsterFile.TriggerIgnoreUntilReset[trigger] == true)
            {
                chkResetOnVoltage.Checked = true;
            }
            else
            {
                chkResetOnVoltage.Checked = false;
            }
        }

        private void outputNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }


        private static DialogResult ShowInputDialog(ref string input)
        {
            System.Drawing.Size size = new System.Drawing.Size(200, 70);
            Form inputBox = new Form();

            inputBox.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            inputBox.ClientSize = size;
            inputBox.Text = "Name";


            System.Windows.Forms.TextBox textBox = new TextBox();
            textBox.Size = new System.Drawing.Size(size.Width - 10, 23);
            textBox.Location = new System.Drawing.Point(5, 5);
            textBox.Text = input;
            inputBox.Controls.Add(textBox);

            Button okButton = new Button();
            okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            okButton.Name = "okButton";
            okButton.Size = new System.Drawing.Size(75, 23);
            okButton.Text = "&OK";
            okButton.Location = new System.Drawing.Point(size.Width - 80 - 80, 39);
            inputBox.Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new System.Drawing.Size(75, 23);
            cancelButton.Text = "&Cancel";
            cancelButton.Location = new System.Drawing.Point(size.Width - 80, 39);
            inputBox.Controls.Add(cancelButton);


            DialogResult result = inputBox.ShowDialog();
            input = textBox.Text;
            return result;
        }

        private void chkEEPROM0_CheckedChanged(object sender, EventArgs e)
        {

        }


    }
}
