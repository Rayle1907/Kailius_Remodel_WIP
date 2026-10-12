using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class LethalTilemapCreation
{
    [MenuItem("GameObject/2D Object/Tilemap/Create Hazard Tilemap", false, 10)]
    private static void CreateLethalTilemap()
    {
        Grid grid = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<Grid>()
            : null;
        if (grid == null)
        {
            EditorUtility.DisplayDialog("Create Hazard Tilemap", "Select an existing Grid or one of its children first.", "OK");
            return;
        }

        GameObject child = new GameObject("HazardTerrain", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
        Undo.RegisterCreatedObjectUndo(child, "Create Hazard Tilemap");
        Undo.SetTransformParent(child.transform, grid.transform, "Parent Hazard Tilemap");
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName = "Tile";
        renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        child.GetComponent<TilemapCollider2D>().isTrigger = true;
        Undo.AddComponent<LethalTilemapHazard>(child);
        Selection.activeGameObject = child;
    }
}
