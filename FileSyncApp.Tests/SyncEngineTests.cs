using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using FileSyncApp.Core.Services;
using Moq;
using Xunit;

namespace FileSyncApp.Tests;

public class SyncEngineTests
{
    private readonly Mock<IFileStorageService> _mockStorage;
    private readonly Mock<IDatabaseService> _mockDb;
    private readonly SyncEngine _engine;

    public SyncEngineTests()
    {
        _mockStorage = new Mock<IFileStorageService>();
        _mockDb = new Mock<IDatabaseService>();
        _engine = new SyncEngine(_mockStorage.Object, _mockDb.Object);
    }

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
            _mockDb.Setup(d => d.GetSnapshots()).Returns(new List<SnapshotEntry> {
                new SnapshotEntry("test.txt", 100, now, "", "")
            });

            _mockStorage.Setup(s => s.ListFilesAsync(It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FileNode>());

            // Act
            await _engine.SyncAsync(localPath, "", UserRole.Administrator, new Progress<SyncProgress>(), null, CancellationToken.None);

            // Assert
            _mockDb.Verify(d => d.DeleteSnapshot("test.txt"), Times.Once);
        }
        finally
        {
            Directory.Delete(localPath, true);
        }
    }
}
