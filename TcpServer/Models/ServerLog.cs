namespace TcpServer.Models;

public class ServerLog
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; }
    public string LogLevel { get; set; }
    public string ClientId { get; set; }
    public string ClientName { get; set; }
} 