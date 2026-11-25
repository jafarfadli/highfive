using UnityEngine;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// Where the face features sit, shared by the head mesh (which sculpts eye sockets, nose, lips and chin
    /// there) and the face painter in the editor (which paints them at the same spots). Coordinates are rig
    /// units relative to the head centre; the face texture is a front projection of the head:
    /// u = 0.5 + x / (2 · Rx), v = 0.5 + y / (2 · Ry).
    /// </summary>
    public static class FaceMap
    {
        // head ellipsoid (rig units: the rig is 1.9 tall) and its centre above the neck joint
        public const float Rx = 0.092f, Ry = 0.123f, Rz = 0.101f;
        public const float CenterUp = 0.17f, CenterForward = 0.012f;

        public const float EyeX = 0.0325f, EyeY = 0.01f, EyeHalfW = 0.0148f, EyeHalfH = 0.006f;
        public const float BrowY = 0.035f, BrowInnerX = 0.011f, BrowOuterX = 0.054f;
        public const float BridgeTopY = 0.02f, NoseTipY = -0.03f, NoseBaseY = -0.038f, NoseHalfW = 0.0135f;
        public const float MouthY = -0.06f, MouthHalfW = 0.0185f;
        public const float ChinY = -0.112f;

        public static Vector2 ToUV(float x, float y) => new(0.5f + x / (2f * Rx), 0.5f + y / (2f * Ry));
        public static Vector2 FromUV(float u, float v) => new((u - 0.5f) * 2f * Rx, (v - 0.5f) * 2f * Ry);
    }
}
