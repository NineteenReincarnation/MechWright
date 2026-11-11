using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using MechWright.Infrastructure;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public ObservableCollection<GraphicRow> Graphics { get; } = new();
    public ObservableCollection<TextureRow> TextureCandidates { get; } = new();

    private GraphicRow? _selectedGraphic;
    private TextureRow? _selectedTexture;
    private Bitmap? _texturePreview;
    private string _graphicInfo = "选择带有 graphicData / bodyGraphicData 的定义以预览 PNG";
    private long _textureLoadRevision;

    public string GraphicInfo
    {
        get => _graphicInfo;
        private set { _graphicInfo = value; Notify(); }
    }

    public Bitmap? TexturePreview
    {
        get => _texturePreview;
        private set
        {
            var old = _texturePreview;
            _texturePreview = value;
            Notify();
            if (old != null && !ReferenceEquals(old, value)) old.Dispose();
        }
    }

    public GraphicRow? SelectedGraphic
    {
        get => _selectedGraphic;
        set
        {
            if (ReferenceEquals(_selectedGraphic, value)) return;
            _selectedGraphic = value;
            Notify();
            RefreshTextureCandidates();
        }
    }

    public TextureRow? SelectedTexture
    {
        get => _selectedTexture;
        set
        {
            _selectedTexture = value;
            Notify();
            _ = LoadSelectedTextureAsync(value);
        }
    }

    private void RefreshGraphics()
    {
        Interlocked.Increment(ref _textureLoadRevision);
        TexturePreview = null;
        Graphics.Clear();
        TextureCandidates.Clear();
        _selectedGraphic = null;
        _selectedTexture = null;
        Notify(nameof(SelectedGraphic));
        Notify(nameof(SelectedTexture));
        GraphicInfo = "当前定义没有显式 graphicData / bodyGraphicData";

        if (_document == null || SelectedDefinition == null) return;
        foreach (var descriptor in GraphicReferences.Inspect(_document, SelectedDefinition.Entry))
            Graphics.Add(new GraphicRow(descriptor));
        SelectedGraphic = Graphics.FirstOrDefault();
    }

    private void RefreshTextureCandidates()
    {
        Interlocked.Increment(ref _textureLoadRevision);
        TexturePreview = null;
        TextureCandidates.Clear();
        if (_selectedGraphic == null)
        {
            GraphicInfo = "未选择贴图定义";
            SelectedTexture = null;
            return;
        }

        var descriptor = _selectedGraphic.Descriptor;
        var assets = TextureAssetLocator.ResolveWorkspace(_workspace, descriptor);
        foreach (var candidate in assets) TextureCandidates.Add(new TextureRow(candidate));
        GraphicInfo = $"{descriptor.GraphicClass} · drawSize: {descriptor.DrawSize} · " +
            $"{assets.Count(x => x.Exists)}/{assets.Count} 本机素材（仅预览）";
        SelectedTexture = TextureCandidates.FirstOrDefault(x => x.Candidate.Exists)
            ?? TextureCandidates.FirstOrDefault();
    }

    private async Task LoadSelectedTextureAsync(TextureRow? row)
    {
        var revision = Interlocked.Increment(ref _textureLoadRevision);
        TexturePreview = null;
        if (row?.Candidate.LocalPath is not { } file) return;
        try
        {
            // Limit decoding work and memory pressure from unexpectedly huge assets.
            if (new FileInfo(file).Length > 12 * 1024 * 1024)
            {
                GraphicInfo = "PNG 文件超过 12 MB 预览上限，可在外部查看";
                return;
            }

            var image = await Task.Run(() =>
            {
                using var stream = File.OpenRead(file);
                return Bitmap.DecodeToWidth(stream, 480);
            });
            if (revision != Interlocked.Read(ref _textureLoadRevision))
            {
                image.Dispose();
                return;
            }
            TexturePreview = image;
        }
        catch (Exception ex)
        {
            if (revision == Interlocked.Read(ref _textureLoadRevision))
                GraphicInfo = $"PNG 预览失败：{ex.Message}";
        }
    }
}

public sealed class GraphicRow(GraphicReferences.GraphicDescriptor descriptor)
{
    public GraphicReferences.GraphicDescriptor Descriptor { get; } = descriptor;
    public string Display => $"{Descriptor.TexturePath} · {Descriptor.GraphicClass}";
}

public sealed class TextureRow(TextureAssetLocator.Candidate candidate)
{
    public TextureAssetLocator.Candidate Candidate { get; } = candidate;
    public string Display => Candidate.Display;
}
