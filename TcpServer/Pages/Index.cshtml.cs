using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TcpServer.Services;
using TcpServer.Models;

namespace TcpServer.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly TcpTimeService _tcpService;

    public IndexModel(ILogger<IndexModel> logger, TcpTimeService tcpService)
    {
        _logger = logger;
        _tcpService = tcpService;
    }

    public bool IsServerRunning => _tcpService.IsRunning;
    public int TotalMessages => _tcpService.TotalMessages;
    public int TimeRequests => _tcpService.TimeRequests;
    public int ActiveConnections => _tcpService.ActiveConnections;
    public TimeSpan Uptime => _tcpService.Uptime;
    public DateTime StartTime => _tcpService.StartTime;
    public IReadOnlyDictionary<string, ClientInfo> ConnectedClients => _tcpService.ConnectedClients;
    public IReadOnlyList<ServerLog> ServerLogs => _tcpService.Logs;

    public IActionResult OnGet()
    {
        return Page();
    }

    public IActionResult OnPostStartServer()
    {
        try
        {
            _tcpService.Start();
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start server");
            return RedirectToPage();
        }
    }

    public IActionResult OnPostStopServer()
    {
        try
        {
            _tcpService.Stop();
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop server");
            return RedirectToPage();
        }
    }

    public IActionResult OnPostDisconnectClient(string clientId)
    {
        try
        {
            _tcpService.DisconnectClient(clientId);
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect client");
            return RedirectToPage();
        }
    }

    public IActionResult OnPostResetCounters()
    {
        try
        {
            _tcpService.ResetCounters();
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset counters");
            return RedirectToPage();
        }
    }
}
