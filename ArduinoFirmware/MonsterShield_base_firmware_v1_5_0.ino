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
#include <EEPROM.h>
#include <Wire.h>
#include <SoftwareSerial.h>

#include "MonsterShield.h"
//#include <Adafruit_PWMServoDriver.h>

MonsterShield monster;


//Adafruit_PWMServoDriver pwm = Adafruit_PWMServoDriver();
//#define SERVOMIN  300 // this is the 'minimum' pulse length count (out of 4096)
//#define SERVOMAX  500 // this is the 'maximum' pulse length count (out of 4096)



void setup()
{
  Serial.begin(115200);    // Used for host computer communication  
  monster.init();


  //pwm.begin();
  //pwm.setPWMFreq(60);  // Analog servos run at ~60 Hz updates
  

}

void loop()
{
  
  boolean wasTriggered = monster.isTriggerSensed(0);
  boolean modeButton = monster.inputButtonPress(0);
  boolean triggeredFromAmbient = false;
  
  monster.processSerialPort(); // Allow for upload / download of animation and other events to automatically be handled.
  
  ///////////////////////////////////////////
  // If Ambient mode is turned on 
  ///////////////////////////////////////////
  if (monster.getPlayAmbient() == true)
  {
    // Play animation!  Parameters:  slot, repeat, ambient, bit mask for triggers, bit mask for input buttons, bit mask for keypad buttons
    int rc = monster.playAnimationWithInterrupts(0, true, true, 0x01, 0xFF, 0x08);
    if (rc == MONSTERSHIELD_INT_BUTTON_2)
    {
        monster.setPlayAmbient(false);
        triggeredFromAmbient = false;
    }
    else
    {
      monster.playAnimationWithInterrupts(monster.getActiveSlot(), false, false, 0x00, 0x01, 0x08);
    }
  }
  else
  {
  
  
    ////////////////////////////////////////////////////////////
    // Check for Manual playback from MonsterShield button 0 or keypad play button
    ////////////////////////////////////////////////////////////
    if (modeButton == HIGH || monster.keypadButtonPress(MONSTERSHIELD_KEYPAD_PLAY) == HIGH || wasTriggered == true || triggeredFromAmbient == true)
    {
      if (modeButton == HIGH && monster.inputButtonPressLength(0) > 500)
      {
        // Change playback modes
        monster.setNextAnimationSelectMode();
      }
      else
      {
        // Play animation!  Parameters:  slot, repeat, bit mask for triggers, bit mask for input buttons, bit mask for keypad buttons
        //Serial.println("@T0"); //Uncomment to make a slave MonsterShield play along!
        monster.playAnimationWithInterrupts(monster.getActiveSlot(), false, false, 0x00, 0x01, 0x08);
      }
    }
    //////////////////////////////////////////////////////////////
    // Check for previous slot button press or long press to toggle enable
    /////////////////////////////////////////////////////////////
    else if (monster.inputButtonPress(1) == HIGH || monster.keypadButtonPress(MONSTERSHIELD_KEYPAD_PREV) == HIGH)
    {
      if (monster.inputButtonPressLength(1) > 350)
      {
        int slot = monster.getActiveSlot();
        bool enabled = monster.getSlotEnabled(slot);
        monster.setSlotEnabled(slot, !enabled);
        
        monster.setDigitToActiveSlot();
      }
      else
      {
          monster.setPreviousSlot();
      }
    }
  
    //////////////////////////////////////////////////////////////
    // Check for next slot button press
    //////////////////////////////////////////////////////////////
    else if (monster.inputButtonPress(2) == HIGH)
    {
      if (monster.inputButtonPressLength(2) > 500)
      {
        monster.setPlayAmbient(true);
      }
      else
      {
        monster.setNextSlot();
      }
    }
    else if (monster.keypadButtonPress(MONSTERSHIELD_KEYPAD_NEXT) == HIGH)
    {
      monster.setNextSlot();
    }
    
    /////////////////////////////////////////////////
    // Check for toggle of slot enable from keypad
    ////////////////////////////////////////////////
    else if (monster.keypadButtonPress(MONSTERSHIELD_KEYPAD_ENABLE) == HIGH)
    {
      int slot = monster.getActiveSlot();
      bool enabled = monster.getSlotEnabled(slot);
      monster.setSlotEnabled(slot, !enabled);
      monster.setDigitToActiveSlot();
    }
  
    ///////////////////////////////////////////////////
    // Check if record button is pressed from keypad!
    ///////////////////////////////////////////////////
    if (monster.keypadButtonPress(MONSTERSHIELD_KEYPAD_RECORD) == true)
    {
      // When calling recordAnimation(), the user will be prompted to make 3 choices:
      // 1.  First, the MonsterShield will begin flashing "b" and "-".  This is the bank selection menu.
      //      Choose 0, 1, 2, or 3 from the keypad (the out 1, out 2, out 3, and out 4 buttons on the keypad) and 
      //      then press the record button again.
      //
      // 2.  Next, the MonsterShield will begin flashing "r" and "-".  This is the relay selection menu.
      //      Press out 1, out 2, out 3, or out 4 followed by the record button to only record that track on that bank.
      //      You can also choose to program all 4 tracks on the selected bank at the same time by just pressing the
      //      record button without pressing any of the other buttons on the keypad.
      // 
      // 3.  Finally, the MonsterShield will begin flashing "c" and "-".  This is the record mode menu.
      //      Pressing out 1 and record will overlay recording on what was already previously recorded.
      //      Pressing out 2 and record will wipe the selected track(s) while keeping everything else.
      //      Pressing out 3 and record will wipe the entire animation slot and start from scratch.
      monster.recordAnimation(monster.getActiveSlot());
    }
    
  }
  
  
}





