# CFXBridge

This project is exclusively used by [node-red-contrib-ipc-cfx](https://github.com/TheHighestBit/node-red-contrib-ipc-cfx) to allow construction, verification, sending and receiving of IPC-CFX messages from withing Node-RED using the official
.NET SDK + some additional message fields for our use cases. CFX parsing is permissive in the sense that unknown fields won't throw an error, so the custom fields shouldn't pose a problem when it comes to CFX integration.

However, unknown message types do cause issues, so be extremely cautious when using the new selective soldering namespace for example.

## Building for publish

```bash
dotnet publish -c Release -r linux-x64 --self-contained false -o publish-linux-x64
tar -czf CFXBridge-1.0.7-linux-x64.tar.gz -C publish-linux-x64 .
```

```bash
dotnet publish -c Release -r linux-arm --self-contained false -o publish-linux-arm
tar -czf CFXBridge-1.0.7-linux-arm.tar.gz -C publish-linux-arm .
```

```bash
dotnet publish -c Release -r linux-arm64 --self-contained false -o publish-linux-arm64
tar -czf CFXBridge-1.0.7-linux-arm64.tar.gz -C publish-linux-arm64 .
```

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish-win-x64
tar -czf CFXBridge-1.0.7-win-x64.tar.gz -C publish-win-x64 .
```