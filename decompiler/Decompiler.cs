using decompiler.Exceptions;
using System;
using System.IO;
using System.Text;
using static decompiler.Utility;
using Newtonsoft.Json;
using System.Reflection;
using System.Net.Http;
using decompiler.DecompilerRules;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using System.Collections.Concurrent;
using System.Reflection.Emit;
using SharpDisasm;
namespace decompiler
{
    public enum DecompileType
    {
        ASM
    }
    public enum DecompileMethod
    {
        NATIVE,
        SHARP
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
        public int invalidOpCodeNum;
        public List<byte> invalidOpCodes;
        public Section(string name, byte[] content)
        {
            Name = name;
            invalidOpCodes = new List<byte>();
            invalidOpCodeNum = 0;
            Content = string.Empty;
            convertBytesToReadable(content);
        }
        private void convertBytesToReadable(byte[] sect)
        {
            var contentBag = new ConcurrentBag<string>();
            var invalidOpCodesBag = new ConcurrentBag<byte>();
            var invalidOpCodeNumBag = new ConcurrentBag<int>();
            var stringBag = new ConcurrentBag<string>(); // Stores extracted strings

            StringBuilder asciiBuilder = new StringBuilder();
            StringBuilder unicodeBuilder = new StringBuilder();

            int chunkSize = sect.Length / 4;

            Parallel.For(0, 4, i =>
            {
                int startIdx = i * chunkSize;
                int endIdx = (i == 3) ? sect.Length : startIdx + chunkSize;

                for (int j = startIdx; j < endIdx; j++)
                {
                    if (j >= sect.Length) break;

                    byte opcode = sect[j];
                    DecompilerInstruction? instruction = Decompiler.instructionList.FirstOrDefault(inst =>
                        inst.PrimaryOpcode == opcode.ToString("X2"));

                    if (instruction != null)
                    {
                        StringBuilder operands = new StringBuilder();

                        if (!string.IsNullOrEmpty(instruction.Operand1) && j + 1 < sect.Length)
                        {
                            operands.Append(FormatOperand(instruction.Operand1, sect[j + 1], j));
                            j++;
                        }

                        if (!string.IsNullOrEmpty(instruction.Operand2) && j + 1 < sect.Length)
                        {
                            if (operands.Length > 0) operands.Append(", ");
                            operands.Append(FormatOperand(instruction.Operand2, sect[j + 1], j));
                            j++;
                        }

                        string instructionText = $"{instruction.InstructionMnemonic} {operands.ToString()}\n";
                        contentBag.Add(instructionText);
                        log(instructionText);
                    }
                    else
                    {
                        string invalidText = $"DB 0x{opcode:X2}\n";
                        contentBag.Add(invalidText);
                        log(invalidText);

                        invalidOpCodeNumBag.Add(1);
                        invalidOpCodesBag.Add(opcode);
                    }

                    // **Check Next Bytes for String or Instruction**
                    // Check if the next bytes represent a valid instruction
                    bool isInstruction = false;

                    if (j + 1 < sect.Length)
                    {
                        byte nextOpcode = sect[j + 1];
                        DecompilerInstruction? nextInstruction = Decompiler.instructionList.FirstOrDefault(inst =>
                            inst.PrimaryOpcode == nextOpcode.ToString("X2"));

                        if (nextInstruction != null)
                        {
                            isInstruction = true;
                        }
                    }

                    // If next bytes are not an instruction, check if they form a string
                    if (!isInstruction)
                    {
                        // Check for printable ASCII characters
                        if (opcode >= 0x20 && opcode <= 0x7E)
                        {
                            asciiBuilder.Append((char)opcode);
                        }
                        else
                        {
                            if (asciiBuilder.Length >= 4) // Minimum string length to log
                            {
                                string foundString = asciiBuilder.ToString();
                                stringBag.Add(foundString);
                                log($"Extracted String: {foundString}");
                            }
                            asciiBuilder.Clear();
                        }

                        // Check for potential Unicode (UTF-16 LE) strings
                        if (j < sect.Length - 1 && opcode >= 0x20 && opcode <= 0x7E && sect[j + 1] == 0x00)
                        {
                            unicodeBuilder.Append((char)opcode);
                            j++; // Skip next null byte
                        }
                        else
                        {
                            if (unicodeBuilder.Length >= 4) // Minimum string length to log
                            {
                                string foundString = unicodeBuilder.ToString();
                                stringBag.Add(foundString);
                                log($"Extracted Unicode String: {foundString}");
                            }
                            unicodeBuilder.Clear();
                        }
                    }
                }
            });

            // Merge results
            Content = string.Join("", contentBag);
            invalidOpCodeNum = invalidOpCodeNumBag.Count;
            invalidOpCodes = invalidOpCodesBag.ToList();
            log(string.Join("\n", stringBag));
        }


        private string FormatOperand(string operandType, ushort operandValue, int currentIndex)
        {
            switch (operandType)
            {
                case "m8":
                case "r/m8":
                case "r/m16":
                case "r/m32":
                case "r/m64":
                case "r/m16/32/64":
                    return $"[0x{operandValue:X2}]";  // Memory address representation
                case "m":
                case "m48":
                case "m80":
                    return $"{operandType} 0x{operandValue:X2}";  // Memory operand for descriptor tables

                case "r8":
                    return $"r8 0x{operandValue:X2}";  // 8-bit Register

                case "imm8":
                    return $"0x{operandValue:X2}";  // Immediate 8-bit value

                case "imm16":
                    return $"0x{operandValue:X4}";  // Immediate 16-bit value

                case "imm16/32":
                case "imm16/32/64":
                    return $"0x{operandValue:X8}";  // Immediate 16/32/64-bit value

                case "eFlags":
                case "Flags":
                case "rBP":
                case "rDX":
                case "GS":
                case "FS":
                case "SS":
                case "EAX":
                case "r32/64":
                case "r16/32/64":
                case "rAX":
                case "rCX":
                case "ECX":
                case "RCX":
                case "R11":
                case "CL":
                case "AL":
                case "AH":
                case "IA32_BIOS_SIG":
                case "GDTR":
                case "LDTR":
                case "CRn":
                case "DRn":
                case "MSR":
                    return operandType;  // Special registers and flags

                case "mm":
                    return $"mm 0x{operandValue:X2}";  // mm register

                case "mm/m64":
                    return $"mm/m64 0x{operandValue:X2}";  // mm/m64 register

                case "xmm":
                    return $"xmm 0x{operandValue:X2}";  // xmm register

                case "xmm/m128":
                    return $"xmm/m128 0x{operandValue:X2}";  // xmm/m128 register

                case "m64":
                case "m32/64":
                    return $"m64 0x{operandValue:X2}";  // 64-bit memory

                case "m128":
                    return $"m128 0x{operandValue:X2}";  // 128-bit memory

                case "ST":
                case "STi/m32real":
                    return $"{operandType} 0x{operandValue:X2}";  // ST register or m32 real

                case "m32real":
                    return $"m32real 0x{operandValue:X2}";  // m32 real value

                case "r/m":
                    return $"r/m 0x{operandValue:X2}";  // General r/m format

                case "r":
                    return $"r 0x{operandValue:X2}";  // General register

                case "m512":
                    return $"m512 0x{operandValue:X2}";  // 512-bit memory

                case "rel8":
                    return $"rel8 0x{operandValue:X2}";  // 8-bit relative address

                case "rel16/32":
                    return $"rel 0x{operandValue:X4}";  // 16/32-bit relative address

                case "m16int":
                    return $"m16int 0x{operandValue:X4}";  // Memory 16-bit integer

                case "m64real":
                    return $"m64real 0x{operandValue:X8}";  // Memory 64-bit real

                case "m16/32/64":
                    return $"m{operandValue:X2}";  // General memory operand

                case "moffs16/32/64":
                    return $"moffs{operandValue:X8}";  // Memory offset (16/32/64-bit)

                case "moffs8":
                    return $"moffs8 0x{operandValue:X2}";  // 8-bit memory offset

                default:
                    return $"Unknown Operand Format: {operandType}, Value: 0x{operandValue:X2}";
            }
        }


    }
    public class Decompiler
    {
        private byte[] exeData; // loading into memory is not viable with large executables 
        private DecompileType decompileType;
        
        private Section[] sections;
        private CodeType codeType;
        private Thread[] decompilerThreads;
        public static List<DecompilerInstruction> instructionList;
        public DecompileMethod decompileMethod;
        public List<object> exeInstructionList;
        //public static DecompilerRuleHandler rLoader;
        public Decompiler(string toDecompilePath, string ruleLocPath = null, DecompileType type = DecompileType.ASM, DecompileMethod method = DecompileMethod.SHARP)
        {
            exeInstructionList = new List<object>();
            exeData = File.ReadAllBytes(toDecompilePath);
            // TODO: write code that decompiles .headers
            int peHeaderOffset = toInt32(exeData, 0x3C);
            string peSig = Encoding.UTF8.GetString(exeData, peHeaderOffset, 4);
            if (peSig != "PE\0\0") throw new InvalidPeHeaderOffsetException(); // invalid pe header offset exception..
            log($@"PeHeaderSig @ 0x{peHeaderOffset.ToString("X")} >> {peSig}");

            ushort cType = toUInt16(exeData, peHeaderOffset + 4);
            if (cType == 0x8664) codeType = CodeType.x64; else codeType = CodeType.x32;

            switch (method)
            {
                case DecompileMethod.NATIVE: // honestly, kinda gave up on this. Problem is, I'd rather be doing anything else. Probably **technically** usable, but good luck. Almost a second faster, though.
                    
                    //rLoader = new DecompilerRuleHandler(codeType);
                    downloadCodeRules(codeType); // insures code decompiler actually has access to the byte = mnemonic conversion that this app is designed to run
                    sections = readSections(exeData, peHeaderOffset);
                    break;
                case DecompileMethod.SHARP:
                    // Determine the architecture mode or us 32-bit by default
                    ArchitectureMode mode = ArchitectureMode.x86_32;
                    
                    // Configure the translator to output instruction addresses and instruction binary as hex
                    Disassembler.Translator.IncludeAddress = true;
                    Disassembler.Translator.IncludeBinary = true;

                    // Create the disassembler
                    var disasm = new Disassembler(
                        exeData,
                        mode, 0, true);
                    // Disassemble each instruction and output to console
                    foreach (var insn in disasm.Disassemble())
                        Console.Out.WriteLine(insn.ToString());
                    break;
            }
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

            Task.WhenAll(tasks).Wait();
            return sectionList;
        }



        public static void downloadCodeRules(CodeType codeType) // i just absolutely despise html 
        {
            string tagRip(string inp)
            {
                return Regex.Replace(inp, "<.*?>", string.Empty); // Remove any HTML tags
            }

            string url = @$"http://ref.x86asm.net/coder64.html";
            string temppath = Path.Combine(Directory.GetCurrentDirectory(), "x64.coderules");

            if (codeType == CodeType.x32)
            {
                url = @$"http://ref.x86asm.net/coder32.html";
                temppath = Path.Combine(Directory.GetCurrentDirectory(), "x32.coderules");
            }
            if (File.Exists(temppath))
            {
                instructionList = JsonConvert.DeserializeObject<List<DecompilerInstruction>>(File.ReadAllText(temppath)) ?? throw new RuleLoaderException();
                return;
            }
            // Download the HTML content and save it to a temporary file if not already downloaded
            if (!File.Exists(temppath + ".part"))
            {
                using (HttpClient clnt = new HttpClient())
                {
                    log(@$"Downloading from {url}...");
                    string content = clnt.GetStringAsync(url).Result;
                    File.WriteAllText(temppath + ".part", content);
                    content = ""; // Clear content variable
                }
            }
            // Read and process the downloaded HTML file
            using (FileStream file = File.OpenRead(temppath + ".part"))
            using (StreamReader srdr = new StreamReader(file))
            {
                string htmlContent = srdr.ReadToEnd();
                HtmlDocument htmldoc = new HtmlDocument();
                htmldoc.LoadHtml(htmlContent);

                HtmlNodeCollection tbodies = htmldoc.DocumentNode.SelectNodes("//tbody");
                ConcurrentBag<DecompilerInstruction> tBodyMatches = new ConcurrentBag<DecompilerInstruction>() ;
                Parallel.ForEach(tbodies, tbody => // multithreaded, speed
                {
                    string tbodyId = tbody.GetAttributeValue("id", "");
                    if (string.IsNullOrEmpty(tbodyId)) return;
                    List<string> tdValues = [.. tbody.SelectNodes(".//td")?.Select(td => td.InnerText.Trim()) ?? new List<string>()];

                    tBodyMatches.Add(new DecompilerInstruction(
                        primaryOpcode: tdValues.ElementAtOrDefault(2),
                        instructionMnemonic: tdValues.ElementAtOrDefault(10),
                        prefix: tdValues.ElementAtOrDefault(0),
                        prefix0F: tdValues.ElementAtOrDefault(1),
                        secondaryOpcode: tdValues.ElementAtOrDefault(3),
                        registerOpcodeField: tdValues.ElementAtOrDefault(4),
                        processor: tdValues.ElementAtOrDefault(5),
                        documentationStatus: tdValues.ElementAtOrDefault(6),
                        modeOfOperation: tdValues.ElementAtOrDefault(7),
                        ringLevel: tdValues.ElementAtOrDefault(8),
                        lockPrefixFPU: tdValues.ElementAtOrDefault(9),
                        operand1: tdValues.ElementAtOrDefault(11),
                        operand2: tdValues.ElementAtOrDefault(12),
                        operand3: tdValues.ElementAtOrDefault(13),
                        operand4: tdValues.ElementAtOrDefault(14),
                        instructionExtensionGroup: tdValues.ElementAtOrDefault(15),
                        testedFlags: tdValues.ElementAtOrDefault(16),
                        modifiedFlags: tdValues.ElementAtOrDefault(17),
                        definedFlags: tdValues.ElementAtOrDefault(18),
                        undefinedFlags: tdValues.ElementAtOrDefault(19),
                        flagsValues: tdValues.ElementAtOrDefault(20),
                        descriptionNotes: tdValues.ElementAtOrDefault(21)
                    ));
                });
                log(tBodyMatches.Count);
                File.WriteAllText(temppath, JsonConvert.SerializeObject(tBodyMatches.ToList(), Formatting.Indented));
                instructionList = tBodyMatches.ToList();
            }


            File.Delete(temppath + ".part");
            
        }

    }
}
