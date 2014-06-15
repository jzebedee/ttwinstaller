using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TaleOfTwoWastelands
{
    [Serializable]
    public class Patch
    {
        public Validation Metadata { get; set; }
        public byte[] Data { get; set; }
    }
}
