using System.Text;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Documents;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Document links for the test address, and small files whose first bytes say what they are.</summary>
internal static class DocumentTesting
{
    public const string BaseUrl = "https://passport.example.com";

    public static readonly DppLinks Links = new(Options.Create(new BatteryPassOptions { AppBaseUrl = BaseUrl }));

    public static readonly DppLinks NoLinks = new(Options.Create(new BatteryPassOptions()));

    public static byte[] Pdf(string text = "test") => Encoding.ASCII.GetBytes($"%PDF-1.7\n{text}\n%%EOF");

    public static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    public static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
}
