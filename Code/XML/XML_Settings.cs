﻿using System;
using System.Linq;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace LifecycleRebalance
{
    public abstract class Configuration<C> where C : class, new()
    {
        private static C instance;

        public static C Load()
        {
            if (instance == null)
            {
                var configPath = GetConfigPath();
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(C));
                try
                {
                    if (File.Exists(configPath))
                    {
                        using (StreamReader streamReader = new System.IO.StreamReader(configPath))
                        {
                            instance = xmlSerializer.Deserialize(streamReader) as C;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logging.LogException(e, "error loading XML file");
                }
            }
            return instance ?? (instance = new C());
        }

        public static void Save()
        {
            if (instance == null) return;

            var configPath = GetConfigPath();

            var xmlSerializer = new XmlSerializer(typeof(C));
            var noNamespaces = new XmlSerializerNamespaces();
            noNamespaces.Add("", "");
            try
            {
                using (var streamWriter = new System.IO.StreamWriter(configPath))
                {
                    xmlSerializer.Serialize(streamWriter, instance, noNamespaces);
                }
            }
            catch (Exception e)
            {
                Logging.LogException(e, "error saving XML file");
            }
        }

        private static string GetConfigPath()
        {
            var configPathAttribute = (ConfigurationPathAttribute)typeof(C).GetCustomAttributes(typeof(ConfigurationPathAttribute), true)
                .FirstOrDefault();

            if (configPathAttribute != null)
            {
                return configPathAttribute.Value;
            }
            else
            {
                Logging.Message("ConfigurationPath attribute missing in ", typeof(C).Name);
                return typeof(C).Name + ".xml";
            }
        }
    }


    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigurationPathAttribute : Attribute
    {
        public ConfigurationPathAttribute(string value)
        {
            Value = value;
        }

        public string Value { get; private set; }
    }
}