using SkiaSharp;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Orivy;

public static class SKImageExtensions
{
    private static readonly HttpClient s_httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static SKImage FromUrl(string url, CancellationToken cancellationToken = default)
    {
        return FromUrlAsync(url, cancellationToken).GetAwaiter().GetResult();
    }

    public static Task<SKImage> FromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        return FromUrlAsync(CreateHttpImageUri(url), cancellationToken);
    }

    public static async Task<SKImage> FromUrlAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Image URL must use http or https.", nameof(uri));

        using var response = await s_httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var data = SKData.Create(stream);
        var image = SKImage.FromEncodedData(data);
        return image ?? throw new InvalidOperationException("The downloaded response is not a supported image.");
    }

    private static Uri CreateHttpImageUri(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Image URL cannot be empty.", nameof(url));

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            throw new ArgumentException("Image URL must be absolute.", nameof(url));

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Image URL must use http or https.", nameof(url));

        return uri;
    }
}
