using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System;
using System.Threading.Tasks;

class Program
{
    private static readonly ConsoleColor PrimaryColor = ConsoleColor.Cyan;
    private static readonly ConsoleColor SuccessColor = ConsoleColor.Green;
    private static readonly ConsoleColor WarningColor = ConsoleColor.Yellow;
    private static readonly ConsoleColor ErrorColor = ConsoleColor.Red;
    private static readonly ConsoleColor InfoColor = ConsoleColor.Blue;

    private static string? ClientId;
    private static Stopwatch PingTimer = new();
    private const string ServerIP = "127.0.0.1";
    private const int ServerPort = 8888;
    private static readonly Encoding _encoding = Encoding.UTF8;

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "TCP Time Client";

        PrintHeader();
        PrintSeparator();

        try
        {
            PrintInfo("Connecting to server...");
            using var client = new TcpClient();
            await client.ConnectAsync(ServerIP, ServerPort);
            PrintSuccess("Connected to server successfully!");

            using (var stream = client.GetStream())
            {
                while (true)
                {
                    Console.WriteLine("\nPlease select an option:");
                    Console.WriteLine("1. Get current time");
                    Console.WriteLine("2. Send custom message");
                    Console.WriteLine("3. Exit");
                    Console.Write("Your choice: ");

                    var choice = Console.ReadLine()?.Trim();

                    switch (choice)
                    {
                        case "1":
                            await SendTimeRequest(stream);
                            break;
                        case "2":
                            await SendCustomMessage(stream);
                            break;
                        case "3":
                            Console.WriteLine("Goodbye!");
                            return;
                        default:
                            Console.WriteLine("Invalid choice. Please try again.");
                            break;
                    }

                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                }
            }
        }
        catch (Exception ex)
        {
            PrintError($"Error: {ex.Message}");
        }
        finally
        {
            PrintSeparator();
            PrintInfo("Disconnected from server. Press any key to exit...");
            Console.ReadKey();
        }
    }

    static async Task SendCommand(TcpClient client, string command)
    {
        try
        {
            var stream = client.GetStream();
            var data = _encoding.GetBytes(command);
            await stream.WriteAsync(data);

            var buffer = new byte[1024];
            var bytesRead = await stream.ReadAsync(buffer);
            var response = _encoding.GetString(buffer, 0, bytesRead);
            
            if (command == "whoami")
            {
                ClientId = response.Split(':').LastOrDefault()?.Trim();
            }
            
            PrintSuccess($"Server response: {response}");
        }
        catch (Exception ex)
        {
            PrintError($"Error sending command: {ex.Message}");
            throw;
        }
    }

    static async Task PingServer(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var pingData = new byte[] { 0x70, 0x69, 0x6E, 0x67 }; // "ping" in bytes
            
            PingTimer.Restart();
            await stream.WriteAsync(pingData);
            
            var buffer = new byte[4]; // Small buffer for ping response
            await stream.ReadAsync(buffer);
            
            PingTimer.Stop();
            PrintSuccess($"Ping: {PingTimer.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            PrintError($"Ping failed: {ex.Message}");
        }
    }

    static void PrintStatus(TcpClient client)
    {
        Console.ForegroundColor = InfoColor;
        Console.WriteLine("\nClient Status:");
        Console.ResetColor();
        Console.WriteLine($"  Client ID: {ClientId ?? "Unknown"}");
        Console.WriteLine($"  Connected: {client.Connected}");
        if (client.Connected)
        {
            Console.WriteLine($"  Local Endpoint: {client.Client.LocalEndPoint}");
            Console.WriteLine($"  Remote Endpoint: {client.Client.RemoteEndPoint}");
        }
    }

    static void PrintMenu()
    {
        PrintSeparator();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nAvailable Commands:");
        Console.ResetColor();
        
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("1. Get Current Server Time");
        Console.WriteLine("2. Send Message to Server");
        Console.WriteLine("3. Get All Messages");
        Console.WriteLine("4. Exit Program");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("\nYou can enter either the command number or the full command.");
        Console.WriteLine("Example: '1' or 'gettime'");
        Console.ResetColor();
        PrintSeparator();
    }

    static string GetCommandByChoice(int choice)
    {
        return choice switch
        {
            1 => "gettime",
            2 => "send:",
            3 => "getmessages",
            4 => "exit",
            _ => throw new ArgumentException("Invalid choice")
        };
    }

    static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
                 __
                               _.-~  )
                    _..--~~~~,'   ,-/     _
                 .-'. . . .'   ,-','    ,' )
               ,'. . . _   ,--~,-'__..-'  ,'
             ,'. . .  (@)' ---~~~~      ,'
            /. . . . '~~             ,-'
           /. . . . .             ,-'
          ; . . . .  - .        ,'
         : . . . .       _     /
        . . . . .          `-.:
       . . . ./  - .          )
      .  . . |  _____..---.._/ ____ @gnolswft____
 ~---~~~~----~~~~             ~~                                                     
");
        Console.ResetColor();
        Console.WriteLine("TCP Time Client - Version 1.0");
        Console.WriteLine("=============================");
    }

    static void PrintSeparator()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(new string('=', 60));
        Console.ResetColor();
    }

    static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[✓] {message}");
        Console.ResetColor();
    }

    static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[✗] {message}");
        Console.ResetColor();
    }

    static void PrintInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"[i] {message}");
        Console.ResetColor();
    }

    static void PrintWarning(string message)
    {
        Console.ForegroundColor = WarningColor;
        Console.WriteLine($"[!] {message}");
        Console.ResetColor();
    }

    private static async Task SendTimeRequest(NetworkStream stream)
    {
        try
        {
            Console.WriteLine("\nSelect time format:");
            Console.WriteLine("1. Full date and time");
            Console.WriteLine("2. Date only");
            Console.WriteLine("3. Time only");
            Console.Write("Your choice: ");

            var choice = Console.ReadLine()?.Trim();
            string request;

            switch (choice)
            {
                case "1":
                    request = "TIME:FULL";
                    break;
                case "2":
                    request = "TIME:DATE";
                    break;
                case "3":
                    request = "TIME:TIME";
                    break;
                default:
                    Console.WriteLine("Invalid choice. Using full format.");
                    request = "TIME:FULL";
                    break;
            }

            var requestData = _encoding.GetBytes(request);
            await stream.WriteAsync(requestData, 0, requestData.Length);

            var buffer = new byte[1024];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            var response = _encoding.GetString(buffer, 0, bytesRead);
            PrintSuccess($"Server response: {response}");
        }
        catch (Exception ex)
        {
            PrintError($"Error sending time request: {ex.Message}");
        }
    }

    private static async Task SendCustomMessage(NetworkStream stream)
    {
        try
        {
            while (true)
            {
                Console.Write("\nEnter your message (or 'exit' to return to main menu): ");
                var message = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(message))
                {
                    PrintError("Message cannot be empty.");
                    continue;
                }

                if (message.ToLower() == "exit")
                {
                    break;
                }

                var requestData = _encoding.GetBytes(message);
                await stream.WriteAsync(requestData, 0, requestData.Length);

                var buffer = new byte[1024];
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                var response = _encoding.GetString(buffer, 0, bytesRead);
                PrintSuccess($"Server response: {response}");

                Console.Write("\nDo you want to send another message? (y/n): ");
                var continueSending = Console.ReadLine()?.ToLower().Trim();
                if (continueSending != "y" && continueSending != "yes")
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            PrintError($"Error sending custom message: {ex.Message}");
        }
    }
}