using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class Camera2D
{
    readonly Viewport _vp;
    public Vector2 Position = Vector2.Zero;   // центр камеры в мировых координатах
    public float ZoomX = 1f;                  // масштаб по X
    public float ZoomY = 1f;                  // масштаб по Y
    public float Rotation = 0f;
    public Vector2 Origin;                    // центр экрана
    public Rectangle? Bounds;

    public Camera2D(Viewport viewport, float zoomX, float zoomY, float rot)
    {
        _vp = viewport;
        Origin = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
        ZoomX = zoomX;
        ZoomY = zoomY;
        Rotation = MathHelper.ToRadians(rot);
    }

    public Matrix GetMatrix()
    {
        return Matrix.CreateTranslation(-Position.X, -Position.Y, 0f)
             * Matrix.CreateRotationZ(Rotation)
             * Matrix.CreateScale(ZoomX, ZoomY, 1f)
             * Matrix.CreateTranslation(Origin.X, Origin.Y, 0f);
    }

    public Vector2 WorldToScreen(Vector2 worldPos)
    {
        var m = GetMatrix();
        return Vector2.Transform(worldPos, m);
    }

    public Vector2 ScreenToWorld(Vector2 screenPos)
    {
        Matrix m = GetMatrix();
        Matrix.Invert(ref m, out Matrix inv);
        return Vector2.Transform(screenPos, inv);
    }
}
