using System.IO;

internal static class Sample
{
    internal static string Read(UploadRequest request, string folder)
    {
        // ruleid: path-from-input-csharp
        return File.ReadAllText(Path.Combine(folder, request.Name));
    }

    internal static string Name(UploadRequest request) =>
        // ok: path-from-input-csharp
        Path.Combine("uploads", Path.GetFileName(request.Name));

    internal static void FromConsole(string folder)
    {
        var name = Console.ReadLine();
        // ruleid: path-from-input-csharp
        using var stream = new FileStream(folder + name, FileMode.Open);
        // ok: path-from-input-csharp
        using var settings = new FileStream(Path.Combine(folder, "settings.json"), FileMode.Open);
    }
}
