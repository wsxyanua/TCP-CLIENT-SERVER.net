namespace TcpServer.Models;

public class ClientInfo
{
    public string EndPoint { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public DateTime ConnectedTime { get; set; }
    public DateTime LastActivityTime { get; set; }
    public DateTime? LastRequestTime { get; set; }
    public List<string> Messages { get; set; } = new List<string>();
} 