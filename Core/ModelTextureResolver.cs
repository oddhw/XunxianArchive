using System.Text;
using System.Xml.Linq;
using XunxianDpkViewer.Models;

namespace XunxianDpkViewer.Core;

public sealed class ModelTextureResolver
{
    private readonly DpkWorkspace _workspace;
    private readonly Dictionary<string, IReadOnlyList<ModelTextureBinding>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<CompositeModelEntry>> _compositeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public ModelTextureResolver(DpkWorkspace workspace)
    {
        _workspace = workspace;
    }

    public IReadOnlyList<ModelTextureBinding> Resolve(AssetEntry model)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(model.DisplayPath, out IReadOnlyList<ModelTextureBinding>? cached))
                return cached;

            IReadOnlyList<ModelTextureBinding> result = ResolveCore(model);
            _cache[model.DisplayPath] = result;
            return result;
        }
    }

    public IReadOnlyList<CompositeModelEntry> FindComposites(string archivePath, string folderPath)
    {
        string normalizedFolder = folderPath.Replace('\\', '/').Trim('/');
        if (normalizedFolder.Split('/', StringSplitOptions.RemoveEmptyEntries).Length < 2)
            return Array.Empty<CompositeModelEntry>();
        string cacheKey = $"{archivePath}|{normalizedFolder}";
        lock (_gate)
        {
            if (_compositeCache.TryGetValue(cacheKey, out IReadOnlyList<CompositeModelEntry>? cached))
                return cached;
            IReadOnlyList<CompositeModelEntry> result = FindCompositesCore(archivePath, normalizedFolder);
            _compositeCache[cacheKey] = result;
            return result;
        }
    }

    private IReadOnlyList<CompositeModelEntry> FindCompositesCore(string archivePath, string folderPath)
    {
        var result = new List<CompositeModelEntry>();
        string prefix = folderPath + "/";
        foreach (AssetEntry config in _workspace.Assets.Where(asset =>
                     string.Equals(asset.ArchivePath, archivePath, StringComparison.OrdinalIgnoreCase) &&
                     asset.Extension.Equals(".cct", StringComparison.OrdinalIgnoreCase) &&
                     asset.Entry.Path.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            XDocument document;
            try
            {
                document = XDocument.Parse(DecodeXml(_workspace.Extract(config)), LoadOptions.None);
            }
            catch
            {
                continue;
            }

            var parts = new List<CompositeModelPart>();
            XElement[] materialReferences = document.Descendants()
                .Where(element => element.Name.LocalName.Equals("Material", StringComparison.OrdinalIgnoreCase) &&
                                  element.Value.Contains(".cmf", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (XElement modelElement in document.Descendants()
                         .Where(element => element.Name.LocalName.Equals("Model", StringComparison.OrdinalIgnoreCase)))
            {
                string? meshReference = modelElement.Attributes()
                    .FirstOrDefault(attribute => attribute.Name.LocalName.Equals("MeshFile", StringComparison.OrdinalIgnoreCase))?.Value;
                AssetEntry? mesh = meshReference is null ? null : FindReferencedAsset(meshReference);
                if (mesh?.Kind != AssetKind.Model) continue;
                string materialName = modelElement.Attributes()
                    .FirstOrDefault(attribute => attribute.Name.LocalName.Equals("MatName", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
                ModelTextureBinding? texture = null;
                foreach (XElement materialReference in materialReferences)
                {
                    AssetEntry? cmf = FindReferencedAsset(materialReference.Value);
                    if (cmf is null) continue;
                    var candidates = new List<ModelTextureBinding>();
                    AddCmfBindings(candidates, cmf, materialName, config.Entry.Path);
                    texture = candidates.FirstOrDefault(binding => binding.MapType.StartsWith("BaseMap", StringComparison.OrdinalIgnoreCase));
                    if (texture is not null) break;
                }
                parts.Add(new CompositeModelPart(mesh, materialName, texture));
            }

            foreach (XElement meshElement in document.Descendants()
                         .Where(element => element.Name.LocalName.Equals("MeshFile", StringComparison.OrdinalIgnoreCase)))
            {
                AssetEntry? mesh = FindReferencedAsset(meshElement.Value);
                if (mesh?.Kind != AssetKind.Model || meshElement.Parent is null) continue;
                var candidates = new List<ModelTextureBinding>();
                AddTextureElements(candidates, meshElement.Parent.Elements(), string.Empty, config.Entry.Path);
                parts.Add(new CompositeModelPart(mesh, string.Empty,
                    candidates.FirstOrDefault(binding => binding.MapType.StartsWith("BaseMap", StringComparison.OrdinalIgnoreCase))));
            }

            CompositeModelPart[] distinctParts = parts
                .DistinctBy(part => $"{part.MeshAsset.DisplayPath}|{part.MaterialName}", StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (distinctParts.Length < 2) continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(config.Name) + "（完整组合）";
            result.Add(new CompositeModelEntry(name, config, distinctParts));
        }
        return result.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private IReadOnlyList<ModelTextureBinding> ResolveCore(AssetEntry model)
    {
        string modelReference = NormalizeReference($"{System.IO.Path.GetFileNameWithoutExtension(model.ArchiveName)}/{model.Entry.Path}");
        var bindings = new List<ModelTextureBinding>();
        foreach (AssetEntry config in _workspace.Assets.Where(asset =>
                     string.Equals(asset.ArchivePath, model.ArchivePath, StringComparison.OrdinalIgnoreCase) &&
                     asset.Extension.Equals(".cct", StringComparison.OrdinalIgnoreCase)))
        {
            string text;
            try
            {
                text = DecodeXml(_workspace.Extract(config));
            }
            catch
            {
                continue;
            }
            if (!text.Contains(model.Name, StringComparison.OrdinalIgnoreCase)) continue;

            XDocument document;
            try
            {
                document = XDocument.Parse(text, LoadOptions.None);
            }
            catch
            {
                continue;
            }

            foreach (XElement meshElement in document.Descendants()
                         .Where(element => element.Name.LocalName.Equals("MeshFile", StringComparison.OrdinalIgnoreCase)))
            {
                if (!ReferenceMatches(meshElement.Value, modelReference)) continue;
                XElement? owner = meshElement.Parent;
                if (owner is null) continue;
                AddTextureElements(bindings, owner.Elements(), string.Empty, config.Entry.Path);
            }

            foreach (XElement modelElement in document.Descendants()
                         .Where(element => element.Name.LocalName.Equals("Model", StringComparison.OrdinalIgnoreCase)))
            {
                XAttribute? meshAttribute = modelElement.Attributes()
                    .FirstOrDefault(attribute => attribute.Name.LocalName.Equals("MeshFile", StringComparison.OrdinalIgnoreCase));
                if (meshAttribute is null || !ReferenceMatches(meshAttribute.Value, modelReference)) continue;

                string materialName = modelElement.Attributes()
                    .FirstOrDefault(attribute => attribute.Name.LocalName.Equals("MatName", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
                foreach (XElement materialReference in document.Descendants()
                             .Where(element => element.Name.LocalName.Equals("Material", StringComparison.OrdinalIgnoreCase) &&
                                               element.Value.Contains(".cmf", StringComparison.OrdinalIgnoreCase)))
                {
                    AssetEntry? cmf = FindReferencedAsset(materialReference.Value);
                    if (cmf is not null) AddCmfBindings(bindings, cmf, materialName, config.Entry.Path);
                }
            }
        }

        if (bindings.Count == 0) AddHeuristicBinding(bindings, model);
        return bindings
            .DistinctBy(binding => $"{binding.TextureAsset.DisplayPath}|{binding.MapType}|{binding.MaterialName}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(binding => binding.MapType.StartsWith("BaseMap", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(binding => binding.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void AddCmfBindings(List<ModelTextureBinding> bindings, AssetEntry cmf, string materialName, string configPath)
    {
        try
        {
            XDocument document = XDocument.Parse(DecodeXml(_workspace.Extract(cmf)), LoadOptions.None);
            IEnumerable<XElement> materials = document.Descendants()
                .Where(element => element.Name.LocalName.Equals("Material", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(materialName))
            {
                materials = materials.Where(element => string.Equals(
                    element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals("Name", StringComparison.OrdinalIgnoreCase))?.Value,
                    materialName,
                    StringComparison.OrdinalIgnoreCase));
            }

            foreach (XElement material in materials)
                AddTextureElements(bindings, material.Elements(), materialName, $"{configPath} → {cmf.Entry.Path}");
        }
        catch
        {
            // 个别历史 CMF 不符合 XML 规范时，保留实体着色预览。
        }
    }

    private void AddTextureElements(
        List<ModelTextureBinding> bindings,
        IEnumerable<XElement> elements,
        string materialName,
        string configPath)
    {
        foreach (XElement element in elements.Where(element =>
                     element.Name.LocalName.Contains("Map", StringComparison.OrdinalIgnoreCase) &&
                     element.Value.Contains(".dds", StringComparison.OrdinalIgnoreCase)))
        {
            AssetEntry? texture = FindReferencedAsset(element.Value);
            if (texture is not null)
                bindings.Add(new ModelTextureBinding(texture, element.Name.LocalName, materialName, configPath));
        }
    }

    private void AddHeuristicBinding(List<ModelTextureBinding> bindings, AssetEntry model)
    {
        string basename = System.IO.Path.GetFileNameWithoutExtension(model.Name);
        AssetEntry? texture = _workspace.Assets.FirstOrDefault(asset =>
            string.Equals(asset.ArchivePath, model.ArchivePath, StringComparison.OrdinalIgnoreCase) &&
            asset.Extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) &&
            System.IO.Path.GetFileNameWithoutExtension(asset.Name).Equals(basename, StringComparison.OrdinalIgnoreCase));
        if (texture is not null)
            bindings.Add(new ModelTextureBinding(texture, "同名贴图", string.Empty, "文件名回退匹配"));
    }

    private AssetEntry? FindReferencedAsset(string reference)
    {
        string normalized = NormalizeReference(reference);
        int slash = normalized.IndexOf('/');
        if (slash <= 0) return null;
        string archiveName = normalized[..slash] + ".dpk";
        string internalPath = normalized[(slash + 1)..];
        return _workspace.Assets.FirstOrDefault(asset =>
            asset.ArchiveName.Equals(archiveName, StringComparison.OrdinalIgnoreCase) &&
            asset.Entry.Path.Replace('\\', '/').Equals(internalPath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ReferenceMatches(string reference, string normalizedTarget) =>
        NormalizeReference(reference).Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeReference(string reference)
    {
        string normalized = reference.Trim().Replace('\\', '/');
        if (normalized.StartsWith("$(res)/", StringComparison.OrdinalIgnoreCase)) normalized = normalized[7..];
        return normalized.TrimStart('/');
    }

    private static string DecodeXml(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return Encoding.Unicode.GetString(data).TrimStart('\uFEFF');
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(data).TrimStart('\uFEFF');
        return Encoding.UTF8.GetString(data).TrimStart('\uFEFF');
    }
}
