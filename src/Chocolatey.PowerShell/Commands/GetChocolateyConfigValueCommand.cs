using chocolatey;
using Chocolatey.PowerShell;
using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;
using System;
using System.Collections;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Get, "ChocolateyConfigValue")]
    public class GetChocolateyConfigValueCommand : ChocolateyCmdlet
    {
        [Parameter(Mandatory = true)]
        public string ConfigKey { get; set; }

        protected override void End()
        {
            var result = GetConfigValue(ConfigKey);

            WriteObject(result);
        }

        private string GetConfigValue(string key)
        {
            if (key is null)
            {
                return null;
            }

            string configString = null;
            Exception error = null;
            foreach (var reader in InvokeProvider.Content.GetReader(ApplicationParameters.GlobalConfigFileLocation))
            {
                try
                {
                    var results = reader.Read(1);
                    if (results.Count > 0)
                    {
                        configString = PSHelper.ConvertTo<string>(results[0]);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    WriteWarning($"Could not read configuration file: {ex.Message}");
                }
            }

            if (configString is null)
            {
                // TODO: Replace RuntimeException
                var exception = error is null
                    ? new RuntimeException("Config file is missing or empty.")
                    : new RuntimeException($"Config file is missing or empty. Error reading configuration file: {error.Message}", error);
                ThrowTerminatingError(exception.ErrorRecord);
            }

            var xmlConfig = new XmlDocument();
            xmlConfig.LoadXml(configString);

            foreach (XmlNode configEntry in xmlConfig.SelectNodes("chocolatey/config/add"))
            {
                var nodeKey = configEntry.Attributes["key"];
                if (nodeKey is null || !IsEqual(nodeKey.Value, ConfigKey))
                {
                    continue;
                }

                var value = configEntry.Attributes["value"];
                if (!(value is null))
                {
                    // We don't support duplicate config entries; once found, we're done here.
                    return value.Value;
                }
            }

            return null;
        }
    }
}
