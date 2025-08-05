namespace NexGrades.Common.Services;

public interface IFileSystemDialogService
{
    IEnumerable<string> SelectFolder(string basePath = "");
}