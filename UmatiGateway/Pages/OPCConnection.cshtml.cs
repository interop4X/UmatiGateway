using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using System.Reflection;
using System.Resources;
using UmatiGateway.OPC;

namespace UmatiGateway.Pages
{
    public class OPCConnectionModel : PageModel
    {
        public string LabelConnectionUrl { get;} = "Connection URL:";
        public string LabelSessionId { get; } = "SessionId:";
        public string LabelOPCSessionName { get; } = "OPCSessionName:";
        public string LabelOPCSessionId { get; } = "OPCSessionId:";
        public string LabelConnectionStatus { get; } = "ConnectionStatus:";
        public string ConnectionUrl { get; private set; } = "";
        public ClientFactory ClientFactory;
        public string SessionId { get; private set; } = "";
        public string OPCSessionName { get; private set; } = "";
        public string OPCSessionId { get; private set; } = "";
        public string ConnectionStatus { get; private set; } = "Not Connected";
        public OPCConnectionModel(ClientFactory ClientFactory)
        {
            this.ClientFactory = ClientFactory;
            ResourceManager rm = new ResourceManager("UmatiGateway.Pages.TestPage", Assembly.GetExecutingAssembly());
            string? Label_ConnectionUrl_Translated = rm.GetString("TestPage_Label_ConnectionUrl");
            if (Label_ConnectionUrl_Translated != null) { this.LabelConnectionUrl = Label_ConnectionUrl_Translated; } 
        }

        public IActionResult OnPostConnect(String ConnectionUrl)
        {
            this.ConnectionUrl = ConnectionUrl;
            Client client = this.getClient();
            if (ConnectionUrl != null) {
                _= client.ConnectAsync(this.ConnectionUrl).Result;
            }
            this.UpdateClientData();
            return new PageResult();
        }
        public IActionResult OnPostDisconnect()
        {
            Client client = this.getClient();
            client.Disconnect();
            this.UpdateClientData();
            return new PageResult();
        }
        public void OnGet()
        {
            this.UpdateClientData();
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
        private void UpdateClientData()
        {
            string? mySessionId = HttpContext.Session.GetString("SessionId");
            if (mySessionId == null)
            {
                this.SessionId = string.Empty;
                this.OPCSessionId = string.Empty;
                this.ConnectionStatus = string.Empty;
                this.OPCSessionName = string.Empty;
            }
            else
            {
                Client client = ClientFactory.getClient(mySessionId);
                this.SessionId = mySessionId;
                if (client.Session != null)
                {
                    this.OPCSessionId = client.Session.SessionId.ToString();
                    this.ConnectionStatus = client.Session.Connected.ToString();
                    this.OPCSessionName = client.Session.SessionName;
                    
                }
                this.ConnectionUrl = client.getOpcConnectionUrl();
            }
        }
    }
}
