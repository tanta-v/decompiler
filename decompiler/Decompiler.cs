using decompiler.Exceptions;
using System;
using System.IO;
using System.Text;
using static decompiler.Utility;
using Newtonsoft.Json;
using System.Reflection;
using decompiler.DecompilerRules;
namespace decompiler
{
    public enum DecompileType
    {
        ASM
    }
    public enum CodeType
    {
        x32,
        x64
    }
    public class Section
    {
        public string Name;
        public string Content;
        public Section(string name, byte[] content)
        {
            Name = name;
            Content = string.Empty;
            convertBytesToReadable(content);
        }
        private void convertBytesToReadable(byte[] sect)
        {
            RuleLoader r = Decompiler.rLoader;
            for (int i = 0; i < sect.Length; i++)
            {
                byte opcode = sect[i];
                string opcodest = string.Empty;

                
                Content += $@"{opcodest}\n";
                log(opcodest);
            }
            log(Content[0]);
        }
    }
    public class Decompiler
    {
        private byte[] exeData; // loading into memory is not viable with large executables 
        private DecompileType decompileType;
        private Section[] sections;
        private CodeType codeType;
        private Thread[] decompilerThreads;
        public static RuleLoader rLoader;
        public Decompiler(string toDecompilePath, string ruleLocPath, DecompileType type = DecompileType.ASM)
        {
            rLoader = new RuleLoader(ruleLocPath);
            exeData = File.ReadAllBytes(toDecompilePath);
            // TODO: write code that decompiles .headers
            int peHeaderOffset = toInt32(exeData, 0x3C);
            string peSig = Encoding.UTF8.GetString(exeData, peHeaderOffset, 4);
            if (peSig != "PE\0\0") throw new InvalidPeHeaderOffsetException(); // invalid pe header offset exception..
            log($@"PeHeaderSig @ 0x{peHeaderOffset.ToString("X")} >> {peSig}");

            ushort cType = toUInt16(exeData, peHeaderOffset + 4);
            if (cType == 0x8664) codeType = CodeType.x64; else codeType = CodeType.x32;


            sections = readSections(exeData, peHeaderOffset);
        }
        private Section[] readSections(byte[] exedata, int peHeaderOffset)
        {
            int numOfSections = toInt16(exedata, peHeaderOffset + 6);
            log(numOfSections);
            Section[] sectionList = new Section[numOfSections];
            int optHeaderSize = 0, sectionHeaderOffset;

            switch (codeType)
            {
                case CodeType.x32:
                    optHeaderSize = 224;
                    break;
                case CodeType.x64:
                    optHeaderSize = 240;
                    break;
            }
            sectionHeaderOffset = peHeaderOffset + 24 + optHeaderSize;

            Task[] tasks = new Task[numOfSections];

            for (int i = 0; i < numOfSections; i++)
            {
                int index = i; // Capture index for thread-safe access
                tasks[index] = Task.Run(() =>
                {
                    int sectionNameOffset = sectionHeaderOffset + index * 40;
                    string sectionName = getStringAscii(exedata, sectionNameOffset, 8).Trim('\0');
                    log($@"SectionName found @ {sectionNameOffset} >> {sectionName}");

                    int codeSectionOffset = toInt32(exedata, sectionNameOffset + 12);
                    int codeSectionSize = toInt32(exedata, sectionNameOffset + 16);
                    log($@"CodeSection offset @ 0x{codeSectionOffset} with a size of 0x{codeSectionSize}");

                    // Boundary check to ensure the copy operation is safe
                    if (codeSectionOffset + codeSectionSize <= exedata.Length)
                    {
                        byte[] codeSection = new byte[codeSectionSize];
                        Array.Copy(exedata, codeSectionOffset, codeSection, 0, codeSectionSize);
                        log(@$"Machine code extracted from .text successfully.");
                        sectionList[index] = new Section(sectionName, codeSection);
                    }
                    else
                    {
                        log($@"Error: Code section exceeds available data size. Skipping section {sectionName}.");
                    }
                });
            }

            // Wait for all tasks to complete
            Task.WhenAll(tasks).Wait();
            return sectionList;
        }
    }
}
