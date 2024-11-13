using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MQTTnet.Exceptions;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.ComplexTypes;
using Opc.Ua.Schema.Binary;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace UmatiGateway.OPC
{
    public class UmatiGatewayApp
    {
        public List<UmatiGatewayAppListener> umatiGatewayAppListeners = new List<UmatiGatewayAppListener> ();
        public BlockingTransition blockingTransition = new BlockingTransition("","","", false);
        #region Constructors

        public void AddUmatiGatewayAppListener(UmatiGatewayAppListener umatiGatewayAppListener)
        {
            this.umatiGatewayAppListeners.Add(umatiGatewayAppListener);
        }
        public UmatiGatewayApp(ApplicationConfiguration configuration, TextWriter writer, Action<IList, IList> validateResponse)
        {
            m_validateResponse = validateResponse;
            m_output = writer;
            m_configuration = configuration;
            m_configuration.CertificateValidator.CertificateValidation += CertificateValidation;
            this.TypeDictionaries = new TypeDictionaries(this);
            this.MqttProvider = new MqttProvider(this);

        }

        public void StartUp()
        {
            Console.WriteLine("Reading Configuration");
            this.configuration = new ConfigurationReader().ReadConfiguration();
            this.opcServerUrl = this.configuration.opcServerEndpoint;
            this.opcUser = this.configuration.opcUser;
            this.opcPwd = this.configuration.opcPassword;
            this.readExtraLibs = this.configuration.readExtraLibs; 
            this.MqttProvider.connectionString = this.configuration.mqttServerEndpopint;
            this.MqttProvider.user = this.configuration.mqttUser;
            this.MqttProvider.pwd = this.configuration.mqttPassword;
            this.MqttProvider.useGMSResultEncoding = this.configuration.useGMSResultEncoding;
            this.MqttProvider.clientId = this.configuration.mqttClientId;
            this.MqttProvider.mqttPrefix = this.configuration.mqttPrefix;
            this.MqttProvider.singleThreadPolling = this.configuration.singleThreadPolling;
            this.MqttProvider.PollTimer = this.configuration.pollTime;
            this.opcUser = this.configuration.opcUser;
            this.opcPwd = this.configuration.opcPassword;
            foreach (PublishedNode publishedNode in configuration.publishedNodes)
            {
                this.MqttProvider.publishedNodes.Add(publishedNode);
            }
            if (this.configuration.autostart == true)
            {
                Console.WriteLine("Create OPC Connection");
                _ = this.ConnectAsync(this.opcServerUrl).Result;
                Console.WriteLine("Create Mqtt Connection");
                this.MqttProvider.Connect();
            }
        } 
        #endregion

        #region Public Properties
        /// <summary>
        /// Gets the client session.
        /// </summary>
        public Session Session => m_session;
        public TypeDictionaries TypeDictionaries;
        public ComplexTypeSystem ComplexTypeSystem;
        public String opcServerUrl = "";
        public bool readExtraLibs = false;
        /// <summary>
        /// Auto accept untrusted certificates.
        /// </summary>
        public bool AutoAccept { get; set; } = true;
        #endregion

        public String opcUser = "";
        public String opcPwd = "";
        public Tree BrowseTree = new Tree();
        public Configuration configuration = new Configuration();
        public Configuration loadedConfiguration = new Configuration();
        public Subscription? subscription = null;

        public MqttProvider MqttProvider;

        public string getOpcConnectionUrl()
        {
            return this.opcServerUrl;
        }
        public void ConnectMqtt()
        {
            this.MqttProvider.Connect();
        }
        public void setMqttPrefix(string MqttPrefix)
        {
            this.MqttProvider.mqttPrefix = MqttPrefix;
        }
        public string getMqttPrefix()
        {
            return this.MqttProvider.mqttPrefix;
        }
        public void setMqttConnectionType(string MqttConnectionType)
        {
            this.MqttProvider.connectionType = MqttConnectionType;
        }
        public string getMqttConnectionType()
        {
            return this.MqttProvider.connectionType;
        }
        public string getMqttConnectionUrl()
        {
            return this.MqttProvider.connectionString;
        }
        public void setMqttConnectionUrl(string MqttConectionUrl)
        {
            this.MqttProvider.connectionString = MqttConectionUrl;
        }
        public string getMqttConnectionPort()
        {
            return this.MqttProvider.connectionPort;
        }
        public void setMqttConnectionPort(string port)
        {
            this.MqttProvider.connectionPort = port;
        }
        public string getMqttUser()
        {
            return this.MqttProvider.user;
        }
        public void setMqttUser(string MqttUser)
        {
            this.MqttProvider.user = MqttUser;
        }
        public string getMqttPassword()
        {
            return this.MqttProvider.pwd;
        }
        public void setMqttPassword(string pwd)
        {
            this.MqttProvider.pwd = pwd;
        }
        public string getMqttClientId()
        {
            return this.MqttProvider.clientId;
        }
        public void setMqttClientId(string MqttClientId)
        {
            this.MqttProvider.clientId = MqttClientId;
        }
        public List<PublishedNode> getPublishedNodes() {
            return this.MqttProvider.publishedNodes;
        }
        public void publishNode(NodeId nodeId)
        {
            if (nodeId != null)
            {
                object? identifier = nodeId.Identifier;
                string? stringId = nodeId.Identifier.ToString();
                if (stringId != null)
                {
                    PublishedNode publishedNode = new PublishedNode();
                    publishedNode.type = nodeId.IdType.ToString();
                    publishedNode.nodeId = stringId;
                    publishedNode.namespaceUrl = this.GetNamespaceTable().GetString(nodeId.NamespaceIndex);
                    this.MqttProvider.publishedNodes.Add(publishedNode);
                }
            }
        }



        public void DisconnectMqtt()
        {
            this.MqttProvider.Disconnect();
        }

        #region Public Methods
        /// <summary>
        /// Creates a session with the UA server
        /// </summary>
        public async Task<bool> ConnectAsync(string serverUrl)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            if (!this.blockingTransition.isBlocking)
            {
                this.blockingTransition = new BlockingTransition("Connecting OPC", $"Connecting to {serverUrl}","", true);
                this.blockingTransitionChange(this.blockingTransition);
                try
                {
                    if (/*m_session != null && m_session.Connected == true*/ false)
                    {
                        m_output.WriteLine("Session already connected!");
                    }
                    else
                    {
                        m_output.WriteLine("Connecting to... {0}", serverUrl);

                        // Get the endpoint by connecting to server's discovery endpoint.
                        // Try to find the first endopint with security.
                        EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(m_configuration, serverUrl, false);
                        EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(m_configuration);
                        ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
                        UserIdentity userIdentity = new UserIdentity();
                        if (!String.IsNullOrWhiteSpace(this.opcUser))
                        {
                            userIdentity = new UserIdentity(this.opcUser, this.opcPwd);
                        }

                        // Create the session
                        Session session = await Session.Create(
                            m_configuration,
                            endpoint,
                            false,
                            false,
                            m_configuration.ApplicationName,
                            30 * 60 * 1000,
                            userIdentity,
                            //null,
                            null
                        );

                        // Assign the created session
                        if (session != null && session.Connected)
                        {
                            m_session = session;
                        }

                        // Session created successfully.
                        m_output.WriteLine("New Session Created with SessionName = {0}", m_session.SessionName);
                    }
                    this.TypeDictionaries = new TypeDictionaries(this);
                    this.TypeDictionaries.ReadExtraLibs = this.readExtraLibs;
                    this.blockingTransition.Message = "Read Type Dictionaries";
                    this.blockingTransition.Detail = "Read Binaries";
                    this.blockingTransitionChange(this.blockingTransition);
                    //this.TypeDictionaries.ReadTypeDictionary(false);
                    Console.WriteLine("Read Binaries");
                    this.TypeDictionaries.ReadOpcBinary();
                    this.blockingTransition.Detail = "Read DataTypes";
                    this.blockingTransitionChange(this.blockingTransition);
                    Console.WriteLine("Read DataTypes");
                    this.TypeDictionaries.ReadDataTypes();
                    this.blockingTransition.Detail = "Read EventTypes";
                    this.blockingTransitionChange(this.blockingTransition);
                    Console.WriteLine("Read EventTypes");
                    this.TypeDictionaries.ReadEventTypes();
                    this.blockingTransition.Detail = "Read InterfaceTypes";
                    this.blockingTransitionChange(this.blockingTransition);
                    Console.WriteLine("Read InterfaceTypes");
                    this.TypeDictionaries.ReadInterfaceTypes();
                    this.blockingTransition.Detail = "Read ObjectTypes";
                    this.blockingTransitionChange(this.blockingTransition);
                    Console.WriteLine("Read ObjectTypes");
                    this.TypeDictionaries.ReadObjectTypes();
                    this.blockingTransition.Detail = "Read ReferenceTypes";
                    this.blockingTransitionChange(this.blockingTransition);
                    Console.WriteLine("Read ReferenceTypes");
                    this.TypeDictionaries.ReadReferenceTypes();
                    this.blockingTransition.Detail = "Read VariableTypes";
                    this.blockingTransitionChange(this.blockingTransition);
                    Console.WriteLine("Read VariableTypes");
                    this.TypeDictionaries.ReadVariableTypes();
                    //this.ComplexTypeSystem = new ComplexTypeSystem(this.Session);
                    //this.ComplexTypeSystem.Load().Wait();

                    return true;
                }
                catch (Exception ex)
                {
                    // Log Error
                    m_output.WriteLine("Create Session Error : {0}", ex.Message);
                    return false;
                }
                finally
                {
                    this.blockingTransition = new BlockingTransition();
                }
            } else
            {
                Console.WriteLine("Allready trying to Connect to OPC Server");
                return false;
            }
        }
        private void CertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
        {
            bool certificateAccepted = false;

            // ****
            // Implement a custom logic to decide if the certificate should be
            // accepted or not and set certificateAccepted flag accordingly.
            // The certificate can be retrieved from the e.Certificate field
            // ***

            ServiceResult error = e.Error;
            m_output.WriteLine(error);
            if (error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted && AutoAccept)
            {
                certificateAccepted = true;
            }

            if (certificateAccepted)
            {
                m_output.WriteLine("Untrusted Certificate accepted. Subject = {0}", e.Certificate.Subject);
                e.Accept = true;
            }
            else
            {
                m_output.WriteLine("Untrusted Certificate rejected. Subject = {0}", e.Certificate.Subject);
            }
        }

        /// <summary>
        /// Disconnects the session.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                //NodeId nodeId = new NodeId((uint)5, (ushort)8);
                //this.decodeComplexType(nodeId);
                if (m_session != null)
                {
                    m_output.WriteLine("Disconnecting...");

                    m_session.Close();
                    m_session.Dispose();
                    m_session = null;

                    // Log Session Disconnected event
                    m_output.WriteLine("Session Disconnected.");
                }
                else
                {
                    m_output.WriteLine("Session not created!");
                }
            }
            catch (Exception ex)
            {
                // Log Error
                m_output.WriteLine($"Disconnect Error : {ex.Message}");
            }
        }

        public Node? ReadNode(NodeId nodeId)
        {
            Node? node = null;
            if (m_session != null && m_session.Connected){
                try {
                    node = m_session.ReadNode(nodeId);
                } catch (Exception e) {
                    Console.Out.WriteLine(e.Message + " NodeId:" + nodeId);
                }
            }
            return node;
        }
        
        public List<NodeId> BrowseLocalNodeIds(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes)
        {
            List<NodeId> nodeList = new List<NodeId>();
            BrowseResultCollection browseResults = BrowseNode(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes);
            foreach (BrowseResult browseResult in browseResults)
            {
                ReferenceDescriptionCollection references = browseResult.References;
                foreach (ReferenceDescription reference in references)
                {
                    nodeList.Add(new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex));
                }
            }
            return nodeList;
        }
        public NodeId BrowseFirstLocalNodeIdWithType(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId typeDefinition)
        {
            NodeId nodeId = null;
            BrowseResultCollection browseResults = BrowseNode(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes);
            foreach (BrowseResult browseResult in browseResults)
            {
                ReferenceDescriptionCollection references = browseResult.References;
                foreach (ReferenceDescription reference in references)
                {
                    nodeId = new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex);
                }
            }
            return nodeId;
        }
        public NodeId BrowseFirstLocalNodeIdWithTypeDefinition(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId expectedTypeDefinition)
        {
            NodeId filteredNodeId = null;
            List<NodeId> nodeIds = BrowseLocalNodeIds(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes);
            foreach(NodeId nodeId in nodeIds)
            {
                NodeId typeDefinition = getTypeDefinition(nodeId);
                if(typeDefinition == expectedTypeDefinition)
                {
                    filteredNodeId = nodeId;
                    break;
                }
            }
            return filteredNodeId;
        }
        public NamespaceTable GetNamespaceTable()
        {
            DataValue dv = m_session.ReadValue(VariableIds.Server_NamespaceArray);
            String[] namespaces = (String[])dv.Value;
            return new NamespaceTable(namespaces);
        }
        public List<NodeId> BrowseLocalNodeIdsWithTypeDefinition(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId expectedTypeDefinition)
        {
            List<NodeId> filteredNodeIds = new List<NodeId>();
            List<NodeId> nodeIds = BrowseLocalNodeIds(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes);
            foreach (NodeId nodeId in nodeIds)
            {
                NodeId typeDefinition = getTypeDefinition(nodeId);
                if (typeDefinition == expectedTypeDefinition)
                {
                    filteredNodeIds.Add(nodeId);
                }
            }
            return filteredNodeIds;
        }
        public List<NodeId> BrowseLocalNodeIds(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId expectedTypeDefinition)
        {
            List<NodeId> filteredNodeIds = new List<NodeId>();
            List<NodeId> nodeIds = BrowseLocalNodeIds(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes);
            return nodeIds;
        }
        public NodeId getTypeDefinition(NodeId nodeId)
        {
            NodeId typeDefinition = null;
            BrowseResultCollection browseResults = BrowseNode(nodeId, BrowseDirection.Forward, (uint)NodeClass.ObjectType | (uint) NodeClass.VariableType, ReferenceTypes.HasTypeDefinition, false);
            foreach (BrowseResult browseResult in browseResults)
            {
                ReferenceDescriptionCollection references = browseResult.References;
                foreach (ReferenceDescription reference in references)
                {
                    typeDefinition = new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex);
                    break;
                }
            }
            return typeDefinition;
        }
        public NodeId BrowseLocalNodeId(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes)
        {

            List<NodeId> nodeIds = BrowseLocalNodeIds(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes);
            foreach (NodeId nodeId in nodeIds)
            {
                return nodeId;
            }
            return null;
        }
        public List<Node>BrowseNodeList(Node rootNode, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes)
        {
            List<Node> node = new List<Node>();
            return node;
        }
        public DataValue ReadValue(NodeId nodeId)
        {
            if (!checkSession())
            {
                return null;
            }
            else
            {
                return m_session.ReadValue(nodeId);
            }
        }
        public BrowseResultCollection BrowseNode(NodeId nodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, Boolean includeSubTypes)
        {
            BrowseDescription nodeToBrowse = new BrowseDescription();
            nodeToBrowse.NodeId = nodeId;
            nodeToBrowse.BrowseDirection = browseDirection;
            nodeToBrowse.NodeClassMask = nodeClassMask;
            nodeToBrowse.ReferenceTypeId = referenceTypeIds;
            nodeToBrowse.IncludeSubtypes = includeSubTypes;

            BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
            nodesToBrowse.Add(nodeToBrowse);
            m_session.Browse(null, null, 10000, nodesToBrowse, out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
            return results;
        }
        public BrowseResultCollection BrowseNode(NodeId nodeId)
        {
        
            BrowseDescription nodeToBrowse = new BrowseDescription();
            nodeToBrowse.NodeId = nodeId;
            nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
            nodeToBrowse.NodeClassMask = (int)NodeClass.Object | (int)NodeClass.Variable;
            nodeToBrowse.ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences;
            nodeToBrowse.IncludeSubtypes = true;

            BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
            nodesToBrowse.Add(nodeToBrowse);
            m_session.Browse(null, null, 100, nodesToBrowse,out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
            return results;
        }
        /// <summary>
        /// Read a list of nodes from Server
        /// </summary>
        public void ReadNodes()
        {
            if (m_session == null || m_session.Connected == false)
            {
                m_output.WriteLine("Session not connected!");
                return;
            }

            try
            {
                #region Read a node by calling the Read Service

                // build a list of nodes to be read
                ReadValueIdCollection nodesToRead = new ReadValueIdCollection()
                {
                    // Value of ServerStatus
                    new ReadValueId() { NodeId = Variables.Server_ServerStatus, AttributeId = Attributes.Value },
                    // BrowseName of ServerStatus_StartTime
                    new ReadValueId() { NodeId = Variables.Server_ServerStatus_StartTime, AttributeId = Attributes.BrowseName },
                    // Value of ServerStatus_StartTime
                    new ReadValueId() { NodeId = Variables.Server_ServerStatus_StartTime, AttributeId = Attributes.Value }
                };

                // Read the node attributes
                m_output.WriteLine("Reading nodes...");

                // Call Read Service
                m_session.Read(
                    null,
                    0,
                    TimestampsToReturn.Both,
                    nodesToRead,
                    out DataValueCollection resultsValues,
                    out DiagnosticInfoCollection diagnosticInfos);

                // Validate the results
                m_validateResponse(resultsValues, nodesToRead);

                // Display the results.
                foreach (DataValue result in resultsValues)
                {
                    m_output.WriteLine("Read Value = {0} , StatusCode = {1}", result.Value, result.StatusCode);
                }
                #endregion

                #region Read the Value attribute of a node by calling the Session.ReadValue method
                // Read Server NamespaceArray
                m_output.WriteLine("Reading Value of NamespaceArray node...");
                DataValue namespaceArray = m_session.ReadValue(Variables.Server_NamespaceArray);
                // Display the result
                m_output.WriteLine($"NamespaceArray Value = {namespaceArray}");
                #endregion
            }
            catch (Exception ex)
            {
                // Log Error
                m_output.WriteLine($"Read Nodes Error : {ex.Message}.");
            }
        }

        /// <summary>
        /// Write a list of nodes to the Server
        /// </summary>
        public void WriteNodes()
        {
            if (m_session == null || m_session.Connected == false)
            {
                m_output.WriteLine("Session not connected!");
                return;
            }

            try
            {
                // Write the configured nodes
                WriteValueCollection nodesToWrite = new WriteValueCollection();

                // Int32 Node - Objects\CTT\Scalar\Scalar_Static\Int32
                WriteValue intWriteVal = new WriteValue();
                intWriteVal.NodeId = new NodeId("ns=2;s=Scalar_Static_Int32");
                intWriteVal.AttributeId = Attributes.Value;
                intWriteVal.Value = new DataValue();
                intWriteVal.Value.Value = (int)100;
                nodesToWrite.Add(intWriteVal);

                // Float Node - Objects\CTT\Scalar\Scalar_Static\Float
                WriteValue floatWriteVal = new WriteValue();
                floatWriteVal.NodeId = new NodeId("ns=2;s=Scalar_Static_Float");
                floatWriteVal.AttributeId = Attributes.Value;
                floatWriteVal.Value = new DataValue();
                floatWriteVal.Value.Value = (float)100.5;
                nodesToWrite.Add(floatWriteVal);

                // String Node - Objects\CTT\Scalar\Scalar_Static\String
                WriteValue stringWriteVal = new WriteValue();
                stringWriteVal.NodeId = new NodeId("ns=2;s=Scalar_Static_String");
                stringWriteVal.AttributeId = Attributes.Value;
                stringWriteVal.Value = new DataValue();
                stringWriteVal.Value.Value = "String Test";
                nodesToWrite.Add(stringWriteVal);

                // Write the node attributes
                StatusCodeCollection results = null;
                DiagnosticInfoCollection diagnosticInfos;
                m_output.WriteLine("Writing nodes...");

                // Call Write Service
                m_session.Write(null,
                                nodesToWrite,
                                out results,
                                out diagnosticInfos);

                // Validate the response
                m_validateResponse(results, nodesToWrite);

                // Display the results.
                m_output.WriteLine("Write Results :");

                foreach (StatusCode writeResult in results)
                {
                    m_output.WriteLine("     {0}", writeResult);
                }
            }
            catch (Exception ex)
            {
                // Log Error
                m_output.WriteLine($"Write Nodes Error : {ex.Message}.");
            }
        }

        /// <summary>
        /// Browse Server nodes
        /// </summary>
        public void Browse()
        {
            if (m_session == null || m_session.Connected == false)
            {
                m_output.WriteLine("Session not connected!");
                return;
            }

            try
            {
                // Create a Browser object
                Browser browser = new Browser(m_session);

                // Set browse parameters
                browser.BrowseDirection = BrowseDirection.Forward;
                browser.NodeClassMask = (int)NodeClass.Object | (int)NodeClass.Variable;
                browser.ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences;

                NodeId nodeToBrowse = ObjectIds.Server;

                // Call Browse service
                m_output.WriteLine("Browsing {0} node...", nodeToBrowse);
                ReferenceDescriptionCollection browseResults = browser.Browse(nodeToBrowse);

                // Display the results
                m_output.WriteLine("Browse returned {0} results:", browseResults.Count);

                foreach (ReferenceDescription result in browseResults)
                {
                    m_output.WriteLine("     DisplayName = {0}, NodeClass = {1}", result.DisplayName.Text, result.NodeClass);
                }
            }
            catch (Exception ex)
            {
                // Log Error
                m_output.WriteLine($"Browse Error : {ex.Message}.");
            }
        }
        public void BrowseRootNode()
        {
            if (!this.BrowseTree.Initialized)
            {
                Node? node = this.ReadNode(ObjectIds.RootFolder);
                if (node != null)
                {
                    NodeData nodeData = new NodeData(node);
                    TreeNode treeNode = new TreeNode(nodeData);
                    this.BrowseTree.children.AddLast(treeNode);
                    this.BrowseTree.uids.Add(treeNode.uid, treeNode);
                    this.BrowseTree.Initialized = true;
                    if(node.NodeClass == NodeClass.Variable)
                    {
                        nodeData.DataValue = this.decodeComplexType(node.NodeId);
                    }
                }
            }

        }
        private JObject decodeDataValue(DataValue dataValue)
        {
            JObject jObject = new JObject();
            if(dataValue.Value is ExtensionObject extensionObject)
            {

            } else if(dataValue.Value is String stringValue)
            {

            } else if(dataValue.Value is LocalizedText localizedTextVale)
            {

            } else
            {
                jObject.Add("Error", "Unknown DataValue");
            }
            return jObject;
        }
        private JObject decodeComplexType(NodeId nodeId)
        {
            JObject jObject = new JObject();
            
            Node? node = this.ReadNode(nodeId);
            DataValue dv = this.ReadValue(nodeId);
            if (dv.Value is ExtensionObject eto) {
                ExpandedNodeId expandedNodeId = this.getIndexedNodeId(eto.TypeId);
                NodeIdDictionary<DataTypeDefinition> complexType = this.ComplexTypeSystem.GetDataTypeDefinitionsForDataType(expandedNodeId);
                if (complexType != null)
                {
                    foreach(NodeId complexNode in complexType.Keys)
                    {
                        complexType.TryGetValue(complexNode, out DataTypeDefinition? complexValue);
                        if (complexValue != null)
                        {
                            jObject.Add(complexNode.ToString(), complexValue.ToString());
                        }
                    }
                    Console.WriteLine("Gelesener komplexer Typ: " + complexType.ToString());
                }
                else
                {
                    Console.WriteLine("Dekodierung des komplexen Typs fehlgeschlagen.");
                }
            }
            return jObject;
        }
        public ExpandedNodeId getIndexedNodeId(ExpandedNodeId expandedNodeId) {
            if(expandedNodeId.IsAbsolute)
            {
                return new ExpandedNodeId(expandedNodeId.Identifier, (ushort)(this.GetNamespaceTable().GetIndex(expandedNodeId.NamespaceUri)), expandedNodeId.NamespaceUri, expandedNodeId.ServerIndex);
            } else
            {
                return expandedNodeId;
            }
        }
        public void BrowseSelectedTreeNode(TreeNode TreeNode)
        {

            BrowseDescription nodeToBrowse = new BrowseDescription();
            nodeToBrowse.NodeId = TreeNode.NodeData.node.NodeId;
            nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
            nodeToBrowse.NodeClassMask = (int)NodeClass.Object | (int)NodeClass.Variable | (int)NodeClass.Method | (int)NodeClass.ObjectType | (int)NodeClass.VariableType | (int)NodeClass.DataType | (int)NodeClass.ReferenceType; ;
            nodeToBrowse.ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences;
            nodeToBrowse.IncludeSubtypes = true;
            nodeToBrowse.ResultMask = (int)BrowseResultMask.All ;

            BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
            nodesToBrowse.Add(nodeToBrowse);
            m_session.Browse(null, null, 100, nodesToBrowse, out BrowseResultCollection browseResults, out DiagnosticInfoCollection diagnosticInfos);
            foreach (BrowseResult browseResult in browseResults)
            {
                ReferenceDescriptionCollection references = browseResult.References;
                foreach (ReferenceDescription reference in references)
                {
                    NodeId nodeId = new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex);
                    Node? node = this.ReadNode(nodeId);
                    if (node != null)
                    {
                        NodeData nodeData = new NodeData(node);
                        TreeNode treeNode = new TreeNode(nodeData);
                        TreeNode.children.AddLast(treeNode);
                        this.BrowseTree.uids.Add(treeNode.uid, treeNode);
                        if (node.NodeClass == NodeClass.Variable)
                        {
                            nodeData.DataValue = this.decodeComplexType(node.NodeId);
                        }
                    }
                }
            }
        }
        public IList<object> CallMethod(NodeId objectId, NodeId methodId, object[] inputArguments)
        {
            IList<object> outputArguments = null;
            outputArguments = m_session.Call(objectId, methodId, inputArguments);
            return outputArguments;
        }
        /// <summary>
        /// Call UA method
        /// </summary>
        public void CallMethod()
        {
            if (m_session == null || m_session.Connected == false)
            {
                m_output.WriteLine("Session not connected!");
                return;
            }

            try
            {
                // Define the UA Method to call
                // Parent node - Objects\CTT\Methods
                // Method node - Objects\CTT\Methods\Add
                NodeId objectId = new NodeId("ns=2;s=Methods");
                NodeId methodId = new NodeId("ns=2;s=Methods_Add");

                // Define the method parameters
                // Input argument requires a Float and an UInt32 value
                object[] inputArguments = new object[] { (float)10.5, (uint)10 };
                IList<object> outputArguments = null;

                // Invoke Call service
                m_output.WriteLine("Calling UAMethod for node {0} ...", methodId);
                outputArguments = m_session.Call(objectId, methodId, inputArguments);

                // Display results
                m_output.WriteLine("Method call returned {0} output argument(s):", outputArguments.Count);

                foreach (var outputArgument in outputArguments)
                {
                    m_output.WriteLine("     OutputValue = {0}", outputArgument.ToString());
                }
            }
            catch (Exception ex)
            {
                m_output.WriteLine("Method call error: {0}", ex.Message);
            }
        }
    /// <summary>
        /// Create Subscription and MonitoredItems for DataChanges
        /// </summary>
        public uint SubscribeToDataChanges(NodeId nodeId, MonitoredItemNotificationEventHandler eventHandler)
        {
            uint subscriptionId = 0;
            if (m_session == null || m_session.Connected == false)
            {
                m_output.WriteLine("Session not connected!");
                return 0;
            }

            try
            {
                // Create a subscription for receiving data change notifications

                // Define Subscription parameters
                if (subscription == null)
                {
                    subscription = new Subscription(m_session.DefaultSubscription);
                    subscription.DisplayName = "Subscription for NodeIds";
                    subscription.PublishingEnabled = true;
                    subscription.PublishingInterval = 1000;
                    m_session.AddSubscription(subscription);
                    // Create the subscription on Server side
                    subscription.Create();
                    subscriptionId = subscription.Id;
                }

                m_output.WriteLine("New Subscription created with SubscriptionId = {0}.", subscription.Id);

                // Create MonitoredItems for data changes (Reference Server)

                MonitoredItem intMonitoredItem = new MonitoredItem(subscription.DefaultItem);
                // Int32 Node - Objects\CTT\Scalar\Simulation\Int32
                intMonitoredItem.StartNodeId = nodeId;
                intMonitoredItem.AttributeId = Attributes.Value;
                intMonitoredItem.DisplayName = "Subscription";
                intMonitoredItem.SamplingInterval = 1000;
                intMonitoredItem.Notification += eventHandler;

                subscription.AddItem(intMonitoredItem);

                // Create the monitored items on Server side
                subscription.ApplyChanges();
                m_output.WriteLine("MonitoredItems created for SubscriptionId = {0} with NodeId {1}.", subscription.Id, nodeId);
            }
            catch (Exception ex)
            {
                m_output.WriteLine("Subscribe error: {0}", ex.Message);
            }
            return subscriptionId;
        }
        #endregion

        #region Private Methods

        private bool checkSession()
        {
            if (m_session == null || m_session.Connected == false)
            {
                m_output.WriteLine("Session not connected!");
                return false;
            } else
            {
                return true;
            }
        }
        #endregion

        #region Private Fields
        private ApplicationConfiguration m_configuration;
        private Session m_session;
        private readonly TextWriter m_output;
        private readonly Action<IList, IList> m_validateResponse;
        #endregion
        private void blockingTransitionChange(BlockingTransition blockingTransition)
        {
            foreach(UmatiGatewayAppListener umatiGatewayAppListener in this.umatiGatewayAppListeners)
            {
                try
                {
                    umatiGatewayAppListener.blockingTransitionChanged(blockingTransition);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to notify Listener: {ex.StackTrace}");
                }
            }
        }
    }
}