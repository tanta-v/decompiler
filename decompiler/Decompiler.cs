using decompiler.Exceptions;
using System;
using System.IO;
using System.Text;
using static decompiler.Utility;
namespace decompiler
{
    public enum DecompileType
    {
        ASM
    }
    public class Section
    {
        public string Name;
        public Section(string name)
        {
            Name = name;
        }
    }
    public class Decompiler
    {
        private byte[] exeData; // loading into memory is not viable with large executables 
        private DecompileType decompileType;
        private Section[] sections;
        public Decompiler(string toDecompilePath, DecompileType type = DecompileType.ASM)
        {
            exeData = File.ReadAllBytes(toDecompilePath);
            // TODO: write code that decompiles .headers
            int peHeaderOffset = toInt32(exeData, 0x3C);
            string peSig = Encoding.UTF8.GetString(exeData, peHeaderOffset, 4);
            if (peSig != "PE\0\0") throw new InvalidPeHeaderOffsetException(); // invalid pe header offset exception..
            log($@"PeHeaderSig @ 0x{peHeaderOffset.ToString("X")} >> {peSig}");
            sections = readSections(exeData, peHeaderOffset);
        }
        private Section[] readSections(byte[] exedata, int peHeaderOffset)
        {
            Section[] sectionList = new Section[toInt32(exedata, peHeaderOffset + 6)];
            log(sectionList.Length);
            int sectionHeaderOffset = peHeaderOffset + 24;
            for (int i = 0; i < sectionList.Length; i++)
            {
                string sectionName = getStringAscii(exedata, sectionHeaderOffset + i * 40, 8).Trim('\0');
                log($@"Section {sectionName} found @ {sectionHeaderOffset + i * 40}");
            }
            throw new UnableToGetSectionsException("Empty");
        }
    }
}
