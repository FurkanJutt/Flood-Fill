using System;
using UnityEngine;

namespace FloodFill.ThreeD
{
    [CreateAssetMenu(menuName = "Flood Fill/UI Skin")]
    public sealed class FloodUISkin : ScriptableObject
    {
        [Serializable] public struct Entry { public string key; public Sprite sprite; }
        [SerializeField] private Entry[] entries;
        public Sprite Find(string key)
        {
            if (entries != null)
                foreach (Entry entry in entries) if (entry.key == key) return entry.sprite;
            return null;
        }
    }
}
