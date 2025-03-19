using System.Timers;
using Azure;
using Azure.AI.DocumentIntelligence;
using AnalyzeDocumentOptions = Azure.AI.DocumentIntelligence.AnalyzeDocumentOptions;
using AnalyzeResult = Azure.AI.DocumentIntelligence.AnalyzeResult;
using Timer = System.Timers.Timer;

namespace FSM.OCRService.Services
{
	public class OcrService
	{
		private readonly ILogger<OcrService> _logger;
		private static ManualResetEventSlim? _limiterMre;
		private static int _limiterCounter;

		public OcrService(ILogger<OcrService> logger/*, Conf conf*/)
		{
			_logger = logger;
			//_conf = conf;
			_limiterCounter = 0;
			_limiterMre = new ManualResetEventSlim(true);

			// Azure OCR Form Recognizer has 15 transactions per one second limitation.

			// Every one second let OCR continue working when the GRPc queue of jobs has values
			// even in case when during previous period of time of one second an overload was detected.
			// By the way of setting _limiterMre
			var limiterReleaseTimer = new Timer(TimeSpan.FromMilliseconds(1100)) { AutoReset = true };
			limiterReleaseTimer.Elapsed += OnLimiterReleaseTimerElapsed;
			limiterReleaseTimer.Start();
		}

		/// <summary>
		/// Reset load counter every one sec with limiterReleaseTimer in constructor 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private static void OnLimiterReleaseTimerElapsed(object? sender, ElapsedEventArgs args)
		{
			Interlocked.Exchange(ref _limiterCounter, 0);

			// Every one second let OCR continue working when the GRPc queue of jobs has values
			// even in case when during previous period of time of one second an overload was detected
			_limiterMre?.Set();
		}

		/// <summary>
		/// OCR processing
		/// </summary>
		/// <param name="dataStream"></param>
		/// <param name="templateId"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public async Task<AnalyzeResult?> ProcessOcr(Stream dataStream, string templateId, CancellationToken cancellationToken)
		{
			string errorMessage = string.Empty;
			if (string.IsNullOrWhiteSpace(Conf.AzureFormRecognizerEndpoint)) errorMessage = "No Azure Form Recognizer Endpoint specified.";
			else if (string.IsNullOrWhiteSpace(Conf.AzureFormRecognizerKey)) errorMessage = "No Azure Form Recognizer Key specified.";

			if (!string.IsNullOrWhiteSpace(errorMessage))
			{
				_logger.LogWarning("{m0}", errorMessage);
				return null;
			}

			try
			{
				// If overload was detected wait for next one second period of time
				_limiterMre?.Wait(cancellationToken);

				// If counter riches 15 then pause all threads in GRPc queue
				// and wait for next period of one second to continue by resetting _limiterMre
				//if (Interlocked.Increment(ref _limiterCounter) == Conf.OcrPerSecondLimitation)
				if (Interlocked.Increment(ref _limiterCounter) == Conf.OcrPerSecondLimitation)
				{
					_limiterMre?.Reset();
					_logger.LogInformation("OCR overload. More then {t1} tasks per second detected.", Conf.OcrPerSecondLimitation);
				}

				// Ensure stream is at the beginning
				if (dataStream.CanSeek)
				{
					dataStream.Position = 0;
				}

				// Convert Stream to BinaryData
				var documentData = await BinaryData.FromStreamAsync(dataStream, cancellationToken);

				// Initialize the client
				var client = new DocumentIntelligenceClient(new Uri(Conf.AzureFormRecognizerEndpoint), new AzureKeyCredential(Conf.AzureFormRecognizerKey));

				// Analyze the document
				var operation = await client.AnalyzeDocumentAsync(
					WaitUntil.Completed, new AnalyzeDocumentOptions(templateId, documentData), cancellationToken); // Enable key-value pairs

				return operation.HasValue ? operation.Value : null;
			}
			catch (Exception e)
			{
				_logger.LogWarning(e, "Something is wrong.");
			}

			return null;
		}
	}
}
