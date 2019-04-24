# rpi-album

Just a naive Raspberry Pi album.

## Installation

### Server
```
docker build github.com/sjx95/rpi-album --file album-server/Dockerfile -t album-server
docker run -d -p 8080:80 -v /srv/album-server/userdata:/app/wwwroot/userdata --name album-server album-server
```

### Client

1. Install Xorg
2. Install ".NET Core Runtime" (".NET Core SDK" if you don't want to build on PC)
3. Build and publish use Visual Studio (Or `dotnet build` on RPi)
4. Check if `album-server.dll.config` is owned by `root`
4. `sudo dotnet album-server`

**Attentions**:
- Raspbian (Later than 2018) shoud be installed
- System is booted in text mode
- Wi-Fi or Ethernet should be configured
