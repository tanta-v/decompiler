using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace decompiler.DecompilerRules
{
    public class DecompilerInstruction
    {
        public string PrimaryOpcode { get; set; } // Required
        public string InstructionMnemonic { get; set; } // Required
        public string? Prefix { get; set; }

        public string? Prefix0F { get; set; }
        public string? SecondaryOpcode { get; set; }
        public string? RegisterOpcodeField { get; set; }
        public string? Processor { get; set; }
        public string? DocumentationStatus { get; set; }
        public string? ModeOfOperation { get; set; }
        public string? RingLevel { get; set; }
        public string? LockPrefixFPU { get; set; }
        public string? Operand1 { get; set; }
        public string? Operand2 { get; set; }
        public string? Operand3 { get; set; }
        public string? Operand4 { get; set; }
        public string? InstructionExtensionGroup { get; set; }
        public string? TestedFlags { get; set; }
        public string? ModifiedFlags { get; set; }
        public string? DefinedFlags { get; set; }
        public string? UndefinedFlags { get; set; }
        public string? FlagsValues { get; set; }
        public string? DescriptionNotes { get; set; }
        public DecompilerInstruction(
            string primaryOpcode, string instructionMnemonic, string? prefix = null, string? prefix0F = null, string? secondaryOpcode = null, string? registerOpcodeField = null, string? processor = null, string? documentationStatus = null, string? modeOfOperation = null, string? ringLevel = null, string? lockPrefixFPU = null, string? operand1 = null, string? operand2 = null, string? operand3 = null, string? operand4 = null, string? instructionExtensionGroup = null, string? testedFlags = null, string? modifiedFlags = null, string? definedFlags = null, string? undefinedFlags = null, string? flagsValues = null, string? descriptionNotes = null)
        {
            PrimaryOpcode = primaryOpcode;
            InstructionMnemonic = instructionMnemonic;
            Prefix = prefix;
            Prefix0F = prefix0F;
            SecondaryOpcode = secondaryOpcode;
            RegisterOpcodeField = registerOpcodeField;
            Processor = processor;
            DocumentationStatus = documentationStatus;
            ModeOfOperation = modeOfOperation;
            RingLevel = ringLevel;
            LockPrefixFPU = lockPrefixFPU;
            Operand1 = operand1;
            Operand2 = operand2;
            Operand3 = operand3;
            Operand4 = operand4;
            InstructionExtensionGroup = instructionExtensionGroup;
            TestedFlags = testedFlags;
            ModifiedFlags = modifiedFlags;
            DefinedFlags = definedFlags;
            UndefinedFlags = undefinedFlags;
            FlagsValues = flagsValues;
            DescriptionNotes = descriptionNotes;
        }
    }

}
