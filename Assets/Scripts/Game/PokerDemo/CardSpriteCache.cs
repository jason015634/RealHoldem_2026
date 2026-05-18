using System.Collections.Generic;
using UnityEngine;

internal static class CardSpriteCache
{
    private const string CardResourcesRoot = "Sprites/Cards";
    private static readonly Dictionary<string, Sprite> spritesByPath = new Dictionary<string, Sprite>(64);
    private static bool preloaded;

    public static Sprite Get(Card card)
    {
        return Get(card.ResourcePath);
    }

    public static Sprite GetBack()
    {
        return Get($"{CardResourcesRoot}/back");
    }

    public static Sprite Get(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return null;
        }

        EnsurePreloaded();
        if (spritesByPath.TryGetValue(resourcePath, out Sprite sprite))
        {
            return sprite;
        }

        sprite = Resources.Load<Sprite>(resourcePath);
        spritesByPath[resourcePath] = sprite;
        return sprite;
    }

    private static void EnsurePreloaded()
    {
        if (preloaded)
        {
            return;
        }

        Sprite[] loadedSprites = Resources.LoadAll<Sprite>(CardResourcesRoot);
        for (int i = 0; i < loadedSprites.Length; i++)
        {
            Sprite sprite = loadedSprites[i];
            if (sprite == null)
            {
                continue;
            }

            spritesByPath[$"{CardResourcesRoot}/{sprite.name}"] = sprite;
        }

        preloaded = true;
    }
}
