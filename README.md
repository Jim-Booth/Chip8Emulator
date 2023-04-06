```
This CHIP8 emulator is functional but not perfect. 

The main form defaults to running the chip8 test ROM linked below.
https://github.com/Skosulor/c8int/tree/master/test

It passes various other test ROMS but fails elements of others due to the various implemetation quirks outlined at the link below.
https://games.gulrak.net/cadmium/chip8-opcode-table.html

...however mine plays/runs most CHP8 ROMS. 

Keys are matched in the main form as below

Keypad       Keyboard
+-+-+-+-+    +-+-+-+-+
|1|2|3|C|    |1|2|3|4|
+-+-+-+-+    +-+-+-+-+
|4|5|6|D|    |Q|W|E|R|
+-+-+-+-+ => +-+-+-+-+
|7|8|9|E|    |A|S|D|F|
+-+-+-+-+    +-+-+-+-+
|A|0|B|F|    |Z|X|C|V|
+-+-+-+-+    +-+-+-+-+

Please see Form1.cs for more info how to implement and use this Chip8 Emulator.
```
