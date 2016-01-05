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


  MonsterShield Firmware Library V 1.5.0 {BETA}
  //////////////////////////////////////////////////////////////// 
  Released:   September 4, 2013
  Author:     Jason LeSueur Tatum
  
  HauntSoft 
  http://www.hauntsoft.com
  P.O. Box 1475
  Arlington Heights, IL 60006
  
  This library is intended to be used with the MonsterShield line of
  products produced by HauntSoft and is provided AS-IS.  This
  code has only been tested with Arduino UNO REV 3 microcontrollers,
  but may work with other Arduino compatible boards.
  
  The MonsterShield firmware is built around the "MonsterShield"
  class that provides access to all functionality and hardware.
  
  You are free to use this firmware in any way you see fit.  It 
  is as easy as instantiating a MonsterShield class object and 
  calling the "init()" function in your setup() routine.
  
  Tutorials and example solutions using this library
  will be made available over time on http://www.hauntsoft.com
  
  
  Arduino UNO R3 Pin Usage
  ===================================
  Digital I/O Pins
  ===================================
  13 LED2
  12 Keypad 0 (Prev      / Out 1)
  11 Keypad 1 (Next      / Out 2)
  10 Keypad 2 (Enable    / Out 3)
  9  Keypad 3 (Play/Stop / Out 4)
  8  Keypad 4 (Record)
  7  <---- Unused
  6  <---- Unused
  5  <---- Unused
  4  <---- Unused
  3  TX (Serial to MP3 module)
  2  RX (Serial from MP3 module, not connected)
  1  TX (Serial to host computer)
  0  RX (Serial to host computer)
  
  ===================================
  Analog I/O Pins
  ===================================
  A0 Trigger 0 (MonsterShield)
  A1 Trigger 1 (MonsterShield Expander)
  A2 Trigger 2 (MonsterShield Expander)
  A3 Trigger 3 (MonsterShield Expander)
  A4  SCL (i2c bus)
  A5  SDA (i2c bus)
  
  
  How to use the MonsterShield with the 1.5.0 firmware:
  =====================================================
  There are a few differences in how the MonsterShield 
  works with this new firmware compared to previous versions of the firmware.
  This is a quick guide to the new changes:
  
  Factory Reset:
  --------------
  1. Hold down the middle (prev/enable) button while pressing reset
  2. Release middle (prev/enable) button when "F" flashes rapidly
  3. When "F" flashes once per second, select number of desired slots
     by scrolling up and down with prev and nxt buttons.  Confirm
     desired number of slots by pressing mode/trigger button.
     Available options are: 1,2,3,4,5,6,7,8,9,A,B,C,D,E,F
     Note: If no selection is made for 10 seconds, then the value displayed
     will automatically be selected.
  4. Factory Reset will complete and the display will show "0" when done.
  
  Maximum slot length:
  ---------------------
  The maximum length of animation per slot depends on 2 factors:   
  1.  The number of slots the MonsterShield is configured for.
      You can perform a factory reset to change this number.  You can
      select 1 through 15 slots.
  2.  If you have the MonsterShield Expander board, you can increase
      the available slot length depending on whether you have 
      1, 2, or 3 total EEPROM chips installed.
  3.  A future version of the firmware will allow for larger EEPROM chips,
      but right now it is assumed that only the 24LC256 (32kbyte) chip
      is supported.
      
      Note:  The reason there is no difference between some slot
      numbers and/or chips is because we do not allow an animation
      to be split across multiple EEPROM chips.  We also enforce
      that all slots are equally sized.  There is also a requirement
      that each slot be divided evenly in 64-byte page increments.
      
This chart shows you your maximum available slot lengths:
slots	1 chip   2 chips  3 chips
15    00:54.4	 0:01:41  0:02:43
14    00:57.6	 0:01:55  0:02:43
13    01:02.4	 0:01:55  0:02:43
12    01:07.2	 0:02:16  0:03:23
11    01:13.6	 0:02:16  0:03:23
10    01:21.6	 0:02:43  0:03:23
9     01:29.6	 0:02:43  0:04:32
8     01:40.8	 0:03:23  0:04:32
7     01:55.2	 0:03:23  0:04:32
6     02:16.0	 0:04:32  0:06:48
5     02:43.2	 0:04:32  0:06:48
4     03:23.2	 0:06:48  0:06:48
3     04:32.0	 0:06:48  0:13:36
2     06:48.0	 0:13:36  0:13:36
1     13:36.0	 0:13:36  0:13:36
 
  Recording animation with the keypad:
  --------------------
  With previous versions of the firmware, all you had to do is press the 
  "record" button on the keypad and begin pressing the output buttons
  to record the animation.  With the new firmware, things are a bit
  more complex because there we've greatly increased the capabilities:
  
  * The new firmware supports the addition of up to 3 extra relay banks 
    (each bank contains 4 output relays) for a total of 16 relays when
    the MonsterShield Expander board is employed.
    
  * You can now record a single relay output at a time without affecting
    animation already recorded on the other tracks.  
    
  * You can now lay down additional recording on a track (overlay) 
    without losing the animation already stored on that track.  This can be
    used to extend an animation longer (up to the maximum length as given
    in the chart above)
    
  Recording steps:
  1.  Press the "record" button on the keypad.
  2.  "b" begins flashing.  This is bank-select mode.  Press 
      out 1, out 2, out 3, or out 4 to select banks 0, 1, 2, or 3.  Press
      "record" again to confirm.
  3.  "r" begins flashing.  This is relay-select mode.  Press
      out 1, out 2, out 3, or out 4 to select which relay on the selected
      bank you want to record.  If you'd like to record all 4 relays
      at the same time (like the old firmware), then skip this selection
      and just press "record" again.
  4.  "c" begins flashing.  This is the clear-option.  
      i.  Press out 1 to select overlay mode (record on top of track data)
      ii. Press out 2 to select clear track mode (erase selected track)
      iii.Press out 3 to select clear entire animation slot.
        
            
  
  
  
  
     