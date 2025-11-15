using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

public class MaterialShaderConverter : EditorWindow
{
    private string[] targetShaderNames = new string[]
    {
        "Shader Graphs/S_PBR_OPAQUE_ORM_LeartesMasterMaterial",
        "Shader Graphs/S_PBR_TRANSPARENT_ORM_LeartesMasterMaterial"
    };

    private enum TargetShaderType
    {
        MetaLit,
        URPLit,
        URPSimpleLit,
        CustomORM
    }

    private TargetShaderType targetShaderType = TargetShaderType.MetaLit;
    private bool createNewMaterials = true;
    private string outputFolder = "Assets/Materials/Converted";
    private List<Material> foundMaterials = new List<Material>();
    private Dictionary<Material, Material> materialMapping = new Dictionary<Material, Material>(); // ADD THIS LINE
    private Vector2 scrollPosition;
    private bool conversionComplete = false;
    private bool debugPropertyNames = true;

    [MenuItem("Tools/Convert Materials to Quest-Optimized")]
    static void Init()
    {
        MaterialShaderConverter window = (MaterialShaderConverter)EditorWindow.GetWindow(typeof(MaterialShaderConverter));
        window.titleContent = new GUIContent("Quest Material Converter");
        window.minSize = new Vector2(520, 500);
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Quest Material Converter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Converts materials to Quest-optimized shaders with proper texture mapping", MessageType.Info);
        EditorGUILayout.Space();

        // Settings
        EditorGUILayout.LabelField("Source Shaders to Convert:", EditorStyles.boldLabel);
        for (int i = 0; i < targetShaderNames.Length; i++)
        {
            targetShaderNames[i] = EditorGUILayout.TextField($"Shader {i + 1}:", targetShaderNames[i]);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Target Shader:", EditorStyles.boldLabel);

        targetShaderType = (TargetShaderType)EditorGUILayout.EnumPopup("Shader Type:", targetShaderType);

        // Show info based on selection
        switch (targetShaderType)
        {
            case TargetShaderType.MetaLit:
                EditorGUILayout.HelpBox("Meta/Lit - Meta's Quest-optimized PBR shader. Best choice for Quest!\n" +
                    "• ORM will NOT be assigned (prevents artifacts)\n" +
                    "• Full PBR with Quest optimizations", MessageType.Info);
                break;
            case TargetShaderType.URPLit:
                EditorGUILayout.HelpBox("URP/Lit - Standard Unity PBR shader\n" +
                    "• Full PBR lighting\n" +
                    "• ORM will NOT be assigned", MessageType.Info);
                break;
            case TargetShaderType.URPSimpleLit:
                EditorGUILayout.HelpBox("URP/Simple Lit - Simplified lighting for best performance\n" +
                    "• Basic lighting only\n" +
                    "• ORM will NOT be assigned", MessageType.Info);
                break;
            case TargetShaderType.CustomORM:
                EditorGUILayout.HelpBox("Custom/URP Lit ORM Mobile - Keeps ORM packed\n" +
                    "• Requires custom shader to be present\n" +
                    "• ORM will be properly assigned", MessageType.Warning);
                break;
        }

        createNewMaterials = EditorGUILayout.Toggle("Create New Materials", createNewMaterials);
        debugPropertyNames = EditorGUILayout.Toggle("Debug Property Names", debugPropertyNames);

        if (createNewMaterials)
        {
            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField("Output Folder:", outputFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    outputFolder = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();

        // Find materials button
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("1. Find Materials to Convert", GUILayout.Height(35)))
        {
            FindMaterials();
        }
        GUI.backgroundColor = Color.white;

        // Display found materials
        if (foundMaterials.Count > 0)
        {
            EditorGUILayout.Space();
            GUILayout.Label($"Found {foundMaterials.Count} materials:", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
            foreach (Material mat in foundMaterials)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(mat, typeof(Material), false);
                if (GUILayout.Button("Debug", GUILayout.Width(60)))
                {
                    DebugMaterialProperties(mat);
                }
                if (GUILayout.Button("Ping", GUILayout.Width(50)))
                {
                    EditorGUIUtility.PingObject(mat);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // Convert button
            GUI.backgroundColor = Color.green;
            string shaderName = GetTargetShaderName();
            if (GUILayout.Button($"2. Convert to {shaderName}", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("Convert Materials",
                    $"Convert {foundMaterials.Count} materials to '{shaderName}'?",
                    "Convert", "Cancel"))
                {
                    ConvertMaterials();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        // Replace materials on prefabs button - ONLY SHOW ONCE
        if (conversionComplete && materialMapping.Count > 0)
        {
            EditorGUILayout.Space();
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button($"3. Replace Materials on Prefabs ({materialMapping.Count} mappings)", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("Replace Materials on Prefabs",
                    $"This will scan ALL prefabs and replace old materials with converted ones.\n\n" +
                    $"Found {materialMapping.Count} material mappings.\n\n" +
                    $"This operation can be undone with Ctrl+Z.",
                    "Replace", "Cancel"))
                {
                    ReplaceMaterialsOnPrefabs();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        if (conversionComplete)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("✓ Conversion complete! Check the Console for details.", MessageType.Info);
            
            if (materialMapping.Count > 0 && createNewMaterials)
            {
                EditorGUILayout.HelpBox($"Click Step 3 to replace {materialMapping.Count} materials on prefabs.", MessageType.Info);
            }
        }
    }

    void ReplaceMaterialsOnPrefabs()
    {
        if (materialMapping.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No material mappings found! Please convert materials first.", "OK");
            return;
        }

        // Find all prefabs in the project
        string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab");
        
        int prefabsModified = 0;
        int materialsReplaced = 0;

        Debug.Log($"\n=== STARTING PREFAB MATERIAL REPLACEMENT ===");
        Debug.Log($"Material mappings available: {materialMapping.Count}");

        for (int i = 0; i < prefabGUIDs.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGUIDs[i]);
            
            EditorUtility.DisplayProgressBar("Replacing Materials on Prefabs",
                $"Processing {Path.GetFileName(prefabPath)} ({i + 1}/{prefabGUIDs.Length})",
                (float)i / prefabGUIDs.Length);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) continue;

            bool prefabModified = false;

            // Get all renderers in prefab (including children)
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                bool rendererModified = false;

                for (int m = 0; m < materials.Length; m++)
                {
                    Material oldMat = materials[m];
                    
                    if (oldMat != null && materialMapping.ContainsKey(oldMat))
                    {
                        Material newMat = materialMapping[oldMat];
                        materials[m] = newMat;
                        rendererModified = true;
                        materialsReplaced++;
                        
                        Debug.Log($"  ✓ Replaced: {oldMat.name} -> {newMat.name} on {prefab.name}/{renderer.name}");
                    }
                }

                if (rendererModified)
                {
                    renderer.sharedMaterials = materials;
                    prefabModified = true;
                }
            }

            if (prefabModified)
            {
                PrefabUtility.SavePrefabAsset(prefab);
                prefabsModified++;
                Debug.Log($"✓ Updated prefab: {prefabPath}");
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"\n=== PREFAB UPDATE COMPLETE ===");
        Debug.Log($"  Modified {prefabsModified} prefabs");
        Debug.Log($"  Replaced {materialsReplaced} material assignments\n");

        EditorUtility.DisplayDialog("Prefab Update Complete",
            $"Successfully updated materials on prefabs!\n\n" +
            $"Modified Prefabs: {prefabsModified}\n" +
            $"Replaced Materials: {materialsReplaced}",
            "OK");
    }

    string GetTargetShaderName()
    {
        switch (targetShaderType)
        {
            case TargetShaderType.MetaLit:
                return "Meta/Lit";
            case TargetShaderType.URPLit:
                return "Universal Render Pipeline/Lit";
            case TargetShaderType.URPSimpleLit:
                return "Universal Render Pipeline/Simple Lit";
            case TargetShaderType.CustomORM:
                return "Custom/URP Lit ORM Mobile";
            default:
                return "Meta/Lit";
        }
    }

    void DebugMaterialProperties(Material mat)
    {
        Debug.Log($"\n========================================");
        Debug.Log($"MATERIAL DEBUG: {mat.name}");
        Debug.Log($"Shader: {mat.shader.name}");
        Debug.Log($"========================================");

        int propertyCount = ShaderUtil.GetPropertyCount(mat.shader);
        Debug.Log($"Total properties: {propertyCount}\n");

        Debug.Log("--- TEXTURE PROPERTIES ---");
        for (int i = 0; i < propertyCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(mat.shader, i);
            ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(mat.shader, i);

            if (propType == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                Texture tex = mat.GetTexture(propName);
                string texInfo = "NULL";
                if (tex != null)
                {
                    texInfo = $"{tex.name} (Format: {GetTextureFormat(tex)})";
                }
                Debug.Log($"  [{i}] {propName} = {texInfo}");
            }
        }

        Debug.Log("\n--- COLOR PROPERTIES ---");
        for (int i = 0; i < propertyCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(mat.shader, i);
            ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(mat.shader, i);

            if (propType == ShaderUtil.ShaderPropertyType.Color)
            {
                Color color = mat.GetColor(propName);
                Debug.Log($"  [{i}] {propName} = RGBA({color.r:F3}, {color.g:F3}, {color.b:F3}, {color.a:F3})");
            }
        }

        Debug.Log("\n--- FLOAT PROPERTIES ---");
        for (int i = 0; i < propertyCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(mat.shader, i);
            ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(mat.shader, i);

            if (propType == ShaderUtil.ShaderPropertyType.Float || propType == ShaderUtil.ShaderPropertyType.Range)
            {
                float value = mat.GetFloat(propName);
                Debug.Log($"  [{i}] {propName} = {value}");
            }
        }
        Debug.Log("========================================\n");
    }

    string GetTextureFormat(Texture tex)
    {
        if (tex is Texture2D)
        {
            Texture2D tex2D = tex as Texture2D;
            return tex2D.format.ToString();
        }
        return "Unknown";
    }

    void FindMaterials()
    {
        foundMaterials.Clear();
        materialMapping.Clear(); // CLEAR THE MAPPING
        conversionComplete = false;

        string[] materialGUIDs = AssetDatabase.FindAssets("t:Material");

        foreach (string guid in materialGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat != null && mat.shader != null)
            {
                foreach (string targetShader in targetShaderNames)
                {
                    if (mat.shader.name.Contains(targetShader))
                    {
                        foundMaterials.Add(mat);
                        Debug.Log($"Found: {mat.name} using {mat.shader.name}");
                        break;
                    }
                }
            }
        }

        Debug.Log($"Search complete. Found {foundMaterials.Count} materials to convert.");
    }

    void ConvertMaterials()
    {
        if (foundMaterials.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No materials found to convert!", "OK");
            return;
        }

        materialMapping.Clear(); // CLEAR OLD MAPPINGS

        string shaderToUse = GetTargetShaderName();

        Shader newShader = Shader.Find(shaderToUse);
        if (newShader == null)
        {
            Debug.LogError($"Shader '{shaderToUse}' not found!");
            EditorUtility.DisplayDialog("Error",
                $"Shader '{shaderToUse}' not found!\n\nMake sure the shader exists in your project.",
                "OK");
            return;
        }

        Debug.Log($"Using shader: {newShader.name}");

        if (createNewMaterials && !Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
        }

        int successCount = 0;
        int failCount = 0;

        for (int i = 0; i < foundMaterials.Count; i++)
        {
            Material oldMat = foundMaterials[i];

            EditorUtility.DisplayProgressBar("Converting Materials",
                $"Converting {oldMat.name} ({i + 1}/{foundMaterials.Count})",
                (float)i / foundMaterials.Count);

            try
            {
                Material targetMat;

                if (createNewMaterials)
                {
                    targetMat = new Material(newShader);
                    string oldPath = AssetDatabase.GetAssetPath(oldMat);
                    string newPath = Path.Combine(outputFolder, Path.GetFileName(oldPath));
                    newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);

                    AssetDatabase.CreateAsset(targetMat, newPath);
                    Debug.Log($"Created new material: {newPath}");
                    
                    // ✅ ADD THIS LINE - CREATE THE MAPPING
                    materialMapping[oldMat] = targetMat;
                }
                else
                {
                    targetMat = oldMat;
                    targetMat.shader = newShader;
                }

                // Transfer properties
                TransferMaterialProperties(oldMat, targetMat, newShader);

                EditorUtility.SetDirty(targetMat);
                successCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to convert {oldMat.name}: {e.Message}");
                Debug.LogException(e);
                failCount++;
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        conversionComplete = true;
        Debug.Log($"✓ Conversion complete! Success: {successCount}, Failed: {failCount}");
        
        // ✅ ADD THIS LINE - LOG MAPPING COUNT
        Debug.Log($"Created {materialMapping.Count} material mappings for prefab replacement");

        EditorUtility.DisplayDialog("Conversion Complete",
            $"Successfully converted {successCount} materials to '{shaderToUse}'.\n" +
            $"Failed: {failCount}\n\n" +
            $"{(materialMapping.Count > 0 ? "Click Step 3 to update prefabs!" : "")}", "OK");
    }

    void TransferMaterialProperties(Material sourceMat, Material targetMat, Shader targetShader)
    {
        Debug.Log($"\n=== Converting: {sourceMat.name} ===");

        Shader sourceShader = sourceMat.shader;
        int propertyCount = ShaderUtil.GetPropertyCount(sourceShader);

        // Collect all properties
        Dictionary<string, Texture> textureMap = new Dictionary<string, Texture>();
        Dictionary<string, Color> colorMap = new Dictionary<string, Color>();
        Dictionary<string, float> floatMap = new Dictionary<string, float>();

        // Collect textures, colors and floats
        for (int i = 0; i < propertyCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(sourceShader, i);
            ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(sourceShader, i);

            if (propType == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                Texture tex = sourceMat.GetTexture(propName);
                if (tex != null)
                {
                    textureMap[propName] = tex;
                    Debug.Log($"  ✓ TEXTURE: {propName} = {tex.name}");
                }
            }
            else if (propType == ShaderUtil.ShaderPropertyType.Color)
            {
                Color color = sourceMat.GetColor(propName);
                colorMap[propName] = color;
                Debug.Log($"  ✓ COLOR: {propName} = RGBA({color.r:F3}, {color.g:F3}, {color.b:F3}, {color.a:F3})");
            }
            else if (propType == ShaderUtil.ShaderPropertyType.Float || propType == ShaderUtil.ShaderPropertyType.Range)
            {
                float value = sourceMat.GetFloat(propName);
                floatMap[propName] = value;
                Debug.Log($"  ✓ FLOAT: {propName} = {value}");
            }
        }

        // Manual property transfers
        foreach (var kvp in textureMap)
        {
            string sourceProp = kvp.Key;
            Texture tex = kvp.Value;

            // Skip if target already has this texture assigned
            if (targetMat.HasProperty(sourceProp) && targetMat.GetTexture(sourceProp) == tex)
            {
                Debug.Log($"  └─ Skipped {sourceProp} (already assigned)");
                continue;
            }

            // Transfer using most specific map first
            if (sourceProp == "_MainTex" && targetMat.HasProperty("_BaseMap"))
            {
                targetMat.SetTexture("_BaseMap", tex);
                Debug.Log($"  ✓ TRANSFERRED: {sourceProp} -> _BaseMap ({tex.name})");
            }
            else if (sourceProp == "_BumpMap" && targetMat.HasProperty("_BumpMap"))
            {
                targetMat.SetTexture("_BumpMap", tex);
                Debug.Log($"  ✓ TRANSFERRED: {sourceProp} -> _BumpMap ({tex.name})");
            }
            else if (sourceProp == "_MetallicGlossMap" && targetMat.HasProperty("_MetallicGlossMap"))
            {
                targetMat.SetTexture("_MetallicGlossMap", tex);
                Debug.Log($"  ✓ TRANSFERRED: {sourceProp} -> _MetallicGlossMap ({tex.name})");
            }
            else if (sourceProp == "_OcclusionMap" && targetMat.HasProperty("_OcclusionMap"))
            {
                targetMat.SetTexture("_OcclusionMap", tex);
                Debug.Log($"  ✓ TRANSFERRED: {sourceProp} -> _OcclusionMap ({tex.name})");
            }
            else
            {
                Debug.Log($"  └─ Direct transfer not applied for {sourceProp}");
            }
        }

        // Transfer all colors
        foreach (var kvp in colorMap)
        {
            string propName = kvp.Key;
            Color color = kvp.Value;

            if (targetMat.HasProperty(propName))
            {
                targetMat.SetColor(propName, color);
                Debug.Log($"  ✓ COLOR TRANSFERRED: {propName} = RGBA({color.r:F3}, {color.g:F3}, {color.b:F3}, {color.a:F3})");
            }
            else
            {
                Debug.Log($"  └─ Color property not found on target: {propName}");
            }
        }

        // Transfer all floats
        foreach (var kvp in floatMap)
        {
            string propName = kvp.Key;
            float value = kvp.Value;

            if (targetMat.HasProperty(propName))
            {
                targetMat.SetFloat(propName, value);
                Debug.Log($"  ✓ FLOAT TRANSFERRED: {propName} = {value}");
            }
            else
            {
                Debug.Log($"  └─ Float property not found on target: {propName}");
            }
        }

        // Special case: Transfer Parallax Texture and Scale if applicable
        if (sourceMat.HasProperty("_ParallaxMap") && sourceMat.HasProperty("_Parallax"))
        {
            Texture parallaxTex = sourceMat.GetTexture("_ParallaxMap");
            if (parallaxTex != null && targetMat.HasProperty("_ParallaxMap"))
            {
                targetMat.SetTexture("_ParallaxMap", parallaxTex);
                Debug.Log($"  ✓ TRANSFERRED: _ParallaxMap -> _ParallaxMap ({parallaxTex.name})");
            }

            float parallaxScale = sourceMat.GetFloat("_Parallax");
            if (targetMat.HasProperty("_Parallax"))
            {
                targetMat.SetFloat("_Parallax", parallaxScale);
                Debug.Log($"  ✓ TRANSFERRED: _Parallax -> _Parallax ({parallaxScale})");
            }
        }

        // Regions to be converted to target shader types
        // This can be expanded based on specific conversion rules for each target shader type
        switch (targetShaderType)
        {
            case TargetShaderType.MetaLit:
                // Example: Set specific defaults or conversions for Meta/Lit
                if (targetMat.HasProperty("_Smoothness"))
                {
                    targetMat.SetFloat("_Smoothness", 0.5f);
                    Debug.Log($"  ✓ SET DEFAULT SMOOTHNESS FOR METALIT: 0.5");
                }
                break;

            case TargetShaderType.URPLit:
                // Example: Set specific defaults or conversions for URP Lit
                if (targetMat.HasProperty("_Smoothness"))
                {
                    targetMat.SetFloat("_Smoothness", 0.7f);
                    Debug.Log($"  ✓ SET DEFAULT SMOOTHNESS FOR URP LIT: 0.7");
                }
                break;

            case TargetShaderType.URPSimpleLit:
                // Example: Set specific defaults or conversions for URP Simple Lit
                if (targetMat.HasProperty("_ShadeObject"))
                {
                    targetMat.SetFloat("_ShadeObject", 1.0f);
                    Debug.Log($"  ✓ ENABLE SHADING ON OBJECT FOR URP SIMPLE LIT");
                }
                break;

            case TargetShaderType.CustomORM:
                // Custom processing for ORM packed shaders
                if (targetMat.HasProperty("_CustomORMProp"))
                {
                    targetMat.SetTexture("_CustomORMProp", textureMap["_MainTex"]);
                    Debug.Log($"  ✓ SET CUSTOM ORM PROPERTY");
                }
                break;
        }

        // Texture Transfer Phase 2: Handle specific texture assignments based on target shader
        TransferTextures(sourceMat, targetMat, textureMap);

        Debug.Log($"=== Converted: {sourceMat.name} ===");
    }

    void TransferTextures(Material sourceMat, Material targetMat, Dictionary<string, Texture> textureMap)
    {
        HashSet<string> assignedTargets = new HashSet<string>();
        Texture ormTexture = null;
        string ormPropName = null;
        Texture normalTexture = null;
        string normalPropName = null;
        Texture baseTexture = null;
        string basePropName = null;

        // PASS 1: Identify textures by exact property names and texture characteristics
        foreach (var kvp in textureMap)
        {
            string sourceProp = kvp.Key;
            Texture tex = kvp.Value;
            string lowerProp = sourceProp.ToLower();
            string lowerTexName = tex.name.ToLower();

            Debug.Log($"  🔎 Analyzing: {sourceProp} = {tex.name}");

            // Check for ORM (property name or texture name contains "orm")
            if (lowerProp.Contains("orm") || lowerTexName.Contains("orm") || 
                lowerProp.Contains("mask") || lowerTexName.Contains("mask"))
            {
                ormTexture = tex;
                ormPropName = sourceProp;
                Debug.Log($"  ✅ IDENTIFIED AS ORM: {sourceProp}");
                continue;
            }

            // Check for Normal map (by import type first, then name)
            bool isNormal = IsNormalMap(tex);
            if (isNormal || lowerProp.Contains("normal") || lowerTexName.Contains("normal") || 
                lowerTexName.Contains("nrm") || lowerTexName.Contains("bump"))
            {
                normalTexture = tex;
                normalPropName = sourceProp;
                Debug.Log($"  ✅ IDENTIFIED AS NORMAL: {sourceProp} (IsNormalMap={isNormal})");
                continue;
            }

            // Check for Base/Color (property name contains "colour", "color", "base", "albedo")
            if (lowerProp.Contains("colour") || lowerProp.Contains("color") || 
                lowerProp.Contains("base") || lowerProp.Contains("albedo") ||
                lowerTexName.Contains("color") || lowerTexName.Contains("base") || 
                lowerTexName.Contains("albedo") || lowerTexName.Contains("diffuse"))
            {
                baseTexture = tex;
                basePropName = sourceProp;
                Debug.Log($"  ✅ IDENTIFIED AS BASE: {sourceProp}");
                continue;
            }

            // Fallback: First unidentified texture becomes base
            if (baseTexture == null)
            {
                baseTexture = tex;
                basePropName = sourceProp;
                Debug.Log($"  ⚠️ FALLBACK TO BASE: {sourceProp}");
            }
        }

        // PASS 2: Assign identified textures to target material
        Debug.Log($"\n  === ASSIGNING TEXTURES ===");

        // Base Map
        if (baseTexture != null && targetMat.HasProperty("_BaseMap"))
        {
            targetMat.SetTexture("_BaseMap", baseTexture);
            assignedTargets.Add("_BaseMap");
            
            try
            {
                Vector2 scale = sourceMat.GetTextureScale(basePropName);
                Vector2 offset = sourceMat.GetTextureOffset(basePropName);
                targetMat.SetTextureScale("_BaseMap", scale);
                targetMat.SetTextureOffset("_BaseMap", offset);
                Debug.Log($"  ✓ BASE: {basePropName} -> _BaseMap ({baseTexture.name}) [Scale:{scale}, Offset:{offset}]");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"  ⚠️ Could not transfer scale/offset: {e.Message}");
                Debug.Log($"  ✓ BASE: {basePropName} -> _BaseMap ({baseTexture.name})");
            }
        }
        else if (baseTexture != null)
        {
            Debug.LogError($"  ✗ BASE TEXTURE FOUND BUT TARGET HAS NO _BaseMap PROPERTY!");
        }

        // Normal Map
        if (normalTexture != null && targetMat.HasProperty("_BumpMap"))
        {
            targetMat.SetTexture("_BumpMap", normalTexture);
            assignedTargets.Add("_BumpMap");
            
            try
            {
                Vector2 scale = sourceMat.GetTextureScale(normalPropName);
                Vector2 offset = sourceMat.GetTextureOffset(normalPropName);
                targetMat.SetTextureScale("_BumpMap", scale);
                targetMat.SetTextureOffset("_BumpMap", offset);
                Debug.Log($"  ✓ NORMAL: {normalPropName} -> _BumpMap ({normalTexture.name}) [Scale:{scale}, Offset:{offset}]");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"  ⚠️ Could not transfer scale/offset: {e.Message}");
                Debug.Log($"  ✓ NORMAL: {normalPropName} -> _BumpMap ({normalTexture.name})");
            }
        }
        else if (normalTexture != null)
        {
            Debug.LogError($"  ✗ NORMAL TEXTURE FOUND BUT TARGET HAS NO _BumpMap PROPERTY!");
        }

        // ORM Texture handling - ONLY for Custom ORM shader
        if (ormTexture != null)
        {
            if (targetShaderType == TargetShaderType.CustomORM && targetMat.HasProperty("_ORMMap"))
            {
                targetMat.SetTexture("_ORMMap", ormTexture);
                Debug.Log($"  ✓ ORM: {ormPropName} -> _ORMMap ({ormTexture.name})");
                
                try
                {
                    Vector2 scale = sourceMat.GetTextureScale(ormPropName);
                    Vector2 offset = sourceMat.GetTextureOffset(ormPropName);
                    targetMat.SetTextureScale("_ORMMap", scale);
                    targetMat.SetTextureOffset("_ORMMap", offset);
                }
                catch { }
            }
            else
            {
                // DON'T assign ORM to Meta/Lit - it causes artifacts
                Debug.LogWarning($"  ⚠️ ORM texture found but NOT assigned to Meta/Lit (would cause artifacts)");
                Debug.LogWarning($"  ℹ️ Meta/Lit requires separate Metallic and Occlusion textures");
                Debug.LogWarning($"  ℹ️ Consider using 'Custom/URP Lit ORM Mobile' shader instead");
            }
        }

        // Log final state
        Debug.Log($"\n  === FINAL TEXTURE ASSIGNMENTS ===");
        if (targetMat.HasProperty("_BaseMap"))
        {
            Texture finalBase = targetMat.GetTexture("_BaseMap");
            Debug.Log($"    _BaseMap = {(finalBase != null ? finalBase.name : "⚠️ NOT ASSIGNED")}");
        }
        if (targetMat.HasProperty("_BumpMap"))
        {
            Texture finalNormal = targetMat.GetTexture("_BumpMap");
            Debug.Log($"    _BumpMap = {(finalNormal != null ? finalNormal.name : "⚠️ NOT ASSIGNED")}");
        }
        if (targetMat.HasProperty("_MetallicGlossMap"))
        {
            Texture finalMetallic = targetMat.GetTexture("_MetallicGlossMap");
            Debug.Log($"    _MetallicGlossMap = {(finalMetallic != null ? finalMetallic.name : "NOT ASSIGNED (No artifacts)")}");
        }
        if (targetMat.HasProperty("_OcclusionMap"))
        {
            Texture finalOcclusion = targetMat.GetTexture("_OcclusionMap");
            Debug.Log($"    _OcclusionMap = {(finalOcclusion != null ? finalOcclusion.name : "NOT ASSIGNED (No artifacts)")}");
        }
        
        // Set default metallic/smoothness values when no texture is assigned
        if (targetMat.HasProperty("_Metallic") && targetMat.GetTexture("_MetallicGlossMap") == null)
        {
            targetMat.SetFloat("_Metallic", 0.0f);
            Debug.Log($"    Set _Metallic = 0.0 (no texture)");
        }
        if (targetMat.HasProperty("_Smoothness") && targetMat.GetTexture("_MetallicGlossMap") == null)
        {
            targetMat.SetFloat("_Smoothness", 0.5f);
            Debug.Log($"    Set _Smoothness = 0.5 (no texture)");
        }
    }

    bool IsNormalMap(Texture tex)
    {
        // Check the import settings of the texture to determine if it's a normal map
        string assetPath = AssetDatabase.GetAssetPath(tex);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer != null)
        {
            // Check if the texture is marked as a normal map in the importer settings
            if (importer.textureType == TextureImporterType.NormalMap)
            {
                return true;
            }
        }

        return false;
    }
}