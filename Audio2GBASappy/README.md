# Audio2GBASappy
Wav and Mp3 sappy importer
Audio to Sappy ASM
Supports Wav and Mp3 and assembles with ArmIPS");
Usage:
originalRom romAfterAsm pathtoaudiofile asmname titlename(mf or zm, if not zm or mf then it indicates titles freq) destoffset offsettopointer repeat(true or false)
Example:
testgame.gba newtestgame.gba song.mp3 song.asm mf 0x8800000 0x80a8d5c true
Assemble the asm output with ARMIps
Based on rayguns original work