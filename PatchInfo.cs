using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TaleOfTwoWastelands
{
    [Serializable]
    public class PatchInfo
    {
        public FileValidation Metadata { get; set; }
        public byte[] Data { get; set; }
    }
}
