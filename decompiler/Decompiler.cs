﻿using decompiler.Exceptions;
using System;
using System.IO;
using System.Text;
using static decompiler.Utility;
using Newtonsoft.Json;
using System.Reflection;
using System.Net.Http;
using decompiler.DecompilerRules;
using System.Text.RegularExpressions;
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
            for (int i = 0; i < sect.Length; i++)
            {
                byte opcode = sect[i];
                //string opcodest = Decompiler.rLoader.Decode(sect, ref i);
                //Content += $@"{opcodest}\n";
            }
            //log(Content[0]);
        }
    }
    public class Decompiler
    {
        private byte[] exeData; // loading into memory is not viable with large executables 
        private DecompileType decompileType;
        private Section[] sections;
        private CodeType codeType;
        private Thread[] decompilerThreads;
        //public static DecompilerRuleHandler rLoader;
        public Decompiler(string toDecompilePath, string ruleLocPath = null, DecompileType type = DecompileType.ASM)
        {
            exeData = File.ReadAllBytes(toDecompilePath);
            // TODO: write code that decompiles .headers
            int peHeaderOffset = toInt32(exeData, 0x3C);
            string peSig = Encoding.UTF8.GetString(exeData, peHeaderOffset, 4);
            if (peSig != "PE\0\0") throw new InvalidPeHeaderOffsetException(); // invalid pe header offset exception..
            log($@"PeHeaderSig @ 0x{peHeaderOffset.ToString("X")} >> {peSig}");

            ushort cType = toUInt16(exeData, peHeaderOffset + 4);
            if (cType == 0x8664) codeType = CodeType.x64; else codeType = CodeType.x32;

            //rLoader = new DecompilerRuleHandler(codeType);
            downloadCodeRules(codeType); // temp
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
            /*foreach (byte invalidbyte in DecompilerRuleHandler.invalidOpCodeList)
            {
                log($@"Unregistered byte >> {invalidbyte}");
            }*/
            return sectionList;
        }



        public static void downloadCodeRules(CodeType codeType)
        {
            string tagRip(string inp)
            {
                return Regex.Replace(inp, "<.*?>", string.Empty);
            }
            string url = @$"http://ref.x86asm.net/coder64.html";
            string temppath = Path.Combine(Directory.GetCurrentDirectory(), "x64.coderules");

            if (codeType == CodeType.x32) {
                url = @$"http://ref.x86asm.net/coder32.html";
                temppath = Path.Combine(Directory.GetCurrentDirectory(), "x32.coderules");
            }
            using (HttpClient clnt = new HttpClient())
            {
                log(@$"Downloading from {url}...");
                string content = clnt.GetStringAsync(url).Result;
                File.WriteAllText(temppath + ".part", content);
                content = "";
            }
            List<DecompilerInstruction> decompilerInstList = new List<DecompilerInstruction>();
            using (FileStream file = File.OpenRead(temppath + ".part"))
            using (StreamReader srdr = new StreamReader(file))
            {
                string cline;
                int stage = 0;
                StringBuilder rst = new StringBuilder();
                StringBuilder rst2 = new StringBuilder();
                while ((cline = srdr.ReadLine()) != null)
                {
                    switch (stage)
                    {
                        case 0:
                            if (cline.Contains("<table cellpadding=\"2\" rules=\"groups\" class=\"ref_table notpublic\"")) stage = 1;
                            break;
                        case 1:
                            rst.Append(cline);
                            if (cline.Contains("<div")) stage = 2;
                            break;
                        case 2:
                            if (cline.Contains("two-byte")) stage = 3;
                            break;
                        case 3:
                            rst2.Append(cline);
                            if (cline.Contains("</table>")) stage = 4;
                            break;
                    }
                }
                string[] st = rst.ToString().Split("<TBODY id=\"");
                
                Parallel.For(1, st.Length, i =>
                {
                    string[] a = st[i].ToLower().Split("<td>");
                    decompilerInstList.Add(new DecompilerInstruction(
                            
                        ))
                });
            }
        }
    }
}
