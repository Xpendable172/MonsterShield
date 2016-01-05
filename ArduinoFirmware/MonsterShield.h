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
      
*/

#ifndef MonsterShield_h
#define MonsterShield_h

// The following includes are required to make the MonsterShield library work:
#include <inttypes.h>
#include <EEPROM.h>
#include <Wire.h>
#include <SoftwareSerial.h>

#define MONSTERSHIELD_CONFIG_ADDRESS            0   // Put the settings at the beginning of the memory.
#define MONSTERSHIELD_BYTES_PER_EVENT           2
#define MONSTERSHIELD_MAX_RELAY_COUNT           16
#define MONSTERSHIELD_MAX_SLOT_COUNT            15
#define MONSTERSHIELD_INPUT_BUTTON_CHECK_DELAY  5
#define MONSTERSHIELD_TIMING                    50   // 50 = 1/20th of a second
#define MONSTERSHIELD_BUFFER_SIZE               64   // This is basically our page size when reading and writing to the EEPROM chip.  Don't mess with this!
#define MONSTERSHIELD_CONTROLPAGE_SIZE          128
#define MONSTERSHIELD_RECORD_MODE_NORMAL        0    // Allows to overlay record what was already recorded
#define MONSTERSHIELD_RECORD_MODE_CLEAR_TRACK   1    // Clear just the current track
#define MONSTERSHIELD_RECORD_MODE_ERASE_ALL     2    // Reset the entire animation!

#define MONSTERSHIELD_BLINK_RECORD              100
#define MONSTERSHIELD_BLINK_PLAY                250
#define i2cBaseAddress 0b0100000

#define MONSTERSHIELD_NUMBUTTONS 13

#define MONSTERSHIELD_NEXT_ANIM_SELECT_SEQUENTIAL         0
#define MONSTERSHIELD_NEXT_ANIM_SELECT_RANDOM             1
#define MONSTERSHIELD_NEXT_ANIM_SELECT_SINGLE             2

// Interrupt return codes
////////////////////////////////////////////
#define MONSTERSHIELD_INT_TRIGGER_0    0
#define MONSTERSHIELD_INT_TRIGGER_1    1
#define MONSTERSHIELD_INT_TRIGGER_2    2
#define MONSTERSHIELD_INT_TRIGGER_3    3
#define MONSTERSHIELD_INT_BUTTON_0     10
#define MONSTERSHIELD_INT_BUTTON_1     11
#define MONSTERSHIELD_INT_BUTTON_2     12
#define MONSTERSHIELD_INT_KEYPAD_0     20
#define MONSTERSHIELD_INT_KEYPAD_1     21
#define MONSTERSHIELD_INT_KEYPAD_2     22
#define MONSTERSHIELD_INT_KEYPAD_3     23
#define MONSTERSHIELD_INT_KEYPAD_4     24
#define MONSTERSHIELD_INT_SERIAL       100
#define MONSTERSHIELD_INT_UNKNOWN      255

// Keypad digital I/O pins
/////////////////////////////////
#define MONSTERSHIELD_KEYPAD_RECORD         8
#define MONSTERSHIELD_KEYPAD_PLAY           9
#define MONSTERSHIELD_KEYPAD_OUT4           9
#define MONSTERSHIELD_KEYPAD_ENABLE        10
#define MONSTERSHIELD_KEYPAD_OUT3          10
#define MONSTERSHIELD_KEYPAD_NEXT          11
#define MONSTERSHIELD_KEYPAD_OUT2          11
#define MONSTERSHIELD_KEYPAD_PREV          12
#define MONSTERSHIELD_KEYPAD_OUT1          12


class MonsterShield
{
public:
  
  
  MonsterShield();  // MonsterShield class constructor
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // Init
  //////////////////////////////////////////////////////////////////////////////////////////////////
  void init();  // Make sure you call this in your setup() routine!
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // Relay outputs
  //////////////////////////////////////////////////////////////////////////////////////////////////
  void setRelay(uint8_t relay, uint8_t value);     // Sets a relay bit on or off.  Nothing actually happens until latchRelays() is called.
  void setAllRelays(uint8_t value);                // Sets and latches all relays to either on or off
  void latchRelays();                              // Executes all the state changes to the relays on the actual hardware.
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // Input Triggers
  //////////////////////////////////////////////////////////////////////////////////////////////////
  void waitOnTrigger(int trigger);
  bool isTriggerSensed(uint8_t trigger);           // Canned routine to determine if a trigger has been sensed, taking into account the follwing factors:
                                                   // threshold (voltage), sensitivity (debounce), cooldown, trigger on HIGH vs. LOW, trigger on state reset only
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // On-board MonsterShield input buttons
  //////////////////////////////////////////////////////////////////////////////////////////////////
  uint8_t getInputButtonState(uint8_t button);   // Used mostly internally.  Recommend end-users to use inputButtonPress() instead.
  bool inputButtonPress(int button);             // Checks whether an on-board MonsterShield input button has been pressed and released.
  unsigned long inputButtonPressLength(int button) { return inputButtonPressRelease[button]; }  // Returns how long the button was held down before released in milliseconds.
  
  // Canned routine that works simliar to the getKeypadMenuChoice, except you use the prev/next buttons on the MonsterShield
  // to cycle through the numbers betwen minnum and maxnum.  If no choice is made for timeout seconds since the last button
  // press, then the last choosen option is the result.  Returns the number that was selected.  -1 indicates no selection made.
  int getInputMenuChoice(char displaydigit, int8_t minnum, int8_t maxnum, int8_t timeout);
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // 7-Segment LED display
  //////////////////////////////////////////////////////////////////////////////////////////////////
  bool    showDigitDecimal;   // Indicates whether decimal point should be turned on or off on 7-segment display
  void setDigitChar(char c);  // Sets the 7-segment display to the character defined in the code.
                              // Valid characters are: '0' '1' '2' '3' '4' '5' '6' '7' '8' '9' 'A' 'B' 'C' 'D' 'E' 'F' ' ' 'P' '-' 'r'

  void setDigit(uint8_t value); // Numeric table index of a character for the 7-segment display.  See the MonsterShield.cpp for details.
  void setDigitToActiveSlot();  // Display the currently active slot on the 7-segment display.
  

  //////////////////////////////////////////////////////////////////////////////////////////////////
  // LED lights (on-board MonsterShield)
  //////////////////////////////////////////////////////////////////////////////////////////////////
  void setLed(uint8_t led, bool value); // led = 0 or 1.
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // MP3 Player
  //////////////////////////////////////////////////////////////////////////////////////////////////
  void mp3Play(uint8_t index);        // Plays [index] MP3 file.  The index represents the order the files were copied to the SD Card.
  void mp3Stop();                     // Stops the MP3 player.
  void mp3Pause();                    // Pauses the current MP3 track.
  void mp3Resume();                   // Resumes the current MP3 track.
  void mp3Volume(uint8_t index);      // Sets the volume of the MP3 player.  Valid values are 00 (lowest) to 31 (highest).
 
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // Animation Playback
  //////////////////////////////////////////////////////////////////////////////////////////////////
  
  // Canned routine to play animation slot.  Takes care of everything for you.  Function returns
  // when routine is complete.  Does not allow for interrupts or button presses during playback.
  // If you want to do other processing (such as triggers, inputs, etc) during playback, then
  // you should use the playAnimationWithInterrupts() function or use the stepAnimationStart()
  // and stepAnimationNext functions.
  void playAnimation(int slot);      
                                     
  
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
  uint8_t playAnimationWithInterrupts(int slot, bool repeat, bool ambient, uint8_t triggermask, uint8_t inputmask, uint8_t keypadmask);

  // You use this routine if you want to do some of your own processing while an animation is playing.  Basically you call
  // stepAnimationStart() to initialize everything for playback, and then you call stepAnimationNext() in a loop checking the return code. 
  void stepAnimationStart(int slot);
  
  // Same as stepAnimationStart(int slot) except it takes an extra flag that causes "A" to flash on the display instead of the normal slot number.
  void stepAnimationStart(int slot, bool ambientFlag);
  
  // Equivalent to calling stepAnimationStart(int slot, true)
  void stepAnimationAmbientStart(int slot);
  
  // You call this function in a loop.  Each time you call stepAnimationNext(), it determines if it is time for the next 1/20th of a second event
  // to occur, and if it is, it changes the state of the relays accordingly.  It returns true if the animation loop should continue.
  // Here's an example of how you use this:
  //    stepAnimationStart(1);  
  //    while ( stepAnimationNext() == true )
  //    {
  //          // Do your stuff here
  //    }
  bool stepAnimationNext();


  //////////////////////////////////////////////////////////////////////////////////////////////////
  // Animation Recording
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // This is a canned routine to handle standard recording of animations to an animation slot.
  // This routine prompts the user to answer 3 questions before recording begins:
  //   1.  Which bank do you want to record?  (Flashes 'b')  Press 0, 1, 2, or 3 then press record.
  //   2.  Which relay/track do you want to record?  (Flashes 'r').  Press record (for all) or 0, 1, 2, 3 then record
  //   3.  Which recording mode?  (Flashes 'C').  Press 0, 1, or 2 then press record.  0 = overlay track.  1 = erase track.  2 = erase entire animation.
  void recordAnimation(int slot);

  // Initialize everything in preparation to live record an animation.
  void stepRecordStart(int slot, int8_t track, int8_t recordmode);
  
  // Call this in a loop to record your animation and do your own actions inside the loop.
  // Recommend you study the "recordAnimation()" function to see how this is done.
  bool stepRecordNext(bool abort);
  
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // EEPROM Memory Functions
  //////////////////////////////////////////////////////////////////////////////////////////////////
  
  // Returns true or false to indicate whether EEPROM bank 1 or 2 is populated with a chip on the 
  // MonsterShield Expander board.  bank can be 1 or 2 on the expander board
  bool eepromInstalled(uint8_t bank);

  // Reads 64 bytes from the selected EEPROM chip at the selected address and writes them to the 
  // byte buffer pointed to by *buff.
  void eepromReadPage(uint8_t bus_address, uint16_t memory_address, uint8_t *buff);
  
  // Simplifies reading animation data from the EEPROM chip(s).  All you have to do is indicate
  // which slot you want to read data from, and which page (64 bytes) you want to read.
  void eepromReadSlotData( uint8_t slot, uint16_t page);
  
  // Writes 64 bytes of data to the selected EEPROM chip at memory_address 
  // from the memory buffer at address *buff. 
  void eepromWritePage(uint8_t bus_address, uint16_t memory_address, uint8_t *buff);
  
  // Simplifies writing animation data to the EEPROM chip(s). Just supply which slot number
  // and which 64-byte page you want to write.
  void eepromWriteSlotData( uint8_t slot, uint16_t page);
  
  // Reloads the MonsterShield configuration settings from EEPROM.
  void eepromReadSettings();
  
  // Writes the MonsterShield configuration settings to EEPROM.
  void eepromWriteSettings();
  
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // Keypad functions
  //////////////////////////////////////////////////////////////////////////////////////////////////
  
  // Returns true if the digital I/O pin (on the detachable keypad) was pressed and released.
  bool keypadButtonPress(int button);
  
  // Length of time in milliseconds that a keypad button was pressed and released.
  unsigned long keypadButtonPressLength(int button) { return keypadButtonPressRelease[button]; }
  
  // Canned routine to get a user option.  displaydigit is a character to display on the 7-segment display.  User presses 
  // the desired keypad buttons to cycle through the options and presses the record to select the option.
  // Returns the number selected. If -1 is returned, then no option was selected.  (sort of like a cancel)
  int GetKeypadMenuChoice(char displaydigit, int8_t maxnum);
  

  //////////////////////////////////////////////////////////////////////////////////////////////////
  // Slot operations
  //////////////////////////////////////////////////////////////////////////////////////////////////
  void setActiveSlot(int slot);                    // This function sets the currently active slot.
  int getActiveSlot();                             // This function returns the currently active slot.
  void setNextSlot();                              // This function sets the currently active slot to the next slot.
  void setPreviousSlot();                          // This function sets the currently active slot to the previous slot.
  bool getSlotEnabled(int slot);                   // Returns true if the slot number is enabled.  Disabled slots are skipped when triggered.
  void setSlotEnabled(int slot, bool value);       // Sets whether the slot number is enabled or not.
  void selectNextAnimation(); // Selects an animation based on the which playbackMode is active.
  
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // Animation selection modes
  //////////////////////////////////////////////////////////////////////////////////////////////////  
  void setAnimationSelectMode(int mode);    // Determines how the next slot is selected after an animation is played. 0=sequential, 1=random, 2=single (stay on slot)
  void setNextAnimationSelectMode();        // Cycles to the next animation selection mode.
  void showAnimationSelectMode();           // Routine to briefly display on the 7-segment display which selection mode is active.
  
  
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // Ambient flags
  //////////////////////////////////////////////////////////////////////////////////////////////////  
  void setPlayAmbient(bool value);          // Sets whether ambient mode is currently active.  This controls some internal stuff.  Ambient mode means a slot is played continously in a loop until
                                            // a trigger is sensed.  After the triggered animation is played, the MonsterShield goes back to playing the ambient animation in a continous loop.
  bool getPlayAmbient();                    // Returns ehther ambient mode is currently on or off.
  
  
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // Serial port operations
  //////////////////////////////////////////////////////////////////////////////////////////////////  
  uint8_t processSerialPort();  // This should be called at least once per loop where we care about serial communications!
  
  
  
  //////////////////////////////////////////////////////////////////////////////////////////////////
  // Factory Reset
  //////////////////////////////////////////////////////////////////////////////////////////////////  
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
  void performFactoryReset();  
  
  
  
  
private:
  
  uint8_t i2cMemoryChip;     // = 0x54;
  uint8_t i2cMemoryChip1;    // EEPROM 1 on Expander Board  0x56
  uint8_t i2cMemoryChip2;    // EEPROM 2 on Expander Board  0x55
  uint8_t i2cDataArray[2];   // Small array used for i2c communications.
  bool    eeprom1;  // Indicates whether EEPROM1 is populated on the MonsterShield Expander board.
  bool    eeprom2;  // Indicates whether EEPROM2 is populated on the MonsterShield Expander board.
  
  uint8_t expanderBankA;  // Refers to the 8 "A" pins on the MCP23017
  uint8_t expanderBankB;  // Refers to the 8 "B" pins on the MCP23017
  
  uint8_t expanderUnit0;  // MonsterShield MCP23017 address
  uint8_t expanderUnit1;  // Expander board MCP23017 address
  
  uint8_t digitDisplay;   // 8 bits to represent 7 segment display on MonsterShield.

  uint8_t outputRelayCount; // Indicates how many relays we have.
  
  uint16_t relayStates;    // The current state of all 16 relays.
  bool     led1Indicator;  // Indicates whether LED1 should be on or off.  Controlled by the primary MCP23017 chip.
  
  uint8_t inputButtonStates;  // Current state of the on-board MonsterShield buttons.
  unsigned long inputButtonStatesLastCheck;  // Timestamp indicating when the on-board buttons were last checked.
  
  uint8_t triggerStates;                      // Current state of the trigger inputs
  unsigned long triggerLastStateChange[4];    // Time stamp (in milliseconds) of the last trigger state changes
  unsigned long triggerResetTimer;            // Timestamp of last animation playback completion.
  uint8_t triggerSensitivity[4];              // Sensitivity of each trigger.  Basically how long to debounce in 100's of a second.  i.e, a value of 100 = 10 seconds
  uint8_t triggerResetState[4];               // flag to indicate whether the trigger has been reset (state reversed) since last triggered.
  uint16_t triggerThreshold[4];               // trigger threshold.  Valid values are from 100 to 1023.  Default is 950.
  uint8_t triggerCooldown[4];                 // Number of seconds before a new trigger is allowed.
  uint8_t triggerOnVoltage[4];                // flag is either HIGH or LOW, indicates whether to trigger on HIGH state or LOW state.
  uint8_t triggerAfterResetOnly[4];           // Indicates whether we allow continous trigger or if the trigger must be reset before another trigger is allowed.

  void initPortExpander(uint8_t unit, uint8_t ioflagsA, uint8_t ioflagsB);        // Initialize selected MCP23017 chip.
  void setPortExpanderOutput(uint8_t address, uint8_t bank, uint8_t value);       // Set an output state for one of the 2 banks on a MCP23017 chip.
  void setPortExpanderOutputs(uint8_t address, uint16_t value);                   // Set all 16 output states on a MCP23017 chip using a single 16-bit integer.
  void setPortExpanderOutputs(uint8_t address, uint8_t valueA, uint8_t valueB);   // Sets all 16 output states on a MCP23017 chip using 2 separate 8-bit integers.
  uint8_t getDigitBitmaskChar(char c);     // Gets an 8-bit mask corresponding to the character from a table.
  uint8_t getDigitBitmask(uint8_t value);  // Gets an 8-bit mask correpsonding to an index from a table.
  
  uint8_t pagebuffer[128];      // Buffer for holding data read from / written to EEPROM(s)
  uint16_t controlPageAddress;  // Variable address for the control table (saved variables)
  uint8_t sequenceBuffer[72];  // Buffer for holding animation data
  uint16_t sequenceLength[MONSTERSHIELD_MAX_SLOT_COUNT];  // The number of events currently recorded for each animation.

  uint16_t slotEnabled;                // 16 bits representing whether a slot is enabled or not
  uint8_t slotCount;                   // Number of slots that the MonsterShield has configured.
  uint16_t eventsPerSlot;              // Number of events per slot.
  uint8_t nextAnimSelectMode;          // Specifies how to pick the next animation.
  bool PlayAmbientAnimation;           // Play Ambient animation flag
  uint16_t sequencePosition;           // Current position in the buffer during playback/record
  int currentSlot;                     // Currently selected/recording slot
  int playingSlot;                     // Currently playing slot
  uint16_t currentPage;                 // Current memory page during playback/record
  uint16_t currentByteCount;           // Number of bytes processed
  unsigned long nextEventTime;         // Timestamp of the next event to be played
  uint16_t trackMask;                  // 16 bits representing which slots we want to record on.
  uint16_t currentEventCount;          // Count of how many events have been recorded during recording.
  uint16_t prevEventCount;             // Previous event count before recording began.
  uint8_t recordMode;                  // Indicates whether we should overlaying or overwriting the animation on all banks, just a single bank, or a single track.
  int8_t _recordTrack;                 // Indicates which tracks to record on.
  bool ambientPlayFlag;                // Indicates we are currently playing an ambient animation.
  
  uint8_t watermark[4];                // Used to store which verison of firmware is loaded on the MonsterShield.  Helps prevent using the wrong editor with the wrong version of firmware.
                                       // Also used to determine whether we have started the MonsterShield for the first time after installing a new EEPROM chip, which causes an automatic
                                       // factory reset.

  uint8_t ConvertHexToByte(uint8_t value);  // Helper routine to convert an ASCII HEX character (0,1,2,3,4,5,6,7,8,9,A,B,C,D,E,F) to it's integer representation.  So 'A' converts to 0x0A.
  
  bool          keypadButtonLastState[MONSTERSHIELD_NUMBUTTONS];      // Indicates what the last state was for each of the keypad buttons.
  unsigned long keypadButtonPressStart[MONSTERSHIELD_NUMBUTTONS];     // Indicates when each button was pressed down.
  unsigned long keypadButtonPressRelease[MONSTERSHIELD_NUMBUTTONS];   // After a keypad button was released, indicates how long it was held down.
  
  bool          inputButtonLastState[3];    // Indicates what the last state was for each of the on-board input buttons
  unsigned long inputButtonPressStart[3];   // Indicates when each button was pressed down.
  unsigned long inputButtonPressRelease[3]; // After a button is released, indicates how long it was held down.

  unsigned long blinker;  // Internal timer for flashing stuff
  bool flipflop;          // Internal flag to cause things to flash / blink a regular intervals
  
  uint16_t calculateEventsPerSlot(int slots);  // Canned routine to calculate how many events per slot can be recorded.  There are 20 events per second.
                                               // This routine attempts to evenly distribute the slots across 1, 2 or 3 EEPROM chips.
  bool detectMemory(uint8_t bus);              // A test performed during factory reset to determine whether an EEPROM chip has been populated.  ONLY
                                               // perform this test during factory reset because it WILL result in data loss since a page of memory is written to.
  
  uint8_t memorySlotCount0;                    // Indicates how many slots exist on EEPROM 0.  Was previously calculated by calculateEventsPerSlot().
  uint8_t memorySlotCount1;                    // Indicates how many slots exist on EEPROM 1.  Was previously calculated by calculateEventsPerSlot().
  uint8_t memorySlotCount2;                    // Indicates how many slots exist on EEPROM 2.  Was previously calculated by calculateEventsPerSlot().

};



#endif
