using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ReplaceMetaLitShader : EditorWindow
{
    private int materialsFound = 0;
    private int materialsReplaced = 0;
    private bool showResults = false;
    private List<string> materialPaths = new List<string>();

    [MenuItem("Tools/Replace Meta/Lit with URP/Lit")]
    public static void ShowWindow()
    {
        GetWindow<ReplaceMetaLitShader>("Replace Meta/Lit Shader");
    }

    void OnGUI()
    {
        GUILayout.Label("Replace Meta/Lit Shader with URP/Lit", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "This tool will find all materials using the 'Meta/Lit' shader and replace them with 'Universal Render Pipeline/Lit'.",
            MessageType.Info);

        GUILayout.Space(10);

        if (GUILayout.Button("Find Meta/Lit Materials", GUILayout.Height(30)))
        {
            FindMetaLitMaterials();
        }

        if (materialsFound > 0 && !showResults)
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                $"Found {materialsFound} materials using Meta/Lit shader.",
                MessageType.Warning);

            if (GUILayout.Button("Replace All with URP/Lit", GUILayout.Height(30)))
            {
                ReplaceShaders();
            }
        }

        if (showResults)
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                $"Successfully replaced {materialsReplaced} materials!",
                MessageType.Info);

            if (GUILayout.Button("Close", GUILayout.Height(25)))
            {
                showResults = false;
                materialsFound = 0;
                materialsReplaced = 0;
                materialPaths.Clear();
            }
        }

        // Display found materials
        if (materialsFound > 0 && !showResults)
        {
            GUILayout.Space(10);
            GUILayout.Label("Materials found:", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");
            foreach (string path in materialPaths)
            {
                EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }
    }

    void FindMetaLitMaterials()
    {
        materialsFound = 0;
        materialPaths.Clear();
        showResults = false;

        // Find all material assets in the project
        string[] guids = AssetDatabase.FindAssets("t:Material");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material != null && material.shader != null)
            {
                // Check if the shader name contains "Meta/Lit"
                if (material.shader.name == "Meta/Lit")
                {
                    materialsFound++;
                    materialPaths.Add(path);
                }
            }
        }

        if (materialsFound == 0)
        {
            EditorUtility.DisplayDialog("No Materials Found",
                "No materials using Meta/Lit shader were found in the project.", "OK");
        }

        Debug.Log($"[ReplaceMetaLitShader] Found {materialsFound} materials using Meta/Lit shader.");
    }

    void ReplaceShaders()
    {
        materialsReplaced = 0;

        // Find the URP/Lit shader
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");

        if (urpLitShader == null)
        {
            EditorUtility.DisplayDialog("Error",
                "Could not find 'Universal Render Pipeline/Lit' shader. Make sure URP is installed.", "OK");
            return;
        }

        // Process each material
        foreach (string path in materialPaths)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material != null && material.shader.name == "Meta/Lit")
            {
                // Store properties before changing shader
                Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;

                // Change the shader
                material.shader = urpLitShader;

                // Try to preserve common properties
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", baseColor);
                if (material.HasProperty("_BaseMap") && baseMap != null)
                    material.SetTexture("_BaseMap", baseMap);

                // Save the changes
                EditorUtility.SetDirty(material);
                materialsReplaced++;

                Debug.Log($"[ReplaceMetaLitShader] Replaced shader in: {path}");
            }
        }

        // Save all assets
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        showResults = true;

        Debug.Log($"[ReplaceMetaLitShader] Successfully replaced {materialsReplaced} materials with URP/Lit shader.");
    }
}
