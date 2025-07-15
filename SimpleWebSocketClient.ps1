param(
    [string]$url = "ws://127.0.0.1:8080/ws"
)

$client = [System.Net.WebSockets.ClientWebSocket]::new()
$uri = [Uri]$url

try {
    $client.ConnectAsync($uri, [Threading.CancellationToken]::None).Wait()
    Write-Host "Connected to $url"
    $buffer = [byte[]]::new(4096)
    while ($client.State -eq 'Open') {
        $result = $client.ReceiveAsync([ArraySegment[byte]]::new($buffer,0,$buffer.Length), [Threading.CancellationToken]::None).Result
        if ($result.Count -gt 0) {
            $msg = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count)
            Write-Host "Received: $msg"
        }
        if ($result.EndOfMessage -and $result.MessageType -eq 'Close') {
            Write-Host "Connection closed by server."
            break
        }
    }
} catch {
    Write-Host "Error: $_"
} finally {
    $client.Dispose()
}
