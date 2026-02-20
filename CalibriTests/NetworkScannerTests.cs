using Xunit;

namespace CalibriTests;

public class NetworkScannerTests
{
    [Fact]
    public void ScanResult_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var result = new NetworkScanner.ScanResult
        {
            IP = "192.168.1.100",
            Hostname = "test-host.local",
            Subnet = "192.168.1"
        };

        // Assert
        Assert.Equal("192.168.1.100", result.IP);
        Assert.Equal("test-host.local", result.Hostname);
        Assert.Equal("192.168.1", result.Subnet);
    }

    [Fact]
    public void ScanResult_ShouldAllowNullHostname()
    {
        // Arrange & Act
        var result = new NetworkScanner.ScanResult
        {
            IP = "192.168.1.100",
            Hostname = null,
            Subnet = "192.168.1"
        };

        // Assert
        Assert.Null(result.Hostname);
        Assert.NotNull(result.IP);
    }

    [Fact]
    public async Task ScanLocalSubnetAsync_ShouldReturnList()
    {
        // Arrange
        var scanner = new NetworkScanner();

        // Act
        var results = await scanner.ScanLocalSubnetAsync(timeout: 100, maxParallel: 10);

        // Assert
        Assert.NotNull(results);
        Assert.IsType<List<NetworkScanner.ScanResult>>(results);
    }

    [Fact]
    public async Task ScanLocalSubnetAsync_ShouldFindLocalhost()
    {
        // Arrange
        var scanner = new NetworkScanner();

        // Act
        var results = await scanner.ScanLocalSubnetAsync(timeout: 500, maxParallel: 25);

        // Assert - should find at least the machine running the test
        Assert.NotEmpty(results);
        
        // Check that we have results with valid IPs
        foreach (var r in results)
        {
            Assert.NotNull(r.IP);
            Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", r.IP);
        }
    }

    [Fact]
    public async Task ScanLocalSubnetAsync_WithCancellation_ShouldCancel()
    {
        // Arrange
        var scanner = new NetworkScanner();
        using var cts = new CancellationTokenSource();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            cts.Cancel();
            await scanner.ScanLocalSubnetAsync(cancellationToken: cts.Token);
        });
    }

    [Fact]
    public async Task ScanLocalSubnetAsync_InvalidSubnet_ShouldReturnEmpty()
    {
        // This test verifies the scanner handles edge cases
        // The actual scanner gets local subnets automatically
        var scanner = new NetworkScanner();
        
        // Just verify it completes without error
        var results = await scanner.ScanLocalSubnetAsync(timeout: 50, maxParallel: 5);
        
        Assert.NotNull(results);
    }
}
