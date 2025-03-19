namespace FSM.OCRService.Services
{
	public interface IImageStorageService
	{
		public Task<FileInfo?> GetImageDataFromStorage(string storagePath, CancellationToken cancellationToken);
	}
}
