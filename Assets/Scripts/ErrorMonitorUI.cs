using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ErrorMonitorUI : MonoBehaviour
{
    public Dropdown severityDropdown;
    public Text logText;
    public bool refreshOnStart = true;
    public int maxVisibleLines = 60;

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (severityDropdown == null || logText == null)
        {
            Debug.LogWarning("ErrorMonitorUI requires a Dropdown and a Text component.");
            return;
        }

        if (severityDropdown.options.Count == 0)
        {
            severityDropdown.options.Clear();
            severityDropdown.options.Add(new Dropdown.OptionData("All"));
            severityDropdown.options.Add(new Dropdown.OptionData("Info"));
            severityDropdown.options.Add(new Dropdown.OptionData("Warning"));
            severityDropdown.options.Add(new Dropdown.OptionData("Error"));
        }

        severityDropdown.onValueChanged.RemoveListener(OnFilterChanged);
        severityDropdown.onValueChanged.AddListener(OnFilterChanged);

        if (refreshOnStart)
        {
            RefreshLog();
        }
    }

    public void OnFilterChanged(int _)
    {
        RefreshLog();
    }

    public void RefreshLog()
    {
        if (severityDropdown == null || logText == null)
        {
            return;
        }

        int selectedValue = Mathf.Clamp(severityDropdown.value, 0, 3);
        var filter = (LogSeverity)selectedValue;
        var entries = ErrorLogger.GetEntries(filter);

        if (entries == null || entries.Count == 0)
        {
            logText.text = "No log entries.";
            return;
        }

        var lines = entries
            .Select(x => x.ToString())
            .TakeLast(Mathf.Max(1, maxVisibleLines))
            .ToArray();

        logText.text = string.Join("\n", lines);
    }
}
