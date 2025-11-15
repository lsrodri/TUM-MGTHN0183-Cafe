using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class StaticShadowCasterBatch : EditorWindow
{
    enum TargetMode { Selected, ByTag, ByLayer }

    TargetMode mode = TargetMode.Selected;
    string tagFilter = "ShadowCaster";
    string layerFilter = "";
    bool logChanges = true;

    [MenuItem("Tools/Batch Static Shadow Caster")]
    public static void ShowWindow()
    {
        var window = GetWindow<StaticShadowCasterBatch>();
        window.titleContent = new GUIContent("Static Shadow Caster Batch");
        window.minSize = new Vector2(360, 140);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Batch set static shadow casters", EditorStyles.boldLabel);
        mode = (TargetMode)EditorGUILayout.EnumPopup("Target mode", mode);

        if (mode == TargetMode.ByTag)
        {
            tagFilter = EditorGUILayout.TextField("Tag", tagFilter);
        }
        else if (mode == TargetMode.ByLayer)
        {
            layerFilter = EditorGUILayout.TextField("Layer name", layerFilter);
        }

        logChanges = EditorGUILayout.Toggle("Log changes", logChanges);

        if (GUILayout.Button("Apply"))
        {
            ApplyBatch();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Sets the Static Shadow Caster checkbox on all renderers under target objects.", MessageType.Info);
    }

    void ApplyBatch()
    {
        List<GameObject> targets = new List<GameObject>();

        switch (mode)
        {
            case TargetMode.Selected:
                foreach (var obj in Selection.gameObjects)
                    if (obj != null) targets.Add(obj);
                break;

            case TargetMode.ByTag:
                if (string.IsNullOrEmpty(tagFilter))
                {
                    Debug.LogWarning("Tag filter is empty.");
                    return;
                }
                foreach (var t in GameObject.FindObjectsOfType<Transform>(true))
                {
                    if (t != null && t.gameObject.CompareTag(tagFilter))
                        targets.Add(t.gameObject);
                }
                break;

            case TargetMode.ByLayer:
                int targetLayer = LayerMask.NameToLayer(layerFilter);
                if (targetLayer == -1)
                {
                    Debug.LogWarning("Layer name not found: " + layerFilter);
                    return;
                }
                foreach (var t in GameObject.FindObjectsOfType<Transform>(true))
                {
                    if (t != null && t.gameObject.layer == targetLayer)
                        targets.Add(t.gameObject);
                }
                break;
        }

        int changed = 0;
        foreach (var go in targets)
        {
            if (go == null) continue;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null)
                {
                    Undo.RecordObject(r, "Set Static Shadow Caster");
                    r.staticShadowCaster = true;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    EditorUtility.SetDirty(r);
                }
            }

            changed++;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        if (logChanges)
        {
            Debug.LogFormat("Static shadow casters applied to {0} object(s).", changed);
        }

        SceneView.RepaintAll();
    }
}
