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
    /// AssemblyLoader Class
    /// </summary>
	[Serializable]
	public class AssemblyLoader : MarshalByRefObject
	{
		private string ApplicationBase { get; set; }

        /// <summary>
        /// AssemblyLoader
        /// </summary>
		public AssemblyLoader()
		{
			ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
			AppDomain.CurrentDomain.AssemblyResolve += Resolve;
		}

        /// <summary>
        /// Resolve
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
		private Assembly Resolve(object sender, ResolveEventArgs args)
		{
			AssemblyName assemblyName = new AssemblyName(args.Name);
			string fileName = string.Format("{0}.dll", assemblyName.Name);
			return Assembly.LoadFile(Path.Combine(ApplicationBase, fileName));
		}
	}
}
