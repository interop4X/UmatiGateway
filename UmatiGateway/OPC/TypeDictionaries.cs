using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Configuration;
using System.Text;
using System.Xml;
//ToDo make deep copies in the accessor methods
namespace UmatiGateway.OPC
{
    public class TypeDictionaries
    {
        public Dictionary<GeneratedDataTypeDefinition, GeneratedDataClass> generatedDataTypes = new Dictionary<GeneratedDataTypeDefinition, GeneratedDataClass>(new DataClassComparer());
        private Client client;
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private List<string> errorMemmory = new List<string>();
        private Dictionary<NodeId, Node> opcBinary = new Dictionary<NodeId, Node>(new NodeIdComparer());
        private Dictionary<NodeId, Node> dataTypes = new Dictionary<NodeId, Node>();
        private Dictionary<NodeId, Node> eventTypes = new Dictionary<NodeId, Node>();
        private Dictionary<NodeId, Node> interfaceTypes = new Dictionary<NodeId, Node>();
        private Dictionary<NodeId, Node> objectTypes = new Dictionary<NodeId, Node>();
        private Dictionary<NodeId, Node> referenceTypes = new Dictionary<NodeId, Node>();
        private Dictionary<NodeId, Node> variableTypes = new Dictionary<NodeId, Node>();
        private Dictionary<NodeId, Node> xmlSchema = new Dictionary<NodeId, Node>();
        public List<DataTypeDefinition> dataTypeDefinition = new List<DataTypeDefinition>();

        public TypeDictionaries(Client Client)
        {
            this.client = Client;
        }
        public void ReadTypeDictionary()
        {
            this.ReadOpcBinary();
            this.ReadDataTypes();
            this.ReadEventTypes();
            this.ReadInterfaceTypes();
            this.ReadObjectTypes();
            this.ReadReferenceTypes();
            this.ReadVariableTypes();
            Console.WriteLine("TypeDictionary Read Finished");
        }
        private void ReadOpcBinary()
        {
            List<NodeId> binaryTypeDictionaries = new List<NodeId>();
            binaryTypeDictionaries = this.client.BrowseLocalNodeIdsWithTypeDefinition(ObjectIds.OPCBinarySchema_TypeSystem, BrowseDirection.Forward, (uint)NodeClass.Variable, ReferenceTypeIds.HasComponent, false, VariableTypeIds.DataTypeDictionaryType);
            foreach (NodeId binaryTypeDictionary in binaryTypeDictionaries)
            {
                DataValue dv = this.client.ReadValue(binaryTypeDictionary);
                string xmlString = Encoding.UTF8.GetString((byte[])dv.Value);
                //Console.WriteLine(xmlString);
                this.generateDataClasses(xmlString);
            };
            List<NodeId> opcBinaryNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(ObjectIds.OPCBinarySchema_TypeSystem, NodeClass.Variable, opcBinaryNodeIds, ReferenceTypeIds.HasComponent);
            this.ReadAndAppendTypeNodeIds(ObjectIds.OPCBinarySchema_TypeSystem, NodeClass.Variable, opcBinaryNodeIds, ReferenceTypeIds.HasProperty);
            Dictionary<NodeId, Node> opcBinaryTypes = new Dictionary<NodeId, Node>();
            opcBinaryNodeIds = opcBinaryNodeIds.Distinct().ToList();
            foreach (NodeId opcBinaryNodeId in opcBinaryNodeIds)
            {
                Node? node = this.client.ReadNode(opcBinaryNodeId);
                if (node != null)
                {
                    opcBinaryTypes.Add(opcBinaryNodeId, node);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", opcBinaryNodeId);
                }
            }
            this.SetOpcBinaryTypes(opcBinaryTypes);
        }
        private void ReadAndAppendTypeNodeIds(NodeId nodeId, NodeClass nodeClass, List<NodeId> nodeIds)
        {
            nodeIds.Add(nodeId);
            List<NodeId> subTypeNodeIds = this.client.BrowseLocalNodeIds(nodeId, BrowseDirection.Forward, (uint)nodeClass, ReferenceTypeIds.HasSubtype, true);
            foreach (NodeId subTypeNodeId in subTypeNodeIds)
            {
                this.ReadAndAppendTypeNodeIds(subTypeNodeId, nodeClass, nodeIds);
            }
        }
        private void ReadAndAppendTypeNodeIds(NodeId nodeId, NodeClass nodeClass, List<NodeId> nodeIds, NodeId referenceTypeId)
        {
            nodeIds.Add(nodeId);
            List<NodeId> subTypeNodeIds = this.client.BrowseLocalNodeIds(nodeId, BrowseDirection.Forward, (uint)nodeClass, referenceTypeId, true);
            foreach (NodeId subTypeNodeId in subTypeNodeIds)
            {
                this.ReadAndAppendTypeNodeIds(subTypeNodeId, nodeClass, nodeIds, referenceTypeId);
            }
        }
        private void generateDataClasses(string xmlString)
        {
            Console.Out.WriteLine(xmlString);
            XmlTextReader reader = new XmlTextReader(new System.IO.StringReader(xmlString));
            GeneratedStructure generatedStructure = new GeneratedStructure();
            GeneratedEnumeratedType generatedEnumeratedType = new GeneratedEnumeratedType();
            GeneratedOpaqueType generatedOpaqueType = new GeneratedOpaqueType();
            //Structure or enumerated Type
            GeneratedComplexTypes generatedComplexType = GeneratedComplexTypes.StructuredType;
            string? Name = null;
            string? BaseType = null;
            string documentation = "";
            string? targetNamespace = null;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        string nodeElement = reader.Name;
                        switch (nodeElement)
                        {
                            case ("opc:StructuredType"):
                                generatedComplexType = GeneratedComplexTypes.StructuredType;
                                generatedStructure = new GeneratedStructure();
                                Name = reader.GetAttribute("Name");
                                BaseType = reader.GetAttribute("BaseType");
                                generatedStructure.BaseType = BaseType;
                                if (Name != null)
                                {
                                    generatedStructure.Name = Name;
                                }
                                else
                                {
                                    this.errorMemmory.Add("The Name of the structure is null");
                                }
                                break;
                            case ("opc:Documentation"):
                                documentation = reader.ReadInnerXml();
                                if (generatedComplexType == GeneratedComplexTypes.StructuredType)
                                {
                                    generatedStructure.Documentation = documentation;
                                }
                                else if (generatedComplexType == GeneratedComplexTypes.EnumeratedType)
                                {
                                    generatedEnumeratedType.Documentation = documentation;
                                }
                                else if (generatedComplexType == GeneratedComplexTypes.OpaqueType)
                                {

                                }
                                break;
                            case ("opc:Field"):
                                GeneratedField generatedField = new GeneratedField();
                                string? typeName = reader.GetAttribute("TypeName");
                                if (typeName != null)
                                {
                                    generatedField.TypeName = typeName;
                                }
                                else
                                {
                                    this.errorMemmory.Add("The TypeName of the Field is null");
                                }
                                string? fieldname = reader.GetAttribute("Name");
                                if (fieldname != null)
                                {
                                    generatedField.Name = fieldname;
                                }
                                else
                                {
                                    this.errorMemmory.Add("The Name of the Field is null");
                                }
                                string? lengthField = reader.GetAttribute("LengthField");
                                if(lengthField != null)
                                {
                                    generatedField.IsLengthField = true;
                                    generatedField.LengthField = lengthField;
                                }
                                string? length = reader.GetAttribute("Length");
                                if(length != null)
                                {
                                    generatedField.HasLength = true;
                                    generatedField.Length = UInt32.Parse(length);
                                }
                                string? switchfield = reader.GetAttribute("SwitchField");
                                if(switchfield!= null)
                                {
                                    generatedField.IsSwitchField = true;
                                }
                                if (generatedComplexType == GeneratedComplexTypes.StructuredType)
                                {
                                    generatedStructure.fields.Add(generatedField);
                                }
                                else
                                {
                                    this.errorMemmory.Add("Trying to add a field to a non Structure.");
                                }
                                break;
                            case ("opc:EnumeratedType"):
                                generatedComplexType = GeneratedComplexTypes.EnumeratedType;
                                generatedEnumeratedType = new GeneratedEnumeratedType();
                                Name = reader.GetAttribute("Name");
                                if (Name != null)
                                {
                                    generatedEnumeratedType.Name = Name;
                                }
                                else
                                {
                                    this.errorMemmory.Add("The Name of the structure is null");
                                }
                                break;
                            case ("opc:EnumeratedValue"):
                                break;
                            case ("opc:OpaqueType"):
                                generatedComplexType = GeneratedComplexTypes.OpaqueType;
                                break;
                            case ("opc:TypeDictionary"):
                                targetNamespace = reader.GetAttribute("TargetNamespace");
                                if (targetNamespace == null)
                                {
                                    this.errorMemmory.Add("The TargetNameSpace for the Typedictionary is null.");
                                }
                                break;
                            case ("opc:Import"):
                                break;
                            default:
                                Console.WriteLine("UnknownType: -> ##################" + "###" + reader.Name + "###");
                                break;
                        }
                        //Console.WriteLine("###" + reader.Name + "###");

                        break;
                    case XmlNodeType.Text:
                        break;
                    case XmlNodeType.EndElement:
                        nodeElement = reader.Name;
                        switch (nodeElement)
                        {
                            case ("opc:StructuredType"):
                                if (targetNamespace != null)
                                {
                                    this.generatedDataTypes.Add(new GeneratedDataTypeDefinition(targetNamespace, generatedStructure.Name), generatedStructure);
                                }
                                break;
                        }
                        break;
                }
            }
            foreach (string error in this.errorMemmory)
            {
                logger.Error(error);
            }
        }
        private void ReadDataTypes()
        {
            List<NodeId> dataTypeNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(DataTypeIds.BaseDataType, NodeClass.DataType, dataTypeNodeIds);
            Dictionary<NodeId, Node> dataTypes = new Dictionary<NodeId, Node>();
            foreach (NodeId dataTypeNodeId in dataTypeNodeIds)
            {
                Node? node = this.client.ReadNode(dataTypeNodeId);
                if (node != null)
                {
                    dataTypes.Add(dataTypeNodeId, node);
                    Console.WriteLine(dataTypeNodeId);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", dataTypeNodeId);
                }
            }
            this.SetDataTypes(dataTypes);
        }
        private void ReadEventTypes()
        {
            List<NodeId> eventTypeNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(ObjectTypeIds.BaseEventType, NodeClass.ObjectType, eventTypeNodeIds);
            Dictionary<NodeId, Node> eventTypes = new Dictionary<NodeId, Node>();
            foreach (NodeId eventTypeNodeId in eventTypeNodeIds)
            {
                Node? node = this.client.ReadNode(eventTypeNodeId);
                if (node != null)
                {
                    eventTypes.Add(eventTypeNodeId, node);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", eventTypeNodeId);
                }
            }
            this.SetEventTypes(eventTypes);
        }
        private void ReadInterfaceTypes()
        {
            List<NodeId> interfaceTypeNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(ObjectTypeIds.BaseInterfaceType, NodeClass.ObjectType, interfaceTypeNodeIds);
            Dictionary<NodeId, Node> interfaceTypes = new Dictionary<NodeId, Node>();
            foreach (NodeId interfaceTypeNodeId in interfaceTypeNodeIds)
            {
                Node? node = this.client.ReadNode(interfaceTypeNodeId);
                if (node != null)
                {
                    interfaceTypes.Add(interfaceTypeNodeId, node);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", interfaceTypeNodeId);
                }
            }
            this.SetInterfaceTypes(interfaceTypes);
        }

        private void ReadObjectTypes()
        {
            List<NodeId> objectTypeNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(ObjectTypeIds.BaseObjectType, NodeClass.ObjectType, objectTypeNodeIds);
            Dictionary<NodeId, Node> objectTypes = new Dictionary<NodeId, Node>();
            foreach (NodeId objectTypeNodeId in objectTypeNodeIds)
            {
                Node? node = this.client.ReadNode(objectTypeNodeId);
                if (node != null)
                {
                    objectTypes.Add(objectTypeNodeId, node);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", objectTypeNodeId);
                }
            }
            this.SetObjectTypes(objectTypes);

        }
        private void ReadReferenceTypes()
        {
            List<NodeId> referenceTypeNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(ReferenceTypeIds.References, NodeClass.ReferenceType, referenceTypeNodeIds);
            Dictionary<NodeId, Node> referenceTypes = new Dictionary<NodeId, Node>();
            foreach (NodeId referenceTypeNodeId in referenceTypeNodeIds)
            {
                Node? node = this.client.ReadNode(referenceTypeNodeId);
                if (node != null)
                {
                    referenceTypes.Add(referenceTypeNodeId, node);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", referenceTypeNodeId);
                }
            }
            this.SetReferenceTypes(referenceTypes);
        }

        private void ReadVariableTypes()
        {
            List<NodeId> variableTypeNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(VariableTypeIds.BaseVariableType, NodeClass.VariableType, variableTypeNodeIds);
            Dictionary<NodeId, Node> variableTypes = new Dictionary<NodeId, Node>();
            foreach (NodeId variableTypeNodeId in variableTypeNodeIds)
            {
                Node? node = this.client.ReadNode(variableTypeNodeId);
                if (node != null)
                {
                    variableTypes.Add(variableTypeNodeId, node);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", variableTypeNodeId);
                }
            }
            this.SetVariableTypes(variableTypes);
        }
        public void SetOpcBinaryTypes(Dictionary<NodeId, Node> opcBinary)
        {
            this.opcBinary.Clear();
            if (opcBinary != null)
            {
                this.opcBinary = opcBinary;
            }
        }
        public Dictionary<NodeId, Node> GetOpcBinary()
        {
            return this.opcBinary;
        }
        public void SetDataTypes(Dictionary<NodeId, Node> dataTypes)
        {
            this.dataTypes.Clear();
            if (dataTypes != null)
            {
                this.dataTypes = dataTypes;
            }
        }
        public Dictionary<NodeId, Node> GetDataTypes()
        {
            return this.dataTypes;
        }
        public void SetEventTypes(Dictionary<NodeId, Node> eventTypes)
        {
            this.eventTypes.Clear();
            if (eventTypes != null)
            {
                this.eventTypes = eventTypes;
            }
        }
        public Dictionary<NodeId, Node> GetEventTypes()
        {
            return this.eventTypes;
        }
        public void SetInterfaceTypes(Dictionary<NodeId, Node> interfaceTypes)
        {
            this.interfaceTypes.Clear();
            if (interfaceTypes != null)
            {
                this.interfaceTypes = interfaceTypes;
            }
        }
        public Dictionary<NodeId, Node> GetInterfaceTypes()
        {
            return this.interfaceTypes;
        }
        public void SetObjectTypes(Dictionary<NodeId, Node> objectTypes)
        {
            this.objectTypes.Clear();
            if (interfaceTypes != null)
            {
                this.objectTypes = objectTypes;
            }
        }
        public Dictionary<NodeId, Node> GetObjectTypes()
        {
            return this.objectTypes;
        }
        public void SetReferenceTypes(Dictionary<NodeId, Node> referenceTypes)
        {
            this.referenceTypes.Clear();
            if (referenceTypes != null)
            {
                this.referenceTypes = referenceTypes;
            }
        }
        public Dictionary<NodeId, Node> GetReferenceTypes()
        {
            return this.referenceTypes;
        }
        public void SetVariableTypes(Dictionary<NodeId, Node> variableTypes)
        {
            this.variableTypes.Clear();
            if (variableTypes != null)
            {
                this.variableTypes = variableTypes;
            }
        }
        public Dictionary<NodeId, Node> GetVariableTypes()
        {
            return this.variableTypes;
        }
        public Node? FindBinaryEncodingType(NodeId nodeId)
        {
            Node? encodingType = null;
            encodingType = this.opcBinary[nodeId];
            return encodingType;
        }
    }
    public class NodeIdComparer : IEqualityComparer<NodeId>
    {
        public bool Equals(NodeId? n1, NodeId? n2)
        {
            if (n1 == n2)
            {
                return true;
            }
            if (n1 == null || n2 == null)
            {
                return false;
            }
            return (n1.Identifier == n2.Identifier && n1.NamespaceIndex == n2.NamespaceIndex);
        }
        public int GetHashCode(NodeId n1)
        {
            return n1.Identifier.GetHashCode() + n1.NamespaceIndex.GetHashCode();
        }
    }
    public class DataClassComparer : IEqualityComparer<GeneratedDataTypeDefinition>
    {
        public bool Equals(GeneratedDataTypeDefinition? n1, GeneratedDataTypeDefinition? n2)
        {
            if (n1 == n2)
            {
                return true;
            }
            if (n1 == null || n2 == null)
            {
                return false;
            }
            return (n1.name == n2.name && n1.nameSpace == n2.nameSpace);
        }
        public int GetHashCode(GeneratedDataTypeDefinition n1)
        {
            return n1.name.GetHashCode() + n1.nameSpace.GetHashCode();
        }
    }
    public class GeneratedDataTypeDefinition
    {
        public string nameSpace;
        public string name;
        public GeneratedDataTypeDefinition(string nameSpace, string name)
        {
            this.nameSpace = nameSpace;
            this.name = name;
        }
    }
    public class GeneratedField
    {
        public bool IsLengthField = false;
        public bool HasLength = false;
        public bool IsSwitchField = false;
        public uint Length = 0;
        public string LengthField = "";
        public string Name = "";
        public string TypeName = "";
        public GeneratedField()
        {

        }
    }
    public class GeneratedDataClass
    {
        public string Name = "";
        public GeneratedDataClass()
        {
        }
    }
    public class GeneratedStructure : GeneratedDataClass
    {
        public string Documentation = "";
        public string? BaseType = null;
        public List<GeneratedField> fields = new List<GeneratedField>();
        public GeneratedStructure()
        {

        }
    }
    public class GeneratedEnumeratedType : GeneratedDataClass
    {
        public string Documentation = "";
        public GeneratedEnumeratedType()
        {

        }
    }
    public class GeneratedOpaqueType : GeneratedDataClass
    {
        public string Documentation = "";
        public GeneratedOpaqueType()
        {

        }
    }
    public enum GeneratedComplexTypes
    {
        StructuredType,
        EnumeratedType,
        OpaqueType
    }
}
