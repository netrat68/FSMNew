using FSM.OCRService.Services;

namespace OCRServiceTest
{
	internal class LocalImageStorageService : IImageStorageService
	{
		public Task<FileInfo?> GetImageDataFromStorage(string storagePath, CancellationToken cancellationToken)
		{
			var tempFilePath = Path.Join(Path.GetTempPath(), Guid.NewGuid() + "");
			var fileInfo = new FileInfo(storagePath);

			fileInfo.CopyTo(tempFilePath);
			
			return Task.FromResult(new FileInfo(tempFilePath))!;
		}
	}
}
