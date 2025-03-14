using System.IO;
using System.Text;
using static decompiler.Utility;
namespace decompiler.Exceptions
{

    [Serializable]
    public class InvalidPeHeaderOffsetException : Exception
    {
        public InvalidPeHeaderOffsetException() { }
        public InvalidPeHeaderOffsetException(string message) : base(message) { }
        public InvalidPeHeaderOffsetException(string message, Exception inner) : base(message, inner) { }
        protected InvalidPeHeaderOffsetException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class UnableToGetSectionsException : Exception
    {
        public UnableToGetSectionsException() { }
        public UnableToGetSectionsException(string message) : base(message) { }
        public UnableToGetSectionsException(string message, Exception inner) : base(message, inner) { }
        protected UnableToGetSectionsException(
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
