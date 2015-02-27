using System.Collections.Generic;
using BSAsharp;

namespace TaleOfTwoWastelands.Patching
{
	public interface IBsaDiff
	{
		bool PatchBsa(CompressionOptions bsaOptions, string oldBSA, string newBSA, bool simulate = false);
		bool PatchBsaFile(BSAFile bsaFile, PatchInfo patch, FileValidation targetChk);
		void RenameFiles(BSA bsa, IDictionary<string, string> renameDict);
	}
}