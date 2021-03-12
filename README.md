<h1 align="center">Jellyfin AutoOrganize Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.media">Jellyfin Project</a></h3>

<p align="center">
<img alt="Logo Banner" src="https://raw.githubusercontent.com/jellyfin/jellyfin-ux/master/branding/SVG/banner-logo-solid.svg?sanitize=true"/>
<br/>
<br/>
<a href="https://github.com/jellyfin/jellyfin-plugin-autoorganize/actions?query=workflow%3A%22Test+Build+Plugin%22">
<img alt="GitHub Workflow Status" src="https://img.shields.io/github/workflow/status/jellyfin/jellyfin-plugin-autoorganize/Test%20Build%20Plugin.svg">
</a>
<a href="https://github.com/jellyfin/jellyfin-plugin-autoorganize">
<img alt="MIT License" src="https://img.shields.io/github/license/jellyfin/jellyfin-plugin-autoorganize.svg"/>
</a>
<a href="https://github.com/jellyfin/jellyfin-plugin-autoorganize/releases">
<img alt="Current Release" src="https://img.shields.io/github/release/jellyfin/jellyfin-plugin-autoorganize.svg"/>
</a>
</p>

## About
Jellyfin AutoOrganize plugin is a plugin to automatically organize your media

## Build Process

1. Clone or download this repository

2. Ensure you have .NET Core SDK setup and installed

3. Build plugin with following command.

```sh
dotnet publish --configuration Release --output bin
```

4. Place the resulting .dll file in a folder called ```plugins/``` under  the program data directory or inside the portable install directory
