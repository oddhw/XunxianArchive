using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using XunxianDpkViewer.Models;

namespace XunxianDpkViewer.Controls;

public enum ModelRenderMode
{
    Textured,
    Solid,
    Wireframe
}

public sealed partial class ModelPreviewControl : UserControl
{
    private const int MaximumRasterDimension = 900;
    private IReadOnlyList<ModelRenderPart> _parts = Array.Empty<ModelRenderPart>();
    private string _modelName = string.Empty;
    private ModelRenderMode _mode = ModelRenderMode.Solid;
    private float _yaw = -0.55f;
    private float _pitch = 0.28f;
    private float _zoom = 0.86f;
    private Windows.Foundation.Point _lastPointer;
    private uint? _capturedPointerId;
    private bool _renderQueued;

    public ModelPreviewControl()
    {
        InitializeComponent();
    }

    public void SetMesh(PmfMesh? mesh)
    {
        _parts = mesh is null
            ? Array.Empty<ModelRenderPart>()
            : new[] { new ModelRenderPart(string.Empty, mesh, null, string.Empty) };
        _modelName = string.Empty;
        _yaw = -0.55f;
        _pitch = 0.28f;
        _zoom = 0.86f;
        TextureModeButton.IsEnabled = false;
        SetMode(ModelRenderMode.Solid);
        EmptyText.Visibility = mesh is null ? Visibility.Visible : Visibility.Collapsed;
        UpdateHint();
        ScheduleRender();
    }

    public void SetComposite(IReadOnlyList<ModelRenderPart> parts, string modelName)
    {
        _parts = parts;
        _modelName = modelName;
        _yaw = -0.55f;
        _pitch = 0.28f;
        _zoom = 0.86f;
        TextureModeButton.IsEnabled = parts.Any(part => part.Texture is not null);
        SetMode(TextureModeButton.IsEnabled ? ModelRenderMode.Textured : ModelRenderMode.Solid);
        EmptyText.Visibility = parts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateHint();
        ScheduleRender();
    }

    public void SetTexture(DecodedTexture? texture, string? textureName, bool selectTexturedMode = true)
    {
        if (_parts.Count == 0) return;
        ModelRenderPart part = _parts[0];
        _parts = new[] { part with { Texture = texture, TextureName = textureName ?? string.Empty } };
        TextureModeButton.IsEnabled = texture is not null;
        if (texture is not null && selectTexturedMode) SetMode(ModelRenderMode.Textured);
        else if (texture is null && _mode == ModelRenderMode.Textured) SetMode(ModelRenderMode.Solid);
        UpdateHint();
        ScheduleRender();
    }

    private void SetMode(ModelRenderMode mode)
    {
        if (mode == ModelRenderMode.Textured && !_parts.Any(part => part.Texture is not null)) mode = ModelRenderMode.Solid;
        _mode = mode;
        TextureModeButton.IsChecked = mode == ModelRenderMode.Textured;
        SolidModeButton.IsChecked = mode == ModelRenderMode.Solid;
        WireframeModeButton.IsChecked = mode == ModelRenderMode.Wireframe;
        UpdateHint();
        ScheduleRender();
    }

    private void UpdateHint()
    {
        if (_parts.Count == 0)
        {
            HintText.Text = "模型预览 · 拖拽旋转 · 滚轮缩放";
            return;
        }

        string mode = _mode switch
        {
            ModelRenderMode.Textured when _parts.Count == 1 && !string.IsNullOrWhiteSpace(_parts[0].TextureName) => $"贴图 · {_parts[0].TextureName}",
            ModelRenderMode.Textured => "贴图",
            ModelRenderMode.Wireframe => "线框",
            _ => "实体着色"
        };
        int vertices = _parts.Sum(part => part.Mesh.Vertices.Count);
        long triangles = _parts.Sum(part => (long)part.Mesh.DeclaredTriangleCount);
        string parts = _parts.Count > 1 ? $" · {_parts.Count:N0} 部件" : string.Empty;
        string name = string.IsNullOrWhiteSpace(_modelName) ? string.Empty : $"{_modelName} · ";
        HintText.Text = $"{name}{mode}{parts} · {vertices:N0} 顶点 · {triangles:N0} 面";
    }

    private void ScheduleRender()
    {
        if (_renderQueued) return;
        _renderQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _renderQueued = false;
            RenderMesh();
        });
    }

    private void RenderMesh()
    {
        if (_parts.Count == 0 || Surface.ActualWidth < 20 || Surface.ActualHeight < 20)
        {
            RasterImage.Source = null;
            return;
        }

        double rasterScale = Math.Min(1d, MaximumRasterDimension / Math.Max(Surface.ActualWidth, Surface.ActualHeight));
        int width = Math.Max(1, (int)Math.Round(Surface.ActualWidth * rasterScale));
        int height = Math.Max(1, (int)Math.Round(Surface.ActualHeight * rasterScale));
        byte[] pixels = RenderPixels(_parts, _mode, width, height);

        var bitmap = new WriteableBitmap(width, height);
        using (Stream stream = bitmap.PixelBuffer.AsStream())
            stream.Write(pixels, 0, pixels.Length);
        bitmap.Invalidate();
        RasterImage.Source = bitmap;
    }

    private byte[] RenderPixels(IReadOnlyList<ModelRenderPart> parts, ModelRenderMode mode, int width, int height)
    {
        byte[] pixels = new byte[checked(width * height * 4)];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 32;
            pixels[i + 1] = 21;
            pixels[i + 2] = 13;
            pixels[i + 3] = 255;
        }

        Vector3 min = parts[0].Mesh.Vertices[0];
        Vector3 max = min;
        foreach (Vector3 vertex in parts.SelectMany(part => part.Mesh.Vertices))
        {
            min = Vector3.Min(min, vertex);
            max = Vector3.Max(max, vertex);
        }

        Vector3 center = (min + max) * 0.5f;
        float extent = Math.Max(0.0001f, Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z)));
        Matrix4x4 rotation = Matrix4x4.CreateRotationY(_yaw) * Matrix4x4.CreateRotationX(_pitch);
        float scale = Math.Min(width, height) * 0.82f / extent * _zoom;
        float centerX = width * 0.5f;
        float centerY = height * 0.52f;
        float[]? depthBuffer = mode == ModelRenderMode.Wireframe
            ? null
            : Enumerable.Repeat(float.NegativeInfinity, width * height).ToArray();
        Vector3 light = Vector3.Normalize(new Vector3(-0.35f, 0.65f, 0.9f));
        foreach (ModelRenderPart part in parts)
        {
            PmfMesh mesh = part.Mesh;
            IReadOnlyList<Vector3> source = mesh.Vertices;
            var transformed = new Vector3[source.Count];
            var projected = new Vector2[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                transformed[i] = Vector3.Transform(source[i] - center, rotation);
                projected[i] = new Vector2(centerX + transformed[i].X * scale, centerY - transformed[i].Y * scale);
            }

            IReadOnlyList<ushort> indices = mesh.Indices;
            if (mode == ModelRenderMode.Wireframe)
            {
                for (int offset = 0; offset + 2 < indices.Count; offset += 3)
                {
                    Vector2 a = projected[indices[offset]];
                    Vector2 b = projected[indices[offset + 1]];
                    Vector2 c = projected[indices[offset + 2]];
                    DrawLine(pixels, width, height, a, b);
                    DrawLine(pixels, width, height, b, c);
                    DrawLine(pixels, width, height, c, a);
                }
                continue;
            }

            bool useTexture = mode == ModelRenderMode.Textured && part.Texture is not null && mesh.TextureCoordinates.Count == source.Count;
            for (int offset = 0; offset + 2 < indices.Count; offset += 3)
            {
                int ia = indices[offset];
                int ib = indices[offset + 1];
                int ic = indices[offset + 2];
                Vector3 normal = Vector3.Cross(transformed[ib] - transformed[ia], transformed[ic] - transformed[ia]);
                if (normal.LengthSquared() < 0.0000001f) continue;
                normal = Vector3.Normalize(normal);
                float brightness = 0.28f + 0.72f * MathF.Abs(Vector3.Dot(normal, light));
                RasterizeTriangle(
                    pixels, depthBuffer!, width, height,
                    projected[ia], projected[ib], projected[ic],
                    transformed[ia].Z, transformed[ib].Z, transformed[ic].Z,
                    useTexture ? mesh.TextureCoordinates[ia] : default,
                    useTexture ? mesh.TextureCoordinates[ib] : default,
                    useTexture ? mesh.TextureCoordinates[ic] : default,
                    useTexture ? part.Texture : null,
                    brightness);
            }
        }
        return pixels;
    }

    private static void RasterizeTriangle(
        byte[] pixels,
        float[] depthBuffer,
        int width,
        int height,
        Vector2 a,
        Vector2 b,
        Vector2 c,
        float za,
        float zb,
        float zc,
        Vector2 uva,
        Vector2 uvb,
        Vector2 uvc,
        DecodedTexture? texture,
        float brightness)
    {
        float area = Edge(a, b, c);
        if (MathF.Abs(area) < 0.0001f) return;
        int minX = Math.Clamp((int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))), 0, width - 1);
        int maxX = Math.Clamp((int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))), 0, width - 1);
        int minY = Math.Clamp((int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))), 0, height - 1);
        int maxY = Math.Clamp((int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))), 0, height - 1);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                float wa = Edge(b, c, point) / area;
                float wb = Edge(c, a, point) / area;
                float wc = Edge(a, b, point) / area;
                if (wa < -0.0001f || wb < -0.0001f || wc < -0.0001f) continue;

                int pixelIndex = y * width + x;
                float depth = wa * za + wb * zb + wc * zc;
                if (depth <= depthBuffer[pixelIndex]) continue;

                byte blue;
                byte green;
                byte red;
                byte alpha = 255;
                if (texture is not null)
                {
                    Vector2 uv = wa * uva + wb * uvb + wc * uvc;
                    float wrappedU = uv.X - MathF.Floor(uv.X);
                    float wrappedV = uv.Y - MathF.Floor(uv.Y);
                    int textureX = Math.Clamp((int)(wrappedU * texture.Width), 0, texture.Width - 1);
                    int textureY = Math.Clamp((int)(wrappedV * texture.Height), 0, texture.Height - 1);
                    int textureOffset = (textureY * texture.Width + textureX) * 4;
                    blue = (byte)(texture.BgraPixels[textureOffset] * brightness);
                    green = (byte)(texture.BgraPixels[textureOffset + 1] * brightness);
                    red = (byte)(texture.BgraPixels[textureOffset + 2] * brightness);
                    alpha = texture.BgraPixels[textureOffset + 3];
                    if (alpha < 8) continue;
                }
                else
                {
                    blue = (byte)(214 * brightness);
                    green = (byte)(151 * brightness);
                    red = (byte)(76 * brightness);
                }

                depthBuffer[pixelIndex] = depth;
                int target = pixelIndex * 4;
                if (alpha >= 250)
                {
                    pixels[target] = blue;
                    pixels[target + 1] = green;
                    pixels[target + 2] = red;
                }
                else
                {
                    int inverse = 255 - alpha;
                    pixels[target] = (byte)((blue * alpha + pixels[target] * inverse) / 255);
                    pixels[target + 1] = (byte)((green * alpha + pixels[target + 1] * inverse) / 255);
                    pixels[target + 2] = (byte)((red * alpha + pixels[target + 2] * inverse) / 255);
                }
            }
        }
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 point) =>
        (point.X - a.X) * (b.Y - a.Y) - (point.Y - a.Y) * (b.X - a.X);

    private static void DrawLine(byte[] pixels, int width, int height, Vector2 start, Vector2 end)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(start, end)));
        for (int i = 0; i <= steps; i++)
        {
            float amount = i / (float)steps;
            int x = (int)MathF.Round(start.X + (end.X - start.X) * amount);
            int y = (int)MathF.Round(start.Y + (end.Y - start.Y) * amount);
            if ((uint)x >= width || (uint)y >= height) continue;
            int target = (y * width + x) * 4;
            pixels[target] = 255;
            pixels[target + 1] = 181;
            pixels[target + 2] = 91;
            pixels[target + 3] = 255;
        }
    }

    private void TextureModeButton_Click(object sender, RoutedEventArgs e) => SetMode(ModelRenderMode.Textured);
    private void SolidModeButton_Click(object sender, RoutedEventArgs e) => SetMode(ModelRenderMode.Solid);
    private void WireframeModeButton_Click(object sender, RoutedEventArgs e) => SetMode(ModelRenderMode.Wireframe);
    private void Surface_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleRender();

    private void Surface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _capturedPointerId = e.Pointer.PointerId;
        _lastPointer = e.GetCurrentPoint(Surface).Position;
        Surface.CapturePointer(e.Pointer);
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }

    private void Surface_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_capturedPointerId != e.Pointer.PointerId) return;
        Windows.Foundation.Point current = e.GetCurrentPoint(Surface).Position;
        _yaw += (float)(current.X - _lastPointer.X) * 0.012f;
        _pitch += (float)(current.Y - _lastPointer.Y) * 0.012f;
        _pitch = Math.Clamp(_pitch, -1.5f, 1.5f);
        _lastPointer = current;
        ScheduleRender();
    }

    private void Surface_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_capturedPointerId != e.Pointer.PointerId) return;
        Surface.ReleasePointerCapture(e.Pointer);
        _capturedPointerId = null;
        ProtectedCursor = null;
    }

    private void Surface_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        int delta = e.GetCurrentPoint(Surface).Properties.MouseWheelDelta;
        _zoom = Math.Clamp(_zoom * (delta > 0 ? 1.12f : 0.89f), 0.15f, 8f);
        ScheduleRender();
        e.Handled = true;
    }
}
