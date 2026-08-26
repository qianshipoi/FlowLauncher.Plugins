using Microsoft.Win32.SafeHandles;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace FlowLauncher.Plugin.PortKiller;

internal static class ProcessHelpers
{
    public static unsafe string TryGetProcessFilename(Process p)
    {
        try
        {
            var handle = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)p.Id);
            if (handle == HWND.Null)
            {
                return string.Empty;
            }

            using var safeHandle = new SafeProcessHandle((nint)handle.Value, true);
            uint capacity = 2000;
            Span<char> buffer = new char[capacity];
            if (!PInvoke.QueryFullProcessImageName(safeHandle, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref capacity))
            {
                return string.Empty;
            }

            return buffer[..(int)capacity].ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    public static List<PortProcessInfo> QueryProcessesByPort(int port, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = "netstat",
            Arguments = "-ano",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return [];
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        token.ThrowIfCancellationRequested();

        return ParseNetstatOutput(port, output);
    }

    private static List<PortProcessInfo> ParseNetstatOutput(int port, string output)
    {
        var results = new List<PortProcessInfo>();
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (!line.StartsWith("TCP", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("UDP", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                continue;
            }

            var protocol = parts[0];
            var localEndPoint = parts[1];
            var remoteEndPoint = parts[2];
            var state = string.Empty;
            var pidToken = string.Empty;

            if (string.Equals(protocol, "TCP", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length < 5)
                {
                    continue;
                }

                state = parts[3];
                pidToken = parts[4];
            }
            else
            {
                pidToken = parts[^1];
                state = "UDP";
            }

            if (!TryParsePort(localEndPoint, out var localPort) || localPort != port)
            {
                continue;
            }

            if (!int.TryParse(pidToken, out var pid))
            {
                continue;
            }

            var processName = "(unknown)";
            try
            {
                processName = Process.GetProcessById(pid).ProcessName;
            }
            catch
            {
            }

            var iconPath = string.Empty;
            try
            {
                iconPath = TryGetProcessFilename(Process.GetProcessById(pid));
            }
            catch
            {
            }

            results.Add(new PortProcessInfo(pid, processName, protocol, localEndPoint, remoteEndPoint, state, iconPath));
        }

        return results;
    }

    private static bool TryParsePort(string endPoint, out int port)
    {
        port = default;
        var idx = endPoint.LastIndexOf(':');
        if (idx < 0 || idx == endPoint.Length - 1)
        {
            return false;
        }

        var rawPort = endPoint[(idx + 1)..];
        return int.TryParse(rawPort, out port);
    }

    public sealed record PortProcessInfo(
        int ProcessId,
        string ProcessName,
        string Protocol,
        string LocalEndPoint,
        string RemoteEndPoint,
        string State,
        string IconPath = ""
    );
}
