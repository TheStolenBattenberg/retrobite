using NPlug;
using RetroBite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RetroBiteVST3
{
    public static class RetroBitePlugin
    {
        public static AudioPluginFactory GetFactory()
        {
            var factory = new AudioPluginFactory(new("StolenBattenberg", "https://www.swordofmoonlight.com", "noreply@swordofmoonlight.com"));
            factory.RegisterPlugin<RetroBiteProcessor>(new(RetroBiteProcessor.ClassID, "RetroBite", AudioProcessorCategory.Effect));
            factory.RegisterPlugin<RetroBiteController>(new(RetroBiteController.ClassId, "RetroBite Controller"));
            return factory;
        }

        [ModuleInitializer]
        internal static void ExportThisPlugin() =>
            AudioPluginFactoryExporter.Instance = GetFactory();
    }
}