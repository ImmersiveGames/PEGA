using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class FaceSpriteLibrary
{
    [Serializable]
    public sealed class SpriteTrack
    {
        [SerializeField]
        private string trackId;

        [SerializeField]
        private Texture2D atlas;

        [SerializeField]
        private Vector2Int gridSize = new Vector2Int(4, 4);

        [SerializeField]
        private List<SpriteEntry> sprites = new List<SpriteEntry>();

        private Dictionary<string, Vector2Int> spriteLookup;

        public string TrackId => trackId;
        public Texture2D Atlas => atlas;
        public Vector2Int GridSize => gridSize;
        public IReadOnlyList<SpriteEntry> Sprites => sprites;

        public bool TryGetCoordinate(string spriteId, out Vector2Int coordinate)
        {
            coordinate = Vector2Int.zero;

            if (spriteLookup == null)
            {
                BuildLookup();
            }

            return !string.IsNullOrWhiteSpace(spriteId)
                && spriteLookup.TryGetValue(spriteId, out coordinate);
        }

        internal void BuildLookup()
        {
            spriteLookup = new Dictionary<string, Vector2Int>();

            if (sprites == null)
            {
                return;
            }

            for (int i = 0; i < sprites.Count; i++)
            {
                SpriteEntry sprite = sprites[i];

                if (string.IsNullOrWhiteSpace(sprite.Id) || spriteLookup.ContainsKey(sprite.Id))
                {
                    continue;
                }

                spriteLookup.Add(sprite.Id, sprite.AtlasCoordinate);
            }
        }

        internal void Validate(string label, List<string> errors)
        {
            if (atlas == null)
            {
                errors.Add($"{label} is missing its atlas.");
            }

            if (gridSize.x <= 0 || gridSize.y <= 0)
            {
                errors.Add($"{label} grid size must be greater than zero.");
            }

            if (sprites == null || sprites.Count == 0)
            {
                errors.Add($"{label} has no sprite entries.");
                return;
            }

            HashSet<string> spriteIds = new HashSet<string>();

            for (int i = 0; i < sprites.Count; i++)
            {
                SpriteEntry sprite = sprites[i];

                if (string.IsNullOrWhiteSpace(sprite.Id))
                {
                    errors.Add($"{label} sprite {i} has an empty Sprite ID.");
                }
                else if (!spriteIds.Add(sprite.Id))
                {
                    errors.Add($"{label} has duplicate Sprite ID '{sprite.Id}'.");
                }

                if (gridSize.x > 0 && gridSize.y > 0
                    && (sprite.AtlasCoordinate.x < 0
                        || sprite.AtlasCoordinate.y < 0
                        || sprite.AtlasCoordinate.x >= gridSize.x
                        || sprite.AtlasCoordinate.y >= gridSize.y))
                {
                    errors.Add($"{label} sprite '{sprite.Id}' has coordinates outside the atlas grid.");
                }
            }
        }
    }

    [Serializable]
    public struct SpriteEntry
    {
        [SerializeField]
        private string id;

        [SerializeField]
        private Vector2Int atlasCoordinate;

        public string Id => id;
        public Vector2Int AtlasCoordinate => atlasCoordinate;
    }
}
