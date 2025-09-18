using System;
using System.IO;
using UnityEngine;

public class PlayCollisionSummary : MonoBehaviour
{
    public string fileName = "plays_summary.csv";  // single global file
    public string sessionId = "";                  // auto if empty
    public int count;                              // view in Inspector

    string _path;
    bool _wrote;

    void Start()
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        string folder = Path.Combine(Application.persistentDataPath, "StudyLogs");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, fileName);

        if (!File.Exists(_path))
        {
            using (var sw = new StreamWriter(_path, append: false))
                sw.WriteLine("sessionId,endedLocal,endedUTC,totalCollisions");
        }
    }

    void OnCollisionEnter(Collision c) => count++;

    public void EndPlay(string reason = "end_of_run")
    {
        if (_wrote) return;
        _wrote = true;

        using (var fs = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read))
        using (var sw = new StreamWriter(fs))
            sw.WriteLine($"{sessionId},{DateTime.Now:yyyy-MM-dd HH:mm:ss},{DateTime.UtcNow:o},{count}");

        Debug.Log($"[PlayCollisionSummary] {sessionId}: collisions={count} -> {_path} ({reason})");
    }

    void OnDisable()         { if (Application.isPlaying) EndPlay("component_disabled"); }
    void OnApplicationQuit() { EndPlay("application_quit"); }
}
