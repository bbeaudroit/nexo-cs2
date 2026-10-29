using System.Numerics;

namespace CsEsp;
public static class Projection
{
    public static bool ToScreen(float[] m, Vector3 p, int width, int height, out Vector2 screen)
    {
        screen = default;
        if (m.Length != 16 || width <= 0 || height <= 0) return false;
        float w = m[12]*p.X + m[13]*p.Y + m[14]*p.Z + m[15];
        if (!float.IsFinite(w) || w < .01f) return false;
        float x = m[0]*p.X + m[1]*p.Y + m[2]*p.Z + m[3];
        float y = m[4]*p.X + m[5]*p.Y + m[6]*p.Z + m[7];
        screen = new((1+x/w)*width*.5f, (1-y/w)*height*.5f);
        return float.IsFinite(screen.X) && float.IsFinite(screen.Y);
    }
}
