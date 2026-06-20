using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Infrastructure;
using MvcMusicStore.Models;

namespace MvcMusicStore.Controllers
{
    /// <summary>
    /// Serves SEO crawler files: a <c>robots.txt</c> that points crawlers at the sitemap and a
    /// dynamically generated <c>sitemap.xml</c> covering the public catalog (home, store, albums,
    /// artists). Admin, account, cart and checkout routes are intentionally excluded.
    /// </summary>
    [AllowAnonymous]
    public class SeoController : Controller
    {
        private readonly MusicStoreEntities storeDB;

        public SeoController(MusicStoreEntities storeDb)
        {
            storeDB = storeDb;
        }

        [HttpGet("/robots.txt")]
        public IActionResult Robots()
        {
            var sitemapUrl = Url.AbsoluteContent("~/sitemap.xml");

            var builder = new StringBuilder();
            builder.AppendLine("User-agent: *");
            builder.AppendLine("Allow: /");
            builder.AppendLine("Disallow: /StoreManager");
            builder.AppendLine("Disallow: /Account");
            builder.AppendLine("Disallow: /ShoppingCart");
            builder.AppendLine("Disallow: /Checkout");
            builder.AppendLine();
            builder.AppendLine($"Sitemap: {sitemapUrl}");

            return Content(builder.ToString(), "text/plain", Encoding.UTF8);
        }

        [HttpGet("/sitemap.xml")]
        public async Task<IActionResult> Sitemap(CancellationToken cancellationToken)
        {
            var albums = await storeDB.Albums.ToListAsync(cancellationToken);
            var artists = await storeDB.Artists.ToListAsync(cancellationToken);

            var albumsByArtist = albums
                .GroupBy(a => a.ArtistId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var settings = new XmlWriterSettings
            {
                Async = false,
                Encoding = new UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = false
            };

            await using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

                WriteUrl(writer, Url.AbsoluteContent("~/"), null, "daily", "1.0");
                WriteUrl(writer, AbsoluteAction("Index", "Store"), null, "daily", "0.9");
                WriteUrl(writer, AbsoluteAction("Index", "AiMusic"), null, "monthly", "0.5");

                foreach (var album in albums.OrderBy(a => a.AlbumId))
                {
                    WriteUrl(
                        writer,
                        Url.AlbumUrl(album.AlbumId, album.Title, absolute: true),
                        album.ReleaseDate,
                        "weekly",
                        "0.8");
                }

                foreach (var artist in artists.OrderBy(a => a.ArtistId))
                {
                    if (!albumsByArtist.TryGetValue(artist.ArtistId, out var artistAlbums) || artistAlbums.Count == 0)
                    {
                        continue;
                    }

                    var lastModified = artistAlbums
                        .Where(a => a.ReleaseDate.HasValue)
                        .Select(a => a.ReleaseDate!.Value)
                        .DefaultIfEmpty()
                        .Max();

                    WriteUrl(
                        writer,
                        Url.ArtistUrl(artist.ArtistId, artist.Name, absolute: true),
                        lastModified == default ? null : lastModified,
                        "weekly",
                        "0.6");
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return File(stream.ToArray(), "application/xml; charset=utf-8");
        }

        private string AbsoluteAction(string action, string controller)
            => Url.Action(action, controller, values: null, protocol: Request.Scheme) ?? string.Empty;

        private static void WriteUrl(XmlWriter writer, string location, DateTime? lastModified, string changeFrequency, string priority)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return;
            }

            writer.WriteStartElement("url");
            writer.WriteElementString("loc", location);

            if (lastModified.HasValue)
            {
                writer.WriteElementString("lastmod", lastModified.Value.ToString("yyyy-MM-dd"));
            }

            writer.WriteElementString("changefreq", changeFrequency);
            writer.WriteElementString("priority", priority);
            writer.WriteEndElement();
        }
    }
}
