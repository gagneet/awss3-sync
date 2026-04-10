using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using FileSyncApp.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FileSyncApp.Tests;

public class SyncEngineTests : IDisposable
{
    private readonly Mock<IFileStorageService> _mockStorage;
    private readonly Mock<IAuthService> _mockAuth;
    private readonly MetadataCache _metadataCache;
    private readonly SyncEngine _engine;

    public SyncEngineTests()
    {
        _mockStorage = new Mock<IFileStorageService>();
        _mockAuth = new Mock<IAuthService>();
        var cacheLogger = new Mock<ILogger<MetadataCache>>().Object;
        var engineLogger = new Mock<ILogger<SyncEngine>>().Object;
        _metadataCache = new MetadataCache(":memory:", cacheLogger);
        _engine = new SyncEngine(_mockStorage.Object, _mockAuth.Object, _metadataCache, engineLogger);
    }

    public void Dispose() => _metadataCache.Dispose();

    [Fact]
    public void ResolveBidirectional_NoChanges_ReturnsSkip()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var node = new FileNode("test.txt", "test.txt", false, 100, now);
        var remote = new FileNode("test.txt", "test.txt", false, 100, now, new List<UserRole>());
        var snapshot = new SnapshotEntry("test.txt", 100, now, "", "");

        // Act
        var result = _engine.ResolveBidirectional(node, remote, snapshot);

        // Assert
        Assert.Equal(SyncActionType.Skip, result);
    }

    [Fact]
    public void ResolveBidirectional_LocalOnlyChange_ReturnsUpload()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var node = new FileNode("test.txt", "test.txt", false, 200, now); // Size changed
        var remote = new FileNode("test.txt", "test.txt", false, 100, now, new List<UserRole>());
        var snapshot = new SnapshotEntry("test.txt", 100, now, "", "");

        // Act
        var result = _engine.ResolveBidirectional(node, remote, snapshot);

        // Assert
        Assert.Equal(SyncActionType.Upload, result);
    }

    [Fact]
    public void ResolveBidirectional_RemoteOnlyChange_ReturnsDownload()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var node = new FileNode("test.txt", "test.txt", false, 100, now);
        var remote = new FileNode("test.txt", "test.txt", false, 200, now, new List<UserRole>()); // Size changed
        var snapshot = new SnapshotEntry("test.txt", 100, now, "", "");

        // Act
        var result = _engine.ResolveBidirectional(node, remote, snapshot);

        // Assert
        Assert.Equal(SyncActionType.Download, result);
    }

    [Fact]
    public void ResolveBidirectional_BothChanged_ReturnsConflict()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var node = new FileNode("test.txt", "test.txt", false, 200, now); // Both changed
        var remote = new FileNode("test.txt", "test.txt", false, 300, now, new List<UserRole>());
        var snapshot = new SnapshotEntry("test.txt", 100, now, "", "");

        // Act
        var result = _engine.ResolveBidirectional(node, remote, snapshot);

        // Assert
        Assert.Equal(SyncActionType.Conflict, result);
    }

    [Fact]
    public void ResolveBidirectional_LocalDeleted_ReturnsDeleteRemote()
    {
        // Arrange
        var now = DateTime.UtcNow;
        FileNode? node = null;
        var remote = new FileNode("test.txt", "test.txt", false, 100, now, new List<UserRole>());
        var snapshot = new SnapshotEntry("test.txt", 100, now, "", "");

        // Act
        var result = _engine.ResolveBidirectional(node, remote, snapshot);

        // Assert
        Assert.Equal(SyncActionType.DeleteRemote, result);
    }

    [Fact]
    public void ResolveBidirectional_RemoteDeleted_ReturnsDeleteLocal()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var node = new FileNode("test.txt", "test.txt", false, 100, now);
        FileNode? remote = null;
        var snapshot = new SnapshotEntry("test.txt", 100, now, "", "");

        // Act
        var result = _engine.ResolveBidirectional(node, remote, snapshot);

        // Assert
        Assert.Equal(SyncActionType.DeleteLocal, result);
    }

    [Fact]
    public async Task SyncAsync_RemovesSnapshot_OnDeleteAction()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var localPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(localPath);

        try
        {
            // Pre-populate snapshot with a file that no longer exists locally or remotely
            await _metadataCache.UpdateRecordAsync(localPath, "test.txt", 100, now);

            _mockAuth.Setup(a => a.GetCurrentUser())
                .Returns(new UnifiedUser { Username = "test", Role = UserRole.Administrator });

            _mockStorage.Setup(s => s.ListFilesAsync(It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FileNode>());

            // Act
            await _engine.SyncAsync(localPath, "", ConflictPolicy.NewerWins, new Progress<SyncProgress>(), CancellationToken.None);

            // Assert - stale snapshot entry should have been cleaned up
            var records = await _metadataCache.GetAllRecordsAsync(localPath);
            Assert.Empty(records);
        }
        finally
        {
            Directory.Delete(localPath, true);
        }
    }
}

