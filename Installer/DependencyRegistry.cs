using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using StructureMap;
using TaleOfTwoWastelands.Patching;
using TaleOfTwoWastelands.UI;

namespace TaleOfTwoWastelands
{
    internal static class DependencyRegistry
    {
        private static readonly Lazy<Container> _container =
            new Lazy<Container>(defaultContainer, LazyThreadSafetyMode.ExecutionAndPublication);

        public static IContainer Container
        {
            get { return _container.Value; }
        }

        private static Container defaultContainer()
        {
            return new Container(x =>
            {
                x.ForSingletonOf<ILog>().Use<Log>();
                x.ForSingletonOf<IInstaller>().Use<Installer>();
				x.ForSingletonOf<IPrompts>().Use<Prompts>();
				x.For<IBsaDiff>().Use<BsaDiff>();
				x.For<IPathStore>().Use<SettingsPathStore>();
			});
        }
    }
}
