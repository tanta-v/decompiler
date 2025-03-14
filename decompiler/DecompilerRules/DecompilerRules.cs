using decompiler.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace decompiler.DecompilerRules
{
    public class DecompilerRule
    {
        [JsonConverter(typeof(HexByteConverter))]
        public byte opcode { get; set; }
        public string mnemonic { get; set; }
        public bool usesModRM { get; set; } = false;
    }
    public class HexByteConverter : JsonConverter<byte>
    {
        public override byte ReadJson(JsonReader reader, Type objectType, byte existingValue, bool hasExistingValue, JsonSerializer serializer) => byte.Parse(reader.Value.ToString().Replace("0x", ""), System.Globalization.NumberStyles.HexNumber);

        public override void WriteJson(JsonWriter writer, byte value, JsonSerializer serializer)
        {
            writer.WriteValue("0x" + value.ToString("X2"));
        }
    }
    public class RuleFile//TODO: Phase out this class
    {
        public List<DecompilerRule> rules;
    }
    public class RuleLoader
    {
        private readonly string rulePath;
        public static List<DecompilerRule> ruleList;
        public RuleLoader(string rulePath)
        {
            this.rulePath = rulePath;
        }
        public List<DecompilerRule> loadRules()
        {
            string rulesFile = File.ReadAllText(rulePath);
            var rules = JsonConvert.DeserializeObject<RuleFile>(rulesFile);
            ruleList = rules.rules;
            return ruleList;
        }
    }
    public class InstructionDecoder
    {
        private Dictionary<byte, DecompilerRule> _rulePairs;

        public InstructionDecoder(List<DecompilerRule> _list)
        {
            _rulePairs = new Dictionary<byte, DecompilerRule>();
            foreach (DecompilerRule i in _list)
            {
                _rulePairs.Add(i.opcode, i);
            }
        }
        
        public string Decode(byte[] toDecode, ref int index)
        {
            byte opcode = toDecode[index];

            if(_rulePairs.ContainsKey(opcode))
            {

            }
        }
    }
}
