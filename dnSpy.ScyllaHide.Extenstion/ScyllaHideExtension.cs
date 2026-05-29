using System.Collections.Generic;
using dnSpy.Contracts.App;
using dnSpy.Contracts.Extension;

namespace dnSpy.ScyllaHide
{
	[ExportExtension]
	sealed class ScyllaHideExtension : IExtension
	{
		public IEnumerable<string> MergedResourceDictionaries
		{
			get { yield break; }
		}

		public ExtensionInfo ExtensionInfo => new ExtensionInfo
		{
			ShortDescription = "ScyllaHide - Anti-anti-debugging support for dnSpyEx",
			Author = "crackdisk61",
			Version = "2.0.0",
			Description = "Provides anti-debugging detection bypass with ScyllaHide integration for dnSpyEx 6.6.0+"
		};

		public void OnEvent(ExtensionEvent @event, object? obj)
		{
			switch (@event)
			{
				case ExtensionEvent.Initialized:
					// Extension initialized
					break;
				case ExtensionEvent.WillUnload:
					// Extension about to be unloaded
					break;
			}
		}
	}
}
