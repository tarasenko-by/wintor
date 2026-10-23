namespace Wintor.App.ViewModels;

public enum AddRequestKind
{
    Magnet,
    File,
}

/// <summary>A torrent to add that came from outside the page: a .torrent double-click or a magnet link in a browser.</summary>
public sealed record AddRequest(AddRequestKind Kind, string Value)
{
    public static AddRequest Magnet(string link) => new(AddRequestKind.Magnet, link);

    public static AddRequest File(string path) => new(AddRequestKind.File, path);
}
