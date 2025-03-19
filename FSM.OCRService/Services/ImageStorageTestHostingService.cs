using System.Diagnostics;

namespace FSM.OCRService.Services;

/// <summary>
/// Testing class. Not used in release
/// </summary>
public class ImageStorageTestHostingService(ILogger<ImageStorageTestHostingService> logger, ImageStorageService storage)
	: BackgroundService
{
	private readonly ILogger<ImageStorageTestHostingService> _logger = logger;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// uploads/

		//var b = Environment.GetEnvironmentVariable("app_MinIoSensitive__Bucket");

		var fileNames = new[]
		{
			"60c5d005d93f35cf97e6c7c4-2022-09-15-06-08-39_DriverLicense.jpg",
			"60c5d005d93f35cf97e6c7c4-2022-09-15-06-09-21_NationalId.jpg",
			"60c5d005d93f35cf97e6c7c4-2022-09-15-06-09-42_Passport.jpg",
			"60c5d005d93f35cf97e6c7c4-2022-09-15-06-10-13_DriverLicense.jpg"
		};

		foreach (var file in fileNames)
		{
			var storagePath = "uploads/" + file;
			if (await storage.GetImageDataFromStorage(storagePath, CancellationToken.None) is { } tempFileInfo)
			{
				await using (var ms = tempFileInfo.OpenRead())
				{
					Debug.WriteLine(ms.Length);
				}
					
				tempFileInfo.Delete();
			}
		}
	}
}