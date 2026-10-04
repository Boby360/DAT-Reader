using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum LogSeverity
{
    All,
    Info,
    Warning,
    Error
}

public class LogEntry
{
    public DateTime Time;
    public LogSeverity Severity;
    public string Message;

    public override string ToString()
    {
        return $"[{Time:HH:mm:ss}] {Severity}: {Message}";
    }
}

public class ErrorLogger : MonoBehaviour
{
    private static ErrorLogger instance;
    private static string logFilePath;
    private static readonly List<LogEntry> entries = new List<LogEntry>();
    private const int MaxEntries = 200;

    public static ErrorLogger Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject loggerObject = new GameObject("ErrorLogger");
                instance = loggerObject.AddComponent<ErrorLogger>();
                DontDestroyOnLoad(loggerObject);
            }
            return instance;
        }
    }

    public static string LogFilePath => logFilePath;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeLogging();
    }

    private void InitializeLogging()
    {
        try
        {
            string logsDirectory = Path.Combine(Application.persistentDataPath, "Logs");
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            logFilePath = Path.Combine(logsDirectory, $"DATReader_{timestamp}.log");
            WriteToFile($"=== DAT Reader Error Log Started at {DateTime.Now} ===\n");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize ErrorLogger: {ex.Message}");
        }
    }

    private static void AddEntry(LogSeverity severity, string message)
    {
        if (entries.Count >= MaxEntries)
        {
            entries.RemoveAt(0);
        }

        entries.Add(new LogEntry
        {
            Time = DateTime.Now,
            Severity = severity,
            Message = message
        });

        try
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                File.AppendAllText(logFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {severity}: {message}\n");
            }
        }
        catch
        {
            // Ignore file-write failures so logging never crashes the app.
        }
    }

    public static List<LogEntry> GetEntries(LogSeverity filter = LogSeverity.All)
    {
        if (filter == LogSeverity.All)
        {
            return new List<LogEntry>(entries);
        }

        return entries.FindAll(x => x.Severity == filter);
    }

    public static void LogInfo(string message)
    {
        Debug.Log(message);
        AddEntry(LogSeverity.Info, message);
    }

    public static void LogWarning(string message)
    {
        Debug.LogWarning(message);
        AddEntry(LogSeverity.Warning, message);
    }

    public static void LogError(string message, Exception ex = null)
    {
        string fullMessage = message;
        if (ex != null)
        {
            fullMessage += $"\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}";
        }

        Debug.LogError(fullMessage);
        AddEntry(LogSeverity.Error, fullMessage);
    }

    public static void LogFileError(string operation, string filePath, Exception ex = null)
    {
        LogError($"File operation failed during {operation}. Path: {filePath}", ex);
    }

    public static void LogLoadingError(string objectType, string objectName, string reason, Exception ex = null)
    {
        LogError($"Failed to load {objectType}: '{objectName}'. Reason: {reason}", ex);
    }

    private void WriteToFile(string message)
    {
        try
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                File.AppendAllText(logFilePath, message);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to write to log file: {ex.Message}");
        }
    }

    public static string GetLogFilePath()
    {
        return logFilePath;
    }

    public static void ClearLogs()
    {
        try
        {
            string logsDirectory = Path.Combine(Application.persistentDataPath, "Logs");
            if (Directory.Exists(logsDirectory))
            {
                foreach (string file in Directory.GetFiles(logsDirectory, "*.log"))
                {
                    File.Delete(file);
                }
            }

            entries.Clear();
            LogInfo("All log files cleared.");
        }
        catch (Exception ex)
        {
            LogError("Failed to clear log files.", ex);
        }
    }
}
