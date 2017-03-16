using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.MemoryProfiler;

public static class PackedMemorySnapshotUtility
{
    public static void SaveToFile(PackedMemorySnapshot snapshot)
    {
        var filePath = EditorUtility.SaveFilePanel("Save Snapshot", null, "MemorySnapshot", "memsnap2");
        if(string.IsNullOrEmpty(filePath))
            return;

        SaveToFile(filePath, snapshot);
    }

    static void SaveToFile(string filePath, PackedMemorySnapshot snapshot)
    {
        // Saving snapshots using JsonUtility, instead of BinaryFormatter, is significantly faster.
        // I cancelled saving a memory snapshot that is saving using BinaryFormatter after 24 hours.
        // Saving the same memory snapshot using JsonUtility.ToJson took 20 seconds only.

        UnityEngine.Profiling.Profiler.BeginSample("PackedMemorySnapshotUtility.SaveToFile");

        var json = JsonUtility.ToJson(snapshot);
        File.WriteAllText(filePath, json);

        UnityEngine.Profiling.Profiler.EndSample();
    }

    public static PackedMemorySnapshot LoadFromFile()
    {
        var filePath = EditorUtility.OpenFilePanelWithFilters("Load Snapshot", null, new[] { "Snapshots", "memsnap2,memsnap" });
        if(string.IsNullOrEmpty(filePath))
            return null;

        return LoadFromFile(filePath);
    }

    static PackedMemorySnapshot LoadFromFile(string filePath)
    {
        PackedMemorySnapshot result = null;
        string fileExtension = Path.GetExtension(filePath);

        if(string.Equals(fileExtension, ".memsnap2", System.StringComparison.OrdinalIgnoreCase))
        {
            UnityEngine.Profiling.Profiler.BeginSample("PackedMemorySnapshotUtility.LoadFromFile(json)");

            var json = File.ReadAllText(filePath);
            result = JsonUtility.FromJson<PackedMemorySnapshot>(json);

            UnityEngine.Profiling.Profiler.EndSample();
        }
        else if(string.Equals(fileExtension, ".memsnap", System.StringComparison.OrdinalIgnoreCase))
        {
            UnityEngine.Profiling.Profiler.BeginSample("PackedMemorySnapshotUtility.LoadFromFile(binary)");

            var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            using(Stream stream = File.Open(filePath, FileMode.Open))
            {
                result = binaryFormatter.Deserialize(stream) as PackedMemorySnapshot;
            }

            UnityEngine.Profiling.Profiler.EndSample();
        }
        else
        {
            Debug.LogErrorFormat("MemoryProfiler: Unrecognized memory snapshot format '{0}'.", filePath);
        }

        return result;
    }
}

