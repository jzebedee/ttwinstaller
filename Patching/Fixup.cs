using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace TaleOfTwoWastelands.Patching
{
    [Serializable]
    public class Fixup<TOrig, TUpdate>
    {
        public TOrig Original { get; protected set; }
        public TUpdate Update { get; protected set; }

        public Fixup(TOrig orig, TUpdate upd)
        {
            this.Original = orig;
            this.Update = upd;
        }
        public Fixup()
        {
        }
    }
}
