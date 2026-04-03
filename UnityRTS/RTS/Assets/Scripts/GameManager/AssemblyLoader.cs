using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GameManager
{
    /// <summary>
    /// Resolves .NET assemblies at runtime by searching the application base directory.
    /// Hooks into <see cref="AppDomain.AssemblyResolve"/> so that agent DLLs
    /// (compiled separately and dropped into the Plugins folder) can be loaded
    /// even when the CLR's default probing fails.
    /// </summary>
	[Serializable]
	public class AssemblyLoader : MarshalByRefObject
	{
		private string ApplicationBase { get; set; }

		public AssemblyLoader()
		{
			ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
			AppDomain.CurrentDomain.AssemblyResolve += Resolve;
		}

        /// <summary>
        /// Called by the CLR when normal assembly resolution fails.
        /// Attempts to load the requested assembly from the application base directory.
        /// </summary>
		private Assembly Resolve(object sender, ResolveEventArgs args)
		{
			AssemblyName assemblyName = new AssemblyName(args.Name);
			string fileName = string.Format("{0}.dll", assemblyName.Name);
			return Assembly.LoadFile(Path.Combine(ApplicationBase, fileName));
		}
	}
}
