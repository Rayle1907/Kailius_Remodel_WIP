using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AnalyticsSegmentExporter
{
    [Serializable]
    private sealed class Catalogue
    {
        public int schema_version = 1;
        public SegmentSnapshot[] segments;
    }

    [MenuItem("Tools/Analytics/Bake and export loaded segment catalogue")]
    public static void ExportLoadedCatalogue()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Bake analytics geometry outside Play Mode.");
        var definitions = UnityEngine.Object.FindObjectsByType<AnalyticsSegmentDefinition>(FindObjectsInactive.Include);
        if (definitions.Length == 0) throw new InvalidOperationException("Add AnalyticsSegmentDefinition components and author route boundaries before export.");
        foreach (var definition in definitions) definition.ValidateConfiguration();
        var snapshots = definitions.Select(item => item.Capture()).OrderBy(item => item.segment_id).ToArray();
        if (snapshots.GroupBy(item => item.segment_id).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Segment IDs must be unique, including copied definitions.");
        foreach (var scene in definitions.GroupBy(item => item.gameObject.scene))
        {
            var entries = scene.Where(item => item.IsSceneEntry).ToArray();
            if (entries.Length == 0 || entries.Select(item => item.StartCheckpoint).Distinct().Count() != 1)
                throw new InvalidOperationException(scene.Key.name + " needs entry segments with one shared start checkpoint.");
            var outgoing = scene.ToLookup(item => item.StartCheckpoint);
            var visiting = new HashSet<AnalyticsSegmentDefinition>();
            var visited = new HashSet<AnalyticsSegmentDefinition>();
            foreach (var entry in entries) Visit(entry);
            var unreachable = scene.Where(item => !visited.Contains(item)).Select(item => item.SegmentId).ToArray();
            if (unreachable.Length > 0)
                throw new InvalidOperationException(scene.Key.name + " has unreachable segments: " + string.Join(", ", unreachable));

            void Visit(AnalyticsSegmentDefinition segment)
            {
                if (visited.Contains(segment)) return;
                string label = scene.Key.name + "/" + segment.SegmentId;
                if (!visiting.Add(segment)) throw new InvalidOperationException("Checkpoint cycle at " + label);
                if (segment.TargetCheckpoint != null)
                {
                    if (!outgoing.Contains(segment.TargetCheckpoint))
                        throw new InvalidOperationException("Missing continuation after " + label);
                    foreach (var next in outgoing[segment.TargetCheckpoint]) Visit(next);
                }
                visiting.Remove(segment);
                visited.Add(segment);
            }
        }
        string directory = Path.GetFullPath("Analytics/catalogues");
        Directory.CreateDirectory(directory);
        string payload = JsonUtility.ToJson(new Catalogue { segments = snapshots }, true);
        string suffix = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        string path = Path.Combine(directory, "segments-" + suffix + ".json");
        File.WriteAllText(path, payload);
        var snapshotsById = snapshots.ToDictionary(item => item.segment_id);
        foreach (var definition in definitions)
        {
            Undo.RecordObject(definition, "Bake analytics geometry");
            definition.BakeSnapshot(snapshotsById[definition.SegmentId]);
            EditorUtility.SetDirty(definition);
            EditorSceneManager.MarkSceneDirty(definition.gameObject.scene);
        }
        Debug.Log("Exported analytics catalogue: " + path);
    }
}
