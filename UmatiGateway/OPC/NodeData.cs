using Opc.Ua;
using Opc.Ua.Client;

namespace UmatiGateway.OPC
{
    public class NodeData
    {
        public Node node { get; set; }
        public bool isexpanded { get; set; }
        public NodeData(Node node)
        {
            isexpanded = false;
            this.node = node;
        }
        public NodeData()
        {
            isexpanded = false;
            this.node = new Node();
        }
    }
}
