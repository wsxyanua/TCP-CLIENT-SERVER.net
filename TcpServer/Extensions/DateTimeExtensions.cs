using System;

namespace TcpServer.Extensions;

public static class DateTimeExtensions
{
    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static long ToUnixTimeMilliseconds(this DateTime dateTime)
    {
        return (long)(dateTime.ToUniversalTime() - UnixEpoch).TotalMilliseconds;
    }
} 