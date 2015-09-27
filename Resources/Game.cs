using System.Collections.Generic;
using Newtonsoft.Json;

namespace Resources
{
    using IDSS = IDictionary<string, string>;
    using IDSSA = IDictionary<string, string[]>;

    public static class Game
    {
        public static readonly IDSSA CheckedBSAs = JsonConvert.DeserializeObject<IDSSA>(TaleOfTwoWastelands.Properties.Resources.CheckedBSAs);
        public static readonly IDSS BuildableBSAs = JsonConvert.DeserializeObject<IDSS>(TaleOfTwoWastelands.Properties.Resources.BuildableBSAs);
        public static readonly IDSS VoicePaths = JsonConvert.DeserializeObject<IDSS>(TaleOfTwoWastelands.Properties.Resources.VoicePaths);
        public static readonly string[] CheckedESMs = JsonConvert.DeserializeObject<string[]>(TaleOfTwoWastelands.Properties.Resources.CheckedESMs);
    }
}
