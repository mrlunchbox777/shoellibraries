using System.Runtime.Serialization;

namespace StandardDot.CoreServices.UnitTests
{
    [DataContract]
    public class Foobar
    {
        [DataMember]
        public int Foo { get; set; }

        [IgnoreDataMember]
        public int Bar { get; set; }
    }
}