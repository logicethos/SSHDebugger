using System;
using System.Threading;

namespace SSHDebugger
{
	public interface ITerminal : IDisposable
	{
		clsSSHTerminal SSH {get;}
		void Dispose();
		void Front();
		Thread DebuggerThread {set;}

	}
}

