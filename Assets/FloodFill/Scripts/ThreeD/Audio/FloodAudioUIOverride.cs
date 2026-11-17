using UnityEngine;

namespace FloodFill.ThreeD.Audio
{
    /// <summary>Optional exception for unusual controls. Ordinary controls need no component.</summary>
    [DisallowMultipleComponent]
    public sealed class FloodAudioUIOverride : MonoBehaviour
    {
        public bool silent;
        public FloodSound sound = FloodSound.ButtonClick;
    }
}
