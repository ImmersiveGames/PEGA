using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FaceAnimation))]
public sealed class FaceAnimationEditor : Editor
{
    private const float PreviewSize = 52f;

    private SerializedProperty idProperty;
    private SerializedProperty playbackModeProperty;
    private SerializedProperty intervalProperty;
    private SerializedProperty templateStateProperty;
    private SerializedProperty spriteLibraryProperty;
    private SerializedProperty tracksProperty;

    private void OnEnable()
    {
        idProperty = serializedObject.FindProperty("id");
        playbackModeProperty = serializedObject.FindProperty("playbackMode");
        intervalProperty = serializedObject.FindProperty("interval");
        templateStateProperty = serializedObject.FindProperty("templateState");
        spriteLibraryProperty = serializedObject.FindProperty("spriteLibrary");
        tracksProperty = serializedObject.FindProperty("tracks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(idProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(playbackModeProperty);

        if (playbackModeProperty.enumValueIndex == (int)FaceAnimation.PlaybackMode.Interval)
        {
            EditorGUILayout.PropertyField(intervalProperty);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Authoring Sources", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(templateStateProperty);
        EditorGUILayout.PropertyField(spriteLibraryProperty);

        FaceState templateState = templateStateProperty.objectReferenceValue as FaceState;
        FaceSpriteLibrary spriteLibrary = spriteLibraryProperty.objectReferenceValue as FaceSpriteLibrary;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation Tracks", EditorStyles.boldLabel);

        for (int i = 0; i < tracksProperty.arraySize; i++)
        {
            if (DrawTrack(
                tracksProperty.GetArrayElementAtIndex(i),
                i,
                templateState,
                spriteLibrary))
            {
                tracksProperty.DeleteArrayElementAtIndex(i);
                break;
            }
        }

        if (GUILayout.Button("Add Animation Track"))
        {
            AddTrack(templateState, spriteLibrary);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool DrawTrack(
        SerializedProperty trackProperty,
        int index,
        FaceState templateState,
        FaceSpriteLibrary spriteLibrary)
    {
        SerializedProperty trackIdProperty = trackProperty.FindPropertyRelative("trackId");
        SerializedProperty enabledProperty = trackProperty.FindPropertyRelative("enabled");
        SerializedProperty sourceModeProperty = trackProperty.FindPropertyRelative("sourceMode");
        SerializedProperty stateFramesProperty = trackProperty.FindPropertyRelative("stateFrames");
        SerializedProperty spriteFramesProperty = trackProperty.FindPropertyRelative("spriteFrames");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        string title = string.IsNullOrWhiteSpace(trackIdProperty.stringValue)
            ? $"Animation Track {index}"
            : trackIdProperty.stringValue;
        trackProperty.isExpanded = EditorGUILayout.Foldout(trackProperty.isExpanded, title, true);

        if (trackProperty.isExpanded)
        {
            EditorGUILayout.PropertyField(enabledProperty);
            EditorGUILayout.PropertyField(sourceModeProperty, new GUIContent("Frame Source"));

            bool fromState =
                sourceModeProperty.enumValueIndex == (int)FaceAnimation.FrameSourceMode.FromState;
            string[] trackIds = fromState
                ? FaceEditorUtility.GetTrackIds(templateState)
                : FaceEditorUtility.GetTrackIds(spriteLibrary);

            FaceEditorUtility.DrawIdPopup(trackIdProperty, "Track Id", trackIds);
            string trackId = trackIdProperty.stringValue;

            if (fromState)
            {
                DrawStateFrames(stateFramesProperty, templateState, trackId);
            }
            else
            {
                DrawSpriteFrames(spriteFramesProperty, spriteLibrary, trackId);
            }

            if (FaceEditorUtility.DrawRemoveButton("Remove Track"))
            {
                EditorGUILayout.EndVertical();
                return true;
            }
        }

        EditorGUILayout.EndVertical();
        return false;
    }

    private static void DrawStateFrames(
        SerializedProperty framesProperty,
        FaceState templateState,
        string trackId)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("FromState Frames", EditorStyles.boldLabel);

        if (templateState == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Template State to select semantic Frame IDs.",
                MessageType.Warning);
        }

        string[] frameIds = FaceEditorUtility.GetStateFrameIds(templateState, trackId);

        for (int i = 0; i < framesProperty.arraySize; i++)
        {
            SerializedProperty frame = framesProperty.GetArrayElementAtIndex(i);
            SerializedProperty frameId = frame.FindPropertyRelative("frameId");
            SerializedProperty duration = frame.FindPropertyRelative("duration");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            Rect previewRect = GUILayoutUtility.GetRect(
                PreviewSize,
                PreviewSize,
                GUILayout.Width(PreviewSize),
                GUILayout.Height(PreviewSize));
            FaceEditorUtility.DrawStatePreview(
                previewRect,
                templateState,
                trackId,
                frameId.stringValue);

            EditorGUILayout.BeginVertical();
            FaceEditorUtility.DrawIdPopup(frameId, "Frame Id", frameIds);
            EditorGUILayout.PropertyField(duration);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            bool remove = FaceEditorUtility.DrawRemoveButton("Remove Frame");
            EditorGUILayout.EndVertical();

            if (remove)
            {
                framesProperty.DeleteArrayElementAtIndex(i);
                break;
            }
        }

        if (GUILayout.Button("Add FromState Frame"))
        {
            int index = framesProperty.arraySize;
            framesProperty.InsertArrayElementAtIndex(index);
            SerializedProperty frame = framesProperty.GetArrayElementAtIndex(index);
            frame.FindPropertyRelative("frameId").stringValue =
                frameIds.Length > 0 ? frameIds[0] : string.Empty;
            frame.FindPropertyRelative("duration").floatValue = 0.1f;
        }
    }

    private static void DrawSpriteFrames(
        SerializedProperty framesProperty,
        FaceSpriteLibrary library,
        string trackId)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Custom Library Frames", EditorStyles.boldLabel);

        if (library == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a FaceSpriteLibrary to select custom Sprite IDs.",
                MessageType.Warning);
        }

        string[] spriteIds = FaceEditorUtility.GetSpriteIds(library, trackId);

        for (int i = 0; i < framesProperty.arraySize; i++)
        {
            SerializedProperty frame = framesProperty.GetArrayElementAtIndex(i);
            SerializedProperty spriteId = frame.FindPropertyRelative("spriteId");
            SerializedProperty duration = frame.FindPropertyRelative("duration");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
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
                spriteId.stringValue);

            EditorGUILayout.BeginVertical();
            FaceEditorUtility.DrawIdPopup(spriteId, "Sprite Id", spriteIds);
            EditorGUILayout.PropertyField(duration);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            bool remove = FaceEditorUtility.DrawRemoveButton("Remove Frame");
            EditorGUILayout.EndVertical();

            if (remove)
            {
                framesProperty.DeleteArrayElementAtIndex(i);
                break;
            }
        }

        if (GUILayout.Button("Add Custom Frame"))
        {
            int index = framesProperty.arraySize;
            framesProperty.InsertArrayElementAtIndex(index);
            SerializedProperty frame = framesProperty.GetArrayElementAtIndex(index);
            frame.FindPropertyRelative("spriteId").stringValue =
                spriteIds.Length > 0 ? spriteIds[0] : string.Empty;
            frame.FindPropertyRelative("duration").floatValue = 0.1f;
        }
    }

    private void AddTrack(FaceState templateState, FaceSpriteLibrary spriteLibrary)
    {
        int index = tracksProperty.arraySize;
        tracksProperty.InsertArrayElementAtIndex(index);
        SerializedProperty track = tracksProperty.GetArrayElementAtIndex(index);
        string[] trackIds = GetCombinedTrackIds(templateState, spriteLibrary);

        track.FindPropertyRelative("trackId").stringValue =
            trackIds.Length > 0 ? trackIds[0] : string.Empty;
        track.FindPropertyRelative("enabled").boolValue = true;
        track.FindPropertyRelative("sourceMode").enumValueIndex =
            (int)FaceAnimation.FrameSourceMode.FromState;
        track.FindPropertyRelative("stateFrames").ClearArray();
        track.FindPropertyRelative("spriteFrames").ClearArray();
        track.isExpanded = true;
    }

    private static string[] GetCombinedTrackIds(
        FaceState templateState,
        FaceSpriteLibrary spriteLibrary)
    {
        HashSet<string> ids = new HashSet<string>(FaceEditorUtility.GetTrackIds(templateState));
        ids.UnionWith(FaceEditorUtility.GetTrackIds(spriteLibrary));
        string[] result = new string[ids.Count];
        ids.CopyTo(result);
        return result;
    }
}
