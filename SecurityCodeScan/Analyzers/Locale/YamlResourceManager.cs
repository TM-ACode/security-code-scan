﻿using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using YamlDotNet.RepresentationModel;

namespace SecurityCodeScan.Analyzers.Locale
{
    public class YamlResourceManager : ResourceManager
    {
        private const string MessagesFileName = "Messages.yml";

        private readonly IDictionary<string, string> LocaleString = new Dictionary<string, string>();
        public readonly IList<string> LocaleKeyIds = new List<string>();

        public YamlResourceManager() : base("SecurityCodeScan.Empty",
                                            typeof(YamlResourceManager).GetTypeInfo().Assembly)
        {
        }

        public void Load()
        {
            var assembly = typeof(YamlResourceManager).GetTypeInfo().Assembly;

            using (Stream stream = assembly.GetManifestResourceStream("SecurityCodeScan.Config." + MessagesFileName))
            using (var reader = new StreamReader(stream))
            {
                var yaml = new YamlStream();
                yaml.Load(reader);

                var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;

                foreach (var entry in mapping.Children)
                {
                    var key   = (YamlScalarNode)entry.Key;
                    var value = (YamlMappingNode)entry.Value;

                    LocaleKeyIds.Add(key.Value);

                    foreach (var child in value.Children)
                    {
                        LocaleString[$"{key.Value}_{child.Key}"] = ((YamlScalarNode)child.Value).Value;
                    }

                    //Logger.Log(key.Value);
                }

                //Logger.Log(LocaleString.Count + " locales loaded.");
            }
        }

        public new string GetString(string name)
        {
            return GetString(name, CultureInfo.CurrentCulture);
        }

        public override string GetString(string name, CultureInfo culture)
        {
            if (!LocaleString.TryGetValue(name, out string val))
                return "??" + name + "??";

            return val;
        }
    }
}
