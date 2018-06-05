﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using SecurityCodeScan.Analyzers.Taint;
using YamlDotNet.Serialization;

namespace SecurityCodeScan.Config
{
    internal class ConfigurationManager
    {
        public static ConfigurationManager Instance { get; } = new ConfigurationManager();

        private const string ConfigName = "SecurityCodeScan.config.yml";
        private const string UserConfigName = @"SecurityCodeScan/config.yml";
        private readonly Dictionary<string, Configuration> ProjectConfigs = new Dictionary<string, Configuration>();

        private static readonly object ProjectConfigsLock = new object();
        private static readonly object ConfigurationLock = new object();

        private ConfigurationManager() { }

        private Configuration configuration;

        private Configuration Configuration
        {
            get
            {
                lock (ConfigurationLock)
                {
                    if (configuration != null)
                        return configuration;

                    var assembly     = typeof(ConfigurationManager).GetTypeInfo().Assembly;
                    var deserializer = new Deserializer();

                    using (Stream stream = assembly.GetManifestResourceStream($"SecurityCodeScan.Config.Main.yml"))
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var configData = deserializer.Deserialize<ConfigData>(reader);
                            configuration = ConvertDataToConfig(configData);
                        }
                    }

                    var userConfigFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), UserConfigName);

                    if (!File.Exists(userConfigFile))
                        return configuration;

                    using (StreamReader reader = new StreamReader(userConfigFile))
                    {
                        var userConfig = deserializer.Deserialize<ConfigData>(reader);
                        configuration = MergeConfigData(userConfig);
                    }

                    return configuration;
                }
            }
        }

        private Configuration ConvertDataToConfig(ConfigData configData)
        {
            var config = new Configuration();

            foreach (var data in configData.Behavior)
            {
                config.Behavior[data.Key] = CreateBehavior(data.Value);
            }

            foreach (var data in configData.Sinks)
            {
                config.Sinks[data.Key] = CreateBehavior(data.Value);
            }

            return config;
        }

        private KeyValuePair<string, MethodBehavior> CreateBehavior(MethodBehaviorData behavior)
        {
            var key = behavior.ArgTypes != null ?
                       $"{behavior.Namespace}.{behavior.ClassName}|{behavior.Name}|{behavior.ArgTypes}": //With arguments types discriminator
                       $"{behavior.Namespace}.{behavior.ClassName}|{behavior.Name}"; //Minimalist configuration

            return new KeyValuePair<string, MethodBehavior>(key, new MethodBehavior(behavior.InjectableArguments,
                                                                                    behavior.PasswordArguments,
                                                                                    behavior.TaintFromArguments,
                                                                                    behavior.Locale,
                                                                                    behavior.LocalePass,
                                                                                    behavior.InjectableField,
                                                                                    behavior.IsPasswordField));
        }

        private Configuration GetProjectConfiguration(ImmutableArray<AdditionalText> additionalFiles)
        {
            foreach (var file in additionalFiles)
            {
                if (Path.GetFileName(file.Path) != ConfigName)
                    continue;

                lock (ProjectConfigsLock)
                {
                    if (ProjectConfigs.TryGetValue(file.Path, out var projectConfig))
                        return projectConfig;

                    using (var reader = new StreamReader(file.Path))
                    {
                        var deserializer = new Deserializer();
                        var userConfig   = deserializer.Deserialize<ConfigData>(reader);
                        projectConfig = MergeConfigData(userConfig);
                        ProjectConfigs[file.Path] = projectConfig;
                        return projectConfig;
                    }
                }
            }

            return Configuration;
        }

        private Configuration MergeConfigData(ConfigData config)
        {
            var mergeInto = new Configuration(Configuration);
            if (config.Behavior != null)
            {
                foreach (var behavior in config.Behavior)
                {
                    if (behavior.Value == default(MethodBehaviorData))
                        mergeInto.Behavior.Remove(behavior.Key);
                    else
                        mergeInto.Behavior[behavior.Key] = CreateBehavior(behavior.Value);
                }
            }

            if (config.Sinks != null)
            {
                foreach (var sink in config.Sinks)
                {
                    if (sink.Value == default(MethodBehaviorData))
                        mergeInto.Sinks.Remove(sink.Key);
                    else
                        mergeInto.Sinks[sink.Key] = CreateBehavior(sink.Value);
                }
            }

            return mergeInto;
        }

        public IEnumerable<KeyValuePair<string, MethodBehavior>> GetBehaviors(ImmutableArray<AdditionalText> additionalFiles)
        {
            var config = GetProjectConfiguration(additionalFiles);

            var behaviorInfos = new List<KeyValuePair<string, MethodBehavior>>(config.Behavior.Values);
            behaviorInfos.AddRange(config.Sinks.Values);
            return behaviorInfos;
        }

        private class ConfigData
        {
            public Dictionary<string, MethodBehaviorData> Behavior { get; set; }
            public Dictionary<string, MethodBehaviorData> Sinks    { get; set; }
        }

        private class MethodBehaviorData
        {
            public string ClassName           { get; set; }
            // TODO: use member field in tain analyzis or remove it completely
            public string Member              { get; set; }
            public string Name                { get; set; }
            public string Namespace           { get; set; }
            public string ArgTypes            { get; set; }
            public int[]  InjectableArguments { get; set; }
            public int[]  PasswordArguments   { get; set; }
            public int[]  TaintFromArguments  { get; set; }
            public string Locale              { get; set; }
            public string LocalePass          { get; set; }
            public bool   InjectableField     { get; set; }
            public bool   IsPasswordField     { get; set; }
        }
    }
}
