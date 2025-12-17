using Microsoft.Extensions.FileProviders;
using Moq;

namespace TestUtilities;

/// <summary>
/// Helper methods for creating mock IFileProvider instances for testing
/// </summary>
public static class MockFileProviderHelpers
{
    /// <summary>
    /// Creates a mock IFileProvider with the specified file contents
    /// </summary>
    /// <param name="fileContents">Dictionary mapping file paths to their contents</param>
    /// <returns>A configured mock IFileProvider</returns>
    public static Mock<IFileProvider> CreateMockFileProvider(Dictionary<string, string> fileContents)
    {
        var mockFileProvider = new Mock<IFileProvider>();
        
        // Setup GetFileInfo for each file
        foreach (var (path, content) in fileContents)
        {
            var mockFileInfo = CreateMockFileInfo(content, path);
            mockFileProvider.Setup(fp => fp.GetFileInfo(path)).Returns(mockFileInfo.Object);
        }
        
        // Setup non-existent files to return NotFoundFileInfo
        mockFileProvider.Setup(fp => fp.GetFileInfo(It.Is<string>(p => !fileContents.ContainsKey(p))))
            .Returns<string>(name => new NotFoundFileInfo(name));
        
        return mockFileProvider;
    }
    
    /// <summary>
    /// Creates a mock IFileInfo that returns the specified content
    /// </summary>
    /// <param name="content">The file content to return when the file is read</param>
    /// <param name="path">The path to use for PhysicalPath</param>
    /// <returns>A configured mock IFileInfo</returns>
    public static Mock<IFileInfo> CreateMockFileInfo(string content, string path)
    {
        var mockFileInfo = new Mock<IFileInfo>();
        mockFileInfo.Setup(fi => fi.Exists).Returns(true);
        mockFileInfo.Setup(fi => fi.PhysicalPath).Returns(path);
        mockFileInfo.Setup(fi => fi.Name).Returns(Path.GetFileName(path));
        mockFileInfo.Setup(fi => fi.CreateReadStream()).Returns(() =>
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        });
        
        return mockFileInfo;
    }
}
