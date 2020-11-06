using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NAudio.Wave;

namespace AudioToGbaAsm
{
    class Program
    {
        /// <summary>
        /// Based on rayguns code
        /// </summary>
        /// <param name="writefile"></param>
        /// <param name="buffer"></param>
        /// <param name="repeat"></param>
        /// <param name="freq"></param>
        /// <param name="songtableentry"></param>
        /// <param name="freespace"></param>
        static void AudioToSappyASM(string romname, string newromname, string writefile, List<sbyte> buffer, bool repeat, int freq, string songtableentry, string freespace)
        {
            int sampleSizeFreq = (int)Math.Round((double)(freq / 60));
            int i = 0;
            int chunksize = freq * 13;
            int filesize = buffer.Count;
            if (127 * chunksize < filesize)
            {
                Console.WriteLine("ErRoR!1! too long of a file");
                //# break;
            }

            Dictionary<int, List<sbyte>> part = new Dictionary<int, List<sbyte>>();
            int dictCounter = 0;


            //Dictionaries by sample and an index.
            for (int pc = 0; pc < filesize;)
            {
                int copySize = chunksize;
                if (copySize + pc > filesize)
                {
                    copySize = filesize - pc;
                }

                part.Add(dictCounter++, buffer.GetRange(pc, copySize));

                pc += copySize;
            }


            StringBuilder samplesBuilder = new StringBuilder();
            StringBuilder songBuilder = new StringBuilder();


            Console.WriteLine("Decoding parts, writing voice tables.");

            List<string> parsed = new List<string>();
            foreach (KeyValuePair<int, List<sbyte>> parts in part)
            {
                Console.WriteLine($"On part index {parts.Key}");
                samplesBuilder.Append($"\r\n\r\nvoice{parts.Key}:");
                songBuilder.Append("\r\n.byte 0xce,0xBD,0x" + parts.Key.ToString("X2") + ",0xcf,0x3C,0x60\r\n");
                for (int i2 = 0; i2 < parts.Value.Count; i2++)
                {
                    sbyte tbyte = parts.Value[i2];
                    if ((i2 % 16) == 0)
                    {
                        samplesBuilder.Append($"\r\n.byte 0x{tbyte.ToString("X2")}");
                    }
                    else
                    {
                        samplesBuilder.Append($",0x{tbyte.ToString("X2")}");
                    }

                    if (i2 % sampleSizeFreq == 0)
                    {
                        if (i2 % (sampleSizeFreq * 20) == 0)
                        {
                            songBuilder.Append("\r\n.byte 0x81");
                        }
                        else
                        {
                            songBuilder.Append(",0x81");
                        }
                    }
                }
            }

            //Add repeating code.
            if (repeat)
            {
                songBuilder.Append("\r\n.byte 0xb2\r\n.word song");
            }
            else
            {
                songBuilder.Append("\r\n.byte 0xb1,0x00");
            }

            //Build the voice table.
            StringBuilder voiceTableBuilder = new StringBuilder();

            for (int i3 = 0; i3 < 128; i3++)
            {
                if (i3 < part.Keys.Count)
                {
                    voiceTableBuilder.Append($".word 0x00003c08\r\n.word voice{i3}\r\n.word 0x00ff00ff\r\n");
                }
                else
                {
                    voiceTableBuilder.Append($".word 0x00003c08\r\n.word voice0\r\n.word 0x00ff00ff\r\n");
                }
            }

            //Build sappy asm script.
            StringBuilder outfile = new StringBuilder();
            outfile.Append($".gba \r\n .open \"{romname}\",\"{newromname}\",0x8000000\r\n");
            outfile.Append($".definelabel songtable,{songtableentry}\r\n");
            outfile.Append($".definelabel freespace,{freespace}\r\n\r\n");
            outfile.Append(".org songtable\r\n.word songheader\r\n.org freespace\r\n");
            outfile.Append(".align 4\r\nsongheader:\r\n");
            outfile.Append(".byte 1\r\n.byte 0\r\n.byte 0xc3\r\n.byte 0\r\n.word voicetable\r\n.word song\r\n");
            outfile.Append(".align 4\r\nsong:\r\n.byte 0xBC,0x00,0xBE,0x50\r\n");
            outfile.Append($"{songBuilder}\r\n");
            outfile.Append(".align 4\r\nvoicetable:\r\n");
            outfile.Append($"{voiceTableBuilder}\r\n");
            outfile.Append($".align 4\r\n {samplesBuilder}");
            outfile.Append("\r\n.close");

            File.WriteAllText(writefile, outfile.ToString());
        }

        static void PrintUsage()
        {
            Console.WriteLine("Audio to Sappy ASM");
            Console.WriteLine("Supports Wav and Mp3 and assembles with ArmIPS");
            Console.WriteLine("Usage: ");
            Console.WriteLine("originalRom romAfterAsm pathtoaudiofile asmname titlename(mf or zm, if not zm or mf then it indicates titles freq) destoffset offsettopointer repeat(true or false");
            Console.WriteLine("\tExample: ");
            Console.WriteLine("\t\t testgame.gba newtestgame.gba song.mp3 song.asm mf 0x8800000 0x80a8d5c true");
        }

        static void Main(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("Not enough arguments");
                PrintUsage();
                return;
            }
            int argC = 0;
            string originalRomName = args[argC++];
            string newRomName = args[argC++];
            string srcFile = args[argC++];
            string asmFile = args[argC++];
            string title = args[argC++];
            string destAddress = args[argC++];
            string pointerAddress = args[argC++];
            string repeat = args[argC++];


            bool bRepeat = bool.Parse(repeat);
            //Add to support other games
            int mffreq = 10512;
            int zmfreq = 13379;
            int freq = 0;
            if (title.ToLower() == "mf")
            {
                freq = mffreq;
            }
            else if (title.ToLower() == "zm")
            {
                freq = zmfreq;
            }
            else
            {
                Console.WriteLine("Not Fusion or ZM, using freq " + title);
                freq = int.Parse(title);
            }

            FileInfo srcAudio = new FileInfo(srcFile);

            //we need to be 8bit and mono channel, apply desired frequency.
            var outFormat = new WaveFormat(freq, 8, 1);

            //Find out level of decode 
            WaveStream srcStream = null;

            if (srcAudio.Extension.ToLower() == ".mp3")
            {
                Console.WriteLine("Decoding mp3.");
                srcStream = new Mp3FileReader(srcFile);
            }

            if (srcAudio.Extension.ToLower() == ".wav")
            {
                Console.WriteLine("Decoding wav.");
                srcStream = new WaveFileReader(srcFile);
            }

            if (srcStream == null)
            {
                Console.WriteLine($"{srcAudio.Extension} is an unsupported format");
                return;
            }

            //Convert either source to wave.
            using (WaveFormatConversionStream conversionStream = new WaveFormatConversionStream(outFormat, srcStream))
            {
                using (RawSourceWaveStream raw = new RawSourceWaveStream(conversionStream, outFormat))
                {
                    //Convert to signed 8bit.
                    raw.Seek(0, SeekOrigin.Begin);
                    int len = 0;
                    List<sbyte> data = new List<sbyte>();
                    for (; len < raw.Length; len++)
                    {
                        sbyte n = Convert.ToSByte(raw.ReadByte() - 128);
                        data.Add(n);

                    }
                    //Generate 
                    AudioToSappyASM(originalRomName, newRomName, asmFile, data, bRepeat, freq, pointerAddress, destAddress);
                }
            }
        }
    }
}