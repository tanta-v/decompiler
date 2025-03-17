using decompiler.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static decompiler.Utility;

namespace decompiler.DecompilerRulesold
{
    public class DecompilerRule
    {
        [JsonConverter(typeof(HexByteConverter))]
        public byte opcode { get; set; }
        public string mnemonic { get; set; }
        public bool usesModRM { get; set; } = false;
        public string operandFormat { get; set; } = "none";
    }

    public class HexByteConverter : JsonConverter<byte>
    {
        public override byte ReadJson(JsonReader reader, Type objectType, byte existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string value = reader.Value.ToString().Trim(); // Get the value as a string

            // If the value is numeric, convert it to byte directly
            if (int.TryParse(value, out int numericValue)) if (numericValue >= 0 && numericValue <= 255) return (byte)numericValue; else throw new JsonSerializationException($"Invalid byte value: {numericValue}. The value must be within the range 0 to 255.");
            value = value.Replace("0x", "").Trim();

            if (byte.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out byte result)) return result;

            // If it's not a valid byte, throw an exception
            throw new JsonSerializationException($"Invalid byte value: {reader.Value}. The value must be within the range 0 to 255.");
        }

        public override void WriteJson(JsonWriter writer, byte value, JsonSerializer serializer)
        {
            writer.WriteValue("0x" + value.ToString("X2"));
        }
    }

    public class RuleFile //TODO: Phase out this class
    {
        public List<DecompilerRule> rules;
    }

    public class DecompilerRuleHandler
    {
        private readonly string rulePath;
        private Dictionary<byte, DecompilerRule> _rulePairs;
        public static int invalidOpCodes = 0;
        public static List<byte> invalidOpCodeList = new List<byte>();

        public DecompilerRuleHandler(string rulePath = null)
        {
            // Default to "example.r" in the current directory if no path is provided
            this.rulePath = rulePath ?? Path.Combine(Directory.GetCurrentDirectory(), "example.r");
            LoadRules();
        }

        // Load rules from the specified file
        private void LoadRules()
        {
            if (!File.Exists(rulePath))
            {
                throw new FileNotFoundException($"Rule file not found at path: {rulePath}");
            }

            string rulesFile = File.ReadAllText(rulePath);
            var rules = JsonConvert.DeserializeObject<RuleFile>(rulesFile);

            // Initialize the dictionary of rules
            _rulePairs = new Dictionary<byte, DecompilerRule>();
            foreach (var rule in rules.rules)
            {
                _rulePairs.Add(rule.opcode, rule);
            }
        }
        public string Decode(byte[] toDecode, ref int index)
        {
            byte opcode = toDecode[index];

            if (_rulePairs.ContainsKey(opcode))
            {
                DecompilerRule rule = _rulePairs[opcode];
                StringBuilder sb = new StringBuilder();
                sb.Append(rule.mnemonic);

                // Handle ModRM if the rule uses it
                if (rule.usesModRM)
                {
                    index++; // Move past the opcode
                    byte modRMByte = toDecode[index];
                    byte mod = (byte)((modRMByte >> 6) & 0x03); // Extract Mod (2 bits)
                    byte reg = (byte)((modRMByte >> 3) & 0x07); // Extract Reg (3 bits)
                    byte rm = (byte)(modRMByte & 0x07); // Extract RM (3 bits)

                    // Handle registers
                    string regOperand = GetRegisterOperand(reg);
                    string rmOperand = GetRMOperand(mod, rm, toDecode, ref index);

                    sb.Append(" " + regOperand + ", " + rmOperand);
                }
                else if (rule.operandFormat == "immediate")
                {
                    // Handle immediate operands (assumed 32-bit immediate)
                    int immediate = BitConverter.ToInt32(toDecode, index + 1);
                    index += 4; // Move past the immediate value
                    sb.Append(" " + $"0x{immediate:X}");
                }

                index++; // Move to the next byte after decoding the current instruction
                log(sb.ToString());
                return sb.ToString();
            }
            else
            {
                invalidOpCodes++;
                if (!invalidOpCodeList.Contains(opcode)) invalidOpCodeList.Add(opcode);
                return $"Unknown opcode @ {index} >> {opcode.ToString("X")}";
            }
        }

        // Helper function to decode register operands
        private string GetRegisterOperand(byte reg)
        {
            // Map register code to register name
            switch (reg)
            {
                case 0: return "EAX";
                case 1: return "ECX";
                case 2: return "EDX";
                case 3: return "EBX";
                case 4: return "ESP";
                case 5: return "EBP";
                case 6: return "ESI";
                case 7: return "EDI";
                default: return "Unknown";
            }
        }

        // Helper function to decode RM operands based on ModRM byte
        // TODO: implement multi-byte code. 
        private string GetRMOperand(byte mod, byte rm, byte[] toDecode, ref int index)
        {
            // Memory operand handling: 
            if (mod == 0x00) // Direct memory address or register
            {
                if (rm == 0x06) // [Displacement] addressing mode (e.g., [address])
                {
                    int displacement = BitConverter.ToInt32(toDecode, index + 1);
                    index += 4; // Move past the displacement
                    return $"[{displacement:X}]";
                }
                else
                {
                    return GetRegisterOperand(rm);
                }
            }
            else if (mod == 0x01) // [Displacement + Register] addressing mode
            {
                int displacement = (sbyte)toDecode[index + 1];
                index++; // Move past the displacement
                return $"[{GetRegisterOperand(rm)} + {displacement}]";
            }
            else if (mod == 0x02) // [Displacement] addressing mode (e.g., [address])
            {
                int displacement = BitConverter.ToInt32(toDecode, index + 1);
                index += 4; // Move past the displacement
                return $"[{GetRegisterOperand(rm)} + {displacement}]";
            }
            else if (mod == 0x03) // Register addressing mode
            {
                return GetRegisterOperand(rm);
            }
            return "Unknown operand";
        }
    }
}
