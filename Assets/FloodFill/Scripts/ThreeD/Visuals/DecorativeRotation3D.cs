using UnityEngine;

namespace FloodFill.ThreeD
{
    public sealed class DecorativeRotation3D : MonoBehaviour
    {
        private void Update()
        {
            transform.Rotate(0f, 12f * Time.unscaledDeltaTime, 0f, Space.World);
        }
    }
}
