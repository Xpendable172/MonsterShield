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
    public partial class ProgressForm : Form
    {
        public ProgressForm()
        {
            InitializeComponent();
        }

        public int ProgressMax
        {
            get
            {
                return progressBar1.Maximum;
            }

            set
            {
                if (value > 0)
                    progressBar1.Maximum = value;
            }
        }

        public int ProgressValue
        {
            get
            {
                return progressBar1.Value;
            }

            set
            {
                if (value <= progressBar1.Maximum)
                    progressBar1.Value = value;
                this.Refresh();
            }
        }

        public string MessageLabel
        {
            get
            {
                return label1.Text;
            }

            set
            {
                label1.Text = value;
                this.Refresh();
            }
        }


        public void Center(int x, int y)
        {
            //this.CenterToScreen();
            //this.Left = x - (this.Width / 2);
            //this.Top = y - (this.Height / 2);
        }


    }
}
