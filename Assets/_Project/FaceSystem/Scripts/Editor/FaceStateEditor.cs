using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FaceState))]
public sealed class FaceStateEditor : Editor
{
    private const float PreviewSize = 52f;

    private SerializedProperty idProperty;
    private SerializedProperty spriteLibraryProperty;
    private SerializedProperty trackStatesProperty;

    private void OnEnable()
    {
        idProperty = serializedObject.FindProperty("id");
        spriteLibraryProperty = serializedObject.FindProperty("spriteLibrary");
        trackStatesProperty = serializedObject.FindProperty("trackStates");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(idProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sprite Library", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(spriteLibraryProperty);
        FaceSpriteLibrary library = spriteLibraryProperty.objectReferenceValue as FaceSpriteLibrary;

        if (library == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a FaceSpriteLibrary to select Track IDs and Sprite IDs.",
                MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Track States", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Each Track State maps semantic Frame IDs to sprites and selects an explicit default frame.",
            MessageType.Info);

        for (int i = 0; i < trackStatesProperty.arraySize; i++)
        {
            if (DrawTrackState(trackStatesProperty.GetArrayElementAtIndex(i), i, library))
            {
                trackStatesProperty.DeleteArrayElementAtIndex(i);
                break;
            }
        }

        if (GUILayout.Button("Add Track State"))
        {
            AddTrackState(library);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool DrawTrackState(
        SerializedProperty trackState,
        int index,
        FaceSpriteLibrary library)
    {
        SerializedProperty trackIdProperty = trackState.FindPropertyRelative("trackId");
        SerializedProperty defaultFrameIdProperty = trackState.FindPropertyRelative("defaultFrameId");
        SerializedProperty framesProperty = trackState.FindPropertyRelative("frames");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        string title = string.IsNullOrWhiteSpace(trackIdProperty.stringValue)
            ? $"Track State {index}"
            : trackIdProperty.stringValue;
        trackState.isExpanded = EditorGUILayout.Foldout(trackState.isExpanded, title, true);

        if (trackState.isExpanded)
        {
            FaceEditorUtility.DrawIdPopup(
                trackIdProperty,
                "Track Id",
                FaceEditorUtility.GetTrackIds(library));

            string trackId = trackIdProperty.stringValue;
            DrawDefaultFrame(defaultFrameIdProperty, framesProperty);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Frame Mappings", EditorStyles.boldLabel);

            string[] spriteIds = FaceEditorUtility.GetSpriteIds(library, trackId);

            for (int i = 0; i < framesProperty.arraySize; i++)
            {
                if (DrawFrame(
                    framesProperty.GetArrayElementAtIndex(i),
                    i,
                    library,
                    trackId,
                    spriteIds))
                {
                    string removedFrameId = framesProperty
                        .GetArrayElementAtIndex(i)
                        .FindPropertyRelative("frameId")
                        .stringValue;
                    framesProperty.DeleteArrayElementAtIndex(i);

                    if (defaultFrameIdProperty.stringValue == removedFrameId)
                    {
                        defaultFrameIdProperty.stringValue = string.Empty;
                    }

                    break;
                }
            }

            if (GUILayout.Button("Add Frame Mapping"))
            {
                AddFrame(framesProperty, spriteIds);
            }

            if (FaceEditorUtility.DrawRemoveButton("Remove Track State"))
            {
                EditorGUILayout.EndVertical();
                return true;
            }
        }

        EditorGUILayout.EndVertical();
        return false;
    }

    private static void DrawDefaultFrame(
        SerializedProperty defaultFrameIdProperty,
        SerializedProperty framesProperty)
    {
        string[] frameIds = GetFrameIds(framesProperty);
        FaceEditorUtility.DrawIdPopup(
            defaultFrameIdProperty,
            "Default Frame Id",
            frameIds,
            false);

        if (string.IsNullOrWhiteSpace(defaultFrameIdProperty.stringValue))
        {
            EditorGUILayout.HelpBox("Default Frame ID is required.", MessageType.Error);
        }
        else if (System.Array.IndexOf(frameIds, defaultFrameIdProperty.stringValue) < 0)
        {
            EditorGUILayout.HelpBox(
                $"Default Frame ID '{defaultFrameIdProperty.stringValue}' is missing from the mappings.",
                MessageType.Error);
        }
    }

    private static bool DrawFrame(
        SerializedProperty frameProperty,
        int index,
        FaceSpriteLibrary library,
        string trackId,
        string[] spriteIds)
    {
        SerializedProperty frameIdProperty = frameProperty.FindPropertyRelative("frameId");
        SerializedProperty spriteIdProperty = frameProperty.FindPropertyRelative("spriteId");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Frame Mapping {index}", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();

        Rect previewRect = GUILayoutUtility.GetRect(
            PreviewSize,
            PreviewSize,
            GUILayout.Width(PreviewSize),
            GUILayout.Height(PreviewSize));
        FaceEditorUtility.DrawSpritePreview(
            previewRect,
            library,
            trackId,
            spriteIdProperty.stringValue);

        EditorGUILayout.BeginVertical();
        EditorGUILayout.PropertyField(frameIdProperty, new GUIContent("Semantic Frame Id"));
        FaceEditorUtility.DrawIdPopup(spriteIdProperty, "Sprite Id", spriteIds);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        bool remove = FaceEditorUtility.DrawRemoveButton("Remove Mapping");
        EditorGUILayout.EndVertical();
        return remove;
    }

    private void AddTrackState(FaceSpriteLibrary library)
    {
        int index = trackStatesProperty.arraySize;
        trackStatesProperty.InsertArrayElementAtIndex(index);
        SerializedProperty trackState = trackStatesProperty.GetArrayElementAtIndex(index);
        string[] trackIds = FaceEditorUtility.GetTrackIds(library);

        trackState.FindPropertyRelative("trackId").stringValue =
            trackIds.Length > 0 ? trackIds[0] : string.Empty;
        trackState.FindPropertyRelative("defaultFrameId").stringValue = string.Empty;
        trackState.FindPropertyRelative("frames").ClearArray();
        trackState.isExpanded = true;
    }

    private static void AddFrame(SerializedProperty framesProperty, string[] spriteIds)
    {
        int index = framesProperty.arraySize;
        framesProperty.InsertArrayElementAtIndex(index);
        SerializedProperty frame = framesProperty.GetArrayElementAtIndex(index);
        frame.FindPropertyRelative("frameId").stringValue = string.Empty;
        frame.FindPropertyRelative("spriteId").stringValue =
            spriteIds.Length > 0 ? spriteIds[0] : string.Empty;
    }

    private static string[] GetFrameIds(SerializedProperty framesProperty)
    {
        System.Collections.Generic.List<string> ids = new System.Collections.Generic.List<string>();

        for (int i = 0; i < framesProperty.arraySize; i++)
        {
            string id = framesProperty
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("frameId")
                .stringValue;

            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        return ids.ToArray();
    }
}
