using System.Numerics;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using XunxianDpkViewer.Models;

namespace XunxianDpkViewer.Controls;

public sealed partial class ModelPreviewControl : UserControl
{
    private const int ShadeCount = 12;
    private const int TrianglesPerLayer = 600;
    private PmfMesh? _mesh;
    private float _yaw = -0.55f;
    private float _pitch = 0.28f;
    private float _zoom = 0.86f;
    private Windows.Foundation.Point _lastPointer;
    private uint? _capturedPointerId;
    private bool _renderQueued;

    private sealed record RenderedTriangle(float Depth, int Shade, Vector2 A, Vector2 B, Vector2 C);

    public ModelPreviewControl()
    {
        InitializeComponent();
    }

    public void SetMesh(PmfMesh? mesh)
    {
        _mesh = mesh;
        _yaw = -0.55f;
        _pitch = 0.28f;
        _zoom = 0.86f;
        EmptyText.Visibility = mesh is null ? Visibility.Visible : Visibility.Collapsed;
        HintText.Text = mesh is null
            ? "实体预览 · 拖拽旋转 · 滚轮缩放"
            : $"实体预览 · {mesh.Vertices.Count:N0} 顶点 · {mesh.DeclaredTriangleCount:N0} 面";
        ScheduleRender();
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
        Surface.Children.Clear();
        if (_mesh is null || Surface.ActualWidth < 20 || Surface.ActualHeight < 20) return;

        IReadOnlyList<Vector3> source = _mesh.Vertices;
        Vector3 min = source[0];
        Vector3 max = source[0];
        foreach (Vector3 vertex in source)
        {
            min = Vector3.Min(min, vertex);
            max = Vector3.Max(max, vertex);
        }

        Vector3 center = (min + max) * 0.5f;
        float extent = Math.Max(0.0001f, Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z)));
        Matrix4x4 rotation = Matrix4x4.CreateRotationY(_yaw) * Matrix4x4.CreateRotationX(_pitch);
        float scale = (float)(Math.Min(Surface.ActualWidth, Surface.ActualHeight) * 0.82 / extent) * _zoom;
        float centerX = (float)Surface.ActualWidth * 0.5f;
        float centerY = (float)Surface.ActualHeight * 0.52f;
        var transformed = new Vector3[source.Count];
        var projected = new Vector2[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            transformed[i] = Vector3.Transform(source[i] - center, rotation);
            projected[i] = new Vector2(
                centerX + transformed[i].X * scale,
                centerY - transformed[i].Y * scale);
        }

        Vector3 light = Vector3.Normalize(new Vector3(-0.35f, 0.65f, 0.9f));
        IReadOnlyList<ushort> indices = _mesh.Indices;
        var triangles = new List<RenderedTriangle>(indices.Count / 3);
        for (int offset = 0; offset + 2 < indices.Count; offset += 3)
        {
            int ia = indices[offset];
            int ib = indices[offset + 1];
            int ic = indices[offset + 2];
            Vector3 normal = Vector3.Cross(transformed[ib] - transformed[ia], transformed[ic] - transformed[ia]);
            if (normal.LengthSquared() < 0.0000001f) continue;
            normal = Vector3.Normalize(normal);
            float brightness = 0.18f + 0.82f * MathF.Abs(Vector3.Dot(normal, light));
            int shade = Math.Clamp((int)(brightness * (ShadeCount - 1)), 0, ShadeCount - 1);
            float depth = (transformed[ia].Z + transformed[ib].Z + transformed[ic].Z) / 3f;
            triangles.Add(new RenderedTriangle(depth, shade, projected[ia], projected[ib], projected[ic]));
        }

        if (triangles.Count == 0)
        {
            HintText.Text = "实体渲染失败：模型没有有效三角面";
            return;
        }

        triangles.Sort((left, right) => left.Depth.CompareTo(right.Depth));
        for (int layerStart = 0; layerStart < triangles.Count; layerStart += TrianglesPerLayer)
        {
            int layerEnd = Math.Min(layerStart + TrianglesPerLayer, triangles.Count);
            for (int shade = 0; shade < ShadeCount; shade++)
            {
                var geometry = new PathGeometry { FillRule = FillRule.Nonzero };
                for (int index = layerStart; index < layerEnd; index++)
                {
                    RenderedTriangle triangle = triangles[index];
                    if (triangle.Shade != shade) continue;
                    var figure = new PathFigure
                    {
                        StartPoint = ToPoint(triangle.A),
                        IsClosed = true,
                        IsFilled = true
                    };
                    figure.Segments.Add(new LineSegment { Point = ToPoint(triangle.B) });
                    figure.Segments.Add(new LineSegment { Point = ToPoint(triangle.C) });
                    geometry.Figures.Add(figure);
                }
                if (geometry.Figures.Count == 0) continue;

                float amount = shade / (float)(ShadeCount - 1);
                byte red = (byte)(48 + amount * 72);
                byte green = (byte)(86 + amount * 92);
                byte blue = (byte)(150 + amount * 86);
                Surface.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
                {
                    Data = geometry,
                    Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, red, green, blue))
                });
            }
        }
    }

    private static Windows.Foundation.Point ToPoint(Vector2 point) => new(point.X, point.Y);

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
