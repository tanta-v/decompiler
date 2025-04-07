using decompiler.Exceptions;
using System.Text;
using static decompiler.Utility;
using Newtonsoft.Json;
using decompiler.DecompilerRules;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using System.Collections.Concurrent;
namespace decompiler
{
    public enum DecompileType
    {
        ASM,
        kernelSYS
    }
    public enum RecompilationType
    {
        RAW = 0, // Raw. No real formatting applied, just direct byte-opcode translations.
        RAWSIMPLE = 1 // Simple. Some very limited formatting applied. For example, maps the application's start point and all functions.
    }
    public enum DecompileMethod
    {
        NATIVE
    }
    public enum CodeType
    {
        x32,
        x64
    }
    public class peSection
    {
        public string Name;
        public string Content;
        public int invalidOpCodeNum;
        public List<byte> invalidOpCodes;
        public peSection(string name, int codeSectionOffset = 0, int codeSize = 0)
        {
            Name = name;
            invalidOpCodes = new List<byte>();
            invalidOpCodeNum = 0;
            Content = string.Empty;
            convertBytesToReadableStream(Decompiler.filePath, codeSectionOffset, codeSize);
        }
        public peSection(string name, byte[] content)
        {
            Name = name;
            invalidOpCodes = new List<byte>();
            invalidOpCodeNum = 0;
            Content = string.Empty;
            convertBytesToReadable(content!);
            
        }
        public peSection(string name, string content)
        {
            Name = name;
            Content = content;
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
            int sldtCount = 0;

            Parallel.For(0, 4, i =>
            {
                int startIdx = i * chunkSize;
                int endIdx = (i == 3) ? sect.Length : startIdx + chunkSize;

                for (int j = startIdx; j < endIdx; j++)
                {
                    if (j >= sect.Length) break;

                    byte opcode = sect[j];
                    // Check for two-byte instructions (e.g., 0x0F as a prefix)
                    if (opcode == 0x0F && j + 1 < sect.Length)
                    {
                        byte nextOpcode = sect[j + 1];
                        string combinedOpcode = $"{opcode:X2}{nextOpcode:X2}";
                        DecompilerInstruction? instruction = Decompiler.instructionList.FirstOrDefault(inst =>
                            inst.PrimaryOpcode == combinedOpcode);

                        if (instruction != null)
                        {
                            StringBuilder operands = new StringBuilder();
                            if (!string.IsNullOrEmpty(instruction.Operand1) && j + 2 < sect.Length)
                            {
                                operands.Append(FormatOperand(instruction.Operand1, sect[j + 2], j));
                                j++;
                            }

                            if (!string.IsNullOrEmpty(instruction.Operand2) && j + 2 < sect.Length)
                            {
                                if (operands.Length > 0) operands.Append(", ");
                                operands.Append(FormatOperand(instruction.Operand2, sect[j + 2], j));
                                j++;
                            }

                            string instructionText = $"{instruction.InstructionMnemonic} {operands.ToString()}\n";
                            contentBag.Add(instructionText);
                        }
                        else // Handle invalid two-byte opcode
                        {
                            string invalidText = $"DB 0x{opcode:X2}{nextOpcode:X2}\n";
                            contentBag.Add(invalidText);
                            log(invalidText);

                            invalidOpCodeNumBag.Add(1);
                            invalidOpCodesBag.Add(opcode);
                        }
                        j++; // Skip the next byte after processing the two-byte instruction
                    }
                    else
                    {
                        // Handle single-byte instructions
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

                            string instructionText = $"{instruction.InstructionMnemonic} {operands}\n";
                            contentBag.Add(instructionText);

                            if (instruction.InstructionMnemonic == "SLDT" && operands.ToString().Contains("m16 0x00")) 
                            {
                                sldtCount++;
                            }
                        }
                        else // Handle invalid single-byte opcode
                        {
                            string invalidText = $"DB 0x{opcode:X2}\n";
                            contentBag.Add(invalidText);
                            log(invalidText);

                            invalidOpCodeNumBag.Add(1);
                            invalidOpCodesBag.Add(opcode);
                        }
                    }

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
            log(@$"{sldtCount} counts of sldt. {Name}");
            // Merge results
            Content = string.Join("", contentBag);
            invalidOpCodeNum = invalidOpCodeNumBag.Count;
            invalidOpCodes = invalidOpCodesBag.ToList();
            log(string.Join("\n", stringBag));
        }
        private void convertBytesToReadableStream(string filePath, int codeSectionOffset, int codeSize)
        {
            var contentBag = new ConcurrentBag<string>(); // Stores all instructions and opcodes
            var invalidOpCodesBag = new ConcurrentBag<byte>(); // Stores invalid opcodes
            var invalidOpCodeNumBag = new ConcurrentBag<int>(); // Stores count of invalid opcodes
            var stringBag = new ConcurrentBag<string>(); // Stores extracted strings

            StringBuilder asciiBuilder = new StringBuilder(); // Builds ASCII strings
            StringBuilder unicodeBuilder = new StringBuilder(); // Builds Unicode strings

            int fileChunkSize = (codeSize < Decompiler.maxFileChunkSize) ? codeSize : Decompiler.maxFileChunkSize;
            if (codeSectionOffset == 0) throw new SectionReaderException("Invalid section-code offset.");
            if (codeSize == 0) throw new SectionReaderException("Invalid section-code size.");
            int sldtCount = 0;
            using (FileStream OStrm = new(filePath, FileMode.Open, FileAccess.Read))
            {
                int nOChunks = codeSize / fileChunkSize;
                log($"{nOChunks} chunks @ {Name} >> {codeSize}");
                OStrm.Seek(codeSectionOffset, SeekOrigin.Begin);

                byte[] chunkBuffer = new byte[fileChunkSize * 2]; // Double-sized buffer: [chunk1][chunk2]
                byte[] chunkR = new byte[fileChunkSize];
                int currentChunk = 0;
                int currentChunkOffset = 0; // Keeps track of the position to insert new chunk
                
                while (currentChunk < nOChunks)
                {
                    try {
                        OStrm.Read(chunkR, 0, fileChunkSize);
                    }
                    catch (Exception e) { break; }

                    if (currentChunkOffset == 0) // Starting fresh with the first chunk
                    {
                        Array.Copy(chunkR, 0, chunkBuffer, currentChunkOffset, fileChunkSize);
                        currentChunkOffset = fileChunkSize; // Update offset

                    }
                    else // Shift the content of chunkBuffer left by fileChunkSize
                    {
                        Array.Copy(chunkBuffer, fileChunkSize, chunkBuffer, 0, fileChunkSize);
                        Array.Copy(chunkR, 0, chunkBuffer, fileChunkSize, fileChunkSize);

                    }

                    for (int byt = 0; byt < chunkR.Length; byt++) // Process bytes in chunkBuffer
                    {
                        byte opcode = chunkBuffer[byt];
                        if (opcode == 0x0F && byt + 1 < chunkBuffer.Length) // Two-byte instruction check
                        {
                            byte nextOpcode = chunkBuffer[byt + 1];
                            string combinedOpcode = $"{opcode:X2}{nextOpcode:X2}";
                            DecompilerInstruction? instruction = Decompiler.instructionList.FirstOrDefault(inst => inst.PrimaryOpcode == combinedOpcode);

                            if (instruction != null)
                            {
                                StringBuilder operands = new StringBuilder();
                                if (!string.IsNullOrEmpty(instruction.Operand1) && byt + 2 < chunkBuffer.Length)
                                {
                                    operands.Append(FormatOperand(instruction.Operand1, chunkBuffer[byt + 2], byt));
                                    byt++;
                                }
                                if (!string.IsNullOrEmpty(instruction.Operand2) && byt + 2 < chunkBuffer.Length)
                                {
                                    if (operands.Length > 0) operands.Append(", ");
                                    operands.Append(FormatOperand(instruction.Operand2, chunkBuffer[byt + 2], byt));
                                    byt++;
                                }

                                contentBag.Add($"{currentChunk * fileChunkSize}+{byt}: {instruction.InstructionMnemonic} {operands}\n");
                            }
                            else // Handle invalid opcode
                            {
                                contentBag.Add($"DB 0x{opcode:X2}{nextOpcode:X2}\n");
                                invalidOpCodeNumBag.Add(1);
                                invalidOpCodesBag.Add(opcode);
                            }
                            
                            byt++; // Skip the next byte for two-byte instruction
                        }
                        else // Handle single-byte instructions
                        {
                            DecompilerInstruction? instruction = Decompiler.instructionList.FirstOrDefault(inst => inst.PrimaryOpcode == opcode.ToString("X2"));
                            if (instruction != null)
                            {
                                StringBuilder operands = new StringBuilder();
                                if (!string.IsNullOrEmpty(instruction.Operand1) && byt + 1 < chunkBuffer.Length)
                                {
                                    operands.Append(FormatOperand(instruction.Operand1, chunkBuffer[byt + 1], byt));
                                    byt++;
                                }
                                if (!string.IsNullOrEmpty(instruction.Operand2) && byt + 1 < chunkBuffer.Length)
                                {
                                    if (operands.Length > 0) operands.Append(", ");
                                    operands.Append(FormatOperand(instruction.Operand2, chunkBuffer[byt + 1], byt));
                                    byt++;
                                }

                                if (instruction.InstructionMnemonic == "SLDT" && operands.ToString().Contains("m16 0x00"))
                                {
                                    sldtCount++;
                                    continue;
                                }
                                contentBag.Add($"{currentChunk * fileChunkSize}+{byt}: {instruction.InstructionMnemonic} {operands}\n");
                            }
                            else // Handle invalid opcodes
                            {
                                contentBag.Add($"DB 0x{opcode:X2}\n");
                                invalidOpCodeNumBag.Add(1);
                                invalidOpCodesBag.Add(opcode);
                            }
                        }

                        bool isInstruction = false;
                        if (byt + 1 < chunkBuffer.Length)
                        {
                            byte nextOpcode = chunkBuffer[byt + 1];
                            DecompilerInstruction? nextInstruction = Decompiler.instructionList.FirstOrDefault(inst => inst.PrimaryOpcode == nextOpcode.ToString("X2"));
                            byt++;
                            if (nextInstruction != null) isInstruction = true;
                        }

                        // ASCII and Unicode string extraction logic
                        if (!isInstruction)
                        {
                            if (opcode >= 0x20 && opcode <= 0x7E) asciiBuilder.Append((char)opcode);
                            else
                            {
                                if (asciiBuilder.Length >= 4)
                                {
                                    string foundString = asciiBuilder.ToString();
                                    stringBag.Add(foundString);
                                    log($"Extracted String: {foundString}");
                                }
                                asciiBuilder.Clear();
                            }

                            if (byt < chunkBuffer.Length - 1 && opcode >= 0x20 && opcode <= 0x7E && chunkBuffer[byt + 1] == 0x00)
                            {
                                unicodeBuilder.Append((char)opcode);
                                byt++;
                            }
                            else
                            {
                                if (unicodeBuilder.Length >= 4)
                                {
                                    string foundString = unicodeBuilder.ToString();
                                    stringBag.Add(foundString);
                                    log($"Extracted Unicode String: {foundString}");
                                }
                                unicodeBuilder.Clear();
                            }
                        }
                    }

                    currentChunk++; 
                }
            }
            Content = string.Join("", contentBag); // Combine all content into the final output

        }



        private string FormatOperand(string operandType, ushort operandValue, int currentIndex)
        {
            switch (operandType.ToLower()) // Convert operandType to lowercase
            {
                case "m8":
                case "r/m8":
                case "r/m16":
                case "r/m32":
                case "r/m64":
                case "r/m16/32/64":
                case "r32/64":
                case "r16/32/64":
                case "r/m16/32":
                    return $"[0x{operandValue:X2}]";  // Memory address representation

                case "m":
                case "m16":
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

                case "eflags":
                case "flags":
                case "rbp":
                case "rdx":
                case "gs":
                case "fs":
                case "ss":
                case "eax":
                case "rax":
                case "rcx":
                case "ecx":
                case "r11":
                case "cl":
                case "al":
                case "ah":
                case "ia32_bios_sig":
                case "gdtr":
                case "ldtr":
                case "crn":
                case "drn":
                case "msr":
                    return $"{operandType} 0x{operandValue:X2}";  // Special registers and flags

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

                case "st":
                case "sti/m32real":
                    return $"{operandType} 0x{operandValue:X2}";  // ST register or m32 real

                case "m32real":
                    return $"m32real 0x{operandValue:X2}";  // m32 real value

                case "r/m":
                    return $"r/m 0x{operandValue:X2}";  // General r/m format

                case "r64":
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

                case "dx":
                    return $"dx 0x{operandValue:X4}";  // DX register operand, assuming it's 16-bit in this context

                case "m16/32/64":
                    return $"m{operandValue:X2}";  // General memory operand

                case "moffs16/32/64":
                    return $"moffs{operandValue:X8}";  // Memory offset (16/32/64-bit)

                case "moffs8":
                    return $"moffs8 0x{operandValue:X2}";  // 8-bit memory offset

                case "cr0": 
                    return $"CR0 0x{operandValue:X4}"; // Control register 0

                case "xmm/m64":
                    return $"xmm/m64 0x{operandValue:X2}";  // xmm or m64 register

                case "m32int":
                    return $"m32int 0x{operandValue:X4}";  // Memory 32-bit integer
                case "3":
                case "1":
                    return $"{operandValue:X2}";
                default:
                    return $"Unknown Operand Format: {operandType}, Value: 0x{operandValue:X2}";
            }

        }
    }
    public class Decompiler
    {
        private byte[] exeData; // loading into memory is not viable with large executables 
        private DecompileType decompileType;
        
        public peSection[] rawSections;
        private CodeType codeType;
        private Thread[] decompilerThreads;
        public DecompileMethod decompileMethod;
        public List<object> exeInstructionList;

        public static List<DecompilerInstruction> instructionList;
        public static string filePath;
        public static int maxFileChunkSize = 512;
        //public static DecompilerRuleHandler rLoader;
        public Decompiler(string toDecompilePath, string ruleLocPath = null, DecompileType type = DecompileType.ASM, DecompileMethod method = DecompileMethod.NATIVE, RecompilationType recompilationMethod = RecompilationType.RAW, bool loadExeDataToMemory = false, byte[] driverEntryPattern = null)
        {
            if (type == DecompileType.kernelSYS)
            {
                if (driverEntryPattern == null)
                    driverEntryPattern = [0x48, 0x89, 0x5C, 0x24, 0x08];
                if (loadExeDataToMemory)
                {
                    byte[] driverData = File.ReadAllBytes(toDecompilePath);
                    int index = -1;
                    for (int i = 0; i <= driverData.Length - driverEntryPattern.Length; i++)
                    {
                        bool match = true;
                        for (int j = 0; j < driverEntryPattern.Length; j++)
                        {
                            if (driverData[i + j] != driverEntryPattern[j])
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match)
                        {
                            index = i;
                            break;
                        }
                    }
                    if (index >= 0)
                    {
                        log($@"int __fastcall DriverEntry(_DRIVER_OBJECT *DriverObject, _UNICODE_STRING *RegistryPath) @ 0x{index:X} >> {string.Join(", ", driverEntryPattern.Select(b => $"0x{b:X2}"))}");
                    }
                    else
                        throw new InvalidDriverEntryException("No DriverEntry exception");
                } else {
                    using (FileStream fs = new FileStream(toDecompilePath, FileMode.Open, FileAccess.Read))
                    {
                        int index = -1;
                        byte[] buffer = new byte[driverEntryPattern.Length];
                        long fileLength = fs.Length;
                        for (long i = 0; i <= fileLength - buffer.Length; i++)
                        {
                            fs.Position = i;
                            fs.Read(buffer, 0, buffer.Length);
                            bool match = true;
                            for (int j = 0; j < buffer.Length; j++)
                            {
                                if (buffer[j] != driverEntryPattern[j])
                                {
                                    match = false;
                                    break;
                                }
                            }
                            if (match)
                            {
                                index = (int)i;
                                break;
                            }
                        }
                        if (index >= 0)
                        {
                            log($@"int __fastcall DriverEntry(_DRIVER_OBJECT *DriverObject, _UNICODE_STRING *RegistryPath) @ 0x{index:X} >> {string.Join(", ", driverEntryPattern.Select(b => $"0x{b:X2}"))}");
                        }
                        else
                        {
                            throw new InvalidDriverEntryException("No DriverEntry exception");
                        }
                    }
                }
            }
            exeInstructionList = new List<object>();
            filePath = toDecompilePath;
            if (loadExeDataToMemory)
                exeData = File.ReadAllBytes(toDecompilePath);
            else
            {
                using (FileStream ftmp = new FileStream(toDecompilePath, FileMode.Open, FileAccess.Read))
                {
                    exeData = new byte[1024];
                    if (ftmp.Read(exeData, 0, 1024) == 0) throw new RuleLoaderException();
                }
            }
            // TODO: write code that decompiles .headers
            int peHeaderOffset = toInt32(exeData, 0x3C);
            string peSig = Encoding.UTF8.GetString(exeData, peHeaderOffset, 4);
            if (peSig != "PE\0\0") throw new InvalidPeHeaderException(); // invalid pe header offset exception..
            log($@"PeHeaderSig @ 0x{peHeaderOffset.ToString("X")} >> {peSig}");

            ushort cType = toUInt16(exeData, peHeaderOffset + 4);
            if (cType == 0x8664) codeType = CodeType.x64; else codeType = CodeType.x32;

            switch (method)
            {
                case DecompileMethod.NATIVE: // Faster, less consistent.
                    downloadCodeRules(codeType); // ensures code decompiler actually has access to the byte = mnemonic conversion that this app is designed to run
                    if (loadExeDataToMemory) 
                        rawSections = readRawSections(exeData, peHeaderOffset); 
                    else
                        rawSections = readRawSectionsStream(toDecompilePath, peHeaderOffset);
                    break;
            }
            long ct = UTCTimeAsLong;
            Directory.CreateDirectory($@"{ct.ToString()}");
            Directory.CreateDirectory($@"{ct.ToString()}\\raw");
            foreach (peSection sct in rawSections)
            {
                if (sct == null) continue;
                using StreamWriter mn = new StreamWriter($@"{ct.ToString()}\\raw\\{sct.Name}");
                mn.Write(sct.Content);

                switch (recompilationMethod)
                {
                    case RecompilationType.RAWSIMPLE:

                        break;
                    case RecompilationType.RAW:
                    default:
                        break;
                }
            }
        }
        private peSection[] readRawSectionsStream(string filePath, int peHeaderOffset)
        {
            using (FileStream mainstream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                int head = peHeaderOffset + 24 + (codeType == CodeType.x32 ? 224 : 240);
                byte[] peHeader = new byte[head];
                mainstream.Read(peHeader, 0, head);

                int nOSections = toInt16(peHeader, peHeaderOffset + 6);
                ConcurrentBag<peSection> sectionBag = new ConcurrentBag<peSection>();
                log($"{nOSections} section headers detected..");
                Task[] sectionTask = new Task[nOSections];
                for (int i = 0; i < nOSections; i++)
                {
                    int index = i;
                    sectionTask[i] = Task.Run(() =>
                    {
                        byte[] sBuffer = new byte[40];
                        lock (mainstream) // Ensure thread-safe access to FileStream
                        {
                            mainstream.Seek(head + index * 40, SeekOrigin.Begin);
                            if (mainstream.Read(sBuffer, 0, 40) < 40) throw new InvalidPeHeaderException();
                        }
                        string sectionName = getStringAscii(sBuffer, 0, 8).Trim('\0');
                        log($@"SectionName found @ {head + index * 40} >> {sectionName}");

                        int codeSectionOffset = toInt32(sBuffer, 12);
                        int codeSectionSize = toInt32(sBuffer, 16);
                        log($@"CodeSection offset @ 0x{codeSectionOffset} {sectionName} with a size of 0x{codeSectionSize}");

                        sectionBag.Add(new peSection(sectionName, codeSectionOffset, codeSectionSize)); 
                    });
                }
                Task.WaitAll(sectionTask);
                return sectionBag.ToArray();
            }
        }

        private peSection[] readRawSections(byte[] exedata, int peHeaderOffset)
        {
            int numOfSections = toInt16(exedata, peHeaderOffset + 6);
            log(numOfSections);
            peSection[] sectionList = new peSection[numOfSections];
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
                        sectionList[index] = new peSection(sectionName, codeSection);
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

        public peSection getRawSection(string name) => rawSections.Where(i => i.Name == name).ToList()[0];

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
