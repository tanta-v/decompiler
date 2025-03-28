using System.IO;
using System.Text;
using static decompiler.Utility;
namespace decompiler.Exceptions
{

    [Serializable]
    public class InvalidPeHeaderException : Exception
    {
        public InvalidPeHeaderException() { }
        public InvalidPeHeaderException(string message) : base(message) { }
        public InvalidPeHeaderException(string message, Exception inner) : base(message, inner) { }
        protected InvalidPeHeaderException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class SectionReaderException : Exception
    {
        public SectionReaderException() { }
        public SectionReaderException(string message) : base(message) { }
        public SectionReaderException(string message, Exception inner) : base(message, inner) { }
        protected SectionReaderException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }


    [Serializable]
    public class RuleLoaderException : Exception
    {
        public RuleLoaderException() { }
        public RuleLoaderException(string message) : base(message) { }
        public RuleLoaderException(string message, Exception inner) : base(message, inner) { }
        protected RuleLoaderException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
