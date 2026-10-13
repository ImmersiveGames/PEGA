using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FaceSpriteLibrary))]
public sealed class FaceSpriteLibraryEditor : Editor
{
    private const float PreviewSize = 56f;
    private SerializedProperty tracksProperty;

    private void OnEnable()
    {
        tracksProperty = serializedObject.FindProperty("tracks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Face Sprite Library", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Each Track ID owns an atlas, grid size, and dynamic sprite list.",
            MessageType.Info);

        for (int i = 0; i < tracksProperty.arraySize; i++)
        {
            if (DrawTrack(tracksProperty.GetArrayElementAtIndex(i), i))
            {
                tracksProperty.DeleteArrayElementAtIndex(i);
                break;
            }
        }

        if (GUILayout.Button("Add Track"))
        {
            AddTrack();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool DrawTrack(SerializedProperty trackProperty, int index)
    {
        SerializedProperty trackIdProperty = trackProperty.FindPropertyRelative("trackId");
        SerializedProperty atlasProperty = trackProperty.FindPropertyRelative("atlas");
        SerializedProperty gridSizeProperty = trackProperty.FindPropertyRelative("gridSize");
        SerializedProperty spritesProperty = trackProperty.FindPropertyRelative("sprites");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        string title = string.IsNullOrWhiteSpace(trackIdProperty.stringValue)
            ? $"Track {index}"
            : trackIdProperty.stringValue;
        trackProperty.isExpanded = EditorGUILayout.Foldout(trackProperty.isExpanded, title, true);

        if (trackProperty.isExpanded)
        {
            EditorGUILayout.PropertyField(trackIdProperty, new GUIContent("Track Id"));
            EditorGUILayout.PropertyField(atlasProperty);
            EditorGUILayout.PropertyField(gridSizeProperty);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sprite Entries", EditorStyles.boldLabel);

            for (int i = 0; i < spritesProperty.arraySize; i++)
            {
                if (DrawSpriteEntry(
                    spritesProperty.GetArrayElementAtIndex(i),
                    i,
                    atlasProperty.objectReferenceValue as Texture2D,
                    gridSizeProperty.vector2IntValue))
                {
                    spritesProperty.DeleteArrayElementAtIndex(i);
                    break;
                }
            }

            if (GUILayout.Button("Add Sprite Entry"))
            {
                AddSprite(spritesProperty);
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

    private static bool DrawSpriteEntry(
        SerializedProperty spriteProperty,
        int index,
        Texture2D atlas,
        Vector2Int gridSize)
    {
        SerializedProperty idProperty = spriteProperty.FindPropertyRelative("id");
        SerializedProperty coordinateProperty = spriteProperty.FindPropertyRelative("atlasCoordinate");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Sprite {index}", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();

        Rect previewRect = GUILayoutUtility.GetRect(
            PreviewSize,
            PreviewSize,
            GUILayout.Width(PreviewSize),
            GUILayout.Height(PreviewSize));
        FaceEditorUtility.DrawTexturePreview(
            previewRect,
            atlas,
            gridSize,
            coordinateProperty.vector2IntValue);

        EditorGUILayout.BeginVertical();
        EditorGUILayout.PropertyField(idProperty, new GUIContent("Sprite Id"));
        EditorGUILayout.PropertyField(coordinateProperty, new GUIContent("Atlas Coordinate"));
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        bool remove = FaceEditorUtility.DrawRemoveButton("Remove Sprite");
        EditorGUILayout.EndVertical();
        return remove;
    }

    private void AddTrack()
    {
        int index = tracksProperty.arraySize;
        tracksProperty.InsertArrayElementAtIndex(index);
        SerializedProperty track = tracksProperty.GetArrayElementAtIndex(index);
        track.FindPropertyRelative("trackId").stringValue = string.Empty;
        track.FindPropertyRelative("atlas").objectReferenceValue = null;
        track.FindPropertyRelative("gridSize").vector2IntValue = new Vector2Int(4, 4);
        track.FindPropertyRelative("sprites").ClearArray();
        track.isExpanded = true;
    }

    private static void AddSprite(SerializedProperty spritesProperty)
    {
        int index = spritesProperty.arraySize;
        spritesProperty.InsertArrayElementAtIndex(index);
        SerializedProperty sprite = spritesProperty.GetArrayElementAtIndex(index);
        sprite.FindPropertyRelative("id").stringValue = string.Empty;
        sprite.FindPropertyRelative("atlasCoordinate").vector2IntValue = Vector2Int.zero;
    }
}
