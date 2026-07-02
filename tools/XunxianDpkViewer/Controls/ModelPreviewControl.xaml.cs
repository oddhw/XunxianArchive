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
    private const int TriangleLimit = 12_000;
    private PmfMesh? _mesh;
    private float _yaw = -0.55f;
    private float _pitch = 0.28f;
    private float _zoom = 0.86f;
    private Windows.Foundation.Point _lastPointer;
    private uint? _capturedPointerId;

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
        if (mesh is not null)
            HintText.Text = mesh.DeclaredTriangleCount > TriangleLimit
                ? $"拖拽旋转 · 滚轮缩放 · 显示前 {TriangleLimit:N0} 面"
                : "拖拽旋转 · 滚轮缩放";
        RenderMesh();
    }

    private void RenderMesh()
    {
        if (_mesh is null || Surface.ActualWidth < 20 || Surface.ActualHeight < 20)
        {
            Wireframe.Data = null;
            return;
        }

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
        float scale = (float)(Math.Min(Surface.ActualWidth, Surface.ActualHeight) * 0.78 / extent) * _zoom;
        float centerX = (float)Surface.ActualWidth * 0.5f;
        float centerY = (float)Surface.ActualHeight * 0.52f;

        var projected = new Windows.Foundation.Point[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            Vector3 transformed = Vector3.Transform(source[i] - center, rotation);
            projected[i] = new Windows.Foundation.Point(
                centerX + transformed.X * scale,
                centerY - transformed.Y * scale);
        }

        var geometry = new PathGeometry { FillRule = FillRule.Nonzero };
        IReadOnlyList<ushort> indices = _mesh.Indices;
        int triangles = Math.Min(indices.Count / 3, TriangleLimit);
        int step = Math.Max(1, (indices.Count / 3) / triangles);
        for (int triangle = 0; triangle < indices.Count / 3 && geometry.Figures.Count < triangles; triangle += step)
        {
            int offset = triangle * 3;
            Windows.Foundation.Point a = projected[indices[offset]];
            Windows.Foundation.Point b = projected[indices[offset + 1]];
            Windows.Foundation.Point c = projected[indices[offset + 2]];
            var figure = new PathFigure { StartPoint = a, IsClosed = true, IsFilled = false };
            figure.Segments.Add(new LineSegment { Point = b });
            figure.Segments.Add(new LineSegment { Point = c });
            geometry.Figures.Add(figure);
        }
        Wireframe.Data = geometry;
    }

    private void Surface_SizeChanged(object sender, SizeChangedEventArgs e) => RenderMesh();

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
        RenderMesh();
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
        RenderMesh();
        e.Handled = true;
    }
}
