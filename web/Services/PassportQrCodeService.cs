using QRCoder;

namespace BatteryPassWeb.Services;

public sealed class PassportQrCodeService
{
    public string BuildPayloadUrl(HttpRequest request, string batteryId)
    {
        var escapedBatteryId = Uri.EscapeDataString(batteryId);
        return $"{request.Scheme}://{request.Host}/{escapedBatteryId}/latest";
    }

    public string GenerateSvg(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new SvgQRCode(data);
        return AddBatteryCellMark(qrCode.GetGraphic(10));
    }

    public byte[] GeneratePng(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(12);
    }

    public string BuildFileName(string batteryId)
    {
        var safeId = new string(batteryId
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray())
            .Trim('-');

        return $"{(string.IsNullOrWhiteSpace(safeId) ? "battery-passport" : safeId)}-qr.svg";
    }

    private static string AddBatteryCellMark(string svg)
    {
        const string mark = """
<svg class="bp-qr-battery-mark" x="40%" y="38%" width="20%" height="24%" viewBox="0 0 100 120" aria-hidden="true" focusable="false">
  <rect x="7" y="7" width="86" height="106" rx="15" fill="#ffffff"/>
  <path class="bp-qr-battery-cell" d="M31 18 72 32v70L31 88z" fill="#f8fafc" stroke="#1f2937" stroke-width="6" stroke-linejoin="round"/>
  <path d="M72 32 82 28v70l-10 4z" fill="#e5e7eb" stroke="#1f2937" stroke-width="6" stroke-linejoin="round"/>
  <path d="M45 55 63 49M45 70 63 64" stroke="#18bd84" stroke-width="6" stroke-linecap="round"/>
</svg>
""";

        var insertionIndex = svg.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
        return insertionIndex < 0
            ? svg + mark
            : svg.Insert(insertionIndex, mark);
    }
}
