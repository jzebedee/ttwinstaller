using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TaleOfTwoWastelands.Patching
{
    [Serializable]
    public class PatchFixup : Fixup<FileValidation, PatchInfo>
    {
        public PatchFixup(FileValidation orig, PatchInfo update)
            : base()
        {
            this.Original = orig;
            this.Update = update;
        }

        public PatchFixup() { }
    }
}
