using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Resources
{
 	using IDSS = IDictionary<string, string>;
	using IDSSA = IDictionary<string, string[]>;

   public static class Game
   {
       public static readonly IDSSA CheckedBSAs = JsonConvert.DeserializeObject<IDSSA>(Properties.Resources.CheckedBSAs);
       public static readonly IDSS BuildableBSAs = JsonConvert.DeserializeObject<IDSS>(Properties.Resources.BuildableBSAs);
       public static readonly IDSS VoicePaths = JsonConvert.DeserializeObject<IDSS>(Properties.Resources.VoicePaths);
       public static readonly string[] CheckedESMs = JsonConvert.DeserializeObject<string[]>(Properties.Resources.CheckedESMs);
   }
}
