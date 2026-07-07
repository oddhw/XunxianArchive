using System.Text;
using System.Xml.Linq;
using XunxianDpkViewer.Models;

namespace XunxianDpkViewer.Core;

public sealed class ModelTextureResolver
{
    private const int MaximumCompositeParts = 64;

    private readonly DpkWorkspace _workspace;
    private readonly Dictionary<string, IReadOnlyList<ModelTextureBinding>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<CompositeModelEntry>> _compositeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private Dictionary<string, AssetEntry>? _assetByReference;

    private sealed record CompositePartCandidate(
        AssetEntry MeshAsset,
        string MaterialName,
        XElement SourceElement,
        bool UsesMaterialReferences);

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

            var candidates = new List<CompositePartCandidate>();
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
                candidates.Add(new CompositePartCandidate(mesh, materialName, modelElement, UsesMaterialReferences: true));
            }

            foreach (XElement meshElement in document.Descendants()
                         .Where(element => element.Name.LocalName.Equals("MeshFile", StringComparison.OrdinalIgnoreCase)))
            {
                AssetEntry? mesh = FindReferencedAsset(meshElement.Value);
                if (mesh?.Kind != AssetKind.Model || meshElement.Parent is null) continue;
                candidates.Add(new CompositePartCandidate(mesh, string.Empty, meshElement.Parent, UsesMaterialReferences: false));
            }

            CompositePartCandidate[] selectedCandidates = SelectCompositeCandidates(
                candidates
                    .DistinctBy(part => $"{part.MeshAsset.DisplayPath}|{part.MaterialName}", StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                config);
            if (selectedCandidates.Length < 2) continue;

            CompositeModelPart[] distinctParts = selectedCandidates
                .Select(candidate => new CompositeModelPart(
                    candidate.MeshAsset,
                    candidate.MaterialName,
                    ResolveCompositeTexture(candidate, materialReferences, config.Entry.Path)))
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
        foreach (AssetEntry config in GetTextureCandidateConfigs(model))
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
            if (!TextMayReferenceModel(text, model)) continue;

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
        string? modelRoot = GetMeshRoot(model.Entry.Path);
        return bindings
            .DistinctBy(binding => $"{binding.TextureAsset.DisplayPath}|{binding.MapType}|{binding.MaterialName}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(binding => IsTextureUnderRoot(binding, modelRoot) ? 0 : 1)
            .ThenBy(binding => binding.MapType.StartsWith("BaseMap", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(binding => binding.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private ModelTextureBinding? ResolveCompositeTexture(
        CompositePartCandidate candidate,
        IReadOnlyList<XElement> materialReferences,
        string configPath)
    {
        var bindings = new List<ModelTextureBinding>();
        if (candidate.UsesMaterialReferences)
        {
            foreach (XElement materialReference in materialReferences)
            {
                AssetEntry? cmf = FindReferencedAsset(materialReference.Value);
                if (cmf is null) continue;
                AddCmfBindings(bindings, cmf, candidate.MaterialName, configPath);
                ModelTextureBinding? baseMap = SelectBaseMapBinding(bindings, GetMeshRoot(candidate.MeshAsset.Entry.Path));
                if (baseMap is not null) return baseMap;
            }
        }
        else
        {
            AddTextureElements(bindings, candidate.SourceElement.Elements(), candidate.MaterialName, configPath);
        }

        return SelectBaseMapBinding(bindings, GetMeshRoot(candidate.MeshAsset.Entry.Path));
    }

    private CompositePartCandidate[] SelectCompositeCandidates(
        IReadOnlyList<CompositePartCandidate> candidates,
        AssetEntry config)
    {
        string? root = GetConfigRoot(config.Entry.Path);
        CompositePartCandidate[] selected = candidates.ToArray();

        if (selected.Length > MaximumCompositeParts && root is not null)
        {
            CompositePartCandidate[] local = selected
                .Where(candidate => IsUnderRoot(candidate.MeshAsset.Entry.Path, root))
                .ToArray();
            if (local.Length is >= 2 and <= MaximumCompositeParts) selected = local;
            else return Array.Empty<CompositePartCandidate>();
        }

        return root is null ? selected : CollapseMaterialVariants(selected, config.ArchivePath, root);
    }

    private CompositePartCandidate[] CollapseMaterialVariants(
        IReadOnlyList<CompositePartCandidate> candidates,
        string archivePath,
        string root)
    {
        HashSet<string> localMaterialNames = _workspace.Assets
            .Where(asset => string.Equals(asset.ArchivePath, archivePath, StringComparison.OrdinalIgnoreCase) &&
                            asset.Extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) &&
                            IsUnderRoot(asset.Entry.Path, root))
            .Select(asset => System.IO.Path.GetFileNameWithoutExtension(asset.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (localMaterialNames.Count == 0) return candidates.ToArray();

        return candidates
            .GroupBy(candidate => candidate.MeshAsset.DisplayPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.FirstOrDefault(candidate => localMaterialNames.Contains(candidate.MaterialName)) ?? group.First())
            .ToArray();
    }

    private IReadOnlyList<AssetEntry> GetTextureCandidateConfigs(AssetEntry model)
    {
        AssetEntry[] configs = _workspace.Assets
            .Where(asset => string.Equals(asset.ArchivePath, model.ArchivePath, StringComparison.OrdinalIgnoreCase) &&
                            asset.Extension.Equals(".cct", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string? root = GetMeshRoot(model.Entry.Path);
        if (root is not null)
        {
            string configPrefix = root + "/config/";
            AssetEntry[] localConfigs = configs
                .Where(config => NormalizeAssetPath(config.Entry.Path).StartsWith(configPrefix, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (localConfigs.Length > 0) return localConfigs;
        }

        return configs;
    }

    private static bool TextMayReferenceModel(string text, AssetEntry model)
    {
        string path = NormalizeAssetPath(model.Entry.Path);
        return text.Contains(path, StringComparison.OrdinalIgnoreCase) ||
               text.Contains(path.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
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
        return AssetByReference.TryGetValue(normalized, out AssetEntry? asset) ? asset : null;
    }

    private Dictionary<string, AssetEntry> AssetByReference =>
        _assetByReference ??= _workspace.Assets
            .GroupBy(asset => NormalizeAssetPath(
                $"{System.IO.Path.GetFileNameWithoutExtension(asset.ArchiveName)}/{asset.Entry.Path}"),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    private static bool ReferenceMatches(string reference, string normalizedTarget) =>
        NormalizeReference(reference).Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeReference(string reference)
    {
        string normalized = reference.Trim().Replace('\\', '/');
        if (normalized.StartsWith("$(res)", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[6..].TrimStart('/');
        return normalized.TrimStart('/');
    }

    private static string NormalizeAssetPath(string path) => path.Replace('\\', '/').Trim('/');

    private static string? GetConfigRoot(string configPath)
    {
        string normalized = NormalizeAssetPath(configPath);
        int marker = normalized.IndexOf("/config/", StringComparison.OrdinalIgnoreCase);
        return marker > 0 ? normalized[..marker] : null;
    }

    private static string? GetMeshRoot(string meshPath)
    {
        string normalized = NormalizeAssetPath(meshPath);
        int marker = normalized.IndexOf("/mesh/", StringComparison.OrdinalIgnoreCase);
        return marker > 0 ? normalized[..marker] : null;
    }

    private static bool IsUnderRoot(string assetPath, string root)
    {
        string normalized = NormalizeAssetPath(assetPath);
        return normalized.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextureUnderRoot(ModelTextureBinding binding, string? root) =>
        root is not null && IsUnderRoot(binding.TextureAsset.Entry.Path, root);

    private static ModelTextureBinding? SelectBaseMapBinding(
        IReadOnlyList<ModelTextureBinding> bindings,
        string? root) =>
        bindings
            .Where(binding => binding.MapType.StartsWith("BaseMap", StringComparison.OrdinalIgnoreCase))
            .OrderBy(binding => IsTextureUnderRoot(binding, root) ? 0 : 1)
            .ThenBy(binding => binding.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    private static string DecodeXml(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return Encoding.Unicode.GetString(data).TrimStart('\uFEFF');
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(data).TrimStart('\uFEFF');
        return Encoding.UTF8.GetString(data).TrimStart('\uFEFF');
    }
}
