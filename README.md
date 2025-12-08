# CFXBridge

This project is exclusively used by node-red-contrib-ipc-cfx to allow construction, verification, sending and receiving of IPC-CFX messages from withing Node-RED using the official
.NET SDK.

## Building for publish

```bash
dotnet publish -c Release -r linux-x64 --self-contained false -o publish-linux-x64
tar -czf CFXBridge-1.0.4-linux-x64.tar.gz   -C publish-linux-x64  .
```