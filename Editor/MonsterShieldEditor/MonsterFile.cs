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
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Text;

namespace MonsterShieldEditor
{
    public class MonsterFile
    {
        public List<AnimationSlot2> slots = new List<AnimationSlot2>(15);

        // TODO:  Add settings

        public void SaveFile(string filename)
        {

                XmlSerializer serializer = new XmlSerializer(typeof(MonsterFile));
                TextWriter textWriter = new StreamWriter(filename);
                serializer.Serialize(textWriter, this);
                textWriter.Close();
        }

        public string[] outputName = new string[16];
        public int SlotSetting { get; set; }
        public int PlaybackMode { get; set; }
        public bool AmbientMode { get; set; }

        public int[] TriggerThreshold = new int[4];
        public int[] TriggerSensitivity = new int[4];
        public int[] TriggerCooldown = new int[4];
        public bool[] TriggerOnHigh = new bool[4];
        public bool[] TriggerIgnoreUntilReset = new bool[4];

        /*
        public int TriggerThreshold { get; set; }
        public int TriggerSensitivity { get; set; }
        public int TriggerCooldown { get; set; }
        public bool TriggerOnHigh { get; set; }
        public bool TriggerIgnoreUntilReset { get; set; }

         * 
         * */


        public int TotalEventsPerSlot { get; set; }   // Dynamic...

        public bool eeprom1 { get; set; }
        public bool eeprom2 { get; set; }

        public void setTotalEventsPerSlot(int slots)
        {
            int memory = 32768;
            int MONSTERSHIELD_CONTROLPAGE_SIZE = 128;
            int MONSTERSHIELD_BUFFER_SIZE = 64;
            int memorySlotCount0 = 0;
            int memorySlotCount1 = 0;
            int memorySlotCount2 = 0;
            if ((eeprom1 == true && eeprom2 == false) || (eeprom1 == false && eeprom2 == true))
            {
                // 2 memory chips

                memorySlotCount0 = (int)(slots / 2);
                memorySlotCount1 = slots - memorySlotCount0;
                memorySlotCount2 = 0;

                TotalEventsPerSlot = ((((memory - MONSTERSHIELD_CONTROLPAGE_SIZE) / Math.Max(memorySlotCount0, memorySlotCount1)) / MONSTERSHIELD_BUFFER_SIZE) * MONSTERSHIELD_BUFFER_SIZE) / 2;

            }
            else if (eeprom1 == true && eeprom2 == true)
            {
                // 3 memory chips

                memorySlotCount0 = (int)(slots / 3);
                memorySlotCount1 = (int)((slots - memorySlotCount0) / 2);
                memorySlotCount2 = slots - (memorySlotCount0 + memorySlotCount1);

                TotalEventsPerSlot = ((((memory - MONSTERSHIELD_CONTROLPAGE_SIZE) / Math.Max(Math.Max(memorySlotCount0, memorySlotCount1), memorySlotCount2)) / MONSTERSHIELD_BUFFER_SIZE) * MONSTERSHIELD_BUFFER_SIZE) / 2;
            }
            else
            {
                // 1 memory chip

                memorySlotCount0 = slots;
                memorySlotCount1 = 0;
                memorySlotCount2 = 0;


                TotalEventsPerSlot = ((((memory - MONSTERSHIELD_CONTROLPAGE_SIZE) / slots) / MONSTERSHIELD_BUFFER_SIZE) * MONSTERSHIELD_BUFFER_SIZE) / 2;
            }
            
            Console.WriteLine("TotalEventsPerSlot={0}", TotalEventsPerSlot);
        }

        public void ConvertOldFormat(List<AnimationSlot> oldslots)
        {
            int i = 0;
            foreach (AnimationSlot oldslot in oldslots)
            {
                slots[i].Enabled = oldslot.Enabled;
                slots[i].MP3File = oldslot.MP3File;

                // Old format used 1 byte per command and 1 byte per delay offset.

                int k = 0;
                for (int j = 0; j < oldslot.AnimationCommandLength; j++)
                {
                    Console.WriteLine("Cmd={0} Delay={1}", oldslot.commands[j], oldslot.delays[j]);
                    //long index = oldslot.delays[j] * 10;
                }


                int idx = 0;
                long timeindex = 0;
                long nextcommandtime = timeindex + (oldslot.delays[idx+1] * 10);
                for (int j = 0; j < slots[i].cmd1.Length; j++)
                {
                    byte currentcmd = oldslot.commands[idx];

                    slots[i].cmd1[j] = (byte)(0xF0 | currentcmd);

                    timeindex += 50; // each command is 50 ms (or 1/20 of a second)
                    if (timeindex >= nextcommandtime)
                    {
                        idx += 1;
                        nextcommandtime = timeindex + (oldslot.delays[idx+1] * 10);
                        if (idx > oldslot.AnimationCommandLength - 1)
                            break;
                    }

                }
                slots[i].AnimationEnd = nextcommandtime / 50;
                i++; // next slot!
            }
        }

    }
}
