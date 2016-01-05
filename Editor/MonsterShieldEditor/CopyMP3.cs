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
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace MonsterShieldEditor
{
    public partial class CopyMP3 : Form
    {
        public List<AnimationSlot2> slots;

        public CopyMP3()
        {
            InitializeComponent();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        public string SelectedDrive { get; set; }

        private void CopyMP3_Load(object sender, EventArgs e)
        {
            listBox1.Items.Clear();

            DriveInfo[] allDrives = DriveInfo.GetDrives();

            foreach (DriveInfo d in allDrives)
            {
                //listBox1.Items.Add(d.Name + " " + d.VolumeLabel);
                listBox1.Items.Add(d.Name);
            }


        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            SelectedDrive = listBox1.SelectedItem.ToString();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnCopy.Enabled = true;
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            SelectedDrive = listBox1.SelectedItem.ToString();
            string folder = SelectedDrive;
            if (!string.IsNullOrEmpty(folder))
            {
                
                txtOut.Clear();
                txtOut.Visible = true;
                progressBar1.Maximum = 14;
                progressBar1.Value = 1;
                progressBar1.Visible = true;
                progressBar1.Refresh();
                CopyFile(slots[0].MP3File, folder + "000.mp3");
                CopyFile(slots[1].MP3File, folder + "001.mp3");
                CopyFile(slots[2].MP3File, folder + "002.mp3");
                CopyFile(slots[3].MP3File, folder + "003.mp3");
                CopyFile(slots[4].MP3File, folder + "004.mp3");
                CopyFile(slots[5].MP3File, folder + "005.mp3");
                CopyFile(slots[6].MP3File, folder + "006.mp3");
                CopyFile(slots[7].MP3File, folder + "007.mp3");
                CopyFile(slots[8].MP3File, folder + "008.mp3");
                CopyFile(slots[9].MP3File, folder + "009.mp3");
                CopyFile(slots[10].MP3File, folder + "010.mp3");
                CopyFile(slots[11].MP3File, folder + "011.mp3");
                CopyFile(slots[12].MP3File, folder + "012.mp3");
                CopyFile(slots[13].MP3File, folder + "013.mp3");
                CopyFile(slots[14].MP3File, folder + "014.mp3");
                progressBar1.Visible = false;
                txtOut.AppendText("Finished!\r\n");
            }

        }

        private void CopyFile(string source, string dest)
        {
            if (!string.IsNullOrEmpty(source))
            {
                try
                {
                    txtOut.AppendText(string.Format("Copying {0} to {1}...",source,dest));
                    System.IO.File.Copy(source, dest, true);
                    txtOut.AppendText("SUCCESS\r\n");
                    if (progressBar1.Value < progressBar1.Maximum) progressBar1.Value += 1;
                    progressBar1.Refresh();
                }
                catch (Exception ex)
                {
                    txtOut.AppendText(string.Format("FAILURE! {0}\r\n", ex.Message));
                }
            }
        }

        private void txtOut_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }
    }
}
