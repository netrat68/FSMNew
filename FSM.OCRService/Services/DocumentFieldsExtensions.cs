using System.Globalization;
using Azure.AI.DocumentIntelligence;
using FSM.OCRService.Models;
using Google.Protobuf.WellKnownTypes;

namespace FSM.OCRService.Services;

public static class DocumentFieldsExtensions
{
	//public static string ToRecognizeModelName(this IdCardType idCardType)
	//{
	//	return idCardType switch
	//	{
	//		IdCardType.DriverLicense => Conf.PrebuiltIdDocumentModel, // "prebuilt-idDocument",
	//		IdCardType.NationalId => Conf.CustomNationalIdDocumentModel, // "TeudatZehutSmart2",
	//		IdCardType.Passport => Conf.PrebuiltIdDocumentModel, // "prebuilt-idDocument",
	//		IdCardType.Unknown => Conf.CustomNationalIdDocumentModel, // "TeudatZehutSmart2",
	//		_ => throw new ArgumentOutOfRangeException(nameof(idCardType), idCardType, null)
	//	};
	//}

	//public static string ToRecognizeDocumentType(this IdCardType idCardType)
	//{
	//	return idCardType switch
	//	{
	//		IdCardType.DriverLicense => "idDocument.driverLicense", // "prebuilt-idDocument",
	//		IdCardType.NationalId => Conf.CustomNationalIdDocumentModel, // "TeudatZehutSmart2",
	//		IdCardType.Passport => "idDocument.passport", // "prebuilt-idDocument",
	//		IdCardType.Unknown => Conf.CustomNationalIdDocumentModel, // "TeudatZehutSmart2",
	//		_ => throw new ArgumentOutOfRangeException(nameof(idCardType), idCardType, null)
	//	};
	//}

	private static void AnalyzeString(string fieldKey, string? value, float? confidence, float minConfidence)
	{
		switch (value, confidence, minConfidence)
		{
			case (null or "", null, not 0):
				break;
			case (not null and not "", not null, not 0) when string.IsNullOrWhiteSpace(value) || confidence < minConfidence:
				throw new ArgumentOutOfRangeException(fieldKey, $@"Required field not found or low confidence.");
			case (_, _, 0):
				throw new ArgumentOutOfRangeException(nameof(minConfidence), "Confidence argument not passed.");
		}
	}

	private static void AnalyzeString(string fieldKey1, string fieldKey2, string? value, float? confidence, float minConfidence)
	{
		switch (value, confidence, minConfidence)
		{
			case (null or "", null, not 0):
				break;
			case (not null and not "", not null, not 0) when string.IsNullOrWhiteSpace(value) || confidence < minConfidence:
				throw new ArgumentOutOfRangeException($@"{fieldKey1}, {fieldKey2}", $@"Required field not found or low confidence.");
			case (_, _, 0):
				throw new ArgumentOutOfRangeException(nameof(minConfidence), "Confidence argument not passed.");
		}
	}

	private static void AnalyzeString(string fieldKey1, string fieldKey2, string? value, float? confidence,
		float minConfidence, Action<string, string> lowConfidenceAction)
	{
		switch (value, confidence, minConfidence)
		{
			case (null or "", null, not 0):
				break;
			case (not null and not "", not null, not 0)
				when string.IsNullOrWhiteSpace(value) || confidence < minConfidence:
				lowConfidenceAction(fieldKey1, fieldKey2);
				break;
			case (_, _, 0):
				throw new ArgumentOutOfRangeException(nameof(minConfidence), "Confidence argument not passed.");
		}
	}

	private static void AnalyzeString(string fieldKey, string? value, float? confidence, float minConfidence,
		Action<string> lowConfidenceAction)
	{
		switch (value, confidence, minConfidence)
		{
			case (null or "", null, not 0):
				break;
			case (not null and not "", not null, not 0) when string.IsNullOrWhiteSpace(value) || confidence < minConfidence:
				lowConfidenceAction(fieldKey);
				break;
			case (_, _, 0):
				throw new ArgumentOutOfRangeException(nameof(minConfidence), "Confidence argument not passed.");
		}
	}

	private static void AnalyzeString(string fieldKey, string? value, float? confidence, float minConfidence,
		Action<float?> lowConfidenceAction)
	{
		switch (value, confidence, minConfidence)
		{
			case (null or "", null, not 0):
				break;
			case (not null and not "", not null, not 0) when string.IsNullOrWhiteSpace(value) || confidence < minConfidence:
				lowConfidenceAction(confidence);
				break;
			case (_, _, 0):
				throw new ArgumentOutOfRangeException(nameof(minConfidence), "Confidence argument not passed.");
		}
	}

	private static void AnalyzeTimeStamp(string fieldKey, Timestamp? value, float? confidence, float minConfidence)
	{
		switch (value, confidence, minConfidence)
		{
			case (null, null, not 0):
				break;
			case (not null, not null, not 0) when confidence < minConfidence:
				throw new ArgumentOutOfRangeException(fieldKey, $@"Required field not found or low confidence.");
			case (_, _, 0):
				throw new ArgumentOutOfRangeException(nameof(minConfidence), "Confidence argument not passed.");
		}
	}

	private static void AnalyzeTimeStamp(string fieldKey, Timestamp? value, float? confidence, float minConfidence,
		Action<string> lowConfidenceAction)
	{
		switch (value, confidence, minConfidence)
		{
			case (null, null, not 0):
				break;
			case (not null, not null, not 0) when confidence < minConfidence:
				lowConfidenceAction(fieldKey);
				break;
			case (_, _, 0):
				throw new ArgumentOutOfRangeException(nameof(minConfidence), "Confidence argument not passed.");
		}
	}

	public static Timestamp? GetMrzTimeStamp(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		out float? confidence)
	{
		var mrzDictionary = fields.TryGetValue(PassportFields.MachineReadableZone, out var dictionary)
			? dictionary.ValueDictionary
			: null;

		var retVal = mrzDictionary != null
			? mrzDictionary.TryGetValue(fieldKey, out var documentField) && documentField.ValueDate != null
				? Timestamp.FromDateTimeOffset((DateTimeOffset)documentField.ValueDate)
				: null
			: null;

		confidence = dictionary?.Confidence;

		return retVal;
	}


	public static Timestamp? GetMrzTimeStamp(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		float minConfidence)
	{
		var retVal = fields.GetMrzTimeStamp(fieldKey, out var confidence);

		// Throw exception if something is wrong
		AnalyzeTimeStamp(fieldKey, retVal, confidence, minConfidence);

		return retVal;
	}

	public static Timestamp? GetMrzTimeStamp(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		float minConfidence, Action<string> lowConfidenceAction)
	{
		var retVal = fields.GetMrzTimeStamp(fieldKey, out var confidence);

		// Launch callback if something is wrong 
		AnalyzeTimeStamp(fieldKey, retVal, confidence, minConfidence, lowConfidenceAction);

		return retVal;
	}

	public static string GetMrzString(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		out float? confidence)
	{
		var mrzDictionary = fields.TryGetValue(PassportFields.MachineReadableZone, out var dictionary)
			? dictionary.ValueDictionary
			: null;

		var retVal = mrzDictionary != null
			? mrzDictionary.TryGetValue(fieldKey, out var documentField) && documentField.ValueString != null
				? documentField.ValueString.Trim()
				: string.Empty
			: string.Empty;

		confidence = dictionary?.Confidence;

		return retVal;
	}

	public static string GetMrzString(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		float minConfidence)
	{
		var retVal = fields.GetMrzString(fieldKey, out var confidence);

		// Throw exception if something is wrong
		AnalyzeString(fieldKey, retVal, confidence, minConfidence);

		return retVal;
	}

	public static string GetMrzString(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		float minConfidence, Action<string> lowConfidenceAction)
	{
		var retVal = fields.GetMrzString(fieldKey, out var confidence);

		// Launch callback if something is wrong 
		AnalyzeString(fieldKey, retVal, confidence, minConfidence, lowConfidenceAction);

		return retVal;
	}

	public static string GetMrzString(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		float minConfidence, Action<float?> lowConfidenceAction)
	{
		var retVal = fields.GetMrzString(fieldKey, out var confidence);

		// Launch callback if something is wrong 
		AnalyzeString(fieldKey, retVal, confidence, minConfidence, lowConfidenceAction);

		return retVal;
	}

	/// <summary>
	/// Get string value from DocumentField.
	/// </summary>
	/// <param name="fields"></param>
	/// <param name="fieldKey"></param>
	/// <param name="confidence"></param>
	/// <returns></returns>
	public static string GetString(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		out float? confidence)
	{
		var retVal = fields.TryGetValue(fieldKey, out var documentField) && documentField.ValueString != null
			? !string.IsNullOrWhiteSpace(documentField.ValueString)
				? documentField.ValueString.Trim()
				: string.Empty
			: string.Empty;

		confidence = documentField?.Confidence;

		return retVal;
	}

	/// <summary>
	/// Get string value from DocumentField.
	/// </summary>
	/// <param name="fields"></param>
	/// <param name="fieldKey"></param>
	/// <param name="minConfidence"></param>
	/// <param name="resConfidence"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static string GetString(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		float minConfidence, out float? resConfidence)
	{
		var retVal = fields.GetString(fieldKey, out var confidence);

		// Throw exception if something is wrong
		AnalyzeString(fieldKey, retVal, confidence, minConfidence);

		resConfidence = confidence;

		return retVal;
	}

	/// <summary>
	/// Get string value from DocumentField.
	/// </summary>
	/// <param name="fields"></param>
	/// <param name="fieldKey"></param>
	/// <param name="minConfidence"></param>
	/// <param name="lowConfidenceAction">Callback when confidence lower then "minConfidence" value.</param>
	/// <param name="resConfidence"></param>
	/// <returns></returns>
	public static string GetString(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		float minConfidence, Action<string> lowConfidenceAction, out float? resConfidence)
	{
		var retVal = fields.GetString(fieldKey, out var confidence);

		// Launch callback if something is wrong 
		AnalyzeString(fieldKey, retVal, confidence, minConfidence, lowConfidenceAction);

		resConfidence = confidence;

		return retVal;
	}

	/// <summary>
	/// Get TimeStamp value from DocumentField.
	/// </summary>
	/// <param name="fields"></param>
	/// <param name="fieldKey"></param>
	/// <param name="confidence"></param>
	/// <returns></returns>
	public static Timestamp? GetTimeStamp(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		out float? confidence)
	{
		Timestamp? retVal = null;

		if (fields.TryGetValue(fieldKey, out var documentField) && !string.IsNullOrWhiteSpace(documentField.Content))
		{
			retVal = documentField  switch
			{
				not null when documentField.FieldType == DocumentFieldType.Date &&  documentField.ValueDate != null =>
					Timestamp.FromDateTimeOffset((DateTimeOffset)documentField.ValueDate),

				not null when documentField.FieldType == DocumentFieldType.Date && DateTimeOffset.TryParse(documentField.Content,
					CultureInfo.InvariantCulture, out var dateTimeOffset) => Timestamp
					.FromDateTimeOffset(dateTimeOffset),

				_ => null
			};
		}

		confidence = documentField?.Confidence;

		return retVal;
	}

	/// <summary>
	/// Get TimeStamp value from DocumentField.
	/// </summary>
	/// <param name="fields"></param>
	/// <param name="fieldKey"></param>
	/// <param name="minConfidence"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static Timestamp? GetTimeStamp(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		float minConfidence)
	{
		var retVal = fields.GetTimeStamp(fieldKey, out var confidence);

		// Throw exception if something is wrong
		AnalyzeTimeStamp(fieldKey, retVal, confidence, minConfidence);

		return retVal;
	}

	/// <summary>
	/// Get TimeStamp value from DocumentField.
	/// </summary>
	/// <param name="fields"></param>
	/// <param name="fieldKey"></param>
	/// <param name="minConfidence"></param>
	/// <param name="lowConfidenceAction">Callback when confidence lower then "minConfidence" value.</param>
	/// <returns></returns>
	public static Timestamp? GetTimeStamp(this IReadOnlyDictionary<string, DocumentField> fields, string fieldKey,
		float minConfidence, Action<string> lowConfidenceAction)
	{
		var retVal = fields.GetTimeStamp(fieldKey, out var confidence);

		// Launch callback if something is wrong 
		AnalyzeTimeStamp(fieldKey, retVal, confidence, minConfidence, lowConfidenceAction);

		return retVal;
	}

	/// <summary>
	/// Get string value from one of two DocumentField with higher confidence.
	/// </summary>
	/// <param name="fields"></param>
	/// <param name="fieldKey1"></param>
	/// <param name="fieldKey2"></param>
	/// <param name="minConfidence"></param>
	/// <param name="confidence"></param>
	/// <returns></returns>
	public static string GetMaxConfidenceString(this IReadOnlyDictionary<string, DocumentField> fields,
		string fieldKey1, string fieldKey2, float minConfidence, out float? confidence)
	{
		var retVal = (fields.TryGetValue(fieldKey1, out var df1),
				fields.TryGetValue(fieldKey2, out var df2)) switch
		{
			(true, false) => df1,
			(false, true) => df2,
			(false, false) => null,
			(true, true) => (df1, df2) switch
			{
				var val when val.df1!.Confidence >= minConfidence => df1, // at first place to return
				var val when val.df2!.Confidence >= minConfidence => df2,
				_ => null
			}
		};

		confidence = retVal?.Confidence;

		return retVal?.ValueString?.Trim() ?? string.Empty;
	}

	/// <summary>
	/// Get string value from one of two DocumentField with higher confidence.
	/// </summary>
	/// <param name="fields"></param>
	/// <param name="fieldKey1"></param>
	/// <param name="fieldKey2"></param>
	/// <param name="minConfidence"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static string GetMaxConfidenceString(this IReadOnlyDictionary<string, DocumentField> fields,
		string fieldKey1, string fieldKey2, float minConfidence)
	{
		var retVal = fields.GetMaxConfidenceString(fieldKey1, fieldKey2, minConfidence, out var confidence);

		// Throw exception if something is wrong
		AnalyzeString(fieldKey1, fieldKey2, retVal, confidence, minConfidence);

		return retVal;
	}

	/// <summary>
	/// Get string value from one of two DocumentField with higher confidence.
	/// </summary>
	/// <param name="fields"></param>
	/// <param name="fieldKey1"></param>
	/// <param name="fieldKey2"></param>
	/// <param name="minConfidence"></param>
	/// <param name="lowConfidenceAction">Callback when confidence lower then "minConfidence" value.</param>
	/// <returns></returns>
	public static string GetMaxConfidenceString(this IReadOnlyDictionary<string, DocumentField> fields,
		string fieldKey1, string fieldKey2, float minConfidence, Action<string, string> lowConfidenceAction)
	{
		var retVal = fields.GetMaxConfidenceString(fieldKey1, fieldKey2, minConfidence, out var confidence);

		// Launch callback if something is wrong 
		AnalyzeString(fieldKey1, fieldKey2, retVal, confidence, minConfidence, lowConfidenceAction);

		return retVal;
	}
}