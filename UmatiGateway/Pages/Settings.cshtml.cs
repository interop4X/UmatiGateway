using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using UmatiGateway.OPC;

namespace UmatiGateway.Pages
{
    public class SettingsModel : PageModel
    {
        private String SessionId = "";
        private ClientFactory ClientFactory;
        public Configuration configuration { get; set; } = new Configuration();

        public SettingsModel(ClientFactory ClientFactory)
        {
            this.ClientFactory = ClientFactory;
        }
        public void OnGet()
        {
            Client client = this.getClient();
            this.configuration = client.configuration;
        }
        public IActionResult OnPostSave(string configFilePath, bool? AutoStart, bool? UseGMSResultEncoding, bool? ReadExtraLibs)
        {
            Client client = this.getClient();
            this.configuration = client.configuration;
            if (AutoStart == null)
            {
                configuration.autostart = false;
            } else
            {
                configuration.autostart = true;
            }
            if(UseGMSResultEncoding == null)
            {
                configuration.useGMSResultEncoding = false;
            }
            else
            {
                configuration.useGMSResultEncoding = true;
            }
            if (ReadExtraLibs == null)
            {
                configuration.readExtraLibs = false;
            }
            else
            {
                configuration.readExtraLibs = true;
            }
            configuration.configFilePath = configFilePath;
 
            ConfigurationWriter configurationWriter = new ConfigurationWriter();
            configurationWriter.WriteConfiguration(configuration);
            return new PageResult();
        }
        private Client getClient()
        {
            string? mySessionId = HttpContext.Session.GetString("SessionId");
            if (mySessionId == null)
            {
                this.SessionId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("SessionId", this.SessionId);
            }
            else
            {
                this.SessionId = mySessionId;
            }
            Client client = ClientFactory.getClient(this.SessionId);
            return client;
        }
    }
}
