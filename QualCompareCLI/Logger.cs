namespace QualCompareCLI;
using System;

/// <summary>
/// Simple logging interface for console output.
/// </summary>
public interface ILogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogDebug(string message);
}

/// <summary>
/// Console-based logger implementation.
/// </summary>
public class ConsoleLogger : ILogger
{
    private readonly bool _verboseMode;

    public ConsoleLogger(bool verbose = false)
    {
        _verboseMode = verbose;
    }

    public void LogInfo(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    public void LogWarning(string message)
    {
        Console.WriteLine($"[WARN] {message}");
    }

    public void LogError(string message)
    {
        Console.Error.WriteLine($"[ERROR] {message}");
    }

    public void LogDebug(string message)
    {
        if (_verboseMode)
            Console.WriteLine($"[DEBUG] {message}");
    }
}
