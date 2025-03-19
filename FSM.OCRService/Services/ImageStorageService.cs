using Minio;
using Minio.DataModel.Args;

namespace FSM.OCRService.Services
{
	public class ImageStorageService(ILogger<ImageStorageService> logger) : IImageStorageService
	{
		/// <summary>
		/// Get image from Minio storage
		/// </summary>
		/// <param name="storagePath"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<FileInfo?> GetImageDataFromStorage(string storagePath, CancellationToken cancellationToken)
		{
			string? errorMessage = null;

			if (string.IsNullOrWhiteSpace(Conf.Bucket)) errorMessage =
				"No MinIo Sensitive Bucket specified.";
			else if (string.IsNullOrWhiteSpace(Conf.Address))
				errorMessage = "No MinIo Sensitive Address specified.";
			else if (string.IsNullOrWhiteSpace(Conf.AccessKey))
				errorMessage
					= "No MinIo Sensitive AccessKey specified.";
			else if (string.IsNullOrWhiteSpace(Conf.AccessSecret))
				errorMessage = "No MinIo Sensitive AccessSecret specified.";

			if (!string.IsNullOrWhiteSpace(errorMessage))
			{
				logger.LogWarning("{m0}", errorMessage);
				return null;
			}

			var tempFilePath = Path.Join(Path.GetTempPath(), Guid.NewGuid() + "");

			var args = new GetObjectArgs()
				.WithBucket(Conf.Bucket)
				.WithObject(storagePath)
				.WithFile(tempFilePath)
				.WithServerSideEncryption(null);

			try
			{
				using var minioClient = new MinioClient()
					.WithEndpoint(Conf.Address)
					.WithCredentials(Conf.AccessKey, Conf.AccessSecret)
					.WithSSL(false)
					.Build();

				_ = await minioClient.GetObjectAsync(args, cancellationToken);
			}
			catch (Exception e)
			{
				logger.LogWarning(e, "Error while getting image from Minio storage.");
				return null;
			}

			return new FileInfo(tempFilePath);
		}
	}
}
