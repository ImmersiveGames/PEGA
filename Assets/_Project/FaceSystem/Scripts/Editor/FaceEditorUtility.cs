using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class FaceEditorUtility
{
    public static string[] GetTrackIds(FaceSpriteLibrary library)
    {
        if (library == null)
        {
            return Array.Empty<string>();
        }

        List<string> ids = new List<string>();

        for (int i = 0; i < library.Tracks.Count; i++)
        {
            FaceSpriteLibrary.SpriteTrack track = library.Tracks[i];

            if (track != null && !string.IsNullOrWhiteSpace(track.TrackId))
            {
                ids.Add(track.TrackId);
            }
        }

        return ids.ToArray();
    }

    public static string[] GetTrackIds(FaceState state)
    {
        if (state == null)
        {
            return Array.Empty<string>();
        }

        List<string> ids = new List<string>();

        for (int i = 0; i < state.TrackStates.Count; i++)
        {
            FaceState.TrackState track = state.TrackStates[i];

            if (track != null && !string.IsNullOrWhiteSpace(track.TrackId))
            {
                ids.Add(track.TrackId);
            }
        }

        return ids.ToArray();
    }

    public static string[] GetSpriteIds(FaceSpriteLibrary library, string trackId)
    {
        if (library == null || !library.TryGetTrack(trackId, out FaceSpriteLibrary.SpriteTrack track))
        {
            return Array.Empty<string>();
        }

        List<string> ids = new List<string>();

        for (int i = 0; i < track.Sprites.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(track.Sprites[i].Id))
            {
                ids.Add(track.Sprites[i].Id);
            }
        }

        return ids.ToArray();
    }

    public static string[] GetStateFrameIds(FaceState state, string trackId)
    {
        if (state == null || !state.TryGetTrackState(trackId, out FaceState.TrackState trackState))
        {
            return Array.Empty<string>();
        }

        List<string> ids = new List<string>();

        for (int i = 0; i < trackState.Frames.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(trackState.Frames[i].FrameId))
            {
                ids.Add(trackState.Frames[i].FrameId);
            }
        }

        return ids.ToArray();
    }

    public static void DrawIdPopup(
        SerializedProperty property,
        string label,
        string[] ids,
        bool allowManualEntry = true)
    {
        string currentValue = property.stringValue;

        if (ids.Length == 0)
        {
            if (allowManualEntry)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label));
            }
            else
            {
                EditorGUILayout.HelpBox($"No values are available for {label}.", MessageType.Warning);
            }

            return;
        }

        int currentIndex = Array.IndexOf(ids, currentValue);
        string[] options = ids;

        if (currentIndex < 0)
        {
            options = new string[ids.Length + 1];
            options[0] = string.IsNullOrWhiteSpace(currentValue)
                ? "<Select>"
                : $"<Missing: {currentValue}>";
            ids.CopyTo(options, 1);
            currentIndex = 0;
        }

        int selectedIndex = EditorGUILayout.Popup(label, currentIndex, options);

        if (selectedIndex > 0 || options.Length == ids.Length)
        {
            property.stringValue = options[selectedIndex];
        }
    }

    public static void DrawStatePreview(Rect rect, FaceState state, string trackId, string frameId)
    {
        DrawPreviewBackground(rect);

        if (state != null
            && state.SpriteLibrary != null
            && state.TryGetCoordinate(trackId, frameId, out Vector2Int coordinate))
        {
            DrawAtlasPreview(rect, state.SpriteLibrary, trackId, coordinate);
        }
    }

    public static void DrawSpritePreview(Rect rect, FaceSpriteLibrary library, string trackId, string spriteId)
    {
        DrawPreviewBackground(rect);

        if (library != null && library.TryGetCoordinate(trackId, spriteId, out Vector2Int coordinate))
        {
            DrawAtlasPreview(rect, library, trackId, coordinate);
        }
    }

    public static void DrawTexturePreview(
        Rect rect,
        Texture2D atlas,
        Vector2Int gridSize,
        Vector2Int coordinate)
    {
        DrawPreviewBackground(rect);

        if (atlas == null || gridSize.x <= 0 || gridSize.y <= 0)
        {
            return;
        }

        GUI.DrawTextureWithTexCoords(rect, atlas, GetUvRect(gridSize, coordinate), true);
    }

    public static bool DrawRemoveButton(string label = "Remove")
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        bool clicked = GUILayout.Button(label, GUILayout.Width(110f));
        EditorGUILayout.EndHorizontal();
        return clicked;
    }

    private static void DrawAtlasPreview(
        Rect rect,
        FaceSpriteLibrary library,
        string trackId,
        Vector2Int coordinate)
    {
        if (library != null && library.TryGetTrack(trackId, out FaceSpriteLibrary.SpriteTrack track))
        {
            DrawTexturePreview(rect, track.Atlas, track.GridSize, coordinate);
        }
    }

    private static void DrawPreviewBackground(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
    }

    private static Rect GetUvRect(Vector2Int gridSize, Vector2Int coordinate)
    {
        float tileWidth = 1f / gridSize.x;
        float tileHeight = 1f / gridSize.y;

        return new Rect(
            coordinate.x * tileWidth,
            1f - ((coordinate.y + 1) * tileHeight),
            tileWidth,
            tileHeight);
    }
}
