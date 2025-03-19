using System.Diagnostics;
using System.Reflection.Metadata;
using System.Threading;
using Azure;
using FSM.OCRService;
using FSM.OCRService.Models;
using FSM.OCRService.Services;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using static System.Net.Mime.MediaTypeNames;


namespace OCRServiceTest
{
	[TestClass]
	public class OcrServiceServiceTest
	{
		private static ServiceProvider? _serviceProvider;

		private static CancellationTokenSource? _cancellationSource;

		[ClassInitialize]
		public static void ClassInit(TestContext context)
		{
			_cancellationSource = context.CancellationTokenSource;

			var builder = WebApplication.CreateBuilder();

			var services = builder.Services;

			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.WriteTo.Console().WriteTo.Debug()
				//.WriteTo.File("logs/myapp.txt", rollingInterval: RollingInterval.Day)
				.CreateLogger();

			services.AddLogging(configure => configure.AddSerilog(Log.Logger));
			//services.AddSingleton(new Conf());
			services.AddScoped<OcrService>();
			services.AddScoped<IImageStorageService, LocalImageStorageService>();
			services.AddScoped<IdCardReaderService>();

			_serviceProvider = services.BuildServiceProvider();
		}

		[TestInitialize]
		public void Initialize()
		{
			Conf.AzureFormRecognizerEndpoint = "https://fsm-ocr.cognitiveservices.azure.com/";
			Conf.AzureFormRecognizerKey = "780ac7372fe6473b8208d739d15dac0d";
			Conf.OcrPerSecondLimitation = 15;
			Conf.OcrDocumentConfidenceLimitation = .32f;
			Conf.OcrConfidenceLimitation = .5f;
			Conf.PrebuiltIdDocumentModel = "prebuilt-idDocument";
			Conf.CustomNationalIdDocumentModel = "TeudatZehutSmart4";
		}

		[TestMethod]
		public async Task TestOcrServiceServiceTest()
		{
			var ct = _cancellationSource!.Token;

			var fileNames =
				Directory.GetFiles(@"C:\MyApplications\by_bikesh\BikeSharingResources\OCR source images\TestSet1\Originals");
			//var fileNames = Directory.GetFiles("C:\\MyApplications\\BikeSharingResources\\OCR source images\\TestSet2");
			foreach (var fileName in fileNames)
			{
				await using var scope = _serviceProvider?.CreateAsyncScope();
				var ocrService = scope?.ServiceProvider.GetRequiredService<OcrService>();
				await using var fileStream = new FileInfo(fileName).Open(FileMode.Open);
				var res = await ocrService!.ProcessOcr(fileStream, Conf.CustomNationalIdDocumentModel, ct);

				if (res == null) break;

				foreach (var doc in res.Documents)
				{
					doc.Fields.TryGetValue(IsraelIdDocumentFields.DocumentNumber.Name, out var docNumber);
					doc.Fields.TryGetValue(IsraelIdDocumentFields.DateOfBirth.Name, out var dateOfBirth);
					doc.Fields.TryGetValue(IsraelIdDocumentFields.DateOfExpiration.Name, out var dateOfExpiration);

					Debug.WriteLine($@"File: {Path.GetFileName(fileName)}, Doc type: {doc.DocumentType}, Fields count: {doc.Fields.Count}, Doc confidence: {doc.Confidence}");
					Debug.WriteLine($@"	 DocumentNumber: {docNumber?.Content}, Confidence: {docNumber?.Confidence}");
					Debug.WriteLine($@"	 DateOfBirth: {dateOfBirth?.Content}, Confidence: {dateOfBirth?.Confidence}");
					Debug.WriteLine($@"	 DateOfExpiration: {dateOfExpiration?.Content}, Confidence: {dateOfBirth?.Confidence}");

					//foreach (var o in (IEnumerable)field.Value)
					//{

					//}

					//Debug.WriteLine($@"File: {Path.GetFileName(fileName)}:");
					//Debug.WriteLine($@"	Document type: {doc.DocumentType}, Fields count: {doc.Fields.Count}, Document confidence: {doc.Confidence}");
					//var docNumber = doc.Fields.GetString(PassportFields.DocumentNumber, out var docNumberConfidence);
					//var dateOfBirth =
					//	doc.Fields.GetTimeStamp(PassportFields.DateOfBirth, out var docDateOfBirthConfidence);

					//Debug.WriteLine($@"		Document number: {docNumber}, Document number confidence: {docNumberConfidence}");
					//Debug.WriteLine($@"		Date of birth: {dateOfBirth}, Date of birth confidence: {docDateOfBirthConfidence}");
				}
			}
		}

		[TestMethod]
		public async Task TestOcrServiceIdCardReaderTest()
		{
			var ct = _cancellationSource!.Token;

			var fileNames = Directory.GetFiles(@"C:\MyApplications\by_bikesh\BikeSharingResources\OCR source images\TestSet2");
			foreach (var fileName in fileNames)
			{
				var f = Path.GetFileName(fileName);

				Debug.WriteLine($@"Begin: {f}.");

				//if (f.Equals("pass.jpg"))
				//	Debugger.Break();

				await using var scope = _serviceProvider?.CreateAsyncScope();
				var idReaderService = scope?.ServiceProvider.GetRequiredService<IdCardReaderService>();
				ByteString byteString;

				var type = IdCardType.DriverLicense;


				var retVal =
					await idReaderService?.ReadIdCard(
						new IdCardReadRequest() { Type = type, StoragePath = fileName },
						TestServerCallContext.Create())!;

				if (retVal is not { Details: not null, Success: true })
				{
					Debug.WriteLine($@"	Unsuccess: {Path.GetFileName(fileName)}.");
					continue;
				}

				var det = retVal.Details;

				Debug.WriteLine($@"File: {Path.GetFileName(fileName)}, Expected doc type: {type}");
				Debug.WriteLine($@"	 DocumentNumber: {det.DocumentNumber}");
				Debug.WriteLine($@"	 DateOfBirth: {det.DateOfBirth}");
				Debug.WriteLine($@"	 DateOfExpiration: {det.DateOfExpiry}");

				//foreach (var o in (IEnumerable)field.Value)
				//{

				//}

				//Debug.WriteLine($@"File: {Path.GetFileName(fileName)}:");
				//Debug.WriteLine($@"	Document type: {doc.DocumentType}, Fields count: {doc.Fields.Count}, Document confidence: {doc.Confidence}");
				//var docNumber = doc.Fields.GetString(PassportFields.DocumentNumber, out var docNumberConfidence);
				//var dateOfBirth =
				//	doc.Fields.GetTimeStamp(PassportFields.DateOfBirth, out var docDateOfBirthConfidence);

				//Debug.WriteLine($@"		Document number: {docNumber}, Document number confidence: {docNumberConfidence}");
				//Debug.WriteLine($@"		Date of birth: {dateOfBirth}, Date of birth confidence: {docDateOfBirthConfidence}");

			}
		}

	}
}