using System.Globalization;
using System.Numerics;
using System.Text;
using XunxianDpkViewer.Models;

namespace XunxianDpkViewer.Core;

public static class ObjExporter
{
    public static void Export(PmfMesh mesh, string path, string objectName)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        Vector3[] normals = CalculateNormals(mesh);
        bool hasUv = mesh.TextureCoordinates.Count == mesh.Vertices.Count;
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false), 1024 * 64);
        writer.WriteLine("# Exported by Xunxian DPK Resource Viewer");
        writer.WriteLine($"# PMF v{mesh.Version}; {mesh.Vertices.Count} vertices; {mesh.DeclaredTriangleCount} triangles");
        writer.WriteLine($"o {SanitizeName(objectName)}");

        foreach (Vector3 vertex in mesh.Vertices)
            writer.WriteLine(FormattableString.Invariant($"v {vertex.X:R} {vertex.Y:R} {vertex.Z:R}"));
        if (hasUv)
        {
            foreach (Vector2 uv in mesh.TextureCoordinates)
                writer.WriteLine(FormattableString.Invariant($"vt {uv.X:R} {1f - uv.Y:R}"));
        }
        foreach (Vector3 normal in normals)
            writer.WriteLine(FormattableString.Invariant($"vn {normal.X:R} {normal.Y:R} {normal.Z:R}"));

        for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
        {
            int a = mesh.Indices[i] + 1;
            int b = mesh.Indices[i + 1] + 1;
            int c = mesh.Indices[i + 2] + 1;
            writer.WriteLine(hasUv
                ? $"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}"
                : $"f {a}//{a} {b}//{b} {c}//{c}");
        }
    }

    private static Vector3[] CalculateNormals(PmfMesh mesh)
    {
        var normals = new Vector3[mesh.Vertices.Count];
        for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
        {
            int a = mesh.Indices[i];
            int b = mesh.Indices[i + 1];
            int c = mesh.Indices[i + 2];
            Vector3 normal = Vector3.Cross(mesh.Vertices[b] - mesh.Vertices[a], mesh.Vertices[c] - mesh.Vertices[a]);
            if (normal.LengthSquared() < 0.0000001f) continue;
            normals[a] += normal;
            normals[b] += normal;
            normals[c] += normal;
        }
        for (int i = 0; i < normals.Length; i++)
            normals[i] = normals[i].LengthSquared() < 0.0000001f ? Vector3.UnitY : Vector3.Normalize(normals[i]);
        return normals;
    }

    private static string SanitizeName(string name)
    {
        var result = new StringBuilder(name.Length);
        foreach (char value in name)
            result.Append(char.IsLetterOrDigit(value) || value is '_' or '-' ? value : '_');
        return result.Length == 0 ? "xunxian_model" : result.ToString();
    }
}
