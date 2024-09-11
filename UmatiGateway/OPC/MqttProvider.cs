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
using Microsoft.AspNetCore.Authentication;
using Opc.Ua.Schema.Binary;
using Org.BouncyCastle.Utilities.Encoders;
using MQTTnet.Server;
using System.Reflection.PortableExecutable;
using Org.BouncyCastle.Crypto.IO;
using static UmatiGateway.OPC.MqttProvider;
using System.Reflection;


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
        public List<NodeId> onlineMachines = new List<NodeId>();

        private Client client;
        private Dictionary<NodeId, string> MqttValues = new Dictionary<NodeId, string>();
        private Boolean connected = false;
        private Boolean subscriptionscreated = false;
        private System.Timers.Timer aTimer;
        public MqttProvider(Client client){
            this.client = client;
            this.mqttClient = mqttFactory.CreateMqttClient();
            user = "fva/matthias2";
            pwd = "";
            connectionString = "localhost";
            clientId = "fva/matthias2";
            mqttPrefix = "umati/v2";
            connectionPort = "1883";
            aTimer = new System.Timers.Timer(2000);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
            

        }
        public bool isConnected() {
            return this.connected;
        }
        public void Connect()
        {
            this.Connect(this.connectionString, this.connectionType, this.connectionPort, this.user, this.pwd);
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
                    this.Connect_Client_Using_WebSockets();
                } else {
                    Console.Out.WriteLine("Unkonown Mqtt Connection Type");
                }
                connected = true;
            } catch (Exception e) {
                connected = false;
            }
        }
        private void Connect_Client_Using_WebSockets() {
            if(this.mqttClient != null) {
                 MqttClientOptions mqttClientOptions;
                if (this.user != null && this.user != "" && this.pwd != null)
                {
                    mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithWebSocketServer(this.connectionString)
                    .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                    .WithCredentials(this.user, this.pwd)
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
            } else {
                Console.Out.WriteLine("m_mqttClient is null.");
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
                if (this.user != null && this.user != "" && this.pwd != null)
                {
                    mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(this.connectionString, port)
                    .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                    .WithCredentials(this.user, this.pwd)
                    .Build();
                }
                else
                {
                    mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(this.connectionString, port)
                    .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                    .Build();
                }
                AsyncHelper.RunSync(() => this.mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None));
            } else {
                Console.Out.WriteLine("The MqttClient is null");
            }
        }
        public void WriteMessage() {
        }
        public bool WriteMessage(JObject jObject, string machineId) {
            try {
                string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + "WireHarnessMachineType" + "/" + machineId;
                MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic)
                .WithPayload(jObject.ToString(Newtonsoft.Json.Formatting.Indented))
                .Build();
                if(this.mqttClient != null) {
                    _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                }
                return true; 
            } catch (Exception e) {
                //logger.Error ("Unable to publish Machine", e);
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
        public bool publishOnlineMachines(List<StructuredNode> onlineMachines) {
            try {
                foreach(StructuredNode machine in onlineMachines) {
                string MyTopic = this.mqttPrefix + "/" + this.clientId + "/"+ "online/";
                MyTopic += this.getInstanceNsu(machine);  
                MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic)
                .WithPayload("1")
                .Build();
                    if(this.mqttClient != null) {
                         _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                    }
                }
                return true;
            }catch(Exception e) {
                //logger.Error ("Unable to publish online Machines", e);
                this.connected = false;
                return false;
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
                .WithPayload("wireharness_1.0")
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
                        this.WriteMessage(body, this.getInstanceNsu(machine));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                this.connected = false;
            }
        }
        private void createJSON(JObject jObject, NodeId nodeId)
        {
            List<NodeId> hierarchicalChilds = this.client.BrowseLocalNodeIds(nodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HasChild, true);
            foreach (NodeId child in hierarchicalChilds)
            {
                Node? childNode = this.client.ReadNode(child);
                if (childNode != null)
                {
                    String browseName = childNode.BrowseName.ToString();
                    JObject childObject = new JObject();
                    createJSON(childObject, child);
                    if (childNode.NodeClass == NodeClass.Object)
                    {
                        jObject.Add(browseName, childObject);
                    }
                    if (childNode.NodeClass == NodeClass.Variable)
                    {
                        object dataValue = getDataValueAsObject(child);
                        if (dataValue is string) {
                            jObject.Add(browseName, (string)dataValue);
                        } else if (dataValue is JObject)
                        {
                            jObject.Add(browseName, (JObject)dataValue);
                        } else if (dataValue is JArray)
                        {
                            jObject.Add(browseName, (JArray)dataValue);
                        }
                    }
                }
                
            }

            /*Object obj = getDataValueAsObject(nodeId);
            Node? node = this.client.ReadNode(nodeId);
            if (node != null) {
                
                if (obj is String)
                {

                    if (!jObject.ContainsKey(browseName))
                    {
                        jObject.Add(browseName, (string)obj);
                    }
                }
                else if (obj is JObject)
                {
                    if (!jObject.ContainsKey(browseName))
                    {
                        jObject.Add(browseName, (JObject)obj);
                    }
                }
                else if (obj is JArray)
                {
                    if (!jObject.ContainsKey(browseName))
                    {
                        jObject.Add(browseName, (JArray)obj);
                    }
                }
            }*/
        }
        public bool publishIdentification(List<StructuredNode> onlineMachines) {
            if (!this.subscriptionscreated) {
                this.CreateSubscriptions(onlineMachines);
                this.subscriptionscreated = true;
            }
            try {
                string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + "list/WireHarnessMachineType";
                JObject json = new JObject();
                List<StructuredNode> identifications = new List<StructuredNode>();
                List<string> nsus = new List<string>();
                foreach(StructuredNode machine in onlineMachines) {
                    foreach(StructuredNode child in machine.childNodes) {
                        if(child.browsename.Name == "Identification") {
                            identifications.Add(child);
                            nsus.Add(this.getInstanceNsu(machine));
                        }
                    }
                }
                JArray jArray = new JArray();
                int i = 0;
                foreach(StructuredNode identification in identifications) {
                    JObject jobject = new JObject();
                    createJsonForIdentification(jobject, identification,nsus[i],"nsu=http:_2F_2Fopcfoundation.org_2FUA_2FMachinery_2F;i=1001", this.mqttPrefix + "/" + this.clientId + "/" + "WireHarnessMachineType" + "/" + this.getInstanceNsu(onlineMachines[i]));
                    jArray.Add(jobject);
                    i++;
                }
                MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic)
                .WithPayload(jArray.ToString(Newtonsoft.Json.Formatting.Indented))
                .Build();
                if(this.mqttClient != null) {
                    _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                
                }
                return true;
            } catch(Exception e) {
                //logger.Error ("Unable to publish Identification", e);
                this.connected = false;
                return false;
            } 
        }

        public bool valueChanged(StructuredNode structuredNode) {
            this.getInstanceNsu(structuredNode);
            JObject body = new JObject();
            foreach(StructuredNode childNode in structuredNode.childNodes) {
              this.createJson(body, childNode);  
            }
            return this.WriteMessage(body, this.getInstanceNsu(structuredNode));
        }
        public void valuesChanged(List<NodeId> values) {

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
                        } else
                        {
                            jobject = this.decode(eto);
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
                    Console.WriteLine("Here");
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
        public JObject generalDecodeExtensionObject(JObject obj, ExtensionObject eto)
        {
            JObject jObject = new JObject();
            jObject.Add("TypeId", eto.TypeId.ToString());
            NodeId etoId = ExpandedNodeId.ToNodeId(eto.TypeId, this.client.GetNamespaceTable());
            NodeId dataType = this.client.BrowseLocalNodeId(etoId , BrowseDirection.Inverse, (uint)NodeClass.DataType, ReferenceTypeIds.HasEncoding, true);
            Dictionary<NodeId, Node> dataTypes = this.client.TypeDictionaries.GetDataTypes();
            NodeId search = dataType;
            bool success = dataTypes.TryGetValue(search, out Node? value);
            if(!success)
            {
                if (value != null)
                {
                    //this.client.TypeDictionaries.generatedDataTypes.TryGetValue();
                    jObject.Add("Error", "Unable to get TypeInformation.");
                }
            } else
            {
                if (value != null)
                {
                    Dictionary < GeneratedDataTypeDefinition, GeneratedDataClass > gclasses = this.client.TypeDictionaries.generatedDataTypes;
                    DataTypeNode dtn = (DataTypeNode) value;
                    GeneratedDataTypeDefinition generatedDataTypeDefinition = new GeneratedDataTypeDefinition(this.client.GetNamespaceTable().GetString(dtn.NodeId.NamespaceIndex), dtn.BrowseName.Name);
                    gclasses.TryGetValue(generatedDataTypeDefinition, out GeneratedDataClass? gdc);
                    ExtensionObject dtd = dtn.DataTypeDefinition;
                    jObject.Add("Success", "Found TypeInformation.");
                    if (gdc != null)
                    {
                        BinaryDecoder BinaryDecoder = new BinaryDecoder((byte[])eto.Body, ServiceMessageContext.GlobalContext);
                        if(gdc is GeneratedStructure)
                        {
                            GeneratedStructure generatedStructure = (GeneratedStructure)gdc;
                            Int32 previousInt32 = 0;
                            foreach(GeneratedField field in generatedStructure.fields)
                            {
                                if (field.IsLengthField == true)
                                {
                                    if (field.TypeName == "ua:Variant")
                                    {
                                        Variant[] v = new Variant[previousInt32];
                                        for (int i = 0; i < previousInt32; i++)
                                        {
                                            if (field.TypeName == "ua:Variant")
                                            {
                                                v[i] = BinaryDecoder.ReadVariant(field.Name);
                                            }
                                        }
                                    }
                                    jObject.Add(field.Name, value.ToString());
                                } else
                                {
                                    if (field.TypeName == "ua:ExtensionObject")
                                    {
                                        ExtensionObject valueEto = BinaryDecoder.ReadExtensionObject(field.Name);
                                        jObject.Add(field.Name, valueEto.ToString());
                                    }
                                    if (field.TypeName == "opc:Int32")
                                    {
                                        Int32 valueInt32 = BinaryDecoder.ReadInt32(field.Name);
                                        jObject.Add(field.Name, valueInt32);
                                        previousInt32 = valueInt32;
                                    }
                                }

                            }
                        }

                    }
                    jObject.Add("Value", value.ToString());
                }
            }
            return jObject;
        }
        public JObject decode( Object obj)
        {
            JObject jObject = new JObject();
            return jObject;
        }
        public JObject decode(Variant variant)
        {
            JObject jObject = new JObject();
            ExtensionObject? eto = variant.Value as ExtensionObject;
            if (eto != null) { jObject.Add("Vale", decode(eto)); }
            return jObject;
        }
        public JObject decode(ExtensionObject eto)
        {
            JObject jObject = new JObject();
            jObject.Add("TypeId", eto.TypeId.ToString());
            NodeId etoId = ExpandedNodeId.ToNodeId(eto.TypeId, this.client.GetNamespaceTable());
            NodeId dataType = this.client.BrowseLocalNodeId(etoId, BrowseDirection.Inverse, (uint)NodeClass.DataType, ReferenceTypeIds.HasEncoding, true);
            Dictionary<NodeId, Node> dataTypes = this.client.TypeDictionaries.GetDataTypes();
            NodeId search = dataType;
            bool success = dataTypes.TryGetValue(search, out Node? value);
            if (!success)
            {
                if (value != null)
                {
                    //this.client.TypeDictionaries.generatedDataTypes.TryGetValue();
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
                    //jObject.Add("Success", "Found TypeInformation.");
                    if (gdc != null)
                    {
                        BinaryDecoder BinaryDecoder = new BinaryDecoder((byte[])eto.Body, ServiceMessageContext.GlobalContext);
                        if (gdc is GeneratedStructure)
                        {
                            GeneratedStructure generatedStructure = (GeneratedStructure)gdc;
                            Int32 previousInt32 = 0;
                            UInt32 mask = 0;
                            Int32 currentSwitchBit = 0;
                            bool lastFieldWasSwitchedOff = false;
                            foreach (GeneratedField field in generatedStructure.fields)
                            {
                                if (field.IsLengthField == true)
                                {
                                    if (lastFieldWasSwitchedOff)
                                    {
                                        lastFieldWasSwitchedOff = false;
                                        continue;
                                        
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
                                    if (field.TypeName == "ua:XVType")
                                    {
                                        ExtensionObject[] etos = new ExtensionObject[previousInt32];
                                        JArray array = new JArray();
                                        for (int i = 0; i < previousInt32; i++)
                                        {
                                            if (field.TypeName == "ua:XVType")
                                            {
                                                etos[i] = BinaryDecoder.ReadExtensionObject(field.Name);
                                                array.Add(decode(etos[i]));
                                            }
                                        }
                                        jObject.Add(field.Name, array);
                                    }
                                }
                                else
                                {
                                    if (field.IsSwitchField)
                                    {
                                        bool optionalFieldPresent = this.IsBitSet(mask, currentSwitchBit);
                                        currentSwitchBit++;
                                        if(!optionalFieldPresent)
                                        {
                                            lastFieldWasSwitchedOff = true;
                                            continue;
                                        } else
                                        {
                                            lastFieldWasSwitchedOff = false;
                                        }
                                    } else
                                    {
                                        lastFieldWasSwitchedOff = false;
                                    }

                                    if (field.TypeName == "opc:Bit" && !field.HasLength)
                                    {
                                        continue;
                                    }
                                    if (field.TypeName == "opc:Bit" && field.HasLength)
                                    {
                                        mask = BinaryDecoder.ReadUInt32("EncodingMask");
                                    }
                                    if (field.TypeName == "opc:CharArray")
                                    {
                                        String valueString = BinaryDecoder.ReadString(field.Name);
                                        jObject.Add(field.Name, valueString);
                                    }

                                    if (field.TypeName == "opc:Boolean")
                                    {
                                        Boolean valueBoolean = BinaryDecoder.ReadBoolean(field.Name);
                                        jObject.Add(field.Name, valueBoolean);
                                    }

                                    if (field.TypeName == "ua:ExtensionObject")
                                    {
                                        ExtensionObject valueEto = BinaryDecoder.ReadExtensionObject(field.Name);
                                        jObject.Add(field.Name, decode(valueEto));
                                    }
                                    if (field.TypeName == "opc:Int32")
                                    {
                                        Int32 valueInt32 = BinaryDecoder.ReadInt32(field.Name);
                                        jObject.Add(field.Name, valueInt32);
                                        previousInt32 = valueInt32;
                                    }
                                    if(field.TypeName == "opc:DateTime")
                                    {
                                        DateTime dateTimeValue = BinaryDecoder.ReadDateTime(field.Name);
                                        jObject.Add(field.Name, dateTimeValue.ToString());
                                    }
                                    if (field.TypeName == "opc:Int64")
                                    {
                                        Int64 int64Value = BinaryDecoder.ReadInt64(field.Name);
                                        jObject.Add(field.Name, int64Value.ToString());
                                    }
                                    if (field.TypeName == "ua:LocalizedText")
                                    {
                                        LocalizedText localizedTextValue = BinaryDecoder.ReadLocalizedText(field.Name);
                                        jObject.Add(field.Name, localizedTextValue.ToString());
                                    }
                                    if (field.TypeName == "opc:Double")
                                    {
                                        Double doubleValue = BinaryDecoder.ReadDouble(field.Name);
                                        jObject.Add(field.Name, doubleValue.ToString());
                                    }
                                    if (field.TypeName == "tns:ResultEvaluationEnum")
                                    {
                                        Int32 int32Value = BinaryDecoder.ReadInt32(field.TypeName);
                                        jObject.Add(field.Name, int32Value);
                                    }
                                    if (field.TypeName == "tns:ForceCurveDataType")
                                    {
                                        GeneratedDataTypeDefinition generatedDataTypeDefinition2 = new GeneratedDataTypeDefinition(generatedDataTypeDefinition.nameSpace, field.TypeName.Substring(4));
                                        gclasses.TryGetValue(generatedDataTypeDefinition2, out GeneratedDataClass? gdc2);
                                        if (gdc2 != null)
                                        {
                                            jObject.Add(field.Name, this.decode(BinaryDecoder, gdc2));
                                        }
                                    }
                                    if (field.TypeName == "ua:XVType")
                                    {
                                        ExtensionObject etoValue = BinaryDecoder.ReadExtensionObject(field.TypeName);
                                        jObject.Add(field.Name, decode(etoValue));
                                    }
                                    if (field.TypeName == "ua:EUInformation")
                                    {
                                        ExtensionObject etoValue = BinaryDecoder.ReadExtensionObject(field.TypeName);
                                        jObject.Add(field.Name, decode(etoValue));
                                    }
                                }
                            }
                               
                 
                        }

                    }
                    //jObject.Add("Value", value.ToString());
                }
            }
            return jObject;
        }
        public JObject decode(BinaryDecoder BinaryDecoder, GeneratedDataClass gdc)
        {
            JObject jObject = new JObject();
            if (gdc != null)
            {
                if (gdc is GeneratedStructure)
                {
                    GeneratedStructure generatedStructure = (GeneratedStructure)gdc;
                    Int32 previousInt32 = 0;
                    UInt32 mask = 0;
                    Int32 currentSwitchBit = 0;
                    bool lastFieldWasSwitchedOff = false;
                    foreach (GeneratedField field in generatedStructure.fields)
                    {
                        if (field.IsLengthField == true)
                        {
                            if (lastFieldWasSwitchedOff)
                            {
                                lastFieldWasSwitchedOff = false;
                                continue;

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
                            if (field.TypeName == "ua:XVType")
                            {
                                ExtensionObject[] etos = new ExtensionObject[previousInt32];
                                JArray array = new JArray();
                                for (int i = 0; i < previousInt32; i++)
                                {
                                    if (field.TypeName == "ua:XVType")
                                    {
                                        etos[i] = BinaryDecoder.ReadExtensionObject(field.Name);
                                        array.Add(decode(etos[i]));
                                    }
                                }
                                jObject.Add(field.Name, array);
                            }
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
                            if (field.TypeName == "opc:Bit" && field.HasLength)
                            {
                                mask = BinaryDecoder.ReadUInt32("EncodingMask");
                            }
                            if (field.TypeName == "opc:CharArray")
                            {
                                String valueString = BinaryDecoder.ReadString(field.Name);
                                jObject.Add(field.Name, valueString);
                            }

                            if (field.TypeName == "opc:Boolean")
                            {
                                Boolean valueBoolean = BinaryDecoder.ReadBoolean(field.Name);
                                jObject.Add(field.Name, valueBoolean);
                            }

                            if (field.TypeName == "ua:ExtensionObject")
                            {
                                ExtensionObject valueEto = BinaryDecoder.ReadExtensionObject(field.Name);
                                jObject.Add(field.Name, decode(valueEto));
                            }
                            if (field.TypeName == "opc:Int32")
                            {
                                Int32 valueInt32 = BinaryDecoder.ReadInt32(field.Name);
                                jObject.Add(field.Name, valueInt32);
                                previousInt32 = valueInt32;
                            }
                            if (field.TypeName == "opc:DateTime")
                            {
                                DateTime dateTimeValue = BinaryDecoder.ReadDateTime(field.Name);
                                jObject.Add(field.Name, dateTimeValue.ToString());
                            }
                            if (field.TypeName == "opc:Int64")
                            {
                                Int64 int64Value = BinaryDecoder.ReadInt64(field.Name);
                                jObject.Add(field.Name, int64Value.ToString());
                            }
                            if (field.TypeName == "ua:LocalizedText")
                            {
                                LocalizedText localizedTextValue = BinaryDecoder.ReadLocalizedText(field.Name);
                                jObject.Add(field.Name, localizedTextValue.ToString());
                            }
                            if (field.TypeName == "opc:Double")
                            {
                                Double doubleValue = BinaryDecoder.ReadDouble(field.Name);
                                jObject.Add(field.Name, doubleValue.ToString());
                            }
                            if (field.TypeName == "tns:ResultEvaluationEnum")
                            {
                                Int32 int32Value = BinaryDecoder.ReadInt32(field.TypeName);
                                jObject.Add(field.Name, int32Value);
                            }
                            /*if (field.TypeName == "tns:ForceCurveDataType")
                            {
                                GeneratedDataTypeDefinition generatedDataTypeDefinition2 = new GeneratedDataTypeDefinition(generatedDataTypeDefinition.nameSpace, field.TypeName.Substring(4));
                                gclasses.TryGetValue(generatedDataTypeDefinition2, out GeneratedDataClass? gdc2);

                                ExtensionObject etoValue = BinaryDecoder.ReadExtensionObject(field.TypeName);
                                jObject.Add(field.Name, decode(etoValue));
                            }*/
                            if (field.TypeName == "ua:XVType")
                            {
                                ExtensionObject etoValue = BinaryDecoder.ReadExtensionObject(field.TypeName);
                                jObject.Add(field.Name, decode(etoValue));
                            }
                            if (field.TypeName == "ua:EUInformation")
                            {
                                ExtensionObject etoValue = BinaryDecoder.ReadExtensionObject(field.TypeName);
                                jObject.Add(field.Name, decode(etoValue));
                            }
                        }
                    }
                }
            }
            return jObject;
        }

        public JObject decodeExtensionObject(ExtensionObject eto)
        {
            JObject jObject = new JObject();
            //BinaryDecoder decoder = new BinaryDecoder((byte[])eto.Body, ServiceMessageContext.GlobalContext);
            jObject.Add("TypeId", eto.TypeId.ToString());
            return jObject;
        }
        public JObject decodeCrimpOutputType(ExtensionObject crimpOutput)
        {
            JObject jobj = new JObject();
            BinaryDecoder decoder = new BinaryDecoder((byte[])crimpOutput.Body, ServiceMessageContext.GlobalContext);
            UInt32 encodingMask = decoder.ReadUInt32("EncodingMask");
            bool hasActualCrimpHeigth = IsBitSet(encodingMask, 0);
            bool hasActualCrimpWith = IsBitSet(encodingMask, 1);
            bool hasActualCrimpForceCurve = IsBitSet(encodingMask, 2);
            bool hasActualReferenceCrimpCurve = IsBitSet(encodingMask, 3);
            bool hasActualInsulationCrimpHeight = IsBitSet(encodingMask, 4);
            bool hasCrimpForceMonitoringResultDataSet = IsBitSet(encodingMask, 5);
            bool hasMaximumCrimpForce = IsBitSet(encodingMask, 6);
            bool hasActualCrimpPullOutForce = IsBitSet(encodingMask, 7);
            jobj.Add("ToolInstance", decoder.ReadString("ToolInstance"));
            jobj.Add("ProcessResult", decoder.ReadString("ProcessResult"));
            jobj.Add("ProcessStartTime", decoder.ReadDouble("ProcessStartTime"));
            jobj.Add("ProcessEndTime", decoder.ReadDouble("ProcessEndTime"));
            jobj.Add("ProcessIdentifier", decoder.ReadString("ProcessIdentifier"));
            jobj.Add("ProductIdentifier", decoder.ReadString("ProductIdentifier"));
            if (hasActualCrimpHeigth) {
                jobj.Add("ActualCrimpHeight", this.decodeValueUnitDataType(decoder));
            }
            if (hasActualCrimpWith)
            {
                jobj.Add("ActualCrimpWidth", this.decodeValueUnitDataType(decoder));
            }
            if (hasActualCrimpForceCurve)
            {
                jobj.Add("ActualCrimpForceCurve", this.decodeForceCurveDataType(decoder));
            }
            if (hasActualReferenceCrimpCurve)
            {
                jobj.Add("ActualReferenceCrinpCurve", this.decodeForceCurveDataType(decoder));
            }
            if(hasActualInsulationCrimpHeight)
            {
                jobj.Add("ActualInsulationCrimpHeight", this.decodeValueUnitDataType(decoder));
            }
            if(hasCrimpForceMonitoringResultDataSet)
            {
                jobj.Add("CrimpForceMonitoringResulDataSet", this.decodeKeyValuePairArray(decoder));
            }
            if(hasMaximumCrimpForce)
            {
                jobj.Add("MaximumCrimpForce", this.decodeValueUnitDataType(decoder));
            }
            if(hasActualCrimpPullOutForce)
            {
                jobj.Add("ActualCrimpPullOutForce", this.decodeValueUnitDataType(decoder));
            }
            return jobj;
        }
        private JArray decodeKeyValuePairArray(BinaryDecoder decoder)
        {
            JArray keyValuePairArray = new JArray();
            UInt32 length = decoder.ReadUInt32("length");
            for (int i = 0; i < length; i++)
            {
                keyValuePairArray.Add(this.decodeKeyValuePair(decoder));
            }
            return keyValuePairArray;
        }

        private JObject decodeKeyValuePair(BinaryDecoder decoder)
        {
            JObject keyValuePairObject = new JObject();
            QualifiedName qualifiedName = decoder.ReadQualifiedName("Key");
            keyValuePairObject.Add("Key", qualifiedName.NamespaceIndex + ":" + qualifiedName.Name);
            Variant variant = decoder.ReadVariant("Value");
            keyValuePairObject.Add("Value", variant.Value.ToString());
            return keyValuePairObject;
        }
        private JObject decodeValueUnitDataType(BinaryDecoder decoder) {
            JObject valueUnitDataTypeObject = new JObject();
            valueUnitDataTypeObject.Add("Value", decoder.ReadDouble("Value"));
            valueUnitDataTypeObject.Add("EngineeringUnits", this.decodeEUInformation(decoder));
            return valueUnitDataTypeObject;
        }
        private JObject decodeForceCurveDataType(BinaryDecoder decoder) {
            JObject forceCurveDataType = new JObject();
            forceCurveDataType.Add("Points", this.decodeXVTypeArray(decoder));
            forceCurveDataType.Add("EngineeringUnitsX", this.decodeEUInformation(decoder));
            forceCurveDataType.Add("EngineeringUnitsValue", this.decodeEUInformation(decoder));
            return forceCurveDataType;
        }
        private JArray decodeXVTypeArray(BinaryDecoder decoder)
        {
            JArray xvTypeArrayObject = new JArray();
            UInt32 length = decoder.ReadUInt32("length");
            for (int i = 0; i < length; i++)
            {
                xvTypeArrayObject.Add(this.decodeXVType(decoder));
            }
            return xvTypeArrayObject;
        }
        private JObject decodeXVType(BinaryDecoder decoder)
        {
            JObject xvTypeObject = new JObject();
            xvTypeObject.Add("X", decoder.ReadDouble("X"));
            xvTypeObject.Add("Value", decoder.ReadFloat("Value"));
            return xvTypeObject;
        }
        private JObject decodeEUInformation(BinaryDecoder decoder) {
            JObject EUInformationObject = new JObject();
            EUInformationObject.Add("NamespaceUri", decoder.ReadString("NamespaceUri"));
            EUInformationObject.Add("UnitId", decoder.ReadInt32("UnitId"));
            EUInformationObject.Add("DisplayName", this.decodeLocalizedText(decoder));
            EUInformationObject.Add("Description", this.decodeLocalizedText(decoder));
            return EUInformationObject;
        }
        private JObject decodeLocalizedText(BinaryDecoder decoder)
        {
            LocalizedText localizedText = decoder.ReadLocalizedText("DisplayName");
            JObject localizedTextObject = new JObject();
            localizedTextObject.Add("locale", localizedText.Locale);
            localizedTextObject.Add("text", localizedText.Text);
            return localizedTextObject;
        }
        public JObject decodeJobOrderAndStatus(ExtensionObject jobOrderAndState)
        {
            JObject jobj = new JObject();
            BinaryDecoder decoder = new BinaryDecoder((byte[])jobOrderAndState.Body, ServiceMessageContext.GlobalContext);
            UInt32 encodingMask = decoder.ReadUInt32("EncodingMask");
            String jobOrderId = decoder.ReadString("JobOrderId");
            UInt32 stateLength = decoder.ReadUInt32("Length");
            UInt32 relativePathLength = decoder.ReadUInt32("Length");
            for (int i = 0; i < relativePathLength; i++)
            {
                NodeId ReferenceTypeId = decoder.ReadNodeId("ReferencerypeId");
                Boolean IsInverse = decoder.ReadBoolean("IsInverse");
                Boolean IncludeSubTypes = decoder.ReadBoolean("IncludeSubTypes");
                QualifiedName TargetName = decoder.ReadQualifiedName("QualifiedName");
            }
            LocalizedText localizedText = decoder.ReadLocalizedText("StateText");
            UInt32 StateNumber = decoder.ReadUInt32("StateNumber");
            jobj.Add("JobOrderId", jobOrderId);
            jobj.Add("Browsepath", null);
            JObject StateText = new JObject();
            StateText.Add("locale", localizedText.Locale);
            StateText.Add("text", localizedText.Text);
            jobj.Add("StateText", StateText);
            jobj.Add("StateNumber", StateNumber);
            return jobj;
        }
        public JObject decodeJobOrder(ExtensionObject jobOrder)
        {
            JObject jobj = new JObject();
            BinaryDecoder decoder = new BinaryDecoder((byte[])jobOrder.Body, ServiceMessageContext.GlobalContext);
            UInt32 encodingMask = decoder.ReadUInt32("EncodingMask");
            String jobOrderId = decoder.ReadString("JobOrderId");
            jobj.Add("JobOrderId", jobOrderId);
            if(this.IsBitSet(encodingMask,0))
            {
                Opc.Ua.LocalizedText localizedText = decoder.ReadLocalizedText("Description");
                JObject description = new JObject();
                description.Add("locale", localizedText.Locale);
                description.Add("text", localizedText.Text);
                jobj.Add("Description", description);
            }
            if (this.IsBitSet(encodingMask, 1))
            {
                jobj.Add("WorkmasterId", decoder.ReadString("WorkmasterId"));
            }

            if (this.IsBitSet(encodingMask, 2))
            {
                jobj.Add("StartTime", decoder.ReadDateTime("StartTime"));
            }
            if (this.IsBitSet(encodingMask, 3))
            {
                jobj.Add("EndTime", decoder.ReadDateTime("EndTime"));
            }
            if (this.IsBitSet(encodingMask, 4))
            {
                jobj.Add("Priority", decoder.ReadUInt16("Priority"));
            }
            if (this.IsBitSet(encodingMask, 5))
            {
                jobj.Add("JobOrderParameters", decodeIsa95ParameterDataType(decoder));
            }

            return jobj;
        }

        public JObject decodeJobOrderResponse(ExtensionObject jobOrderResponse)
        {
            JObject jobj = new JObject();
            BinaryDecoder decoder = new BinaryDecoder((byte[])jobOrderResponse.Body, ServiceMessageContext.GlobalContext);
            UInt32 encodingMask = decoder.ReadUInt32("EncodingMask");
            String jobResponseId = decoder.ReadString("JobResponseId");
            String jobOrderId = decoder.ReadString("JobOrderId");
            UInt32 pathLength = decoder.ReadUInt32("Length");
            UInt32 relativePathLength = decoder.ReadUInt32("Length");
            for(int i = 0; i < relativePathLength; i++)
            {
                decoder.ReadNodeId("ReferenceTypeId");
                decoder.ReadBoolean("IsInverse");
                decoder.ReadBoolean("IncludeSubTypes");
                decoder.ReadQualifiedName("TargetName");
            }
            Opc.Ua.LocalizedText localizedText = decoder.ReadLocalizedText("StateText");
            UInt32 StateNumber = decoder.ReadUInt32("StateNumber");
            jobj.Add("JobResponseId", jobResponseId);
            jobj.Add("JobOrderId", jobOrderId);
            jobj.Add("Browsepath", null);
            JObject StateText = new JObject();
            StateText.Add("locale", localizedText.Locale);
            StateText.Add("text", localizedText.Text);
            jobj.Add("StateText", StateText);
            jobj.Add("StateNumber", StateNumber);
            if (this.IsBitSet(encodingMask, 4))
            {
                jobj.Add("JobResponseData", decodeIsa95Parameter(decoder));
            }
            if (this.IsBitSet(encodingMask, 8))
            {
                jobj.Add("MaterialRequirements", decodeMaterialRequirements(decoder));
            }
            return jobj;
        }
        public JArray decodeMaterialRequirements(BinaryDecoder decoder)
        {
            JArray isaParameters = new JArray();
            UInt32 length = decoder.ReadUInt32("length");
            for (int i = 0; i < length; i++)
            {
                UInt32 encodingMask = decoder.ReadUInt32("EncodingMask");
                //TBD decode the Material DataType
            }
            return isaParameters;
        }
        public JArray decodeIsa95ParameterDataType(BinaryDecoder decoder)
        {
            JArray isaParameters = new JArray();
            UInt32 length = decoder.ReadUInt32("length");
            for (int i = 0; i < length; i++)
            {
                UInt32 encodingMask = decoder.ReadUInt32("EncodingMask");
                JObject obj = new JObject();
                obj.Add("ID", decoder.ReadString("ID"));
                obj.Add("Value", decoder.ReadVariant("Value").ToString());
                isaParameters.Add(obj);
                if(this.IsBitSet(encodingMask,0))
                {
                    Opc.Ua.LocalizedText localizedText = decoder.ReadLocalizedText("Description");
                    JObject description = new JObject();
                    description.Add("locale", localizedText.Locale);
                    description.Add("text", localizedText.Text);
                    obj.Add("Description", description);
                }
                if (this.IsBitSet(encodingMask, 1))
                {
                    obj.Add("EngineeringUnits", decodeEUInformation(decoder));
                }
                if (this.IsBitSet(encodingMask, 2))
                {
                    obj.Add("Subparameters", decodeIsa95ParameterDataType(decoder));
                }
            }
            return isaParameters;
        }
        public JArray decodeIsa95Parameter(BinaryDecoder decoder)
        {
            JArray isaParameters = new JArray();
            UInt32 length = decoder.ReadUInt32("length");
            for (int i = 0; i < length; i++)
            {
                JObject obj = new JObject();
                obj.Add("ID", decoder.ReadString("ID"));
                obj.Add("Value", decoder.ReadVariant("Value").ToString());
                isaParameters.Add(obj);
            }
            return isaParameters;
        }
        public JObject decodeResultContent(ExtensionObject resultContent) {
            JObject jobj = new JObject();
            BinaryDecoder decoder = new BinaryDecoder((byte[])resultContent.Body, ServiceMessageContext.GlobalContext);
            jobj.Add("ResultKey", decoder.ReadString("ResultKey"));
            jobj.Add("ItemCount", decoder.ReadUInt32("ItemCount"));
            jobj.Add("MeanValue", decoder.ReadDouble("MeanValue"));
            jobj.Add("StandardDeviation", decoder.ReadDouble("StandardDeviation"));
            jobj.Add("CoefficientOfVariation", decoder.ReadDouble("CoefficientOfVariation"));
            jobj.Add("MinValue", decoder.ReadDouble("MinValue"));
            jobj.Add("MaxValue", decoder.ReadDouble("MaxValue"));
            jobj.Add("ConfidenceInterval95", decoder.ReadDouble("ConfidenceInterval95"));
            return jobj;
        }
        public JObject decodeResultMetaData(ExtensionObject resultMetaData) {
            JObject jobj = new JObject();
            BinaryDecoder decoder = new BinaryDecoder((byte[])resultMetaData.Body, ServiceMessageContext.GlobalContext);
            UInt32 encodingMask = decoder.ReadUInt32("EncodingMask");
            bool hasHasTransferableDataOnFile = this.IsBitSet(encodingMask, 0);
            bool hasIsPartial = this.IsBitSet(encodingMask, 1);
            bool hasIsSimulated = this.IsBitSet(encodingMask, 2);
            bool hasResultState = this.IsBitSet(encodingMask, 3);
            bool hasStepId = this.IsBitSet(encodingMask, 4);
            bool hasPartId = this.IsBitSet(encodingMask, 5);
            bool hasExternalRecipeId = this.IsBitSet(encodingMask, 6);
            bool hasInternalRecipeId = this.IsBitSet(encodingMask, 7);
            bool hasProductId = this.IsBitSet(encodingMask, 8);
            bool hasExternalConfigurationId = this.IsBitSet(encodingMask, 9);
            bool hasInternalConfigurationId = this.IsBitSet(encodingMask, 10);
            bool hasJobId = this.IsBitSet(encodingMask, 11);
            bool hasCreationTime = this.IsBitSet(encodingMask, 12);
            bool hasProcessingTimes = this.IsBitSet(encodingMask, 13);
            bool hasResultUri = this.IsBitSet(encodingMask, 14);
            bool hasResultEvaluation = this.IsBitSet(encodingMask, 15);
            bool hasResultEvaluationCode = this.IsBitSet(encodingMask, 16);
            bool hasResultEvaluationDetails = this.IsBitSet(encodingMask, 17);
            bool hasFileFormat = this.IsBitSet(encodingMask, 18);
            jobj.Add("ResultId", decoder.ReadString("ResultId"));
            if (hasHasTransferableDataOnFile)
            {
                jobj.Add("HasTransferableDataOnFile", decoder.ReadBoolean("HasTransferableDataOnFile"));
            }
            if (hasIsPartial)
            {
                jobj.Add("IsPartial", decoder.ReadBoolean("IsPartial"));
            }
            if (hasIsSimulated)
            {
                jobj.Add("IsSimulated", decoder.ReadBoolean("IsSimulated"));
            }
            if (hasResultState)
            {
                jobj.Add("ResultState", decoder.ReadInt32("ResulState"));
            }
            if (hasStepId)
            {
                jobj.Add("StepId", decoder.ReadString("StepId"));
            }
            if (hasPartId)
            {
                jobj.Add("PartId", decoder.ReadString("PartId"));
            }
            if (hasExternalRecipeId)
            {
                jobj.Add("ExternalRecipeId", decoder.ReadString("ExternalRecipeId"));
            }
            if (hasInternalRecipeId)
            {
                jobj.Add("InternalRecipeId", decoder.ReadString("InternalRecipeId"));
            }
            if (hasProductId)
            {
                jobj.Add("ProductId", decoder.ReadString("ProductId"));
            }
            if (hasExternalConfigurationId)
            {
                jobj.Add("ExternalConfigurationId", decoder.ReadString("ExternalConfigurationId"));
            }
            if (hasInternalConfigurationId)
            {
                jobj.Add("InternalConfigurationId", decoder.ReadString("InternalConfigurationId"));
            }
            if (hasJobId)
            {
                jobj.Add("JobId", decoder.ReadString("JobId"));
            }
            if (hasCreationTime)
            {
                jobj.Add("CreationTime", decoder.ReadDateTime("CreationTime"));
            }
            if (hasProcessingTimes)
            {
                jobj.Add("ProcessingTimes", decoder.ReadExtensionObject("ProcessingTimes").ToString());
            }
            if (hasResultUri)
            {
                JArray resultUriArray = new JArray();
                StringCollection resultUris = decoder.ReadStringArray("ResultUri");
                foreach (string resultUri in resultUris)
                {
                    resultUriArray.Add(resultUri);
                }
                jobj.Add("ResultUri", resultUriArray);
            }
            if (hasResultEvaluation)
            {
                jobj.Add("ResultEvaluation",decoder.ReadInt32("ResultEvaluation"));
            }
            if (hasResultEvaluationCode)
            {
                jobj.Add("ResulEvaluationCode", decoder.ReadInt64("EvaluationCode"));
            }
            if (hasResultEvaluationDetails)
            {
                decoder.ReadLocalizedText("EvaluationDetails");
            }
            if (hasFileFormat)
            {
                decoder.ReadStringArray("FileFormat");
            }
            
            return jobj;
        }
       
        public JObject decodeJobOrder() {
            JObject jobj = new JObject();
            return jobj;
        }
        public void decodeResult(JObject jobject, ExtensionObject resultEto) {
            jobject.Add("$TypeDefinition", "nsu=http://opcfoundation.org/UA/Machinery/Result;i=2001");
            BinaryDecoder binaryDecoder = new BinaryDecoder((byte[])resultEto.Body, ServiceMessageContext.GlobalContext);
            ExtensionObject resultMetaDataEto = binaryDecoder.ReadExtensionObject("ResultMetadate");
            if(resultMetaDataEto.Encoding == ExtensionObjectEncoding.Binary) {
                ExpandedNodeId binaryEncodingType = resultMetaDataEto.TypeId;
                if((uint)binaryEncodingType.Identifier == 5005) {
                    jobject.Add("ResultMetaData", this.decodeResultMetaData(resultMetaDataEto));
                }
            }
            VariantCollection vc = binaryDecoder.ReadVariantArray("ResultContent");
            JArray resultContents = new JArray();
            foreach (Variant resultContent in vc)
            {
                if (resultContent.Value is ExtensionObject)
                {
                    ExtensionObject resultContentEto = (ExtensionObject)resultContent.Value;
                    ExtensionObjectEncoding encoding = resultContentEto.Encoding;
                    if (encoding == ExtensionObjectEncoding.Binary)
                    {
                        ExpandedNodeId binaryEncodingType = resultContentEto.TypeId;
                        if ((uint)binaryEncodingType.Identifier == 5063)
                        {
                            resultContents.Add(this.decodeCrimpOutputType(resultContentEto));
                        }
                    }
                }
            }
            jobject.Add("ResultContent", resultContents);
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
                identifier = "s=" + (string)machineId.Identifier;
            }
            return nsuString + nameSpace + ";" + identifier;
        }
        bool IsBitSet(UInt32 value, int pos){
            return ((value >> pos) & 1) != 0;
        }
        private void CreateSubscriptions(List<StructuredNode> onlineMachines)
        {
            string storeAndStartTopic = this.mqttPrefix + "/" + this.clientId + "/" + "WireHarnessMachineType" + "/" + this.getInstanceNsu(onlineMachines[0]) + "/" + "storeAndStart";

            if (this.mqttClient != null)
            {
                this.mqttClient.ApplicationMessageReceivedAsync += e => {
                    MqttApplicationMessage applicationMessage = e.ApplicationMessage;
                    string topic = applicationMessage.Topic;
                    string responseTopic = applicationMessage.ResponseTopic;

                    //responseTopic = "BeispielresponseTopic";
                    Console.WriteLine(string.Format("Received application messagefor topic: {0} .", e.ApplicationMessage.Topic));
                    Console.WriteLine(string.Format("ResponseTopic: {0} .", responseTopic));
                    if (topic == storeAndStartTopic)
                    {
                        byte[] payload = applicationMessage.Payload;
                        String jsonPayload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                        JObject? json = JObject.Parse(jsonPayload);
                        string? jobId = json["JobId"].ToString();
                        string? articleId = json["ArticleId"].ToString();
                        Console.Out.WriteLine(jsonPayload);
                        //this.gateWay.callStoreAndStart(jobId, articleId);
                    }

                    return Task.CompletedTask;
                };

                SubscribeToTopic(storeAndStartTopic);
            }
            else
            {
                Console.Out.WriteLine("m_mqttClient is null.");
            }
            
        }
        private void SubscribeToTopic(string topic)
        {
            var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
               .WithTopicFilter(f => f.WithTopic(topic))
               .Build();

            _ = this.mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None).Result;
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
                    this.publishBadList();
                    this.publishClientOnline();
                    this.publishOnlineMachines();
                    this.publishNode();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}