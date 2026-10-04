using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Centralized error logging system for the DAT Reader application.
/// Logs errors to both the Unity Console and a persistent log file.
/// </summary>
public class ErrorLogger : MonoBehaviour
{
    private static ErrorLogger instance;
    private string logFilePath;
    private static bool initialized = false;

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

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (!initialized)
        {
            InitializeLogging();
            initialized = true;
        }
    }

    private void InitializeLogging()
    {
        try
        {
            // Create Logs directory if it doesn't exist
            string logsDirectory = Path.Combine(Application.persistentDataPath, "Logs");
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            // Create log file with timestamp
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            logFilePath = Path.Combine(logsDirectory, $"DATReader_{timestamp}.log");

            // Write initialization message
            WriteToFile($"=== DAT Reader Error Log Started at {System.DateTime.Now} ===\n");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize ErrorLogger: {ex.Message}");
        }
    }

    /// <summary>
    /// Log an error message with optional exception details
    /// </summary>
    public static void LogError(string message, Exception ex = null)
    {
        string fullMessage = message;
        if (ex != null)
        {
            fullMessage += $"\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}";
        }

        Debug.LogError(fullMessage);
        Instance.WriteToFile($"[ERROR] {System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {fullMessage}\n");
    }

    /// <summary>
    /// Log a warning message
    /// </summary>
    public static void LogWarning(string message)
    {
        Debug.LogWarning(message);
        Instance.WriteToFile($"[WARNING] {System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}\n");
    }

    /// <summary>
    /// Log an informational message
    /// </summary>
    public static void LogInfo(string message)
    {
        Debug.Log(message);
        Instance.WriteToFile($"[INFO] {System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}\n");
    }

    /// <summary>
    /// Log a file operation failure
    /// </summary>
    public static void LogFileError(string operation, string filePath, Exception ex = null)
    {
        string message = $"File Operation Failed - {operation}\nPath: {filePath}";
        LogError(message, ex);
    }

    /// <summary>
    /// Log a loading failure with context
    /// </summary>
    public static void LogLoadingError(string objectType, string objectName, string reason, Exception ex = null)
    {
        string message = $"Failed to load {objectType}: '{objectName}'\nReason: {reason}";
        LogError(message, ex);
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

    /// <summary>
    /// Get the path to the current log file
    /// </summary>
    public static string GetLogFilePath()
    {
        return Instance.logFilePath;
    }

    /// <summary>
    /// Clear all log files (useful for debugging)
    /// </summary>
    public static void ClearLogs()
    {
        try
        {
            string logsDirectory = Path.Combine(Application.persistentDataPath, "Logs");
            if (Directory.Exists(logsDirectory))
            {
                string[] logFiles = Directory.GetFiles(logsDirectory, "*.log");
                foreach (string file in logFiles)
                {
                    File.Delete(file);
                }
                LogInfo("All log files cleared.");
            }
        }
        catch (Exception ex)
        {
            LogError("Failed to clear log files", ex);
        }
    }
}
