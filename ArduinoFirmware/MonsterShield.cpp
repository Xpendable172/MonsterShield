/*
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
  
*/

#include <Wire.h>
#include <Arduino.h> 
#include "MonsterShield.h"

// Software Serial object (Configure so we can use different digital I/O pins to control the MP3 player 
// and still use the normal serial TX & RX pins for commmunication to host computer.
SoftwareSerial MP3SERIAL(2, 3); // RX, TX

////////////////////////////////////////////////////////////////////////////////////////////
// MonsterShield::MonsterShield    
//    (Constructor)
////////////////////////////////////////////////////////////////////////////////////////////
MonsterShield::MonsterShield()
{
    // EEPROM i2c device addresses.  Do not ever change these!
    i2cMemoryChip = 0x54;
    i2cMemoryChip1 = 0x53;  // Expander board EEPROM 1
    i2cMemoryChip2 = 0x55;  // Expander board EEPROM 2
    
    i2cDataArray[0] = 0x00;
    i2cDataArray[1] = 0x00;
    expanderBankA = 0x12;     // Refers to the 8 "A" pins on the MCP23017
    expanderBankB = 0x13;     // Refers to the 8 "B" pins on the MCP23017
    expanderUnit0 = 0x20;     // MonsterShield MCP23017 address
    expanderUnit1 = 0x27;     // Expander board MCP23017 address
    relayStates = 0xFFFF;
    showDigitDecimal = false;
    triggerStates = 0x00;
    
    // Default trigger settings when a factory reset is performed.
    for (int i=0; i < 4; i++)
    { 
      triggerLastStateChange[i] = 0;
      triggerSensitivity[i] = 50;
      triggerThreshold[i] = 950;
      triggerResetState[i] = false;
      triggerCooldown[i]=5; 
      triggerAfterResetOnly[i] = true;
      triggerOnVoltage[i] = true;
    }
    controlPageAddress = MONSTERSHIELD_CONFIG_ADDRESS;
    slotEnabled = 0xFF;
    slotCount = 15;
    trackMask = 0xFF;
}


////////////////////////////////////////////////////////////////////////////////////////////
// This routine should be called in your setup() Arduino function.  If you don't call 
// this, the MonsterShield library isn't going to work properly.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::init()
{
  Wire.begin();   // start I2C
  initPortExpander(expanderUnit0, 0xE0, 0x00);
  initPortExpander(expanderUnit1, 0x00, 0x00);
  setPortExpanderOutput(expanderUnit0, expanderBankA, 0xFF);
  setPortExpanderOutput(expanderUnit1, expanderBankA, 0xFF);
  setPortExpanderOutput(expanderUnit1, expanderBankB, 0xFF);
  setDigitChar('-');
  setLed(1, false);
  pinMode(13, OUTPUT);
  pinMode(12, INPUT_PULLUP);
  pinMode(11, INPUT_PULLUP);
  pinMode(10, INPUT_PULLUP);
  pinMode(9, INPUT_PULLUP);
  pinMode(8, INPUT_PULLUP);
  eepromReadSettings();
  
  if (watermark[0] == 0x19 && watermark[1] == 0x74 && watermark[2] == 0x67 && watermark[3] == 0x91)
  {
    // The watermark is good!
    
    // Check to see if the prev/enable button is held down.  If it is, FORMAT the memory chip!
    // This forces us to run the same code that would be run if we had installed a brand new 
    // EEPROM chip.  You can think of this as being a "factory reset".
    if ( getInputButtonState(1) == HIGH)
    {
      Serial.println("%MANUAL FORMAT OF CHIP SELECTED!");
      performFactoryReset();
    }
    else
    {
      eventsPerSlot = calculateEventsPerSlot(slotCount);
    }
  }
  else
  {
    // Wrong or missing watermark.  Assume we need to init the EEPROM because it may be corrupted or
    // it might be a brand new chip that was installed.  If we don't do this, then 
    // the main loop will likely freak out and the Arduino will not function right.
    
    Serial.println("%No watermark found! Initializing EEPROM memory chip!");
    performFactoryReset();
  }
  
  ambientPlayFlag = false;
  MP3SERIAL.begin(4800);  // Used to control MP3 player on pins 3 (TX) and 2 (RX).
  mp3Volume(31);  // Turn the volume up to max on the MP3 player.
  delay(250);     // Slight delay to allow the MP3 player to process command.
  mp3Stop();      // Make sure the MP3 player isn't playing anything when we power up.

  setDigitToActiveSlot();
  
  Serial.println("%Init complete.");

}

////////////////////////////////////////////////////////////////////////////////////////////
// Sets an individual relay bit flag.  Don't forget that you have to call
// latchRelays() to actually push the state changes to the MCP23017 (and hence the relays!)
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setRelay(uint8_t relay, uint8_t value)
{
  if (value == HIGH)
  {
    bitClear(relayStates, relay);
  }
  else
  {
    bitSet(relayStates, relay);
  }
}

////////////////////////////////////////////////////////////////////////////////////////////
// Sets all of the relays to passed in value.  Also latches them, pushing the states
// to the MCP23017 chips.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setAllRelays(uint8_t value)
{
  for (int i = 0; i < 16; i++)
  {
    setRelay(i, value);
  }
  latchRelays();
}


////////////////////////////////////////////////////////////////////////////////////////////
// Calling this method will write the output states to the MCP23017 chip(s), effectively 
// latching each relay on or off.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::latchRelays()
{
  uint8_t data = ((lowByte(relayStates) << 4) >> 4);
  if (led1Indicator == false) bitClear(data, 4);
  setPortExpanderOutput(expanderUnit0, expanderBankA, data); 
  setPortExpanderOutputs(expanderUnit1, (relayStates >> 4));
}


////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::waitOnTrigger(int trigger)
{
  while (isTriggerSensed(trigger) == false)
  {
    delay(5);
  }
}

////////////////////////////////////////////////////////////////////////////////////////////
// Canned routine to determine if a trigger has been sensed, taking into account the follwing factors:
// threshold (voltage), sensitivity (debounce), cooldown, trigger on HIGH vs. LOW, trigger on state reset only
// returns true if the trigger was sensed.
////////////////////////////////////////////////////////////////////////////////////////////
bool MonsterShield::isTriggerSensed(uint8_t trigger)
{
  bool rc = false;
  uint16_t value = analogRead(trigger);
  boolean seeTrigger = false;

  if (triggerOnVoltage[trigger] == true)
  {
    // Trigger on HIGH voltage
    if (value >= triggerThreshold[trigger])
    {
      if ((triggerAfterResetOnly[trigger] == true && triggerResetState[trigger] == false) || triggerAfterResetOnly[trigger] == false)
        seeTrigger = true;
    }
    else
    {
      triggerLastStateChange[trigger] = millis();
      triggerResetState[trigger] = false;
    }
  }
  else
  {
    // Trigger on LOW voltage
    if (value < triggerThreshold[trigger])
    {
      if ((triggerAfterResetOnly[trigger] == true && triggerResetState[trigger] == false) || triggerAfterResetOnly[trigger] == false)
        seeTrigger = true;
    }
    else
    {
      triggerLastStateChange[trigger] = millis();
      triggerResetState[trigger] = false;
    }    
  }
  
  if (seeTrigger == true)
  {
    setLed(0, HIGH);  
    
    if (bitRead(triggerStates, trigger) == LOW)
    {
      bitSet(triggerStates, trigger);
      //triggerStates[trigger] = true;
      triggerLastStateChange[trigger] = millis();
    }
  }
  else
  {
    setLed(0, LOW);

    //triggerStates[trigger] = false;
    bitClear(triggerStates, trigger);
  }
  
  if (bitRead(triggerStates, trigger) == HIGH  
  && ((millis() - triggerResetTimer) > (triggerCooldown[trigger]*1000))  
  && (millis() - triggerLastStateChange[trigger] > (triggerSensitivity[trigger] * 10))  )
  {
    bitClear(triggerStates, trigger);
    triggerResetState[trigger] = true;
    rc = true;
  }

  return rc;
}


////////////////////////////////////////////////////////////////////////////////////////////
// Returns the current state of the on-board buttons.
// Used mostly internally.  Recommend end-users to use inputButtonPress() instead.
////////////////////////////////////////////////////////////////////////////////////////////
uint8_t MonsterShield::getInputButtonState(uint8_t button)
{
  uint8_t rc = LOW;

  if ((millis() - inputButtonStatesLastCheck) > MONSTERSHIELD_INPUT_BUTTON_CHECK_DELAY)
  {
    // Grab an update for the inputs!
    Wire.beginTransmission(expanderUnit0);
    Wire.write(expanderBankA);
    Wire.endTransmission();
    Wire.requestFrom(expanderUnit0, (uint8_t)1);
    inputButtonStates = (Wire.read() >> 5);
    Wire.endTransmission();
    
    //Serial.println(inputButtonStates, BIN);
    
    inputButtonStatesLastCheck = millis();
  }

  // Return HIGH / LOW based on which bit is requested.
  rc = bitRead(inputButtonStates, button);
    
  return rc;
}

////////////////////////////////////////////////////////////////////////////////////////////
// Sets the 7-segment display to the character defined in the code.
// Valid characters are: '0' '1' '2' '3' '4' '5' '6' '7' '8' '9' 'A' 'B' 'C' 'D' 'E' 'F' ' ' 'P' '-' 'r'
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setDigitChar(char c)
{
  digitDisplay = getDigitBitmaskChar(c);
  if (showDigitDecimal) bitSet(digitDisplay, 7);
  setPortExpanderOutput(expanderUnit0, expanderBankB, digitDisplay ); 
}

////////////////////////////////////////////////////////////////////////////////////////////
// Numeric table index of a character for the 7-segment display.  See the MonsterShield.cpp for details.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setDigit(uint8_t value)
{
  digitDisplay = getDigitBitmask(value);
  if (showDigitDecimal) bitSet(digitDisplay, 7);
  setPortExpanderOutput(expanderUnit0, expanderBankB, digitDisplay ); 
}

////////////////////////////////////////////////////////////////////////////////////////////
// Display the currently active slot on the 7-segment display.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setDigitToActiveSlot()
{
  showDigitDecimal = getSlotEnabled(currentSlot);
  setDigit(currentSlot);
}

////////////////////////////////////////////////////////////////////////////////////////////
// Function to turn either LED 1 or LED 2 on or off.  LED1 doesn't work right
// with this function.  TODO:  Fix LED1 code!
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setLed(uint8_t led, bool value)
{
  if (led == 1)
  {
    // WARNING!  THIS CODE IS NOT WORKING RIGHT!
    led1Indicator = value;
    uint8_t data = ((lowByte(relayStates) << 4) >> 4);
    if (led1Indicator == false) bitClear(data, 4);
    setPortExpanderOutput(expanderUnit0, expanderBankA, data); 
  }
  else
  {
    digitalWrite(13, value);
  }
  
}

////////////////////////////////////////////////////////////////////////////////////////////
// Sets MP3 volume. Minimum is 0, max is 31
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::mp3Volume(uint8_t volume)
{
  if (volume > 31) volume = 31;
  volume += 200;
  MP3SERIAL.write(volume);
  delay(250);
}

////////////////////////////////////////////////////////////////////////////////////////////
// Plays selected MP3 file #.  Note that the MP3 player has a weird nuance in that
// it numbers files in the order they were uploaded to the SD card.  Strange but true.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::mp3Play(uint8_t index)
{
  if (index > 199) index = 199;
  MP3SERIAL.write(index);
}

////////////////////////////////////////////////////////////////////////////////////////////
// Stops the MP3 player.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::mp3Stop()
{
  MP3SERIAL.write(0xEF);
  delay(250);
}

////////////////////////////////////////////////////////////////////////////////////////////
// Pauses the MP3 player.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::mp3Pause()
{
  MP3SERIAL.write(0xEB);
}

////////////////////////////////////////////////////////////////////////////////////////////
// Resumes the MP3 player.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::mp3Resume()
{
  MP3SERIAL.write(0xEC);
}

////////////////////////////////////////////////////////////////////////////////////////////
  // Canned routine to play animation slot.  Takes care of everything for you.  Function returns
  // when routine is complete.  Does not allow for interrupts or button presses during playback.
  // If you want to do other processing (such as triggers, inputs, etc) during playback, then
  // you should use the playAnimationWithInterrupts() function or use the stepAnimationStart()
  // and stepAnimationNext functions.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::playAnimation(int slot)
{
  stepAnimationStart(slot);
  bool cont = true;
  while (stepAnimationNext() && cont == true)
  {
    // You COULD do something here, but you shouldn't modify this function.  It's
    // better to create your own function outside of this class and just call
    // stepAnimationStart() and then call stepAnimationNext() in a loop, using
    // this function as an example.
  }
  Serial.println("$00"); // Tell host that play has completed.
  setAllRelays(LOW);
  latchRelays();
  mp3Stop();
  selectNextAnimation();
  setDigitToActiveSlot();
  
  // Reset timer for trigger cooldown.  This prevents prop from being triggered
  // too often (user configurable cooldown delay)
  triggerResetTimer = millis();
}


////////////////////////////////////////////////////////////////////////////////////////////
// This is another canned animation playback routine, but this one was built-in handling
// for trigger interrupts, on-board button press interrupts, detachable keypad interrupts, and
// serial interrupts.  If an interrupt is encountered during playback, playback is stopped
// and the function returns an 8-bit integer that corresponds to the type of interrupt
// that occurred.
//
// Parameters:
//    int slot            = the animation slot number to be played
//    bool repeat         = Should this animation be looped?
//    bool ambient        = Should this flash the "A" on the 7-segment display to indicate ambient animation?
//    uint8_t triggermask = Mask to indicate which triggers to allow.  Example:  0b1111 Allows all 4 triggers.  0b0001 only allows trigger 0.
//    uint8_t inputmask   = Mask to indicate which on-board buttons to allow.  Example:  0b111 allows all 3 buttons.  0b101 allows button 0 & 2.
//    uint8_t keypadmask  = Mask to indciate which keypad buttons to allow.  Example:  0b11111 allows all 5 buttons.
// 
// Return codes:
//   MONSTERSHIELD_TRIGGER_0    0
//   MONSTERSHIELD_TRIGGER_1    1
//   MONSTERSHIELD_TRIGGER_2    2
//   MONSTERSHIELD_TRIGGER_3    3
//   MONSTERSHIELD_BUTTON_0     10
//   MONSTERSHIELD_BUTTON_1     11
//   MONSTERSHIELD_BUTTON_2     12
//   MONSTERSHIELD_KEYPAD_0     20
//   MONSTERSHIELD_KEYPAD_1     21
//   MONSTERSHIELD_KEYPAD_2     22
//   MONSTERSHIELD_KEYPAD_3     23
//   MONSTERSHIELD_KEYPAD_4     24
//   MONSTERSHIELD_SERIAL       100
//   MONSTERSHIELD_UNKNOWN      255
//
////////////////////////////////////////////////////////////////////////////////////////////
uint8_t MonsterShield::playAnimationWithInterrupts(int slot, bool repeat, bool ambient, uint8_t triggermask, uint8_t inputmask, uint8_t keypadmask)
{
  uint8_t rc = MONSTERSHIELD_INT_UNKNOWN;
  bool cont = true;
  while (cont == true)
  {

    stepAnimationStart(slot, ambient);
      
    while (stepAnimationNext() && cont == true)
    {
      
      // Check to see if the animation should be interrupted
      for (int i=0; i < 4; i++)
      {
        if (bitRead(triggermask, i) == HIGH)
        {
          if (isTriggerSensed(i) == true) 
          {
            cont = false;
            rc = i;
          }
        }
      }
  
      for (int i=0; i < 3; i++)
      {
        if (bitRead(inputmask, i) == HIGH)
        {
          if (inputButtonPress(i) == true) 
          {
            cont = false;
            rc = i + 10;
            if (i == 2) setPlayAmbient(false);
          }
         
        }
      }
  
      if (bitRead(keypadmask, 0) == HIGH && keypadButtonPress(12) == HIGH)
      { 
        cont = false;
        rc = MONSTERSHIELD_INT_KEYPAD_0;
      }
      
      if (bitRead(keypadmask, 1) == HIGH && keypadButtonPress(11) == HIGH) 
      {
        cont = false;
        rc = MONSTERSHIELD_INT_KEYPAD_1;
      }
      
      if (bitRead(keypadmask, 2) == HIGH && keypadButtonPress(10) == HIGH) 
      {
        cont = false;
        rc = MONSTERSHIELD_INT_KEYPAD_2;
      }
      
      if (bitRead(keypadmask, 3) == HIGH && keypadButtonPress(9) == HIGH) 
      {
        cont = false;
        rc = MONSTERSHIELD_INT_KEYPAD_3;
      }
      
      if (bitRead(keypadmask, 4) == HIGH && keypadButtonPress(8) == HIGH) 
      {
        cont = false;
        rc = MONSTERSHIELD_INT_KEYPAD_4;
      }
      
      if (processSerialPort() == 0xFF) 
      {
        cont = false;
        rc = MONSTERSHIELD_INT_SERIAL;
      }
      
    }
    if (repeat == false) cont = false;
  }
  Serial.println("$00"); // Tell host that play has completed.
  setAllRelays(LOW);
  latchRelays();
  mp3Stop();
  selectNextAnimation();
  setDigitToActiveSlot();
  
  // Reset timer for trigger cooldown.  This prevents prop from being triggered
  // too often (user configurable cooldown delay)
  triggerResetTimer = millis();
  
  return rc;
}

////////////////////////////////////////////////////////////////////////////////////////////
// You use this routine if you want to do some of your own processing while an animation is 
// playing.  Basically you call stepAnimationStart() to initialize everything for playback, 
// and then you call stepAnimationNext() in a loop checking the return code. 
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::stepAnimationStart(int slot)
{
  stepAnimationStart(slot, false);
}

////////////////////////////////////////////////////////////////////////////////////////////
// Equivalent to calling stepAnimationStart(int slot, true)
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::stepAnimationAmbientStart(int slot)
{
  stepAnimationStart(slot, true);
}

////////////////////////////////////////////////////////////////////////////////////////////
// Same as stepAnimationStart(int slot) except it takes an extra flag that causes "A" to
// flash on the display instead of the normal slot number.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::stepAnimationStart(int slot, bool ambientFlag)
{
  playingSlot = slot;

  eepromReadSettings();
  
  // Let host computer know that we got a trigger!
  Serial.print("$T");
  Serial.println(slot);

  setDigit(slot);

  setPortExpanderOutput(expanderUnit0, expanderBankA, 0xFF);
  setPortExpanderOutput(expanderUnit1, expanderBankA, 0xFF);
  setPortExpanderOutput(expanderUnit1, expanderBankB, 0xFF);
  
  // Activate MP3 sound module with selected file!
  mp3Play(slot+1);
  
  sequencePosition = 0;
  currentPage = 0;
  currentByteCount = 0;
  eepromReadSlotData(slot, currentPage);
  ambientPlayFlag = ambientFlag;
  nextEventTime = millis() + MONSTERSHIELD_TIMING;  
  
}

////////////////////////////////////////////////////////////////////////////////////////////
// You call this function in a loop.  Each time you call stepAnimationNext(), it determines if it is time for the next 1/20th of a second event
// to occur, and if it is, it changes the state of the relays accordingly.  It returns true if the animation loop should continue.
// Here's an example of how you use this:
//    stepAnimationStart(1);  
//    while ( stepAnimationNext() == true )
//    {
//          // Do your stuff here
//    }
////////////////////////////////////////////////////////////////////////////////////////////
bool MonsterShield::stepAnimationNext()
{
  bool cont = false;
  
  byte cmd1;
  byte cmd2;
  byte data;
  
  if (millis() < nextEventTime)
  {
    // Do nothing!  Not yet time to to run the next event!
  }
  else
  {
    // Grab next command from table
    cmd1 = sequenceBuffer[sequencePosition];
    cmd2 = sequenceBuffer[sequencePosition+1]; 

    //Serial.println("0");
    data = cmd1 & 0B00001111; 
    bitSet(data, 4);      
    setPortExpanderOutput(expanderUnit0, expanderBankA, data);
      
    // V5 logic for additional relays
    data = cmd1 & 0B11110000;
    data = data >> 4;
    data = (cmd2 << 4) + data;
    
    setPortExpanderOutput(expanderUnit1, expanderBankA, data);
    
    data = cmd2 >> 4;
    setPortExpanderOutput(expanderUnit1, expanderBankB, data);
        
    Serial.print("$R");
    //Serial.write(sequenceBuffer+sequencePosition, 2);
    if (cmd1 < 0x10) Serial.print("0");
    Serial.print(cmd1, HEX);
    if (cmd2 < 0x10) Serial.print("0");
    Serial.print(cmd2, HEX);
    Serial.println("");
  
    if (millis() > blinker)
    {
      if (ambientPlayFlag)
      {
        if (flipflop)
          setDigitChar('A');
        else
          setDigit(currentSlot);
      }
      else
      {
        if (flipflop)
          setDigit(currentSlot); // P
        else
          setDigit(16); // clear
      }
      
      flipflop = !flipflop;
      blinker = millis() + MONSTERSHIELD_BLINK_PLAY;
    }
  
    sequencePosition += MONSTERSHIELD_BYTES_PER_EVENT;
    if (sequencePosition >= MONSTERSHIELD_BUFFER_SIZE)
    {
      // Load next page in
      currentPage += 1;
      eepromReadSlotData(playingSlot, currentPage);
      sequencePosition = 0;
    }
    
    currentByteCount += MONSTERSHIELD_BYTES_PER_EVENT;
    delay(1);
    
    // Calculate the time for the next event.
    nextEventTime += MONSTERSHIELD_TIMING;
    
  }
  
  if (currentByteCount < sequenceLength[playingSlot])
  {
    cont = true;
    if (ambientPlayFlag == false)
    {
      // Reset timer for trigger cooldown.  This prevents prop from being triggered
      // too often (user configurable cooldown delay)
      triggerResetTimer = millis();
    }
  }
  
  return cont;
}

////////////////////////////////////////////////////////////////////////////////////////////
// This is a canned routine to handle standard recording of animations to an animation slot.
// This routine prompts the user to answer 3 questions before recording begins:
//   1.  Which bank do you want to record?  (Flashes 'b')  Press 0, 1, 2, or 3 then press record.
//   2.  Which relay/track do you want to record?  (Flashes 'r').  Press record (for all) or 0, 1, 2, 3 then record
//   3.  Which recording mode?  (Flashes 'C').  Press 0, 1, or 2 then press record.  0 = overlay track.  1 = erase track.  2 = erase entire animation.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::recordAnimation(int slot)
{  
    int bank = GetKeypadMenuChoice('B', 4);    // User selects bank 0, 1, 2, or 3
    int track = GetKeypadMenuChoice('r', 4);   // User selects relay 0, 1, 2, 3 or - for all
    int mode = GetKeypadMenuChoice('C', 3);    // User selects clear mode: 0 = normal (overlay), 1 = clear track, 2 = clear entire slot

    delay(100);
    
    if (track != -1)
      stepRecordStart(slot, track+(bank*4), mode);
    else
      stepRecordStart(slot, -1, mode);
      
    digitalWrite(13, HIGH);
    bool abort = false;
    bank = bank * 4;
    
    while (stepRecordNext(abort))
    {

      if (track == 0 || track == -1) setRelay(0+bank, digitalRead(MONSTERSHIELD_KEYPAD_OUT1));
      if (track == 1 || track == -1) setRelay(1+bank, digitalRead(MONSTERSHIELD_KEYPAD_OUT2));
      if (track == 2 || track == -1) setRelay(2+bank, digitalRead(MONSTERSHIELD_KEYPAD_OUT3));
      if (track == 3 || track == -1) setRelay(3+bank, digitalRead(MONSTERSHIELD_KEYPAD_OUT4));

      
      if (keypadButtonPress(MONSTERSHIELD_KEYPAD_RECORD))
      {
        abort = true;
      }
      
    }
    digitalWrite(13, LOW);

    setSlotEnabled(slot, true);
    setDigitToActiveSlot();
    
    delay(1000);
}


////////////////////////////////////////////////////////////////////////////////////////////
// Initialize everything in preparation to live record an animation.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::stepRecordStart(int slot, int8_t track, int8_t recordmode)
{
  currentSlot = slot;
  recordMode = recordmode;

  _recordTrack = track;
  
  currentEventCount = 0;
  
  prevEventCount = sequenceLength[currentSlot] / MONSTERSHIELD_BYTES_PER_EVENT;
  if (recordMode == MONSTERSHIELD_RECORD_MODE_ERASE_ALL) prevEventCount = 0;
  
  currentPage = 0;
  sequencePosition = 0;
  
  // V5 - Read the existing data from the page.
  eepromReadSlotData(currentSlot, currentPage);
  
  setPortExpanderOutput(expanderUnit0, expanderBankA, 0xFF);
  setPortExpanderOutput(expanderUnit1, expanderBankA, 0xFF);
  setPortExpanderOutput(expanderUnit1, expanderBankB, 0xFF);

  // Record intial record of all relays being off V5
  sequenceBuffer[0] = 0xFF;
  sequenceBuffer[1] = 0xFF;
  
  // Increment position to next record.
  sequencePosition += MONSTERSHIELD_BYTES_PER_EVENT;
  currentEventCount += 1;
  
  relayStates = 0x00;
  
  nextEventTime = millis() + MONSTERSHIELD_TIMING;
  blinker = millis() + MONSTERSHIELD_BLINK_RECORD;
  
  // Start MP3 player playback on selected sequence number.
  mp3Play(currentSlot+1);
  
}

////////////////////////////////////////////////////////////////////////////////////////////
// Call this in a loop to record your animation and do your own actions inside the loop.
// Recommend you study the "recordAnimation()" function to see how this is done.
////////////////////////////////////////////////////////////////////////////////////////////
bool MonsterShield::stepRecordNext(bool abort)
{
  bool cont = true;
  unsigned long currentmillis;
  bool changes = false;
  byte nextcmd = 0x0F;
  byte nextcmd2 = 0xFF;
  byte mcpbankA = 0xFF;
  byte mcpbankB = 0xFF;
    
  if (!abort)
  {
    // Assume that track inputs have already been set.
    currentmillis = millis();
    if (currentmillis > nextEventTime)
    {
      changes = true;
      
      // If we are recording a fresh new animation, or if we moved past the 
      // previous end marker, initialize the relays for this event as off.     
      if (recordMode == MONSTERSHIELD_RECORD_MODE_ERASE_ALL || currentEventCount >= prevEventCount)
      {
        nextcmd = 0xFF;
        nextcmd2 = 0xFF;
        mcpbankA = 0xFF;
        mcpbankB = 0xFF;
      }
      else
      {
        // V5 - Initialize for overlay recording.
        nextcmd = sequenceBuffer[sequencePosition];
        nextcmd2 = sequenceBuffer[sequencePosition+1];
        
        if (recordMode == MONSTERSHIELD_RECORD_MODE_CLEAR_TRACK)
        {
          if (_recordTrack < 8)
            bitSet(nextcmd, _recordTrack);
          else
            bitSet(nextcmd2, (_recordTrack - 8));
        }
        
        mcpbankA = nextcmd >> 4;
        mcpbankA = mcpbankA + (nextcmd2 << 4 >> 4);
        mcpbankB = nextcmd2 >> 4; 
      }
      
      // Prep data for writing to EEPROM for first 8 relays
      byte halfword = lowByte(relayStates);
      for (int i=0; i < 8; i++)
      {
        if (_recordTrack == -1 || _recordTrack == i)
        {
          if (bitRead(halfword, i) == 1) bitClear(nextcmd, i);
        }
      }
      // Prep data for MCP23017 chip(s)
      if (bitRead(halfword, 4) == 1) bitClear(mcpbankA, 0);
      if (bitRead(halfword, 5) == 1) bitClear(mcpbankA, 1);
      if (bitRead(halfword, 6) == 1) bitClear(mcpbankA, 2);
      if (bitRead(halfword, 7) == 1) bitClear(mcpbankA, 3);
      
      // Prep data for writing to EEPROM for second set of 8 relays
      halfword = highByte(relayStates);
      Serial.print("==>");
      Serial.println(halfword, HEX);
      for (int i=0; i < 8; i++)
      {
        if (_recordTrack == -1 || _recordTrack == i+8)
        {        
          if (bitRead(halfword, i) == 1) bitClear(nextcmd2, i);
        }
      }
      if (bitRead(halfword, 8) == 1) bitClear(mcpbankA, 4);
      if (bitRead(halfword, 9) == 1) bitClear(mcpbankA, 5);
      if (bitRead(halfword, 10) == 1) bitClear(mcpbankA, 6);
      if (bitRead(halfword, 11) == 1) bitClear(mcpbankA, 7);
      if (bitRead(halfword, 12) == 1) bitClear(mcpbankB, 0);
      if (bitRead(halfword, 13) == 1) bitClear(mcpbankB, 1);
      if (bitRead(halfword, 14) == 1) bitClear(mcpbankB, 2);
      if (bitRead(halfword, 15) == 1) bitClear(mcpbankB, 3);
      
      //Serial.print(nextcmd, HEX);
      //Serial.print("[");
      //Serial.print(nextcmd2, HEX);
      //Serial.println("]");

      // Record information into memory tables
      sequenceBuffer[sequencePosition] = nextcmd;
      sequenceBuffer[sequencePosition+1] = nextcmd2;
      
      sequencePosition += MONSTERSHIELD_BYTES_PER_EVENT;
      currentEventCount += 1;
      if (currentEventCount > eventsPerSlot)
      {
        cont = false;
      } 
      
      // Did we fill a page?  Write it out!
      if (sequencePosition >= MONSTERSHIELD_BUFFER_SIZE) // switched to >= from = on 9/27/2012.  We would drop off the most recently written event without this!
      {
         eepromWriteSlotData(currentSlot, currentPage);
         sequencePosition = 0;

         currentPage += 1;
         
         // V5 - Read existing data from memory for overlay recording
         eepromReadSlotData(currentSlot, currentPage);
      }

      // V5 calculate the next event time. 3/28/2013
      nextEventTime += MONSTERSHIELD_TIMING;
            
    } // if currentmillis > nextEventTime
    
    if (changes)
    {
      byte data = nextcmd & 0b00001111;
      setPortExpanderOutput(expanderUnit0, expanderBankA, data);
      data = nextcmd & 0B11110000;
      data = data >> 4;
      data = (nextcmd2 << 4) + data;
      setPortExpanderOutput(expanderUnit1, expanderBankA, data);
      data = nextcmd2 >> 4;
      setPortExpanderOutput(expanderUnit1, expanderBankB, data);
    }  
    
    if (millis() > blinker)
    {
      if (flipflop)
      {
        setDigit(17); // P
        setLed(0, HIGH);
      }
      else
      {
        setDigit(16); // clear
        setLed(0, LOW);
      }
      
      flipflop = !flipflop;
      blinker = millis() + MONSTERSHIELD_BLINK_RECORD;
    }
    
  } // if !abort
  
  if (abort ||  cont == false)
  {
    Serial.println("Abort code");
    
    // Record information into memory tables
    sequenceBuffer[sequencePosition] = 0xFF; // Turn everything off.
    sequenceBuffer[sequencePosition+1] = 0xFF;
    
    sequencePosition += MONSTERSHIELD_BYTES_PER_EVENT;
    currentEventCount += 1;
    
    // Write the final page.
    if (sequencePosition > 0)
    {          
      eepromWriteSlotData(currentSlot, currentPage);
      currentPage += 1;
    }
    
    if (recordMode == MONSTERSHIELD_RECORD_MODE_ERASE_ALL || currentEventCount > prevEventCount)
    {
      sequenceLength[currentSlot] = currentEventCount*MONSTERSHIELD_BYTES_PER_EVENT;
    }

    bitSet(slotEnabled, currentSlot);
    eepromWriteSettings();
     
    setPortExpanderOutput(expanderUnit0, expanderBankA, 0xEF);
    setPortExpanderOutput(expanderUnit1, expanderBankA, 0xFF);
    setPortExpanderOutput(expanderUnit1, expanderBankB, 0xFF);
    setDigit(currentSlot);
    cont = false;  
  }
  
  return cont;
}

////////////////////////////////////////////////////////////////////////////////////////////
// Selects an animation based on the which playbackMode is active.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::selectNextAnimation()
{
  int firstSeq = 0;
  if (PlayAmbientAnimation)
  {
    firstSeq = 1;
  }

  switch (nextAnimSelectMode)
  {
    case 0:  // Sequential
      for (int seqcount = firstSeq; seqcount < slotCount; seqcount++)
      {
        currentSlot += 1;
        if (currentSlot > slotCount-1) currentSlot = firstSeq;
        if (getSlotEnabled(currentSlot) == true) break;
      }
      break;
      
    case 1: // random
      {
        boolean found = false;
        long randNumber;
        byte curseq = currentSlot;
        int highseq = slotCount - 1;
        // Make sure we have at least 1 sequence enabled!
        int searchcount = 0;
        while (!found && searchcount < highseq*5)
        {
          searchcount += 1;
          randNumber = random(firstSeq, highseq);
          if (randNumber != curseq)
          {
            if (getSlotEnabled(randNumber))
            {
              currentSlot = randNumber;
              found = true;
            }
          }
           
        }
      }     
      break;
      
    case 2: // single
      // Do nothing!  Stay on the same slot.
      break;
    
  }

  eepromWriteSettings();
}


////////////////////////////////////////////////////////////////////////////////////////////
// Initialize selected MCP23017 chip.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::initPortExpander(uint8_t unit, uint8_t ioflagsA, uint8_t ioflagsB)
{
  // for ioflagsA & ioflagsB, a "1" bit = input, a "0" bit = output
  Wire.beginTransmission(unit);
  Wire.write(0x00); // IODIRA register
  Wire.write(ioflagsA); // set all of bank A to outputs
  Wire.endTransmission();
    
  Wire.beginTransmission(unit);
  Wire.write(0x01); // IODIRB register
  Wire.write(ioflagsB); // set all of bank B to outputs
  Wire.endTransmission();
}

////////////////////////////////////////////////////////////////////////////////////////////
// Set an output state for one of the 2 banks on a MCP23017 chip.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setPortExpanderOutput(uint8_t address, uint8_t bank, uint8_t value)
{
    Wire.beginTransmission(address);
    Wire.write(bank); // address bank A
    Wire.write(value);  // value to send
    Wire.endTransmission();   
    //delay(5);
}

////////////////////////////////////////////////////////////////////////////////////////////
// Sets all 16 output states on a MCP23017 chip using 2 separate 8-bit integers.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setPortExpanderOutputs(uint8_t address, uint8_t valueA, uint8_t valueB)
{
  setPortExpanderOutput(address, expanderBankA, valueA);
  setPortExpanderOutput(address, expanderBankB, valueB);
}

////////////////////////////////////////////////////////////////////////////////////////////
// Set all 16 output states on a MCP23017 chip using a single 16-bit integer.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setPortExpanderOutputs(uint8_t address, uint16_t value)
{
  setPortExpanderOutput(address, expanderBankA, (value << 8 >> 8));
  setPortExpanderOutput(address, expanderBankB, (value>> 8));
}

////////////////////////////////////////////////////////////////////////////////////////////
// Gets an 8-bit mask corresponding to the character from a table.
////////////////////////////////////////////////////////////////////////////////////////////
uint8_t MonsterShield::getDigitBitmaskChar(char c)
{
  //                     0         1           2          3          4          5          6          7          8          9         A           B          C          D          E          F          OFF        P
//byte numbers[] = { B01111011, B01100000, B00110111, B01110101, B01101100, B01011101, B01011111, B01110000, B01111111, B01111100, B01111110, B01001111, B00011011, B01100111, B00011111, B00011110, B00000000, B00111110 };
  uint8_t rc = 0x00;
  
  switch(c)
  {
    case '0': rc = getDigitBitmask(0); break;
    case '1': rc = getDigitBitmask(1); break;
    case '2': rc = getDigitBitmask(2); break;
    case '3': rc = getDigitBitmask(3); break;
    case '4': rc = getDigitBitmask(4); break;
    case '5': rc = getDigitBitmask(5); break;
    case '6': rc = getDigitBitmask(6); break;
    case '7': rc = getDigitBitmask(7); break;
    case '8': rc = getDigitBitmask(8); break;
    case '9': rc = getDigitBitmask(9); break;
    case 'A': rc = getDigitBitmask(10); break;
    case 'B': rc = getDigitBitmask(11); break;
    case 'C': rc = getDigitBitmask(12); break;
    case 'D': rc = getDigitBitmask(13); break;
    case 'E': rc = getDigitBitmask(14); break;
    case 'F': rc = getDigitBitmask(15); break;
    case ' ': rc = getDigitBitmask(16); break;
    case 'P': rc = getDigitBitmask(17); break;
    case '-': rc = getDigitBitmask(18); break;
    case 'r': rc = getDigitBitmask(19); break;
  }

  return rc;
}

////////////////////////////////////////////////////////////////////////////////////////////
// Gets an 8-bit mask correpsonding to an index from a table.
////////////////////////////////////////////////////////////////////////////////////////////
uint8_t MonsterShield::getDigitBitmask(uint8_t value)
{
  uint8_t rc = 0x00;

  //     --d--     
  //  e |     | c
  //     --f--  
  //  g |     | b  .a
  //     --h-- 
  //  
  //    abcdefgh
  //   B01100000


  switch(value)
  {
    case 0:  rc = B01111011; break; // 0
    case 1:  rc = B01100000; break; // 1
    case 2:  rc = B00110111; break; // 2
    case 3:  rc = B01110101; break; // 3
    case 4:  rc = B01101100; break; // 4
    case 5:  rc = B01011101; break; // 5
    case 6:  rc = B01011111; break; // 6
    case 7:  rc = B01110000; break; // 7
    case 8:  rc = B01111111; break; // 8
    case 9:  rc = B01111100; break; // 9
    case 10: rc = B01111110; break; // A
    case 11: rc = B01001111; break; // b
    case 12: rc = B00011011; break; // C
    case 13: rc = B01100111; break; // d
    case 14: rc = B00011111; break; // E
    case 15: rc = B00011110; break; // F
    case 16: rc = B00000000; break; // space
    case 17: rc = B00111110; break; // P
    case 18: rc = B00000100; break; // -
    case 19: rc = B00000110; break; // r
    case 20: rc = B00110000; break; // d c
    case 21: rc = B01100000; break; // c b
    case 22: rc = B01000001; break; // b h
    case 23: rc = B00000011; break; // h g
    case 24: rc = B00001010; break; // g e
    case 25: rc = B00011000; break; // e d
    // TODO:
    // c
    // h
    // H
    // J
    // L
    // o
    // r
    // U
    // u
    // y
    // -
    // ||
    // _
    // ~
    // |_
    // _|
    // /
    // \
    
  }
  return rc;
}

////////////////////////////////////////////////////////////////////////////////////////////
// Simplifies reading animation data from the EEPROM chip(s).  All you have to do is indicate
// which slot you want to read data from, and which page (64 bytes) you want to read.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::eepromReadSlotData( uint8_t slot, uint16_t page)
{
  uint8_t chip = i2cMemoryChip;
  if (eeprom1 == true && eeprom2 == true)
  {
    if (slot > ((memorySlotCount0 + memorySlotCount1) - 1))
    {
       chip = i2cMemoryChip2;
    }
    else if (slot > memorySlotCount0 - 1)
    {
      chip = i2cMemoryChip1;
    }
  }
  else if (eeprom1 == true || eeprom2 == true)
  {
    if (slot > (memorySlotCount0 - 1))
    {
      if (eeprom1) chip = i2cMemoryChip1;
      if (eeprom2) chip = i2cMemoryChip2;
      
      slot = slot - memorySlotCount0;
    }
  }

  eepromReadPage(chip,  MONSTERSHIELD_CONTROLPAGE_SIZE+(page*64)+(eventsPerSlot*MONSTERSHIELD_BYTES_PER_EVENT*slot), sequenceBuffer);
}

////////////////////////////////////////////////////////////////////////////////////////////
// Writes 64 bytes of data to the selected EEPROM chip at memory_address 
// from the memory buffer at address *buff. 
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::eepromWriteSlotData( uint8_t slot, uint16_t page)
{
  
  uint8_t chip = i2cMemoryChip;
  if (eeprom1 == true && eeprom2 == true)
  {
    if (slot > ((memorySlotCount0 + memorySlotCount1) - 1))
    {
       chip = i2cMemoryChip2;
    }
    else if (slot > memorySlotCount0 - 1)
    {
      chip = i2cMemoryChip1;
    }
  }
  else if (eeprom1 == true || eeprom2 == true)
  {
    if (slot > (memorySlotCount0 - 1))
    {
      if (eeprom1) chip = i2cMemoryChip1;
      if (eeprom2) chip = i2cMemoryChip2;
      
      slot = slot - memorySlotCount0;
    }
  }  
  
  eepromWritePage(chip, MONSTERSHIELD_CONTROLPAGE_SIZE+(page*64)+(eventsPerSlot*MONSTERSHIELD_BYTES_PER_EVENT*slot), sequenceBuffer);
}


////////////////////////////////////////////////////////////////////////////////////////////
// Reads 64 bytes from the selected EEPROM chip at the selected address and writes them to the 
// byte buffer pointed to by *buff.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::eepromReadPage(uint8_t bus_address, uint16_t memory_address, uint8_t *buff)
{
  
  uint8_t bytelength = 32;
  int position = 0;
  
  Wire.beginTransmission(bus_address);
  Wire.write(highByte(memory_address));
  Wire.write(lowByte(memory_address));
  Wire.endTransmission();
  
  // Read the page as 2 blocks of 32 bytes each
  for (int i = 0; i < 2; i++)
  {
    // Read the first half of the page (maximum Wire.h buffer length)
    Wire.requestFrom(bus_address, bytelength);  // requests 32 Bytes of data in a packet, maximum string size.
    //delay(5);
    while(Wire.available())
    {
      buff[position] = Wire.read();
      position += 1;
    }  
    //if (!FastMemory) delay(5);  
    delay(5);
  }
  Wire.endTransmission();

}

////////////////////////////////////////////////////////////////////////////////////////////
// Writes 64 bytes of data to the selected EEPROM chip at memory_address 
// from the memory buffer at address *buff. 
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::eepromWritePage(uint8_t bus_address, uint16_t memory_address, uint8_t *buff)
{
  Wire.beginTransmission(bus_address);
  Wire.write(highByte(memory_address));
  Wire.write(lowByte(memory_address));
  
  // Write 1st 30 bytes of the page.  
  // (The Wire.h buffer is 32 bytes long, but the first 2 bytes are used to set the memory address.  So we can only write 30 bytes at a time.)

  Wire.write(buff, 30);
  //for (byte a = 0; a < 30; a++)
  //{
  //  Wire.write(buff[a]);
  //}

  Wire.endTransmission(); // This is when the data will actually be written to the EEPROM.
  //if (!FastMemory) delay(10);
  delay(10);
  
  // Write 2nd 30 bytes of the page.
  Wire.beginTransmission(bus_address);
  // This time we don't have to set the memory address.  It should already be in the middle of the current page. (WRONG!)
  memory_address += 30;
  Wire.write(highByte(memory_address));
  Wire.write(lowByte(memory_address));
      
  // Write 2nd 30 bytes of the page.  (32 bytes is the buffer size in the Wire.h library)
  
  Wire.write(buff+30, 30);
  //for (byte b = 30; b < 60; b++)
  //{
  //  Wire.write(buff[b]);
  //}
  
  Wire.endTransmission(); // This is when the data will actually be written to the EEPROM.
  //if (!FastMemory) delay(10);
  delay(10);
  
  memory_address += 30;
  Wire.beginTransmission(bus_address);
  Wire.write(highByte(memory_address));
  Wire.write(lowByte(memory_address));
  
  // Write the last 4 bytes of the page.
  Wire.write(buff+60, 4);
  
  
  //for (byte c = 60; c < 64; c++)
  //{
  //  Wire.write(buff[c]);
  //}
  Wire.endTransmission(); // This is when the data will actually be written to the EEPROM.
  //if (!FastMemory) delay(10);
  delay(10);
  
}

////////////////////////////////////////////////////////////////////////////////////////////
// Reloads the MonsterShield configuration settings from EEPROM.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::eepromReadSettings()
{
  // Read first page
  eepromReadPage(i2cMemoryChip, controlPageAddress, pagebuffer);  // controlAddress should be the FIRST page of the EEPROM chip.  For 32k chip (24LC256) this is 32704
  
  // Parse commands and delays from pagebuffer
  int pos = 0;
  for (int i = 0; i < MONSTERSHIELD_MAX_SLOT_COUNT; i++)
  {
    sequenceLength[i] = (pagebuffer[pos] << 8) + pagebuffer[pos+1];
    //Serial.print("%len=");
    //Serial.println(sequenceLength[i]);
    
    pos += 2;
  }
  
  for (int i = 0; i < MONSTERSHIELD_MAX_SLOT_COUNT; i++)
  {
    if (pagebuffer[pos] == (byte)0x00)
    {
      bitClear(slotEnabled, i);
      //sequenceEnabled[i] = false;
    }
    else
    {
      bitSet(slotEnabled, i);
      //sequenceEnabled[i] = true;
    }
    pos += 1;
  }
  
  // Read number of sequences (mode)
  slotCount = pagebuffer[pos];
  pos += 1;
  if (slotCount > MONSTERSHIELD_MAX_SLOT_COUNT) slotCount = MONSTERSHIELD_MAX_SLOT_COUNT; // Sanity check!
  
  eventsPerSlot = calculateEventsPerSlot(slotCount);
  
  // Get playback mode
  nextAnimSelectMode = pagebuffer[pos];
  pos += 1;
  
  // Verify playback mode is in the correct range, and correct if it is not.
  if (nextAnimSelectMode < 0 || nextAnimSelectMode > 2)
  {
    nextAnimSelectMode = 0;
  }
  
  // Get Ambient Animation flag
  PlayAmbientAnimation = pagebuffer[pos];
  pos += 1;
  
  // read watermark
  watermark[0] = pagebuffer[pos];
  pos += 1;
  watermark[1] = pagebuffer[pos];
  pos += 1;
  watermark[2] = pagebuffer[pos];
  pos += 1;
  watermark[3] = pagebuffer[pos];
  pos += 1;
  
  
  currentSlot = pagebuffer[pos];
  if (currentSlot < 0) currentSlot = 0;
  if (currentSlot > 14) currentSlot = 14;
  pos += 1;

  eeprom1 = pagebuffer[pos];
  pos += 1;
  eeprom2 = pagebuffer[pos];
  pos += 1;
  
  memorySlotCount0 = pagebuffer[pos];
  pos += 1;
  memorySlotCount1 = pagebuffer[pos];
  pos += 1;
  memorySlotCount2 = pagebuffer[pos];
  pos += 1;
  
  
  
  // Read second page
  eepromReadPage(i2cMemoryChip, controlPageAddress+MONSTERSHIELD_BUFFER_SIZE, pagebuffer);  // controlAddress should be the FIRST page of the EEPROM chip.  For 32k chip (24LC256) this is 32704
  pos = 0;
  
  for (int i=0; i < 4; i++)
  {
    triggerThreshold[i] = (pagebuffer[pos] << 8) + pagebuffer[pos+1];
    if (triggerThreshold[i] < 700 || triggerThreshold[i] > 1023)
    {
      triggerThreshold[i] = 700;
    }
    pos += 2;
    
    triggerCooldown[i] = pagebuffer[pos];
    pos += 1;
    
    triggerSensitivity[i] = pagebuffer[pos];
    pos += 1;
    
    triggerOnVoltage[i] = !pagebuffer[pos];
    pos += 1;
    
    triggerAfterResetOnly[i] = pagebuffer[pos];
    pos += 1;
    
  }
  
  //Uncomment for emergency override.
  //triggerMinThreshold = 1023;
  //triggerCooldownSeconds = 15;
  //triggerSensitivity = 50;
  
 
}

////////////////////////////////////////////////////////////////////////////////////////////
// Writes the MonsterShield configuration settings to EEPROM.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::eepromWriteSettings()
{
  for (int i = 0; i < 64; i++)
  {
    pagebuffer[i] = (byte)0x00;
  }
  
  int pos = 0;
  for (int i = 0; i < MONSTERSHIELD_MAX_SLOT_COUNT; i++)
  {
    pagebuffer[pos] = highByte(sequenceLength[i]);
    pagebuffer[pos+1] = lowByte(sequenceLength[i]);
    pos += 2;
  }
  
  // Populate the sequence enabled flags, 1 byte each.
  for (int i = 0; i < MONSTERSHIELD_MAX_SLOT_COUNT; i++)
  {
    if (bitRead(slotEnabled, i) == HIGH)
    {
      pagebuffer[pos] = 0x01;
    }
    else
    {
      pagebuffer[pos] = 0x00;
    }
    pos += 1;
  }
  
  // Set sequence count
  pagebuffer[pos] = slotCount;
  pos += 1;
  
  // Save playback mode
  pagebuffer[pos] = nextAnimSelectMode; //PlaybackMode;
  pos += 1;
  
  // Save AmbientAnimation flag
  pagebuffer[pos] = PlayAmbientAnimation;
  pos += 1;
  
  // Save watermark
  pagebuffer[pos] = 0x19;
  pos += 1;
  pagebuffer[pos] = 0x74;
  pos += 1;
  pagebuffer[pos] = 0x67;
  pos += 1;
  pagebuffer[pos] = 0x91;
  pos += 1;
  
  // Save current slot
  pagebuffer[pos] = lowByte(currentSlot);
  pos += 1;
  
  pagebuffer[pos] = eeprom1;
  pos += 1;
  pagebuffer[pos] = eeprom2;
  pos += 1;
  
  pagebuffer[pos] = memorySlotCount0; 
  pos += 1;
  pagebuffer[pos] = memorySlotCount1; 
  pos += 1;
  pagebuffer[pos] = memorySlotCount2; 
  pos += 1;
  
  
  // Write the first page
  /////////////////////////////////////////////////////////////////////
  eepromWritePage(i2cMemoryChip, controlPageAddress, pagebuffer);
  
  for (int i = 0; i < 64; i++)
  {
    pagebuffer[i] = (byte)0x00;
  }
  
  pos = 0;  
  
  
  for (int i=0; i < 4; i++)
  {
    pagebuffer[pos] = highByte(triggerThreshold[i]);
    pagebuffer[pos+1] = lowByte(triggerThreshold[i]);
    pos += 2;
    
    pagebuffer[pos] = triggerCooldown[i];
    pos += 1;
    
    pagebuffer[pos] = triggerSensitivity[i];
    pos += 1;
    
    pagebuffer[pos] = !triggerOnVoltage[i];
    pos += 1;
    
    pagebuffer[pos] = triggerAfterResetOnly[i];
    pos += 1;
    
  }
  
  // Now write the second page
  eepromWritePage(i2cMemoryChip, controlPageAddress+MONSTERSHIELD_BUFFER_SIZE, pagebuffer);
}


////////////////////////////////////////////////////////////////////////////////////////////
// Returns true if the digital I/O pin (on the detachable keypad) was pressed and released.
////////////////////////////////////////////////////////////////////////////////////////////
bool MonsterShield::keypadButtonPress(int button)
{
  boolean rc = false;
  boolean sample1 = digitalRead(button);
  delay(10);
  boolean sample2 = digitalRead(button);
  
  if (sample1 == sample2)
  {
    if (sample1 != keypadButtonLastState[button])
    {
      // Button state change!
      if (sample1 == HIGH)
      {
        // Start of press, record timestamp.
        keypadButtonPressStart[button] = millis();
      }
      
      if (sample1 == LOW)
      {
        // End of press (release)
        keypadButtonPressRelease[button] = millis() - keypadButtonPressStart[button];
        rc = true;
      }
      
    }
    keypadButtonLastState[button] = sample1;
  }
  
  return rc;
}


////////////////////////////////////////////////////////////////////////////////////////////
// Checks whether an on-board MonsterShield input button has been pressed and released.
////////////////////////////////////////////////////////////////////////////////////////////
bool MonsterShield::inputButtonPress(int button)
{
  boolean rc = false;
  boolean sample1 = getInputButtonState(button);
  delay(5);
  boolean sample2 = getInputButtonState(button);
  
  if (sample1 == sample2)
  {
    if (sample1 != inputButtonLastState[button])
    {
      // Button state change!
      if (sample1 == HIGH)
      {
        // Start of press, record timestamp.
        inputButtonPressStart[button] = millis();
      }
      
      if (sample1 == LOW)
      {
        // End of press (release)
        inputButtonPressRelease[button] = millis() - inputButtonPressStart[button];
        rc = true;
      }
    }
    inputButtonLastState[button] = sample1;
  }
  return rc;
}


////////////////////////////////////////////////////////////////////////////////////////////
// This function returns the currently active slot.
////////////////////////////////////////////////////////////////////////////////////////////
int MonsterShield::getActiveSlot()
{
  return currentSlot;
}

////////////////////////////////////////////////////////////////////////////////////////////
// This function sets the currently active slot.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setActiveSlot(int slot)
{
  if (slot < 0) slot = 0;
  if (slot > slotCount-1) slot = slotCount-1;
  
  currentSlot = slot;
  setDigitToActiveSlot();
  eepromWriteSettings();
}

////////////////////////////////////////////////////////////////////////////////////////////
// This function sets the currently active slot to the next slot.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setNextSlot()
{
  currentSlot += 1;
  if (currentSlot > slotCount-1) currentSlot = 0;
  setDigitToActiveSlot();
  eepromWriteSettings();
}

////////////////////////////////////////////////////////////////////////////////////////////
// This function sets the currently active slot to the previous slot.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setPreviousSlot()
{
  currentSlot -= 1;
  if (currentSlot < 0) currentSlot = slotCount - 1;
  setDigitToActiveSlot();
  eepromWriteSettings();
}

////////////////////////////////////////////////////////////////////////////////////////////
// Returns true if the slot number is enabled.  Disabled slots are skipped when triggered.
////////////////////////////////////////////////////////////////////////////////////////////
bool MonsterShield::getSlotEnabled(int slot)
{
  bool rc = false;
  if (slot >= 0 && slot <= 15)
  {
    if (bitRead(slotEnabled, slot) == 1)
    {
      rc = true;
    }
  }
  return rc;
}

////////////////////////////////////////////////////////////////////////////////////////////
// Sets whether the slot number is enabled or not.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setSlotEnabled(int slot, bool value)
{
  if (slot >= 0 && slot <= 15)
  {
    bitWrite(slotEnabled, slot, value);
  }
  eepromWriteSettings();
}

////////////////////////////////////////////////////////////////////////////////////////////
// Determines how the next slot is selected after an animation is played. 
//    0=sequential, 1=random, 2=single (stay on slot)
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setAnimationSelectMode(int mode)
{
  if (mode < 0 || mode > 2)
  {
    mode = 0;
  }
  
  nextAnimSelectMode = mode;
  eepromWriteSettings();
  showAnimationSelectMode();
}

////////////////////////////////////////////////////////////////////////////////////////////
// Cycles to the next animation selection mode.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setNextAnimationSelectMode()
{
  nextAnimSelectMode += 1;
  if (nextAnimSelectMode > 2) nextAnimSelectMode = 0;
  eepromWriteSettings();
  showAnimationSelectMode();
}

////////////////////////////////////////////////////////////////////////////////////////////
// Routine to briefly display on the 7-segment display which selection mode is active.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::showAnimationSelectMode()
{
  for (int i=0; i < 3; i++)
  {
    setDigitChar('P');
    delay(200);
    setDigitChar(' ');
    delay(200);
  }
  for (int i=0; i < 5; i++)
  {
    setDigit(nextAnimSelectMode);
    delay(200);
    setDigitChar(' ');
    delay(200);
  }
  setDigitToActiveSlot();
  
}

////////////////////////////////////////////////////////////////////////////////////////////
// Sets whether ambient mode is currently active.  This controls some internal stuff.  
// Ambient mode means a slot is played continously in a loop until
// a trigger is sensed.  After the triggered animation is played, the MonsterShield goes 
// back to playing the ambient animation in a continous loop.
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::setPlayAmbient(bool value)
{
  // Only allow ambient mode if slot 0 is not empty.
  if (sequenceLength[0] > 0)
  {
    PlayAmbientAnimation = value;
    currentSlot = 0;
    eepromWriteSettings();
    setDigitToActiveSlot();
  }
  else
  {
    for (int i=0; i < 4; i++)
    {
      setDigitChar('E');
      delay(250);
      setDigitChar(' ');
      delay(250);
    }
    setDigitToActiveSlot();
  }
}

////////////////////////////////////////////////////////////////////////////////////////////
// Returns ehther ambient mode is currently on or off.
////////////////////////////////////////////////////////////////////////////////////////////
bool MonsterShield::getPlayAmbient()
{
  return PlayAmbientAnimation;
}

////////////////////////////////////////////////////////////////////////////////////////////
// This function performs a factory reset, which is normally accomplished by holding down the "prev/enable"
// button while pressing the reset button on the MonsterShield.  The MonsterShield should begin
// rapidly flashing the "F" character to indicate a factory reset is being performed.  It will then
// begin flashing "F" more slowly to indicate that 15 slots are the default.  You can press the 
// "mode/trigger" button to confirm this, or you can cycle through and choose 1 to 15 slots
// using the prev/enable button and the nxt/ambient button.  So, if you only wanted 8 slots, you
// could press the appropriate buttons until "8" is flashing, then press "mode/trigger" to
// confirm this selection.  The MonsterShield will then complete the factory reset procedure
// and prepare the EEPROM chip(s) for this many slots.  The fewer the slots, the longer the animation
// can be in each of those slots. 
////////////////////////////////////////////////////////////////////////////////////////////
void MonsterShield::performFactoryReset()
{
  for (int i=0; i < 30; i++)
  {
    setDigitChar('F');
    delay(50);
    setDigitChar(' ');
    delay(50);
  }
  setDigitChar('-');
  
  for (int i = 0; i < MONSTERSHIELD_MAX_SLOT_COUNT; i++)
  {
    sequenceLength[i] = 0x00;
    bitSet(slotEnabled, i);
  }

  eeprom1 = false;
  eeprom2 = false;
  
  if (detectMemory(0x53) == true)
  {
    Serial.println("%EEPROM1:  Found");
    eeprom1 = true;
  }
  else
  {
    Serial.println("%EEPROM1:  Missing");
  }

  if (detectMemory(0x55) == true)
  {
    Serial.println("%EEPROM2:  Found");
    eeprom2 = true;
  }
  else
  {
    Serial.println("%EEPROM2:  Missing");
  }

  slotCount = getInputMenuChoice('-', 1, 15, 10);
  eventsPerSlot = calculateEventsPerSlot(slotCount);
  
  
  for (int i=0; i < 4; i++)
  { 
    triggerLastStateChange[i] = 0;
    triggerSensitivity[i] = 50;
    triggerThreshold[i] = 950;
    triggerResetState[i] = false;
    triggerCooldown[i]=15;
    triggerAfterResetOnly[i] = true;
    triggerOnVoltage[i] = HIGH;
  }

  nextAnimSelectMode = 0;
  PlayAmbientAnimation = false;
  currentSlot = 0;
  
  eepromWriteSettings();
}


////////////////////////////////////////////////////////////////////////////////////////////
// Canned routine to get a user option.  displaydigit is a character to display on the 7-segment display.  User presses 
// the desired keypad buttons to cycle through the options and presses the record to select the option.
// Returns the number selected. If -1 is returned, then no option was selected.  (sort of like a cancel)
////////////////////////////////////////////////////////////////////////////////////////////
int MonsterShield::GetKeypadMenuChoice(char displaydigit, int8_t maxnum)
{
    // Have user select which bank they want to record.
  setDigitChar(displaydigit);
  int value = -1;
  delay(500);
  bool cont = true;

  while (cont == true) 
  {
    if ((millis() - blinker) > 300)
    {
      if (flipflop)
      {
        if (value > -1)
          setDigit(value);
        else
          setDigitChar('-');
          //setDigitChar(displaydigit);
      }
      else
      {
        setDigitChar(displaydigit);
      }
      flipflop = !flipflop;
      blinker = millis();
    }
    
    if (keypadButtonPress(12))
    {
      value = 0; // Keep bank, overlay on top of what was already recorded
    }
    if (keypadButtonPress(11))
    {
      if (maxnum >= 2)
        value = 1; // Clear entire bank
    }
    if (keypadButtonPress(10))
    {
      if (maxnum >= 3)
        value = 2; // Keep bank, but clear and record selected track
    }
    if (keypadButtonPress(9))
    {
      if (maxnum >= 4)
        value = 3; // Keep bank, but overlay record selected track only
    }
    
    if (keypadButtonPress(8) == true)
    {
      cont = false;
    }
  }
  return value;
}

////////////////////////////////////////////////////////////////////////////////////////////
// Canned routine that works simliar to the getKeypadMenuChoice, except you use the prev/next buttons on the MonsterShield
// to cycle through the numbers betwen minnum and maxnum.  If no choice is made for timeout seconds since the last button
// press, then the last choosen option is the result.  Returns the number that was selected.  -1 indicates no selection made.
////////////////////////////////////////////////////////////////////////////////////////////
int MonsterShield::getInputMenuChoice(char displaydigit, int8_t minvalue, int8_t maxvalue, int8_t timeout)
{
  setDigitChar(displaydigit);
  int value = maxvalue;
  delay(100);
  bool cont = true;
  unsigned long timestamp = millis();
  
  while (cont == true)
  {
    if ((millis() - blinker) > 300)
    {
      if (flipflop)
      {
        setDigit(value);
      }
      else
      {
        setDigitChar(displaydigit);
      }
      flipflop = !flipflop;
      blinker = millis();
    }
    
    if (inputButtonPress(1) == HIGH)
    {
      value -= 1;
      if (value < minvalue) value = minvalue;
      timestamp = millis();
    }
    
    if (inputButtonPress(2) == HIGH)
    {
      value += 1;
      if (value > maxvalue) value = maxvalue;
      timestamp = millis();
    }
    
    if (timeout > 0)
    {
      if ((millis() - timestamp) > timeout * 1000)
      {
        cont = false;
      }
    }
    
    if (inputButtonPress(0) == HIGH)
    {
      cont = false;
    }
  }
  
  return value;
}

////////////////////////////////////////////////////////////////////////////////////////////
// Canned routine to calculate how many events per slot can be recorded.  There are 20 events per second.
// This routine attempts to evenly distribute the slots across 1, 2 or 3 EEPROM chips.
////////////////////////////////////////////////////////////////////////////////////////////
uint16_t MonsterShield::calculateEventsPerSlot(int slots)
{
  uint16_t value = 0;
  unsigned long memory = 32768;
  
  if ((eeprom1 == true && eeprom2 == false) || (eeprom1 == false && eeprom2 == true))
  {
    Serial.println("Calculating for 2 memory chips...");
    //value = ((((memory - MONSTERSHIELD_BUFFER_SIZE) / slots) / MONSTERSHIELD_BUFFER_SIZE) * MONSTERSHIELD_BUFFER_SIZE) / 2;
    
    memorySlotCount0 = (int)(slots / 2);
    memorySlotCount1 = slots - memorySlotCount0;
    memorySlotCount2 = 0;
    
    value = ((((memory - MONSTERSHIELD_CONTROLPAGE_SIZE) / max(memorySlotCount0, memorySlotCount1)) / MONSTERSHIELD_BUFFER_SIZE) * MONSTERSHIELD_BUFFER_SIZE) / 2;
       
    Serial.print("memorySlotCount0=");
    Serial.println(memorySlotCount0);
    
    Serial.print("memorySlotCount1=");
    Serial.println(memorySlotCount1);
  }
  else if (eeprom1 == true && eeprom2 == true)
  {
    Serial.println("Calculating for 3 memory chips...");
    
    memorySlotCount0 = (int)(slots / 3);
    memorySlotCount1 = (int)((slots - memorySlotCount0) / 2);
    memorySlotCount2 = slots - (memorySlotCount0 + memorySlotCount1);
    
    Serial.print("memorySlotCount0=");
    Serial.println(memorySlotCount0);
    
    Serial.print("memorySlotCount1=");
    Serial.println(memorySlotCount1);
    
    Serial.print("memorySlotCount2=");
    Serial.println(memorySlotCount2);
    
    value = ((((memory - MONSTERSHIELD_CONTROLPAGE_SIZE) / max(max(memorySlotCount0, memorySlotCount1), memorySlotCount2)) / MONSTERSHIELD_BUFFER_SIZE) * MONSTERSHIELD_BUFFER_SIZE) / 2;
  }
  else
  {
    Serial.println("Calculating for 1 memory chip...");
    
    memorySlotCount0 = slots;
    memorySlotCount1 = 0;
    memorySlotCount2 = 0;
       
    value = ((((memory - MONSTERSHIELD_CONTROLPAGE_SIZE) / slots) / MONSTERSHIELD_BUFFER_SIZE) * MONSTERSHIELD_BUFFER_SIZE) / 2;
    
    Serial.print("memorySlotCount0=");
    Serial.println(memorySlotCount0);
  }
  
  Serial.print(" eventsperslot=");
  Serial.println(value);
  
  return value;
  
}

////////////////////////////////////////////////////////////////////////////////////////////
// This purpose of this function is to detect which size EEPROM chip(s) are installed
// on the MonsterShield and on the Expander board.
////////////////////////////////////////////////////////////////////////////////////////////
bool MonsterShield::detectMemory(uint8_t bus)
{
  bool good = true;
  //uint8_t bus = 0x53;
  uint16_t addy = 0;
  uint8_t data[8];
  uint8_t len = 8;
  int pos;
  
  for (int i=0; i < 8; i++)
  {
    data[i] = i;
  }

  // Write 8 bytes to EEPROM
  Wire.beginTransmission(bus);
  Wire.write(highByte(addy));
  Wire.write(lowByte(addy));  
  Wire.write(data, len);
  Wire.endTransmission(); // This is when the data will actually be written to the EEPROM.
  delay(50);
  
  for (int i=0; i < 8; i++)
  {
    data[i] = 0x00;
  }
  
  // Read back 8 bytes from EEPROM
  Wire.beginTransmission(bus);
  Wire.write(highByte(addy));
  Wire.write(lowByte(addy));
  Wire.endTransmission();
  Wire.requestFrom(bus, len);  // requests 32 Bytes of data in a packet, maximum string size.
  pos = 0;
  delay(10);
  while(Wire.available())
  {
    data[pos] = Wire.read();
    pos += 1;
  }  
  Wire.endTransmission();
  
  // Check results 
  for (uint8_t i=0; i < 8; i++)
  {
    if (data[i] != i)
    { 
       good = false;
    }
  }
  
  return good;
}

////////////////////////////////////////////////////////////////////////////////////////////
// Returns true or false to indicate whether EEPROM bank 1 or 2 is populated with a chip on the 
// MonsterShield Expander board.  bank can be 1 or 2 on the expander board
////////////////////////////////////////////////////////////////////////////////////////////
bool MonsterShield::eepromInstalled(uint8_t bank)
{
  bool rc = false;
  switch (bank)
  {
    case 1:
      rc = eeprom1;
      break;
      
    case 2:
      rc = eeprom2;
      break;
  }
  return rc;
}

////////////////////////////////////////////////////////////////////////////////////////////
// This should be called at least once per loop where we care about serial communications!
////////////////////////////////////////////////////////////////////////////////////////////
uint8_t MonsterShield::processSerialPort()
{
  uint8_t rc = 0;
  int availBytes;
  byte sercmd;
  if (Serial.available() > 0) 
  {
     // read the incoming byte:
     byte serdata = Serial.read();
     if (serdata == '$') // Indicates a command from a "Master" MonsterShield
     {
        availBytes = Serial.available();
        if (availBytes >= 1)
        {
          sercmd = Serial.read();
         
          switch (sercmd)
          {
            case 'T':  // Trigger playback
              if (Serial.available() > 0)
              {
                byte seq = Serial.read();
                seq = ConvertHexToByte(seq);
                Serial.print("%T=");
                Serial.println(seq);
                setActiveSlot(seq);
                playAnimationWithInterrupts(seq, false, false, 0x00, 0x01, 0x08);
              }
              break;   
              
            case '0':
            {
              byte seq = Serial.read();
              if (seq == '0')
              {
                // This means we received a STOP signal from the host computer.
                // We should stop playing any animation!
                Serial.println("%STOP");
                return 0xFF;
                break;
              }
            } 
         }
        } 
     }
     
     if (serdata == '@')  // Indicates we have received a command from host computer.  Ignore everything else.
     {
        delay(5);
        availBytes = Serial.available();
        if (availBytes >= 1)
        {
          sercmd = Serial.read();
          
          
          switch (sercmd)
          {
            case '*':  // STOP animation
              // This means we received a STOP signal from the host computer.
              // We should stop playing any animation!
              Serial.println("%STOP");
              return 0xFF;
              break;
              
            case 'A': // Ambient mode
              {
                byte ambient = Serial.read();
                ambient = ConvertHexToByte(ambient);
                if (ambient == 1) 
                  setPlayAmbient(true);
                else
                  setPlayAmbient(false);
              }
              break;
             
            case 'E': // Request memory configuration
              Serial.print("$EEPROM1=");
              Serial.println(eeprom1);
              Serial.print("$EEPROM2=");
              Serial.println(eeprom2);              
              break;
              
            case 'V':  // Request version information
              Serial.println("$VER=1.0.5");
              break;       
              
            case 'S':  // Select slot
              if (Serial.available() > 0)
              {
                byte seq = Serial.read();
                seq = ConvertHexToByte(seq);
                Serial.print("%S=");
                Serial.println(seq);
                setActiveSlot(seq);
              }
              break;
              
            case '+':  // Enable slot
              if (Serial.available() > 0)
              {
                byte seq = Serial.read();
                seq = ConvertHexToByte(seq);
                Serial.print("%+=");
                Serial.println(seq);
                setSlotEnabled(seq, true);
                setDigitToActiveSlot();
              }
              break;

            case '-':  // Disable slot
              if (Serial.available() > 0)
              {
                byte seq = Serial.read();
                seq = ConvertHexToByte(seq);
                Serial.print("%-=");
                Serial.println(seq);
                setSlotEnabled(seq, false);
                setDigitToActiveSlot();
              }
              break;      
             
            case 'R':  // Manually set relay states
              if (Serial.available() > 0)
              {
                byte c0 = Serial.read();
                byte c1 = Serial.read();
                byte c2 = Serial.read();
                byte c3 = Serial.read();
                
                c0 = ConvertHexToByte(c0);
                c1 = ConvertHexToByte(c1);
                c2 = ConvertHexToByte(c2);
                c3 = ConvertHexToByte(c3);
                
                byte cmd1 = (c0*16) + c1;
                byte cmd2 = (c2*16) + c3;  
                
                
                Serial.print("%R=");
                Serial.print(cmd1, HEX);
                Serial.print(",");
                Serial.println(cmd2, HEX);
                
                
                byte data = cmd1 & 0B00001111; 
                setPortExpanderOutput(expanderUnit0, expanderBankA, data);
                  
                // V5 logic for additional relays
                data = cmd1 & 0B11110000;
                data = data >> 4;
                data = (cmd2 << 4) + data;
                setPortExpanderOutput(expanderUnit1, expanderBankA, data);
                
                data = cmd2 >> 4;
                setPortExpanderOutput(expanderUnit1, expanderBankB, data);
              }
              break;
       
            case 'T':  // Trigger playback
              if (Serial.available() > 0)
              {
                byte seq = Serial.read();
                seq = ConvertHexToByte(seq);
                Serial.print("%T=");
                Serial.println(seq);
                setActiveSlot(seq);
                playAnimationWithInterrupts(seq, false, false, 0x00, 0x01, 0x08);
              }
              break;   
              
            case 'P':  // Select playback mode
              if (Serial.available() > 0)
              {
                byte mode = Serial.read();
                mode = ConvertHexToByte(mode);
                Serial.print("%P=");
                Serial.println(mode);
                setAnimationSelectMode(mode);
              }
              break;      
     
            case 'Y':  // Request slot count
              Serial.print("$SLOTCNT=");
              Serial.println(slotCount);        
              break;         
              
            case 'X':  // Set slot count
              if (Serial.available() > 0)
              {
                byte count = Serial.read();
                count = ConvertHexToByte(count);
                
                if (count >= 1 && count < 16)
                {
                    slotCount = count;
                    eventsPerSlot = calculateEventsPerSlot(slotCount);
                  
                    nextAnimSelectMode = 0;
                    PlayAmbientAnimation = false;
                    currentSlot = 0;
                    
                    eepromWriteSettings();
                    
                    Serial.print("$SLOTCNT=");
                    Serial.println(slotCount);   
                }
              }
              break;
                  
            case 'D':  // Download sequence to host computer
              //availBytes = Serial.available();
              if (Serial.available() > 0) 
              {
                byte seq = Serial.read();
                seq = ConvertHexToByte(seq);
                currentSlot = seq;
                Serial.print("$SEQ=");
                Serial.println(currentSlot);
                Serial.print("$LEN=");
                Serial.println(sequenceLength[currentSlot]);
                Serial.print("$NUMCMD=");
                Serial.println(sequenceLength[currentSlot] / MONSTERSHIELD_BYTES_PER_EVENT);
                Serial.println("$BEGIN!");
                bool flipper = true; 
                uint8_t  fx = 20;
                currentPage = 0;
                eepromReadSlotData(currentSlot, currentPage);
                int totalpages = (eventsPerSlot * 2) / MONSTERSHIELD_BUFFER_SIZE;
                while (currentPage < totalpages)
                {
                  // Grab next page of data
                  eepromReadSlotData(currentSlot, currentPage);
                  //Serial.print(currentPage);
                  //Serial.write(sequenceBuffer, MONSTERSHIELD_BUFFER_SIZE);
                  
                  Serial.print("$D");
                  
                  for (int i=0; i < MONSTERSHIELD_BUFFER_SIZE; i++)
                  {
                    if (sequenceBuffer[i] < 0x10) Serial.print("0");
                    Serial.print(sequenceBuffer[i], HEX);
                    //Serial.print(".");
                  }
                  
                  // We add this character after each page so that it doesn't
                  // crash the Arduino Serial Monitor by having a really long line!
                  Serial.println("<");  
                  currentPage += 1;
                  flipper = !flipper;
                  if (flipper) 
                    digitalWrite(13, HIGH);
                  else
                    digitalWrite(13, LOW);
                  setDigit(fx);
                  fx += 1; 
                  if (fx > 25) fx = 20;
                } // While more pages to send
                  
                digitalWrite(13, LOW);
                setDigitToActiveSlot();           
              }
              Serial.println("$END!"); 
              break;    
            
            
            case 'U': // Upload to MonsterShield
              if (Serial.available() > 0) 
              {
                byte seq = Serial.read();
                seq = ConvertHexToByte(seq);
                Serial.print("%Upload Seq#");
                Serial.println(seq, HEX);
    
                Serial.read(); // skip \n
                byte len0 = Serial.read();
                byte len1 = Serial.read();
                byte len2 = Serial.read();
                byte len3 = Serial.read();
                Serial.read(); // skip \n
         
                len0 = ConvertHexToByte(len0);
                len1 = ConvertHexToByte(len1);
                len2 = ConvertHexToByte(len2);
                len3 = ConvertHexToByte(len3);
              
                int newSeqLen = (len0 * 16 * 16 * 16) + (len1 * 16 * 16) + (len2 * 16) + len3;
                    
                Serial.print("%NEWSEQLEN=");
                Serial.println(newSeqLen);
    
                byte cmd1;
                byte cmd2;
                sequencePosition = 0;
                int page = 0;   
                short events = 0;             
                uint8_t  fx = 20;
                
                // Now we need a loop to begin reading the data in.
                for (int i = 0; i < newSeqLen; i++)
                {
                  //cmd1
                  while(Serial.available() == 0){}
                  byte c0 = Serial.read();
                  while(Serial.available() == 0){}
                  byte c1 = Serial.read();
                  //cmd2
                  while(Serial.available() == 0){}
                  byte c2 = Serial.read();
                  while(Serial.available() == 0){}
                  byte c3 = Serial.read();
    
    
                  c0 = ConvertHexToByte(c0);
                  c1 = ConvertHexToByte(c1);
                  c2 = ConvertHexToByte(c2);
                  c3 = ConvertHexToByte(c3);
    
                  cmd1 = (c0*16) + c1;
                  cmd2 = (c2*16) + c3;       
           
                  sequenceBuffer[sequencePosition] = cmd1;
                  sequenceBuffer[sequencePosition+1] = cmd2;       
                  
                  events += 1;
                  sequencePosition += MONSTERSHIELD_BYTES_PER_EVENT;
                  
                  if (sequencePosition >= MONSTERSHIELD_BUFFER_SIZE)
                  {
                    eepromWriteSlotData(seq, page);
                    sequencePosition = 0;
                    page += 1;
                  }
                  
                  if ((sequencePosition % 10) == 0)
                  {
                    setDigit(fx);
                    fx += 1; 
                    if (fx > 25) fx = 20;
                  }
                }
            
                // Write final page
                if (sequencePosition > 0)
                {
                  eepromWriteSlotData(seq, page);
                  page += 1;
                }    
                
                sequenceLength[seq] = events * MONSTERSHIELD_BYTES_PER_EVENT; 
                eepromWriteSettings();
                
                Serial.println("%Upload done.");
    
                digitalWrite(13, LOW);
                setDigitToActiveSlot();   
              }            
              break;
            
            case 'Z':  // Retrieve all trigger settings for trigger X
              if (Serial.available() > 0) 
              {
                byte trigger = Serial.read();
                trigger = ConvertHexToByte(trigger);
                
                if (trigger >= 0 && trigger < 4)
                {
                  Serial.print("$TRIG");
                  Serial.print(trigger);
                  Serial.print("=");
                  Serial.print(triggerSensitivity[trigger]);
                  Serial.print(",");
                  Serial.print(triggerThreshold[trigger]);
                  Serial.print(",");
                  Serial.print(triggerCooldown[trigger]);
                  Serial.print(",");
                  Serial.print(triggerOnVoltage[trigger]);
                  Serial.print(",");
                  Serial.println(triggerAfterResetOnly[trigger]);
                }
              }
              break;
              
            case 'z':  // Save new trigger settings for trigger X
              while(Serial.available() == 0){}
              if (Serial.available() > 0) 
              {
                byte trigger = Serial.read();
                trigger = ConvertHexToByte(trigger);
                
                Serial.read();
                
                // Threshold
                //////////////////////////////
                while(Serial.available() == 0){}
                byte c0 = Serial.read();
                while(Serial.available() == 0){}
                byte c1 = Serial.read();
                while(Serial.available() == 0){}
                byte c2 = Serial.read();
                while(Serial.available() == 0){}
                byte c3 = Serial.read();
                
                c0 = ConvertHexToByte(c0);
                c1 = ConvertHexToByte(c1);
                c2 = ConvertHexToByte(c2);
                c3 = ConvertHexToByte(c3);
                
                triggerThreshold[trigger] = (c0 * 16 * 16 * 16) + (c1 * 16 * 16) + (c2 * 16) + c3;

                Serial.read(); // Consume comma
                
                // Sensitivity    
                ////////////////////////////////
                c0 = Serial.read();
                c1 = Serial.read();
                
                c0 = ConvertHexToByte(c0);
                c1 = ConvertHexToByte(c1);
               
                triggerSensitivity[trigger] = (c0*16) + c1;               
               
                Serial.read(); // Consume comma
                
                // Cooldown  
                ////////////////////////////////
                c0 = Serial.read();
                c1 = Serial.read();
                
                c0 = ConvertHexToByte(c0);
                c1 = ConvertHexToByte(c1);
               
                triggerCooldown[trigger] = (c0*16) + c1;                   
               
                Serial.read(); // Consume comma
                
                // Voltage  
                ////////////////////////////////
                c0 = Serial.read();
                c0 = ConvertHexToByte(c0);
                triggerOnVoltage[trigger] = c0;
                Serial.read(); // Consume comma
                
                // Reset  
                ////////////////////////////////
                c0 = Serial.read();
                c0 = ConvertHexToByte(c0);
                triggerAfterResetOnly[trigger] = c0;
                
               
                while(Serial.available() == 0){}
                Serial.read(); // Consume newline character.
                eepromWriteSettings(); 
              }
              break;     
       
            case 'O':  // List sequence enable/disable flags
              Serial.read(); // Consume 0.
              Serial.print("$O");
              for (int a = 0; a < 15; a++)
              {
                if (getSlotEnabled(a) == true)
                {
                  Serial.print("1");
                }
                else
                {
                  Serial.print("0");
                }
              }
              Serial.println("");
              break;       
          }
          
        }
     }
  }
  
  return rc;
  
}

////////////////////////////////////////////////////////////////////////////////////////////
// Helper routine to convert an ASCII HEX character (0,1,2,3,4,5,6,7,8,9,A,B,C,D,E,F) to 
// it's integer representation.  So 'A' converts to 0x0A.
////////////////////////////////////////////////////////////////////////////////////////////
uint8_t MonsterShield::ConvertHexToByte(uint8_t value)
{
  if (value >= 0x30 && value <= 0x39)
  {
    // 0, 1, 2, 3, 4, 5, 6, 7, 8, 9
    value = value - 0x30;
  }
  else if (value >= 0x61 && value <= 0x66)
  {
    // a, b, c, d, e, f
    value = value - 0x57;
  }
  else if (value >= 0x41 && value <= 0x46)
  {
    // A, B, C, D, E, F
    value = value - 0x37;
  }
  
  return value;
}
