﻿using Opc.Ua;

namespace UmatiGateway
{
    /// <summary>
    /// This class holds the configuration for the UMATI Gateway. It stores the start conditions
    /// and the settings vor the connections as well as the published Nodes.
    /// </summary>
    public class Configuration
    {
        public string gatewayConfigVersion = "";
        public string configFilePath = "";
        public bool autostart = false;
        public bool useGMSResultEncoding = false;
        public bool readExtraLibs = false;
        public bool singleThreadPolling = false;

        public string configVersion = "";
        public string opcServerEndpoint = "";
        public string opcAuthentication = "";
        public string opcUser = "";
        public string opcPassword = "";
        public string mqttServerEndpopint = "";
        public string mqttUser = "";
        public string mqttPassword = "";
        public string mqttClientId = "";
        public string mqttPrefix = "";
        public List<PublishedNode> publishedNodes = [];
        public Configuration() { }
    }
    public class PublishedNode()
    {
        public string type = "";
        public string namespaceUrl = "";
        public string nodeId = "";
    }
}
