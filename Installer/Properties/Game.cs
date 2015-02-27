using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TaleOfTwoWastelands.Properties
{
	using IDSS = IDictionary<string, string>;
	using IDSSA = IDictionary<string, string[]>;

	internal partial class Game
	{
		static Lazy<IDSS> _voicePaths = new Lazy<IDSS>(() => JsonConvert.DeserializeObject<IDSS>(ResourceManager.GetString("VoicePaths", resourceCulture)));
		static Lazy<string[]> _checkedEsms = new Lazy<string[]>(() => JsonConvert.DeserializeObject<string[]>(ResourceManager.GetString("CheckedESMs", resourceCulture)));
		static Lazy<IDSSA> _checkedBsas = new Lazy<IDSSA>(() => JsonConvert.DeserializeObject<IDSSA>(ResourceManager.GetString("CheckedBSAs", resourceCulture)));
		static Lazy<IDSS> _buildableBsas = new Lazy<IDSS>(() => JsonConvert.DeserializeObject<IDSS>(ResourceManager.GetString("BuildableBSAs", resourceCulture)));

		internal static IDSS BuildableBSAs
		{
			get { return _buildableBsas.Value; }
		}

		internal static IDSSA CheckedBSAs
		{
			get { return _checkedBsas.Value; }
		}

		internal static string[] CheckedESMs
		{
			get { return _checkedEsms.Value; }
		}

		internal static IDSS VoicePaths
		{
			get { return _voicePaths.Value; }
		}
	}
}
