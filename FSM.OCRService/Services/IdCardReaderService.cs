using System.Diagnostics;
using Azure.AI.DocumentIntelligence;
using FSM.OCRService.Models;
using Grpc.Core;
using SkiaSharp;

namespace FSM.OCRService.Services
{
	public class IdCardReaderService(
		ILogger<IdCardReaderService> logger,
		OcrService ocrService,
		IImageStorageService storageService)
		: IdCardReader.IdCardReaderBase
	{
		/// <summary>
		/// ProtoBuff method for OCR called by mobile bff trough gRPC.
		/// </summary>
		/// <param name="request"></param>
		/// <param name="context"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public override async Task<IdCardReadReply> ReadIdCard(IdCardReadRequest request, ServerCallContext context)
		{
			var unSuccessReply = new IdCardReadReply() { Details = new IdCardDetails(), Success = false };

			IdCardReadReply retVal;
			try
			{
				switch (request)
				{
					// Request may have binary data in request.ImageData
					case { HasImageData: true }:
						// Resize image if needed
						var imageDataFromArray = ResizeImageIfNeeded(request.ImageData.ToByteArray());

						// Process all OCR models in Azure Form Recognizer
						retVal = await IdCardReadCycles(imageDataFromArray, request,
							context.CancellationToken);

						break;

					// Or it may contain a storage path request.StoragePath to file in Minio storage
					// after getting image from storage
					case { HasStoragePath: true } when await storageService.GetImageDataFromStorage(
						request.StoragePath, // get file from Minio
						context.CancellationToken) is { } tempFileInfo:

						// Resize image if needed
						var imageDataFromFile = ResizeImageIfNeeded(tempFileInfo);
						tempFileInfo.Delete(); // delete temporary file

						// Process all OCR models in Azure Form Recognizer
						retVal = await IdCardReadCycles(imageDataFromFile, request,
							context.CancellationToken);

						break;

					default:
						logger.LogInformation("{m1}", "Request contains no image data or storage path or storage not contains image file with the name was passed.");
						retVal = unSuccessReply;

						break;
				}
			}
			catch (Exception e)
			{
				logger.LogWarning(e, "Something is wrong");
				retVal = unSuccessReply;
			}

			return retVal;
		}

		public static byte[] ResizeImageIfNeeded(FileInfo imageFile)
		{
			if (imageFile is not { Exists: true })
				throw new ArgumentException("Invalid image file.");

			byte[] imageData = File.ReadAllBytes(imageFile.FullName);

			return ResizeImageIfNeeded(imageData);
		}

		public static byte[] ResizeImageIfNeeded(byte[] imageData)
		{
			using var inputStream = new SKMemoryStream(imageData);
			using var original = SKBitmap.Decode(inputStream);

			if (original == null)
				throw new ArgumentException("Invalid image data.");

			int maxDimension = 4096;
        
			// Check if resizing is needed
			if (original.Width <= maxDimension && original.Height <= maxDimension)
				return imageData; // No resizing needed

			float scale = Math.Min((float)maxDimension / original.Width, (float)maxDimension / original.Height);

			int newWidth = (int)(original.Width * scale);
			int newHeight = (int)(original.Height * scale);

			using var resized = original.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(new SKCubicResampler()));
        
			if (resized == null)
				throw new Exception("Image resizing failed.");

			using var finalImage  = SKImage.FromBitmap(resized);
			using var data = finalImage.Encode(SKEncodedImageFormat.Jpeg, 90);

			return data.ToArray();
		}

		// Models to process in queue in OCR
		private readonly string[] _processingModels =
		{
			Conf.PrebuiltIdDocumentModel,
			Conf.CustomNationalIdDocumentModel
		};


		/// <summary>
		/// Calls OCR processing one or more times depending on document type. Takes byte array as parameter.
		/// If predefined model does not recognize document then try custom ID model.
		/// </summary>
		/// <param name="memBuffer"></param>
		/// <param name="request"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private async Task<IdCardReadReply> IdCardReadCycles(byte[] memBuffer, IdCardReadRequest request,
			CancellationToken cancellationToken)
		{
			IdCardReadReply? result = null;
			string usedModelName = string.Empty;

			foreach (var model in _processingModels)
			{
				await using var ms = new MemoryStream(memBuffer);
				result = await IdCardReadCycleWrapper(ms, model, request, cancellationToken);
				usedModelName = model;
				if (result is { Success: true }) break;
			}

			LogResult(request, result, usedModelName);

			return result!;
		}


		/// <summary>
		///  Calls OCR processing one or more times depending on document type. Takes file as parameter.
		/// </summary>
		/// <param name="tempFileInfo"></param>
		/// <param name="request"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private async Task<IdCardReadReply> IdCardReadCycles(FileInfo tempFileInfo, IdCardReadRequest request,
			CancellationToken cancellationToken)
		{
			IdCardReadReply? result = null;
			string usedModelName = string.Empty;

			foreach (var model in _processingModels)
			{
				await using (var fs = tempFileInfo.OpenRead())
					result = await IdCardReadCycleWrapper(fs, model, request, cancellationToken);

				usedModelName = model;
				if (result is { Success: true }) break;
			}

			LogResult(request, result, usedModelName);

			return result!;
		}


		/// <summary>
		/// Wraps final method where OCR performs
		/// </summary>
		/// <param name="dataStream"></param>
		/// <param name="model"></param>
		/// <param name="request"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private async Task<IdCardReadReply?> IdCardReadCycleWrapper(Stream dataStream, string model, IdCardReadRequest request,
			CancellationToken cancellationToken)
		{
			IdCardReadReply? result = null;
			result = await IdCardReadCycle(dataStream, model, request, cancellationToken) switch
			{
				{ Success: true } res1 => res1,
				{ Success: false } res2 => res2,
				_ => result
			};

			return result;
		}


		private void LogResult(IdCardReadRequest request, IdCardReadReply? result, string usedModelName)
		{
			switch (result)
			{
				case { Success: true }:
					logger.LogInformation("OCR was successful for model: {f1}, recognized type: {f2}, file: {f3}.",
						usedModelName, result.Details.RecognizedType,
						!string.IsNullOrWhiteSpace(request.StoragePath) ? request.StoragePath : "direct image data");
					break;

				case { Success: false }:
					logger.LogInformation("OCR was not successful for model: {f1}, recognized type: {f2}, file: {f3}.",
						"all used models", "not recognized",
						!string.IsNullOrWhiteSpace(request.StoragePath) ? request.StoragePath : "direct image data");
					break;
			}
		}


		/// <summary>
		/// OCR processing. Use Stream instead of byte array as parameter for less memory consumption.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="templateId"></param>
		/// <param name="request"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private async Task<IdCardReadReply> IdCardReadCycle(Stream data, string templateId,
			IdCardReadRequest request, CancellationToken cancellationToken)
		{
			//AnalyzeDocumentOperation? result;

			// Create details for returning value
			var response = new DocReadReply();

			var stopWatch = new Stopwatch();
			stopWatch.Start();

			AnalyzeResult? result;

			try
			{
				result = await ocrService.ProcessOcr(data, templateId, cancellationToken);
			}
			catch (Exception e)
			{
				logger.LogWarning(e, "Something is wrong.");
				return response.IdCardReadReply;
			}

			stopWatch.Stop();
			logger.LogInformation("OCR operation time length: {t0}", stopWatch.ElapsedMilliseconds);

			switch (result)
			{
				// Return unsuccess when OCR result has no value
				case null:
				//case { HasValue: false }:
				//case { Value: null }:
				case { Documents: null } or { Documents.Count: 0 }:
					logger.LogInformation("OCR not successful for model: {f1}, file: {f2}. No documents found.",
						 templateId, request.StoragePath);
					return response.IdCardReadReply;
			}

			var maxDocumentConfidence = result.Documents.Max(d => d.Confidence);
			var document = result.Documents.First(d => d.Confidence >= maxDocumentConfidence);

			response = response with { RecognizedDocumentType = document.DocumentType };

			// Search document with a higher confidence score to extract data
			if (maxDocumentConfidence < Conf.OcrDocumentConfidenceLimitation)
			{
				logger.LogInformation(
					"OCR not successful for model: {f1}, file: {f2}. Document does not of appropriate type with confidence: {f3}.",
					 templateId, request.StoragePath, maxDocumentConfidence);
				return response.IdCardReadReply;
			}

			// Get details for returning value
			var details = response.IdCardReadReply.Details;

			var isPrebuilt = templateId == Conf.PrebuiltIdDocumentModel;

			// List for bad recognized required field names			
			var lowConfidenceFields = new List<string>();

			//Extracting data from OCR result to output details
			#region Extracting data from OCR result to output details

			// Note that "string" type in protobuffer cannot get null value and generates error in case of.
			// float? docNumberConfidence = null;
			switch (isPrebuilt)
			{
				case true:
					var dn1 = document.Fields.GetMrzString(PassportFields.MrzDocumentNumber, out var conf1) ;
					var dn2 = document.Fields.GetString(IsraelIdDocumentFields.DocumentNumber, out var conf2);

					if ((conf2 == null || conf1 >= conf2) && !string.IsNullOrWhiteSpace(dn1))
						details.DocumentNumber = dn1;
					else if((conf1 == null ||conf1 < conf2) && !string.IsNullOrWhiteSpace(dn2))
						details.DocumentNumber = dn2;

					if (conf1 != null && conf2 != null && MathF.Max((float)conf1, (float)conf2) is var conf &&
					    conf < Conf.OcrConfidenceLimitation)
						lowConfidenceFields.Add(IsraelIdDocumentFields.DocumentNumber);
					else if( conf2 == null && conf1 < Conf.OcrConfidenceLimitation)
						lowConfidenceFields.Add(IsraelIdDocumentFields.DocumentNumber);
					else if( conf1 == null && conf2 < Conf.OcrConfidenceLimitation)
						lowConfidenceFields.Add(IsraelIdDocumentFields.DocumentNumber);
					
					break;

				case false:
					// TeudatZehut has two doc number fields. Assume the first one with good confidence starting with
					// the one from the bottom of photo
					details.DocumentNumber = document.Fields.GetMaxConfidenceString(IsraelIdDocumentFields.DocumentNumber,
						IsraelIdDocumentFields.DocumentNumber2, Conf.OcrConfidenceLimitation,
						(s1, s2) => lowConfidenceFields.AddRange([ s1, s2 ]));

					break;
			}

			//details.DocumentNumber = isPrebuilt switch
			//{
			//	true =>
			//		document.Fields.GetMrzString(PassportFields.MrzDocumentNumber, Conf.OcrConfidenceLimitation,
			//			lowConfidenceAction: lowConfidenceFields.Add) is { } res && !string.IsNullOrWhiteSpace(res)
			//			? res
			//			: document.Fields.GetString(IsraelIdDocumentFields.DocumentNumber,
			//				Conf.OcrConfidenceLimitation, lowConfidenceAction: lowConfidenceFields.Add, out _),
			//	// TeudatZehut has two doc number fields. Assume the first one with good confidence starting with
			//	// the one from the bottom of photo
			//	false =>
			//		document.Fields.GetMaxConfidenceString(IsraelIdDocumentFields.DocumentNumber,
			//			IsraelIdDocumentFields.DocumentNumber2, Conf.OcrConfidenceLimitation,
			//			(s1, s2) => lowConfidenceFields.AddRange(new[] { s1, s2 }))
			//};

			switch (isPrebuilt)
			{
				case true:
					var ts1 = document.Fields.GetMrzTimeStamp(PassportFields.MrzDateOfBirth, out var conf1) ;
					var ts2 = document.Fields.GetTimeStamp(IsraelIdDocumentFields.DateOfBirth, out var conf2);

					if ((conf2 == null || conf1 >= conf2) && ts1 != null)
						details.DateOfBirth = ts1;
					else if((conf1 == null ||conf1 < conf2) && ts2 != null)
						details.DateOfBirth = ts2;

					if (conf1 != null && conf2 != null && MathF.Max((float)conf1, (float)conf2) is var conf &&
					    conf < Conf.OcrConfidenceLimitation)
						lowConfidenceFields.Add(IsraelIdDocumentFields.DateOfBirth);
					else if( conf2 == null && conf1 < Conf.OcrConfidenceLimitation)
						lowConfidenceFields.Add(IsraelIdDocumentFields.DateOfBirth);
					else if( conf1 == null && conf2 < Conf.OcrConfidenceLimitation)
						lowConfidenceFields.Add(IsraelIdDocumentFields.DateOfBirth);
					
					break;

				case false:
					details.DateOfBirth = document.Fields.GetTimeStamp(IsraelIdDocumentFields.DateOfBirth, Conf.OcrConfidenceLimitation,
						lowConfidenceAction: lowConfidenceFields.Add);

					break;
			}

			//details.DateOfBirth = isPrebuilt switch
			//{
			//	true => document.Fields.GetMrzTimeStamp(PassportFields.MrzDateOfBirth, Conf.OcrConfidenceLimitation,
			//				lowConfidenceAction: lowConfidenceFields.Add)
			//			?? document.Fields.GetTimeStamp(IsraelIdDocumentFields.DateOfBirth,
			//				Conf.OcrConfidenceLimitation,
			//				lowConfidenceAction: lowConfidenceFields.Add),
			//	false => document.Fields.GetTimeStamp(IsraelIdDocumentFields.DateOfBirth, Conf.OcrConfidenceLimitation,
			//		lowConfidenceAction: lowConfidenceFields.Add)
			//};

			switch (isPrebuilt)
			{
				case true:
					var ts1 = document.Fields.GetMrzTimeStamp(PassportFields.MrzDateOfExpiration, out var conf1) ;
					var ts2 = document.Fields.GetTimeStamp(IsraelIdDocumentFields.DateOfExpiration, out var conf2);

					if ((conf2 == null || conf1 >= conf2) && ts1 != null)
						details.DateOfExpiry = ts1;
					else if((conf1 == null ||conf1 < conf2) && ts2 != null)
						details.DateOfExpiry = ts2;

					if (conf1 != null && conf2 != null && MathF.Max((float)conf1, (float)conf2) is var conf &&
					    conf < Conf.OcrConfidenceLimitation)
						lowConfidenceFields.Add(IsraelIdDocumentFields.DateOfExpiration);
					else if( conf2 == null && conf1 < Conf.OcrConfidenceLimitation)
						lowConfidenceFields.Add(IsraelIdDocumentFields.DateOfExpiration);
					else if( conf1 == null && conf2 < Conf.OcrConfidenceLimitation)
						lowConfidenceFields.Add(IsraelIdDocumentFields.DateOfExpiration);
					
					break;

				case false:
					details.DateOfExpiry = document.Fields.GetTimeStamp(IsraelIdDocumentFields.DateOfExpiration,
						Conf.OcrConfidenceLimitation,
						lowConfidenceAction: lowConfidenceFields.Add);

					break;
			}

			//details.DateOfExpiry = isPrebuilt switch
			//{
			//	true => document.Fields.GetMrzTimeStamp(PassportFields.MrzDateOfExpiration,
			//				Conf.OcrConfidenceLimitation,
			//				lowConfidenceAction: lowConfidenceFields.Add)
			//			?? document.Fields.GetTimeStamp(IsraelIdDocumentFields.DateOfExpiration,
			//				Conf.OcrConfidenceLimitation,
			//				lowConfidenceAction: lowConfidenceFields.Add),
			//	false => document.Fields.GetTimeStamp(IsraelIdDocumentFields.DateOfExpiration,
			//		Conf.OcrConfidenceLimitation,
			//		lowConfidenceAction: lowConfidenceFields.Add)
			//};

			details.DateOfIssue = isPrebuilt switch
			{
				true => document.Fields.GetTimeStamp(PassportFields.DateOfIssue, out _),
				false => document.Fields.GetTimeStamp(IsraelIdDocumentFields.DateOfIssue, out _)
			};

			details.GivenName = isPrebuilt switch
			{
				true => document.Fields.GetMrzString(PassportFields.MrzFirstName, out _) is { } res &&
						!string.IsNullOrEmpty(res)
					? res
					: document.Fields.GetString(IsraelIdDocumentFields.FirstName, out _),
				false => document.Fields.GetString(
					IsraelIdDocumentFields.FirstName, out _)
			};

			details.Surname = isPrebuilt switch
			{
				true => document.Fields.GetMrzString(PassportFields.MrzLastName, out _) is { } res &&
						!string.IsNullOrEmpty(res)
					? res
					: document.Fields.GetString(IsraelIdDocumentFields.LastName, out _),
				false => document.Fields.GetString(
					IsraelIdDocumentFields.LastName, out _)
			};

			#endregion

			//Debug.WriteLine($@"		{details.DocumentNumber} - {docNumberConfidence}, {details.DateOfBirth}, {details.DateOfExpiry}");
			//Debug.WriteLine($@"		Doc type from OCR:	{document.DocumentType}");

			// Verify the bad recognized required fields			
			if (lowConfidenceFields.Any())
			{
				logger.LogInformation(
					"OCR not successful for model: {f1}, file: {f2}. Required fields are absent or have low confidence. {f3}.",
					templateId, request.StoragePath, string.Join(",", lowConfidenceFields));
				return response.IdCardReadReply;
			}

			// Check for outstanding or out of range fields values
			switch (details)
			{
				case { DateOfBirth: null } or { DateOfExpiry: null } or { DocumentNumber: null or "" }:
					logger.LogInformation(
						"OCR not successful for model: {f1}, file: {f2}. Some required fields are absent: {f3}.",
						templateId, request.StoragePath,
						$@"DocumentNumber:{details.DocumentNumber},DateOfBirth:{details.DateOfBirth},DateOfExpiry:{details.DateOfExpiry} ");

					return response.IdCardReadReply;

				case { DateOfBirth: not null } when details.DateOfBirth.ToDateTime() > DateTime.UtcNow:
					logger.LogInformation("OCR not successful for model: {f1}, file: {f2}.  The person from the future cannot rent bike.",
						 templateId, request.StoragePath);

					return response.IdCardReadReply;

				case { DateOfBirth: not null }
					when DateTime.Now.Subtract(details.DateOfBirth.ToDateTime()) > TimeSpan.FromDays(32120)
					: // more then 88 years old
					logger.LogInformation("OCR not successful for model: {f0}, file: {f1}.  Redundant life expectancy.",
						 templateId, request.StoragePath);

					return response.IdCardReadReply;
			}

			return (response with { Success = true }).IdCardReadReply;
		}
	}
}
