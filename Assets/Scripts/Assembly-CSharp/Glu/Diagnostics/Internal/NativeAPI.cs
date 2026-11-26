using System.Runtime.InteropServices;

namespace Glu.Diagnostics.Internal
{
	public static class NativeAPI
	{
		private const string __importName = "GluDiagnostics";

		public static void Glu_Diagnostics_Abort() { }

		public static void Glu_Console_WriteLine(string line) { }
	}
}
