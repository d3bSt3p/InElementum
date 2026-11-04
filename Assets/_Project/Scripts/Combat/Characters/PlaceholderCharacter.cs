using InElementum.Core.Utils;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Combat.Characters
{
    /// <summary>
    /// Stands in for a character with no prefab yet: its animated sprite set when it has one, otherwise a block in
    /// its element's color.
    /// </summary>
    public class PlaceholderCharacter : CharacterBase
    {
        private static Sprite _square;

        public static PlaceholderCharacter Create(string displayName, Element element, bool isEnemy, Transform parent,
            CharacterSpriteSet spriteSet = null)
        {
            var root = new GameObject($"{displayName} (placeholder)");
            root.transform.SetParent(parent, false);

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var renderer = body.AddComponent<SpriteRenderer>();
            if (spriteSet != null)
            {
                // The sheets face right, toward the enemy side; CharacterBase turns enemies around on Bind.
                body.AddComponent<SpriteAnimator>().SpriteSet = spriteSet;
            }
            else
            {
                body.transform.localScale = isEnemy ? new Vector3(1.4f, 1.4f, 1f) : new Vector3(0.9f, 1.6f, 1f);
                renderer.sprite = Square;
                renderer.color = ElementColor(element);
            }

            var textRoot = new GameObject("Text Root");
            textRoot.transform.SetParent(root.transform, false);
            textRoot.transform.localPosition = new Vector3(0f, 1.3f, 0f);

            return root.AddComponent<PlaceholderCharacter>();
        }

        public static Color ElementColor(Element element)
        {
            switch (element)
            {
                case Element.Fire: return new Color(0.95f, 0.35f, 0.2f);
                case Element.Water: return new Color(0.2f, 0.5f, 0.95f);
                case Element.Wind: return new Color(0.5f, 0.9f, 0.6f);
                case Element.Lightning: return new Color(0.95f, 0.85f, 0.2f);
                case Element.Earth: return new Color(0.6f, 0.45f, 0.25f);
                case Element.Dark: return new Color(0.45f, 0.25f, 0.6f);
                case Element.Light: return new Color(1f, 0.95f, 0.75f);
                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        private static Sprite Square
        {
            get
            {
                if (_square != null) return _square;

                var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                _square = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0f), 1f);
                return _square;
            }
        }
    }
}
