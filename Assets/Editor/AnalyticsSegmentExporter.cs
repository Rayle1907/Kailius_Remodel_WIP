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
            if (scene.Count(item => item.IsSceneEntry) != 1) throw new InvalidOperationException("Each authored scene needs exactly one entry segment.");
            if (scene.GroupBy(item => item.StartCheckpoint).Any(group => group.Count() > 1))
                throw new InvalidOperationException("A checkpoint must have only one outgoing segment.");
            foreach (var segment in scene)
                if (segment.TargetCheckpoint != null && !scene.Any(item => item.StartCheckpoint == segment.TargetCheckpoint))
                    throw new InvalidOperationException("A target checkpoint needs an outgoing segment, including the final segment to its exit.");
            var visited = new System.Collections.Generic.HashSet<AnalyticsSegmentDefinition>();
            var route = scene.Single(item => item.IsSceneEntry);
            while (route != null)
            {
                if (!visited.Add(route)) throw new InvalidOperationException("The authored route contains a checkpoint cycle.");
                route = route.TargetCheckpoint == null ? null : scene.Single(item => item.StartCheckpoint == route.TargetCheckpoint);
            }
            if (visited.Count != scene.Count()) throw new InvalidOperationException("Some segments are disconnected from scene entry.");
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
