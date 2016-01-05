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
using System.Windows.Forms;

namespace MonsterShieldEditor
{
    public partial class MP3Files : Form
    {

        

        public MP3Files()
        {
            InitializeComponent();
        }

        private void MP3Files_Load(object sender, EventArgs e)
        {

        }

        public string MusicPath { get;set; }


        private void btnOk_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void GetMP3File(TextBox txt)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.InitialDirectory = MusicPath;
            open.Filter = "MP3 files (*.mp3)|*.mp3|All files (*.*)|*.*";
            if (open.ShowDialog() == DialogResult.OK)
            {
                txt.Text = open.FileName;
                MusicPath = System.IO.Path.GetDirectoryName(open.FileName);
            }
        }

        private void btnBrowse0_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile0);
            //OpenFileDialog open = new OpenFileDialog();
            //open.Filter = "MP3 files (*.mp3)|*.mp3|All files (*.*)|*.*";
            //if (open.ShowDialog() == DialogResult.OK)
            //{
            //    txtFile0.Text = open.FileName;
            //}
        }

        private void btnBrowse1_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile1);
        }

        private void btnBrowse2_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile2);
        }

        private void btnBrowse3_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile3);
        }

        private void btnBrowse4_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile4);
        }

        private void btnBrowse5_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile5);
        }

        private void btnBrowse6_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile6);
        }

        private void btnBrowse7_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile7);
        }

        private void btnBrowse8_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile8);
        }

        private void btnBrowse9_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile9);
        }

        private void btnBrowse10_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile10);
        }

        private void btnBrowse11_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile11);
        }

        private void btnBrowse12_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile12);
        }

        private void btnBrowse13_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile13);
        }

        private void btnBrowse14_Click(object sender, EventArgs e)
        {
            GetMP3File(txtFile14);
        }

        public void SetMP3Files(List<AnimationSlot2> slots)
        {
            txtFile0.Text = slots[0].MP3File;
            txtFile1.Text = slots[1].MP3File;
            txtFile2.Text = slots[2].MP3File;
            txtFile3.Text = slots[3].MP3File;
            txtFile4.Text = slots[4].MP3File;
            txtFile5.Text = slots[5].MP3File;
            txtFile6.Text = slots[6].MP3File;
            txtFile7.Text = slots[7].MP3File;
            txtFile8.Text = slots[8].MP3File;
            txtFile9.Text = slots[9].MP3File;
            txtFile10.Text = slots[10].MP3File;
            txtFile11.Text = slots[11].MP3File;
            txtFile12.Text = slots[12].MP3File;
            txtFile13.Text = slots[13].MP3File;
            txtFile14.Text = slots[14].MP3File;
        }

        public void GetMP3Files(List<AnimationSlot2> slots)
        {
            if (slots != null)
            {
                slots[0].MP3File = txtFile0.Text;
                slots[1].MP3File = txtFile1.Text;
                slots[2].MP3File = txtFile2.Text;
                slots[3].MP3File = txtFile3.Text;
                slots[4].MP3File = txtFile4.Text;
                slots[5].MP3File = txtFile5.Text;
                slots[6].MP3File = txtFile6.Text;
                slots[7].MP3File = txtFile7.Text;
                slots[8].MP3File = txtFile8.Text;
                slots[9].MP3File = txtFile9.Text;
                slots[10].MP3File = txtFile10.Text;
                slots[11].MP3File = txtFile11.Text;
                slots[12].MP3File = txtFile12.Text;
                slots[13].MP3File = txtFile13.Text;
                slots[14].MP3File = txtFile14.Text;
            }

        }

    }
}
