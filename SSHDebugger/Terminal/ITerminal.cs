using System;

namespace SSHDebugger
{
	public interface ITerminal : IDisposable
	{
		clsSSHTerminal SSH {get;}
		void Dispose();

	}
}

