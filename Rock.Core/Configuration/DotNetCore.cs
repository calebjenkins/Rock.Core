#if !NET45
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace Rock.Configuration
{
    public static class ConfigurationManager
    {
        private static readonly IConfigurationRoot _configuration;

        static ConfigurationManager()
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("rocklib.json", optional: true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();

            AppSettings = new AppSettings(_configuration);
        }

        public static AppSettings AppSettings { get; }

        public static object GetSection(string sectionName)
        {
            var section = _configuration.GetSection(sectionName);

            if (section == null)
            {
                return null;
            }

            return GetLooselyTypedObject(section);
        }

        private static object GetLooselyTypedObject(IConfigurationSection section)
        {
            if (section.Value != null)
            {
                bool b;
                if (bool.TryParse(section.Value, out b))
                {
                    return b;
                }

                int i;
                if (int.TryParse(section.Value, out i))
                {
                    return i;
                }

                return section.Value;
            }

            IDictionary<string, object> expando = new ExpandoObject();

            foreach (var child in section.GetChildren())
            {
                expando[child.Key] = GetLooselyTypedObject(child);
            }

            int dummy;
            if (expando.Keys.Any() && expando.Keys.All(k => int.TryParse(k, out dummy)))
            {
                return expando.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
            }

            return expando;
        }
    }

    public class AppSettings
    {
        private readonly IConfigurationRoot _configuration;

        public AppSettings(IConfigurationRoot configuration)
        {
            _configuration = configuration;
        }

        public string this[string key]
        {
            get
            {
                return _configuration[key];
            }
        }
    }
}
#endif
