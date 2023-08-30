using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZXing;

var host = new HostBuilder()
	.ConfigureFunctionsWorkerDefaults()
	//.ConfigureServices(s =>
	//{
	//    s.AddApplicationInsightsTelemetryWorkerService();
	//    s.ConfigureFunctionsApplicationInsights();
	//    s.AddSingleton<IHttpResponderService, DefaultHttpResponderService>();
	//    s.Configure<LoggerFilterOptions>(options =>
	//    {
	//        // The Application Insights SDK adds a default logging filter that instructs ILogger to capture only Warning and more severe logs. Application Insights requires an explicit override.
	//        // Log levels can also be configured using appsettings.json. For more information, see https://learn.microsoft.com/en-us/azure/azure-monitor/app/worker-service#ilogger-logs
	//        LoggerFilterRule toRemove = options.Rules.FirstOrDefault(rule => rule.ProviderName
	//            == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

	//        if (toRemove is not null)
	//        {
	//            options.Rules.Remove(toRemove);
	//        }
	//    });
	//})
	.Build();

await host.RunAsync();

public class QRCodeApi
{
	private readonly ILogger _logger;

	public QRCodeApi(ILogger<QRCodeApi> logger)
	{
		this._logger = logger;
	}

	// curl -d '{"url": "https://www.google.com"}' http://localhost:7071/qrcode/url
	// curl -d '{"url": "https://www.google.com"}' https://softeering-tools.azurewebsites.net/qrcode/url
	[Function("QRCodeUrl")]
	public async Task<HttpResponseData> GenerateQRCodeUrl([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "url")] HttpRequestData req)
	{
		try
		{
			this._logger.LogInformation("C# HTTP trigger function processed a request.");

			var model = (await req.ReadFromJsonAsync<QRCodeUrlModel>())!;

			var response = req.CreateResponse(HttpStatusCode.OK);
			response.Headers.Add("Content-Type", "image/png");
			await response.Body.WriteAsync(model.ToQRCode());

			return response;
		}
		catch (Exception error)
		{
			this._logger.LogError(error, "error occured");
			throw;
		}
	}

	// curl -d '{"ssid": "dpbbpm", "password": ""}' http://localhost:7071/qrcode/wifi
	[Function("QRCodeWifi")]
	public async Task<HttpResponseData> GenerateQRCodeWifi([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "wifi")] HttpRequestData req)
	{
		this._logger.LogInformation("C# HTTP trigger function processed a request.");

		var model = (await req.ReadFromJsonAsync<QRCodeWifiModel>())!;

		var response = req.CreateResponse(HttpStatusCode.OK);
		response.Headers.Add("Content-Type", "image/png");
		await response.Body.WriteAsync(model.ToQRCode());

		return response;
	}
}

public abstract record QRCodeBase(bool DisplayContent = false, int Height = 500, int Width = 500, int Margin = 5)
{
	public abstract string Content { get; }

	public byte[] ToQRCode()
	{
		ArgumentNullException.ThrowIfNullOrEmpty(this.Content);

		var writer = new ZXing.SkiaSharp.BarcodeWriter()
		{
			Format = BarcodeFormat.QR_CODE,
			// Renderer = new ZXing.SkiaSharp.Rendering.SKBitmapRenderer(),
			Options = new ZXing.Common.EncodingOptions
			{
				Height = this.Height,
				Width = this.Width,
				PureBarcode = !this.DisplayContent,
				Margin = this.Margin,
				NoPadding = true
			}
		};

		using var qrCode = writer.Write(this.Content);
		using var encoded = qrCode.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);

		return encoded.ToArray();
	}
}

public record QRCodeUrlModel([Required] string Url, bool DisplayContent = false, int Height = 500, int Width = 500, int Margin = 5)
	: QRCodeBase(DisplayContent, Height, Width, Margin)
{
	public override string Content => this.Url;
}

public record QRCodeWifiModel([Required] string SSID, [Required] string Password, string Security = "WPA2", bool Hidden = false, bool DisplayContent = false, int Height = 500, int Width = 500, int Margin = 5)
	: QRCodeBase(DisplayContent, Height, Width, Margin)
{
	public override string Content => $"WIFI:S:{this.SSID};T:{this.Security};P:{this.Password};H:{this.Hidden}";
}
