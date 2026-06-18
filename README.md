# MVC Music Store

*This repository now focuses on the upgraded ASP.NET Core version of the MVC Music Store sample application, running on .NET 10. It is intended for illustration purposes. For new applications, we recommend using modern .NET!*

<img src="https://github.com/user-attachments/assets/5aa786f5-6540-4baf-963e-2303518b0e78" alt="MVC Music Store application interface showing the homepage with featured albums">

## About

The MVC Music Store is a tutorial application that introduces and explains step-by-step how to use ASP.NET MVC and Visual Studio for web development.

The MVC Music Store is a lightweight sample store implementation which sells music albums online, and implements basic site administration, user sign-in, and shopping cart functionality.

This repository now contains the upgraded application under [src/MvcMusicStore](src/MvcMusicStore).

## Running the App

From the repository root:

```bash
dotnet build src/MvcMusicStore/MvcMusicStore.csproj
cd src/MvcMusicStore
ASPNETCORE_URLS=http://127.0.0.1:5090 dotnet run
```

The app will start on http://127.0.0.1:5090/.

## Thumbnail behavior

- Storefront and admin pages always render an album thumbnail.
- Thumbnail precedence is: uploaded custom image, metadata-fetched artwork, then placeholder image.
- Admin create/edit pages support uploading custom image files (`.png`, `.jpg`, `.jpeg`, `.gif`, `.webp`).

## Additional Resources

Tutorial documentation is available on [Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/mvc/overview/older-versions/mvc-music-store/).
