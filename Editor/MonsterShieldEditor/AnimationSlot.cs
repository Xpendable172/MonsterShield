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
using System.Linq;
using System.Text;

namespace MonsterShieldEditor
{
    public class AnimationSlot
    {
        private const int MAX_COMMANDS = 8160;

        public byte[] commands = new byte[MAX_COMMANDS];
        public byte[] delays = new byte[MAX_COMMANDS];
        //public ushort[] timing = new ushort[MAX_COMMANDS];

        public AnimationSlot()
        {
            Init();
            AnimationEnd = 100*5;
            Enabled = true;
        }

        public void Init()
        {
            for (int i = 0; i < commands.Length; i++)
            {
                commands[i] = 0x00;
                delays[i] = 0x00;
                //timing[i] = 0;
            }
        }


        public short AnimationCommandLength { get; set; }

        /// <summary>
        /// Length of animation in 1/100th of a second.
        /// </summary>
        public long AnimationEnd { get; set; }

        public bool Enabled { get; set; }
        public string MP3File { get; set; }
    }
}
