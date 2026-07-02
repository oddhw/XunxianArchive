using System.Text;

namespace XunxianDpkViewer.Core;

public static class SelfTest
{
    public static string Run()
    {
        string[] candidates =
        {
            @"D:\Program Files\腾讯游戏\新寻仙\res",
            @"C:\Program Files\腾讯游戏\新寻仙\res"
        };
        string root = candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "gui.dpk")))
            ?? throw new DirectoryNotFoundException("没有发现《新寻仙》res 目录。");

        var report = new StringBuilder()
            .AppendLine($"资源目录: {root}")
            .AppendLine($"时间: {DateTimeOffset.Now:O}");

        CheckFile(report, root, "gui.dpk", ".png", data =>
        {
            if (!data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new InvalidDataException("PNG 文件头不正确。");
            return "PNG 文件头正确";
        });
        CheckFile(report, root, "sound.dpk", ".ogg", data =>
        {
            if (!data.AsSpan(0, 4).SequenceEqual("OggS"u8))
                throw new InvalidDataException("OGG 文件头不正确。");
            return "OGG 文件头正确";
        });
        CheckFile(report, root, "obj.dpk", ".pmf", data => DescribeMesh(PmfParser.Parse(data)));
        CheckFile(report, root, "cha.dpk", ".pmf", data => DescribeMesh(PmfParser.Parse(data)));
        report.AppendLine("SELF-TEST PASSED");
        return report.ToString();
    }

    private static void CheckFile(
        StringBuilder report,
        string root,
        string archiveName,
        string extension,
        Func<byte[], string> validate)
    {
        using var reader = new DpkReader(Path.Combine(root, archiveName));
        IReadOnlyList<Models.DpkEntry> entries = reader.ReadEntries();
        Models.DpkEntry sample = entries.First(entry => entry.Path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        byte[] data = reader.Extract(sample);
        report.AppendLine($"{archiveName}: {entries.Count:N0} 项；{sample.Path}；{data.Length:N0} 字节；{validate(data)}");
    }

    private static string DescribeMesh(Models.PmfMesh mesh) =>
        $"PMF v{mesh.Version}，{mesh.Vertices.Count:N0} 顶点，{mesh.DeclaredTriangleCount:N0} 三角面";
}
