using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TaleOfTwoWastelands.Patching
{
    [Serializable]
    public class Fixup
    {
        public FileValidation Original { get; set; }
        public PatchInfo Update { get; set; }

        public Fixup(FileValidation orig, PatchInfo upd)
        {
            this.Original = orig;
            this.Update = upd;
        }
        public Fixup()
        {
        }
    }
}
