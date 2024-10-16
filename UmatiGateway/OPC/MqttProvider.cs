using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Authentication;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using System.Threading;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using Opc.Ua.Client.ComplexTypes;
using Microsoft.AspNetCore.Authentication;
using Opc.Ua.Schema.Binary;
using Org.BouncyCastle.Utilities.Encoders;
using MQTTnet.Server;
using System.Reflection.PortableExecutable;
using Org.BouncyCastle.Crypto.IO;
using static UmatiGateway.OPC.MqttProvider;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;


namespace UmatiGateway.OPC{
    public class MqttProvider {
        private MqttFactory mqttFactory = new MqttFactory();
        private IMqttClient? mqttClient = null;
        private const string CLIENT_ID = "TestClient";
        private const string TCP = "tcp";
        private const string WEBSOCKET = "websocket";
        public string? connectionType = null;
        public string? connectionString = null;
        public string? connectionPort = null;
        public string? user = null;
        public string? pwd = null;
        private string onlineTopic = "";
        private string machineTopic = "";
        public string mqttPrefix = "";
        public string clientId = "";
        public bool useGMSResultEncoding = false;
        public List<PublishedNode> publishedNodes = new List<PublishedNode>();
        public List<NodeId> onlineMachines = new List<NodeId>();

        private Client client;
        private Dictionary<NodeId, string> MqttValues = new Dictionary<NodeId, string>();
        private Boolean connected = false;
        private Boolean subscriptionscreated = false;
        private System.Timers.Timer aTimer;
        private bool firstRead = true;
        private bool firstReadFinished = false;
        private bool debug = false;
        private bool ReadInProgress = false;
        public bool singleThreadPolling = false;
        public bool ConnectedOnce = false;
        public MqttProvider(Client client){
            this.client = client;
            this.mqttClient = mqttFactory.CreateMqttClient();
            /*user = "fva/matthias2";
            pwd = "";
            connectionString = "localhost";
            clientId = "fva/matthias2";
            mqttPrefix = "umati/v2";
            connectionPort = "1883";*/
            aTimer = new System.Timers.Timer(2000);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
            

        }
        public bool isConnected() {
            return this.connected;
        }
        public void Disconnect()
        {
            Console.WriteLine("Disconnecting");
            AsyncHelper.RunSync(() => this.mqttClient.DisconnectAsync());
            this.connected = false;
            Console.WriteLine("Disconnected");
        }
        public void Connect()
        {
            this.ConnectedOnce = true;
            this.connectionType = WEBSOCKET;
            this.Connect(this.connectionString, this.connectionType, this.connectionPort, this.user, this.pwd);
            foreach (PublishedNode publishedNode in this.publishedNodes)
            {
                int namespaceIndex = this.client.GetNamespaceTable().GetIndex(publishedNode.namespaceUrl);
                if (publishedNode.type == "Numeric")
                {
                    this.onlineMachines.Add(new NodeId(Convert.ToUInt32(publishedNode.nodeId), (ushort)namespaceIndex));
                } else if(publishedNode.type == "String")
                {
                    this.onlineMachines.Add(new NodeId(publishedNode.nodeId, (ushort)namespaceIndex));
                }
            }
            aTimer.Start();
        }
        public void Reconnect() {
            if(!connected) {
                this.Connect(this.connectionString, this.connectionType, this.connectionPort, this.user, this.pwd);
            }
        }
        public void Connect(string connectionString, string connectionType, string port, string user, string pwd) {
            try {
                this.connectionString = connectionString;
                this.connectionType = connectionType;
                this.connectionPort = port;
                this.user = user;
                this.pwd = pwd;
                if(this.connectionType == TCP) {
                    this.Connect_Client_Using_Tcp();
                } else if(this.connectionType == WEBSOCKET) {
                    if (this.connectionString.StartsWith("mqtt"))
                    {
                        this.Connect_Client_Using_Tcp();
                    }
                    else
                    {
                        this.Connect_Client_Using_WebSockets();
                    }
                } else {
                    Console.Out.WriteLine("Unkonown Mqtt Connection Type");
                }
                connected = true;
            } catch (Exception e) {
                connected = false;
            }
        }
        private void Connect_Client_Using_WebSockets() {
            try
            {
                if (this.mqttClient != null)
                {
                    MqttClientOptions mqttClientOptions;
                    if (this.user != null && this.user != "" && this.pwd != null)
                    {
                        mqttClientOptions = new MqttClientOptionsBuilder()
                        .WithWebSocketServer(this.connectionString)
                        .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                        .WithCredentials(this.user, this.pwd)
                        .WithTls()
                        .Build();
                    }
                    else
                    {
                        mqttClientOptions = new MqttClientOptionsBuilder()
                        .WithWebSocketServer(this.connectionString)
                        .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                        .Build();
                    }
                    AsyncHelper.RunSync(() => this.mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None));
                }
                else
                {
                    Console.Out.WriteLine("m_mqttClient is null.");
                }
            } catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        private void Connect_Client_Using_Tcp() {
            if(this.mqttClient != null) {
                MqttClientOptions mqttClientOptions;
                int? port = null;
                if (this.connectionPort != null)
                {
                    port = Int32.Parse(this.connectionPort);
                }
                if (this.connectionString != null)
                {
                    
                    int Index = this.connectionString.LastIndexOf(":");
                    string server = this.connectionString.Substring(7, Index-7);
                    int port1 = int.Parse(this.connectionString.Substring(Index + 1));
                    if (this.user != null && this.user != "" && this.pwd != null)
                    {

                        mqttClientOptions = new MqttClientOptionsBuilder()
                        .WithTcpServer(server, port1)
                        .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                        .WithCredentials(this.user, this.pwd)
                        .Build();
                    }
                    else
                    {
                        mqttClientOptions = new MqttClientOptionsBuilder()
                        .WithTcpServer(server, port1 )
                        .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                        .Build();
                    }
                    AsyncHelper.RunSync(() => this.mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None));
                }
            } else {
                Console.Out.WriteLine("The MqttClient is null");
            }
        }

        public JObject SortJsonKeysRecursively(JObject jsonObj)
        {
            // Erstelle ein neues JObject mit sortierten Keys
            JObject sortedJsonObj = new JObject(
                jsonObj.Properties()
                       .OrderBy(p => p.Name)
                       .Select(p => new JProperty(p.Name, SortToken(p.Value)))
            );
            return sortedJsonObj;
        }
        // Methode, um die Sortierung je nach Token-Typ (JObject, JArray oder JValue) rekursiv anzuwenden
        public JToken SortToken(JToken token)
        {
            if (token is JObject)
            {
                // Sortiere rekursiv, wenn es sich um ein JObject handelt
                return SortJsonKeysRecursively((JObject)token);
            }
            else if (token is JArray)
            {
                // Für Arrays: Überprüfe, ob die einzelnen Elemente sortiert werden müssen
                var array = (JArray)token;
                return new JArray(array.Select(SortToken));
            }
            else
            {
                // Wenn es sich um einen Wert (JValue) handelt, bleibt der Wert gleich
                return token;
            }
        }

        public bool WriteMessage(JObject jObject, string machineId, String type) {
            try {
                JObject sortedJsonObj = this.SortJsonKeysRecursively(jObject);
                string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + type + "/" + machineId;
                MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic)
                .WithPayload(sortedJsonObj.ToString(Newtonsoft.Json.Formatting.Indented))
                .Build();
                if(this.mqttClient != null) {
                    _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                }
                return true; 
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
                this.connected = false;
                throw;
                //return false;
            }
        }
        public bool WriteIdentification(JArray jArray, string machineId, String type)
        {
            try
            {
                string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + "list/" + type;
                MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic)
                .WithPayload(jArray.ToString(Newtonsoft.Json.Formatting.Indented))
                .Build();
                if (this.mqttClient != null)
                {
                    _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;

                }
                return true;
            }
            catch (Exception e)
            {
                //logger.Error ("Unable to publish Identification", e);
                this.connected = false;
                return false;
            }
        }
        public void Start() {

        }
        public void publishOnlineMachines()
        {
            try
            {
                foreach (NodeId machine in this.onlineMachines)
                {
                    string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + "online/";
                    MyTopic += this.getInstanceNsu(machine);
                    MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(MyTopic)
                    .WithPayload("1")
                    .Build();
                    if (this.mqttClient != null)
                    {
                        _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                    }
                }
            } catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                this.connected = false;
            }
        }
        public bool publishBadList() {
            try {
                string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + "bad_list/errors";
                MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic)
                .WithPayload("[]")
                .Build();
                if(this.mqttClient != null) {
                    _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                }
                return true;
            } catch(Exception e) {
                //logger.Error ("Unable to publish BadList", e);
                this.connected = false;
                return false;
            }
        }
        public bool publishClientOnline()
        {
            try
            {
                string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + "clientOnline";
                MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic)
                .WithPayload("1")
                .Build();
                if (this.mqttClient != null)
                {
                    _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                }
                string MyTopic1 = this.mqttPrefix + "/" + this.clientId + "/" + "gw-version";
                MqttApplicationMessage applicationMessage1 = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic1)
                .WithPayload("umatigateway_1.0.0")
                .Build();
                if (this.mqttClient != null)
                {
                    _ = this.mqttClient.PublishAsync(applicationMessage1, CancellationToken.None).Result;
                }
                return true;
            }
            catch (Exception e)
            {
                //logger.Error("Unable to publish ClientOnline", e);
                this.connected = false;
                return false;
            }
        }

        public void publishNode()
        {
            try
            {
                foreach (NodeId machine in this.onlineMachines)
                {
                    if (machine != null)
                    {
                        JObject body = new JObject();
                        createJSON(body, machine);
                        Node? machineNode = this.client.ReadNode(machine);
                        if (machineNode != null) {
                            NodeId? typedefinition = this.client.getTypeDefinition(machine);
                            if (typedefinition != null)
                            {
                                Node? TypeDefinitionNode = this.client.ReadNode(typedefinition);
                                if (TypeDefinitionNode != null)
                                {
                                    this.WriteMessage(body, this.getInstanceNsu(machine), TypeDefinitionNode.BrowseName.Name);
                                }
                            }
                        }
                    }
                }
                
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                this.connected = false;
                throw;
            }
        } 
        private void createJSON(JObject jObject, NodeId nodeId, NodeId? parent = null)
        {
            List<NodeId> hierarchicalChilds = this.client.BrowseLocalNodeIds(nodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true);
            foreach (NodeId child in hierarchicalChilds)
            {
                Node? childNode = this.client.ReadNode(child);
                if (childNode != null)
                {
                    //String browseName = childNode.BrowseName.ToString();
                    String browseName = childNode.BrowseName.Name.ToString();
                    this.Debug($"{browseName}");
                    JObject childObject = new JObject();
                    createJSON(childObject, child, nodeId);
                    if (childNode.NodeClass == NodeClass.Object)
                    {
                        if (jObject.ContainsKey(browseName)) {
                            Console.Out.WriteLine("Warning double browseName");
                        }
                        else
                        {
                            jObject.Add(browseName, childObject);
                        }
                    }
                    if (childNode.NodeClass == NodeClass.Variable)
                    {
                        object dataValue = getDataValueAsObject(child);
                        bool isProperty = false;
                        if (this.client.getTypeDefinition(child) == VariableTypeIds.PropertyType)
                        {
                            isProperty = true;
                        }
                        if(isProperty)
                        {
                            if (dataValue is string)
                            {
                                jObject.Add(browseName, (string)dataValue);
                            }
                            else if (dataValue is JObject)
                            {
                                jObject.Add(browseName, (JObject)dataValue);
                            }
                            else if (dataValue is JArray)
                            {
                                jObject.Add(browseName, (JArray)dataValue);
                            }
                        } else
                        {
                            JObject valueObject = new JObject();
                            if (dataValue is string)
                            {
                                valueObject.Add("value", (string)dataValue);
                            }
                            else if (dataValue is JObject)
                            {
                                valueObject.Add("value", (JObject)dataValue);
                            }
                            else if (dataValue is JArray)
                            {
                                valueObject.Add("value", (JArray)dataValue);
                            }
                            valueObject.Add("properties", childObject);
                            jObject.Add(browseName, valueObject);
                        }
                    }
                }
                
            }

        }
        public void publishIdentification() {
            try
            {
                JArray identificationArray = new JArray();
                foreach (NodeId machine in this.onlineMachines)
                {
                    if (machine != null)
                    {
                        JObject body = new JObject();
                        createJSON(body, machine);
                        Node? machineNode = this.client.ReadNode(machine);
                        if (machineNode != null)
                        {
                            NodeId? typedefinition = this.client.getTypeDefinition(machine);
                            if (typedefinition != null)
                            {
                                Node? TypeDefinitionNode = this.client.ReadNode(typedefinition);
                                if (TypeDefinitionNode != null)
                                {
                                    List <NodeId> identificationNodes = this.client.BrowseLocalNodeIds(machine, BrowseDirection.Forward, (int)NodeClass.Object, ReferenceTypeIds.HierarchicalReferences, true, ObjectTypeIds.FolderType);
                                    foreach(NodeId child in identificationNodes)
                                    {
                                        Node? childNode = this.client.ReadNode(child);
                                        if (childNode != null)
                                        {
                                            if (childNode.BrowseName.Name == "Identification")
                                            {
                                                JObject data = new JObject();
                                                JObject ident = new JObject();
                                                createJSON(ident, child);
                                                data.Add("Data", ident);
                                                data.Add("MachineId", this.getInstanceNsu(machine));
                                                data.Add("ParentId", "nsu=http:_2F_2Fopcfoundation.org_2FUA_2FMachinery_2F;i=1001");
                                                data.Add("Topic", this.mqttPrefix + "/" + this.clientId + "/" + TypeDefinitionNode.BrowseName.Name + "/" + this.getInstanceNsu(machine));
                                                data.Add("TypeDefinition", TypeDefinitionNode.BrowseName.Name);
                                                identificationArray.Add(data);
                                            }
                                        }
                                    }
                                    this.WriteIdentification(identificationArray, this.getInstanceNsu(machine), TypeDefinitionNode.BrowseName.Name);
                                }
                            }
                        }
                    }
                }
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                this.connected = false;
            } 
        }

        public void createJsonForIdentification(JObject jObject, StructuredNode structuredNode, string machineId, string parentId, string topic) {
            if(structuredNode.childNodes.Count > 0) {
                JObject obj = new JObject();
                foreach (StructuredNode sn in structuredNode.childNodes) {
                    createJson(obj, sn);
                }
                jObject.Add("Data", obj);
                jObject.Add("MachineId", machineId);
                jObject.Add("ParentId", parentId);
                jObject.Add("Topic", topic);
                jObject.Add("TypeDefinition", "WireHarnessMachineType");
            }
        }
        public void createJson(JObject jObject, StructuredNode structuredNode) {
            //Current State extra Treading
            if(structuredNode.browsename.Name == "CurrentState") {
                JObject obj = new JObject();
                Object pobj = getDataValueAsObject(structuredNode.nodeId);
                if(pobj is String) {
                    obj.Add("value", (String)getDataValueAsObject(structuredNode.nodeId));
                } else if (pobj is JObject) {
                    obj.Add("value", (JObject)getDataValueAsObject(structuredNode.nodeId));
                }
                jObject.Add(structuredNode.browsename.Name, obj);
                return;
            }
            if(structuredNode.childNodes.Count > 0) {
                JObject obj = new JObject();
                foreach (StructuredNode sn in structuredNode.childNodes) {
                    createJson(obj, sn);
                }
                jObject.Add(structuredNode.browsename.Name, obj);
            } else if (structuredNode.placeholderNodes.Count > 0){
                JObject obj = new JObject();
                JObject placeHolderObjects = new JObject(); 
                foreach(KeyValuePair<string, List<PlaceHolderNode>> entry in structuredNode.placeholderNodes) {
                   List<PlaceHolderNode> placeholdernodes = entry.Value;
                   //Extra Treatment Jobs
                   if(entry.Key == "<OrderedObject>") {
                        obj.Add(entry.Key, placeHolderObjects);
                        jObject.Add(structuredNode.browsename.Name, obj);
                        foreach(PlaceHolderNode placeholdernode in placeholdernodes) {
                            JObject jobObject = new JObject();
                            jobObject.Add("$TypeDefinition", placeholdernodes[0].typeDefinition);
                            foreach(StructuredNode child in placeholdernode.childNodes) {
                                if(child.browsename.Name == "JobId") {
                                    Object jobId = getDataValueAsObject(child.nodeId);
                                    if(jobId is String) {
                                        jobObject.Add(child.browsename.Name, (String) jobId);
                                    } else if (jobId is JObject) {
                                        jobObject.Add(child.browsename.Name, (JObject) jobId);
                                    }
                                }
                                if(child.browsename.Name == "State") {
                                    createJson(jobObject, child);
                                }
                            }
                            placeHolderObjects.Add(placeholdernode.browseName.Name, jobObject);
                        }
                        return;
                   }
                   foreach(PlaceHolderNode placeholdernode in placeholdernodes) {
                        Object pobj = getDataValueAsObject(placeholdernode.nodeId);
                        if(pobj is String) {
                            placeHolderObjects.Add(placeholdernode.browseName.Name, (String) pobj);
                        } else if (pobj is JObject) {
                            placeHolderObjects.Add(placeholdernode.browseName.Name, (JObject) pobj);
                        }
                   }
                   obj.Add(entry.Key, placeHolderObjects);
                }
                jObject.Add(structuredNode.browsename.Name, obj);
            } else {
                Object obj = getDataValueAsObject(structuredNode.nodeId);
                if(obj is String) {
                    jObject.Add(structuredNode.browsename.Name, (string)obj);
                } else if (obj is JObject) {
                    jObject.Add(structuredNode.browsename.Name, (JObject)obj);
                } else if (obj is JArray) {
                    jObject.Add(structuredNode.browsename.Name, (JArray)obj);
                }
            }
        }
        public object getDataValueAsObject(NodeId nodeId) {
            try
            {
                this.Debug(nodeId.ToString());
                if(nodeId == new NodeId(6028, 7))
                {
                    this.Debug("Here");
                }
                Node? node = this.client.ReadNode(nodeId);
                if (node == null)
                {
                    return "";
                }
                if (node.NodeClass != NodeClass.Variable)
                {
                    return "";
                }
                DataValue dv = this.client.ReadValue(nodeId);
                Object value = dv.Value;
                if (value is LocalizedText)
                {
                    LocalizedText localizedText = (LocalizedText)value;
                    JObject jobj = new JObject();
                    jobj.Add("locale", localizedText.Locale);
                    jobj.Add("text", localizedText.Text);
                    return jobj;
                }
                else if (value is String)
                {
                    String str = (String)value;
                    return str;
                }
                else if (value is UInt16)
                {
                    UInt16 number = (UInt16)value;
                    return number.ToString();
                }
                if(value is ExtensionObject) {
                    JObject jobject = new JObject();
                    ExtensionObject eto = (ExtensionObject) value;
                    ExtensionObjectEncoding encoding = eto.Encoding;
                    if(encoding == ExtensionObjectEncoding.Binary) {
                        ExpandedNodeId binaryEncodingType = eto.TypeId; 
                        if((uint)binaryEncodingType.Identifier == 5008) {
                            //this.decodeResult(jobject, eto);
                            jobject = this.decode(eto);
                            //return this.decode(eto);
                        } else
                        {
                            jobject = this.decode(eto);
                            //this.decode(eto);
                        }
                    }
                    return jobject;
                }
                if(value is ExtensionObject[])
                {
                    ExtensionObject[] etos = (ExtensionObject[]) value;
                    JArray array = new JArray();
                    for(int i = 0; i < etos.Length; i++)
                    {
                        array.Add(this.decode(etos[i]));
                    }
                    return array;
                }
                if(value is VariantCollection)
                {
                    this.Debug("Here");
                }
                return "";
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
        public void generaleDecodeResultObject(JObject obj, ExtensionObject eto)
        {

        }
        
        public JObject decode( Object obj)
        {
            JObject jObject = new JObject();
            return jObject;
        }

        public object decode(Variant variant)
        {
            JObject jObject = new JObject();
            object obj = variant.Value;
            if(obj is String)
            {
                return (String)obj;
            } else if (obj is ExtensionObject)
            {
                return decode((ExtensionObject)obj);
            }
            return jObject;
        }
        public JObject decodeProcessingCategory(ExtensionObject eto)
        {
            JObject jObject = new JObject();
            BinaryDecoder binaryDecoder = new BinaryDecoder((byte[])eto.Body, ServiceMessageContext.GlobalContext);
            jObject.Add("ID", binaryDecoder.ReadString("ID"));
            jObject.Add("Description", binaryDecoder.ReadString("Description"));
            int length = binaryDecoder.ReadInt32("length");
            JArray array = new JArray();
            for (int i = 0; i < length; i++)
            {
                JObject supPar = new JObject();
                supPar.Add("Name", binaryDecoder.ReadString("Name"));
                supPar.Add("Description", binaryDecoder.ReadString("Description"));
                //ValueType
                JObject valueType = new JObject();
                valueType.Add("Name", binaryDecoder.ReadString("Name"));
                valueType.Add("Description", binaryDecoder.ReadString("Description"));
                valueType.Add("BaseUnit", binaryDecoder.ReadString("BaseUnit"));
                valueType.Add("PossibleValue", binaryDecoder.ReadString("PossibleValue"));
                supPar.Add("ValueType", valueType);
                supPar.Add("TypicalValue", binaryDecoder.ReadString("TypicalValue"));
                supPar.Add("Mandatory", binaryDecoder.ReadBoolean("Mandatory"));
                //Eclass
                JObject eclass = new JObject();
                eclass.Add("ID", binaryDecoder.ReadString("ID"));
                eclass.Add("Description", binaryDecoder.ReadString("Description"));
                eclass.Add("EClass", binaryDecoder.ReadString("Eclass"));
                supPar.Add("EClass", eclass);
                array.Add(supPar);
            }
            jObject.Add("SupportedParameter", array);
            length = binaryDecoder.ReadInt32("length");
            array = new JArray();
            for(int i = 0; i < length; i++)
            {
                array.Add(binaryDecoder.ReadString("SupportedAssignment"));
            }
            jObject.Add("SupportedAssignment", array);
            length = binaryDecoder.ReadInt32("length");
            array = new JArray();
            for (int i = 0; i < length; i++)
            {
                JObject supVar = new JObject();
                supVar.Add("Name", binaryDecoder.ReadString("Name"));
                supVar.Add("Description", binaryDecoder.ReadString("Description"));
                //ValueType
                JObject valueType = new JObject();
                valueType.Add("Name", binaryDecoder.ReadString("Name"));
                valueType.Add("Description", binaryDecoder.ReadString("Description"));
                valueType.Add("BaseUnit", binaryDecoder.ReadString("BaseUnit"));
                valueType.Add("PossibleValue", binaryDecoder.ReadString("PossibleValue"));
                supVar.Add("ValueType", valueType);
                supVar.Add("TypicalValue", binaryDecoder.ReadString("TypicalValue"));
                supVar.Add("Mandatory", binaryDecoder.ReadBoolean("Mandatory"));
                //Eclass
                JObject eclass = new JObject();
                eclass.Add("ID", binaryDecoder.ReadString("ID"));
                eclass.Add("Description", binaryDecoder.ReadString("Description"));
                eclass.Add("EClass", binaryDecoder.ReadString("Eclass"));
                supVar.Add("EClass", eclass);
                array.Add(supVar);
            }
            jObject.Add("SupportedVariable", array);
            jObject.Add("SupportsTransformation", binaryDecoder.ReadInt32("SupportsTransformation"));
            jObject.Add("SupportsSubProcessing", binaryDecoder.ReadInt32("SupportsSubProcessing"));
            return jObject;

        }
        public JObject decode(ExtensionObject eto)
        {
            if (eto.TypeId.IdType == IdType.Numeric && (uint)eto.TypeId.Identifier == 5007)
            {
                return this.decodeProcessingCategory(eto);
            }
            if (eto.TypeId.IdType == IdType.Numeric && (uint)eto.TypeId.Identifier == 5032)
            {
                this.Debug("Here");
            }
            JObject jObject = new JObject();
            this.Debug("Eto Expanded NodeId:" + eto.TypeId.ToString());
            NodeId etoId = ExpandedNodeId.ToNodeId(eto.TypeId, this.client.GetNamespaceTable());
            this.Debug("Eto NodeId:" + etoId.ToString());
            NodeId dataType = this.client.BrowseLocalNodeId(etoId, BrowseDirection.Inverse, (uint)NodeClass.DataType, ReferenceTypeIds.HasEncoding, true);
            if (dataType != null)
            {
                this.Debug("DataType NodeId:" + dataType.ToString());
            } else
            {
                dataType = etoId;
                this.Debug("DataType NodeId:" + dataType.ToString() + "Took otherId as NodeId");
            }
            Dictionary<NodeId, Node> dataTypes = this.client.TypeDictionaries.GetDataTypes();
            NodeId search = dataType;
            bool success = dataTypes.TryGetValue(search, out Node? value);
            if (!success)
            {
                if (value != null)
                {
                    jObject.Add("Error", "Unable to get TypeInformation.");
                }
            }
            else
            {
                if (value != null)
                {
                    Dictionary<GeneratedDataTypeDefinition, GeneratedDataClass> gclasses = this.client.TypeDictionaries.generatedDataTypes;
                    DataTypeNode dtn = (DataTypeNode)value;
                    GeneratedDataTypeDefinition generatedDataTypeDefinition = new GeneratedDataTypeDefinition(this.client.GetNamespaceTable().GetString(dtn.NodeId.NamespaceIndex), dtn.BrowseName.Name);
                    gclasses.TryGetValue(generatedDataTypeDefinition, out GeneratedDataClass? gdc);
                    ExtensionObject dtd = dtn.DataTypeDefinition;
                    if (gdc != null)
                    {
                        BinaryDecoder BinaryDecoder = new BinaryDecoder((byte[])eto.Body, ServiceMessageContext.GlobalContext);
                        jObject = this.decode(BinaryDecoder, gdc);
                    }
                }
            }
            return jObject;
        }
        public GeneratedDataClass? GetGeneratedDataClass(string namespaceurl, string browsename)
        {
            Dictionary<GeneratedDataTypeDefinition, GeneratedDataClass> gclasses = this.client.TypeDictionaries.generatedDataTypes;
            GeneratedDataTypeDefinition generatedDataTypeDefinition = new GeneratedDataTypeDefinition(namespaceurl, browsename);
            gclasses.TryGetValue(generatedDataTypeDefinition, out GeneratedDataClass? gdc);
            return gdc;
        }
        public JObject decode(BinaryDecoder BinaryDecoder, GeneratedDataClass generatedDataClass)
        {
            JObject jObject = new JObject();
            if (generatedDataClass != null)
            {
                if (generatedDataClass is GeneratedStructure)
                {
                    GeneratedStructure generatedStructure = (GeneratedStructure)generatedDataClass;
                    Int32 previousInt32 = 0;
                    UInt32 mask = 0;
                    Int32 currentSwitchBit = 0;
                    bool lastFieldWasSwitchedOff = false;
                    foreach (GeneratedField field in generatedStructure.fields)
                    {
                        this.Debug("Decode:" + field.Name + " " + field.TypeName);
                        if (field.IsLengthField == true)
                        {
                            if(field.Name == "ResultUri")
                            {
                                this.Debug("Here");
                            }
                            if (lastFieldWasSwitchedOff)
                            {
                                lastFieldWasSwitchedOff = false;
                                continue;

                            }
                            if(previousInt32 == -1)
                            {
                                previousInt32 = 0;
                            }
                            if (field.TypeName == "ua:Variant")
                            {
                                Variant[] v = new Variant[previousInt32];
                                JArray array = new JArray();
                                for (int i = 0; i < previousInt32; i++)
                                {
                                    if (field.TypeName == "ua:Variant")
                                    {
                                        v[i] = BinaryDecoder.ReadVariant(field.Name);
                                        array.Add(decode(v[i]));
                                    }
                                }
                                jObject.Add(field.Name, array);
                            }
                            else if (field.TypeName == "opc:CharArray")
                            {
                                JArray array = new JArray();
                                for (int i = 0; i < previousInt32; i++)
                                {
                                    String valueString = BinaryDecoder.ReadString(field.Name);
                                    this.Debug("Value: " + valueString.ToString());
                                    array.Add(valueString);
                                }
                                jObject.Add(field.Name, array);
                            }
                            else if (field.TypeName == "ua:XVType")
                            {
                                JArray array = new JArray();
                                for (int i = 0; i < previousInt32; i++)
                                {
                                    if (field.TypeName.StartsWith("ua:"))
                                    {
                                        if(generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.ua != null)
                                        {
                                            GeneratedDataClass? gdc2 = this.GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.ua, field.TypeName.Substring(3));
                                            if (gdc2 != null) {
                                                array.Add(decode(BinaryDecoder, gdc2));
                                            }
                                        }   
                                    }
                                }
                                jObject.Add(field.Name, array);
                            } else if (field.TypeName.StartsWith("ua:"))
                            {
                                JArray array = new JArray();
                                for (int i = 0; i < previousInt32; i++)
                                {
                                    if (field.TypeName.StartsWith("ua:"))
                                    {
                                        if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.ua != null)
                                        {
                                            GeneratedDataClass? gdc2 = this.GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.ua, field.TypeName.Substring(3));
                                            if (gdc2 != null)
                                            {
                                                array.Add(decode(BinaryDecoder, gdc2));
                                            }
                                        }
                                    }
                                }
                                jObject.Add(field.Name, array);
                            }
                            if (field.TypeName.StartsWith("tns:"))
                            {
                                JArray array = new JArray();
                                for (int i = 0; i < previousInt32; i++)
                                {
                                    if (field.TypeName.StartsWith("tns:"))
                                    {
                                        if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.tns != null)
                                        {
                                            GeneratedDataClass? gdc2 = this.GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.tns, field.TypeName.Substring(4));
                                            if (gdc2 != null)
                                            {
                                                array.Add(decode(BinaryDecoder, gdc2));
                                            }
                                        }
                                    }
                                }
                                jObject.Add(field.Name, array);
                            }
                            previousInt32 = 0;
                        }
                        else
                        {
                            if (field.IsSwitchField)
                            {
                                bool optionalFieldPresent = this.IsBitSet(mask, currentSwitchBit);
                                currentSwitchBit++;
                                if (!optionalFieldPresent)
                                {
                                    lastFieldWasSwitchedOff = true;
                                    continue;
                                }
                                else
                                {
                                    lastFieldWasSwitchedOff = false;
                                }
                            }
                            else
                            {
                                lastFieldWasSwitchedOff = false;
                            }

                            if (field.TypeName == "opc:Bit" && !field.HasLength)
                            {
                                continue;
                            }
                            else if (field.TypeName == "opc:Bit" && field.HasLength)
                            {
                                mask = BinaryDecoder.ReadUInt32("EncodingMask");
                            }
                            else if (field.TypeName == "opc:Boolean")
                            {
                                Boolean valueBoolean = BinaryDecoder.ReadBoolean(field.Name);
                                jObject.Add(field.Name, valueBoolean);
                                //this.Debug("Value: " + valueBoolean);
                            }
                            else if (field.TypeName == "opc:Byte")
                            {
                                Byte valueByte = BinaryDecoder.ReadByte(field.Name);
                                jObject.Add(field.Name, valueByte.ToString());
                                //this.Debug("Value: " + valueByte.ToString());
                            }
                            else if (field.TypeName == "opc:ByteString")
                            {
                                ByteCollection valueByteCollection = BinaryDecoder.ReadByteString(field.Name);
                                jObject.Add(field.Name, valueByteCollection.ToString());
                                //this.Debug("Value: " + valueByteCollection.ToString());
                            }
                            else if (field.TypeName == "opc:CharArray")
                            {
                                String valueString = BinaryDecoder.ReadString(field.Name);
                                jObject.Add(field.Name, valueString);
                                //Console.WriteLine("Value: " + valueString.ToString());
                            }
                            else if (field.TypeName == "opc:DateTime")
                            {
                                DateTime dateTimeValue = BinaryDecoder.ReadDateTime(field.Name);
                                jObject.Add(field.Name, dateTimeValue.ToString());
                                //this.Debug("Value: " + dateTimeValue.ToString());
                            }
                            else if (field.TypeName == "opc:Double")
                            {
                                Double doubleValue = BinaryDecoder.ReadDouble(field.Name);
                                jObject.Add(field.Name, doubleValue.ToString());
                                //this.Debug("Value: " + doubleValue.ToString());
                            }
                            else if (field.TypeName == "opc:Float")
                            {
                                float floatValue = BinaryDecoder.ReadFloat(field.Name);
                                jObject.Add(field.Name, floatValue.ToString());
                                //this.Debug("Value: " + floatValue.ToString());
                            }
                            else if (field.TypeName == "opc:Guid")
                            {
                                Uuid valueGuid = BinaryDecoder.ReadGuid(field.Name);
                                jObject.Add(field.Name, valueGuid.ToString());
                                //this.Debug("Value: " + valueGuid.ToString());
                            }
                            else if (field.TypeName == "opc:Int16")
                            {
                                short valueInt16 = BinaryDecoder.ReadInt16(field.Name);
                                jObject.Add(field.Name, valueInt16.ToString());
                                //this.Debug("Value: " + valueInt16.ToString());
                            }
                            else if (field.TypeName == "opc:Int32")
                            {
                                Int32 valueInt32 = BinaryDecoder.ReadInt32(field.Name);
                                if (!field.Name.StartsWith("NoOf"))
                                {
                                    jObject.Add(field.Name, valueInt32.ToString());
                                }
                                previousInt32 = valueInt32;
                                //this.Debug("Value: " + valueInt32.ToString());
                            }
                            else if (field.TypeName == "opc:Int64")
                            {
                                long valueInt64 = BinaryDecoder.ReadInt64(field.Name);
                                jObject.Add(field.Name, valueInt64.ToString());
                                //this.Debug("Value: " + valueInt64.ToString());
                            }
                            else if (field.TypeName == "opc:SByte")
                            {
                                sbyte valueSByte = BinaryDecoder.ReadSByte(field.Name);
                                jObject.Add(field.Name, valueSByte.ToString());
                                //this.Debug("Value: " + valueSByte.ToString());
                            }
                            else if (field.TypeName == "opc:String")
                            {
                                String valueString = BinaryDecoder.ReadString(field.Name);
                                jObject.Add(field.Name, valueString);
                                //this.Debug("Value: " + valueString.ToString());
                            }
                            else if (field.TypeName == "opc:UInt16")
                            {
                                ushort uint16Value = BinaryDecoder.ReadUInt16(field.Name);
                                jObject.Add(field.Name, uint16Value.ToString());
                                //this.Debug("Value: " + uint16Value.ToString());
                            }
                            else if (field.TypeName == "opc:UInt32")
                            {
                                uint uint32Value = BinaryDecoder.ReadUInt32(field.Name);
                                jObject.Add(field.Name, uint32Value.ToString());
                                //this.Debug("Value: " + uint32Value.ToString());
                            }
                            else if (field.TypeName == "opc:UInt64")
                            {
                                ulong uint64Value = BinaryDecoder.ReadUInt64(field.Name);
                                jObject.Add(field.Name, uint64Value.ToString());
                                //this.Debug("Value: " + uint64Value.ToString());
                            }
                            else if (field.TypeName == "ua:LocalizedText")
                            {
                                LocalizedText localizedTextValue = BinaryDecoder.ReadLocalizedText(field.Name);
                                jObject.Add(field.Name, localizedTextValue.ToString());
                                //this.Debug("Value: " + localizedTextValue.ToString());
                            }
                            if (field.TypeName == "ua:ExtensionObject")
                            {
                                if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.tns != null && field.Name == "ResultMetaData" && useGMSResultEncoding)
                                {
                                    GeneratedDataClass? gdc2 = this.GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.tns, "ResultMetaDataType");
                                    if (gdc2 != null)
                                    {
                                        jObject.Add(field.Name, decode(BinaryDecoder, gdc2));
                                    }
                                }
                                else
                                {
                                    ExtensionObject valueEto = BinaryDecoder.ReadExtensionObject(field.Name);
                                    jObject.Add(field.Name, decode(valueEto));
                                }
                            }
                            else if (field.TypeName == "ua:Variant")
                            {
                                Variant v = BinaryDecoder.ReadVariant(field.Name);
                                object value = decode(v);
                                if (value is String)
                                {
                                    jObject.Add(field.Name, (String)value);
                                }
                                else if(value is JObject)
                                {
                                    jObject.Add(field.Name, (JObject)value);
                                }
                            }
                            else if (field.TypeName.StartsWith("ua:") && field.TypeName != "ua:LocalizedText" && field.TypeName != "ua:Variant")
                            {
                                if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.ua != null)
                                {
                                    GeneratedDataClass? gdc2 = this.GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.ua, field.TypeName.Substring(3));
                                    if (gdc2 != null)
                                    {
                                            jObject.Add(field.Name, decode(BinaryDecoder, gdc2));
                                    }
                                }
                            }
                            else if (field.TypeName == "tns:ResultEvaluationEnum")
                            {
                                Int32 int32Value = BinaryDecoder.ReadInt32(field.Name);
                                jObject.Add(field.Name, int32Value);
                                //this.Debug("Value: " + int32Value.ToString());
                            }
                            else if (field.TypeName.StartsWith("tns:") && field.TypeName != "tns:ResultEvaluationEnum")
                            {
                                if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.tns != null)
                                {
                                    GeneratedDataClass? gdc2 = this.GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.tns, field.TypeName.Substring(4));
                                    if (gdc2 != null)
                                    {
                                        jObject.Add(field.Name, decode(BinaryDecoder, gdc2));
                                    }
                                }
                            }
                            
                        }
                    }
                } else if (generatedDataClass is GeneratedEnumeratedType)
                {
                    this.Debug("GeneratedEnum");
                }
            }
            return jObject;
        }

        private string getInstanceNsu(StructuredNode machineNode) {
            string nsuString = "nsu=";
            string nameSpace = "";
            string identifier = "";
            NodeId machineId = machineNode.nodeId;
            ushort namespaceIndex = machineId.NamespaceIndex;
            nameSpace = "Instancenamespace";
            nameSpace = nameSpace.Replace("/", "_2F");
            if(machineId.IdType == IdType.Numeric) {
                identifier = "i=" + (uint)machineId.Identifier;
            } else if(machineId.IdType == IdType.String) {
                identifier = "s=" + (string)machineId.Identifier;
            }
            return nsuString + nameSpace + ";" + identifier;
        }
        private string GetNameSpaceForIndex(ushort NamespaceIndex)
        {
            string ns = "";
            DataValue dv = this.client.ReadValue(VariableIds.Server_NamespaceArray);
            if(dv != null)
            {
                String[] namespaces = (String[])dv.Value;
                ns = namespaces[NamespaceIndex];
            }
            return ns;
        }
        private string getInstanceNsu(NodeId nodeId)
        {
            string nsuString = "nsu=";
            string nameSpace = "";
            string identifier = "";
            NodeId machineId = nodeId;
            ushort namespaceIndex = machineId.NamespaceIndex;
            nameSpace = this.GetNameSpaceForIndex(nodeId.NamespaceIndex);
            nameSpace = nameSpace.Replace("/", "_2F");
            if (machineId.IdType == IdType.Numeric)
            {
                identifier = "i=" + (uint)machineId.Identifier;
            }
            else if (machineId.IdType == IdType.String)
            {
                identifier = "s=" + ((string)machineId.Identifier).Replace(" ", "_20");
            }
            return nsuString + nameSpace + ";" + identifier;
        }
        bool IsBitSet(UInt32 value, int pos){
            return ((value >> pos) & 1) != 0;
        }

        public class StructuredNode
        {
            public NodeId nodeId;
            public QualifiedName browsename;
            public List<StructuredNode> childNodes = new List<StructuredNode>();
            public string? placeholderTypeDefinition = null;
            public Dictionary<string, List<PlaceHolderNode>> placeholderNodes = new Dictionary<string, List<PlaceHolderNode>>();
            public StructuredNode(QualifiedName browseName, NodeId nodeId)
            {
                this.browsename = browseName;
                this.nodeId = nodeId;
            }
        }
        public class PlaceHolderNode
        {
            public QualifiedName browseName;
            public NodeId nodeId;
            public string typeDefinition;
            public List<StructuredNode> childNodes = new List<StructuredNode>();
            public PlaceHolderNode(QualifiedName browseName, NodeId nodeId, string typeDefinition)
            {
                this.browseName = browseName;
                this.nodeId = nodeId;
                this.typeDefinition = typeDefinition;
            }
        }
        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            if (this.connected)
            {
                try
                {
                    if (firstRead)
                    {
                        firstRead = false;
                        Console.WriteLine("Publish BadList");
                        this.publishBadList();
                        Console.WriteLine("Publish Bad List finish.");
                        Console.WriteLine("Publish Client Online");
                        this.publishClientOnline();
                        Console.WriteLine("Publish Client Online finish.");
                        Console.WriteLine("Publish Online Machines");
                        this.publishOnlineMachines();
                        Console.WriteLine("Publish Online Machines finish.");
                        Console.WriteLine("Publish Identification");
                        this.publishIdentification();
                        Console.WriteLine("Publish Identification finish.");
                        firstReadFinished = true;
                    }
                    if(firstReadFinished)
                    {
                        
                        if (singleThreadPolling)
                        {
                            if (!ReadInProgress)
                            {
                                ReadInProgress = true;
                                Console.WriteLine("Publish BadList");
                                this.publishBadList();
                                Console.WriteLine("Publish Bad List finish.");
                                Console.WriteLine("Publish Client Online");
                                this.publishClientOnline();
                                Console.WriteLine("Publish Client Online finish.");
                                Console.WriteLine("Publish Online Machines");
                                this.publishOnlineMachines();
                                Console.WriteLine("Publish Online Machines finish.");
                                Console.WriteLine("Publish Identification");
                                this.publishIdentification();
                                Console.WriteLine("Publish Identification finish.");
                                Console.WriteLine("Publish Maschine");
                                this.publishNode();
                                Console.WriteLine("Publish Maschine finished.");
                                ReadInProgress = false;
                            }
                        } else
                        {
                            Console.WriteLine("Publish BadList");
                            this.publishBadList();
                            Console.WriteLine("Publish Bad List finish.");
                            Console.WriteLine("Publish Client Online");
                            this.publishClientOnline();
                            Console.WriteLine("Publish Client Online finish.");
                            Console.WriteLine("Publish Online Machines");
                            this.publishOnlineMachines();
                            Console.WriteLine("Publish Online Machines finish.");
                            Console.WriteLine("Publish Identification");
                            this.publishIdentification();
                            Console.WriteLine("Publish Identification finish.");
                            Console.WriteLine("Publish Maschine");
                            this.publishNode();
                            Console.WriteLine("Publish Maschine finished.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    ReadInProgress = false;
                    this.connected = false;
                   
                }
            } else
            {
                try
                {
                    if (this.ConnectedOnce)
                    {
                        Console.WriteLine("Reconnecting Mqtt");
                        this.Reconnect();
                    }
                }
                catch (Exception ex1)
                {
                    Console.WriteLine(ex1.ToString());
                }
            }
        }
        private Boolean IsPlaceholder()
        {
            return false;
        }
        private void Debug(String message)
        {
            if(debug)
            {
                Console.WriteLine(message);
            }
        }
    }
}