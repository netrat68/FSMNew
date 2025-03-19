using Azure.AI.DocumentIntelligence;

namespace FSM.OCRService.Models
{
	public readonly struct PassportFields
	{
		public static readonly DocumentFieldInfo MachineReadableZone = new("MachineReadableZone", DocumentFieldType.Dictionary);

		public static readonly DocumentFieldInfo MrzDocumentNumber = new("DocumentNumber", DocumentFieldType.String);

		public static readonly DocumentFieldInfo MrzFirstName = new("FirstName",
			DocumentFieldType.String);

		public static readonly DocumentFieldInfo MrzLastName = new("LastName",
			DocumentFieldType.String);

		public static readonly DocumentFieldInfo MrzDateOfBirth = new("DateOfBirth",
			DocumentFieldType.Date);

		public static readonly DocumentFieldInfo
			DateOfIssue = new("DateOfIssue", DocumentFieldType.Date);

		public static readonly DocumentFieldInfo MrzDateOfExpiration =
			new("DateOfExpiration", DocumentFieldType.Date);

		//public static readonly Dictionary<string, DocumentFieldInfo> Fields = new(
		//	new[]
		//	{
		//		new KeyValuePair<string, DocumentFieldInfo>(FirstName, FirstName),
		//		new KeyValuePair<string, DocumentFieldInfo>(LastName, LastName),
		//		new KeyValuePair<string, DocumentFieldInfo>(DocumentNumber,DocumentNumber),
		//		new KeyValuePair<string, DocumentFieldInfo>(DateOfBirth, DateOfBirth),
		//		new KeyValuePair<string, DocumentFieldInfo>(DateOfIssue, DateOfIssue),
		//		new KeyValuePair<string, DocumentFieldInfo>(DateOfExpiration, DateOfExpiration)
		//	});
	}

	public readonly struct IsraelIdDocumentFields
	{
		public static readonly DocumentFieldInfo DocumentNumber =
			new("DocumentNumber", DocumentFieldType.String);

		public static readonly DocumentFieldInfo DocumentNumber2 =
			new("DocumentNumber2", DocumentFieldType.String);

		public static readonly DocumentFieldInfo FirstName = new("FirstName", DocumentFieldType.String);

		public static readonly DocumentFieldInfo LastName = new("LastName", DocumentFieldType.String);

		public static readonly DocumentFieldInfo
			DateOfBirth = new("DateOfBirth", DocumentFieldType.Date);

		public static readonly DocumentFieldInfo
			DateOfIssue = new("DateOfIssue", DocumentFieldType.Date);

		public static readonly DocumentFieldInfo DateOfExpiration =
			new("DateOfExpiration", DocumentFieldType.Date);

		//public static readonly Dictionary<string, DocumentFieldInfo> Fields = new(
		//	new[]
		//	{
		//		new KeyValuePair<string, DocumentFieldInfo>(FirstName, FirstName),
		//		new KeyValuePair<string, DocumentFieldInfo>(LastName, LastName),
		//		new KeyValuePair<string, DocumentFieldInfo>(DocumentNumber,DocumentNumber),
		//		new KeyValuePair<string, DocumentFieldInfo>(DocumentNumber2,DocumentNumber2),
		//		new KeyValuePair<string, DocumentFieldInfo>(DateOfBirth, DateOfBirth),
		//		new KeyValuePair<string, DocumentFieldInfo>(DateOfIssue, DateOfIssue),
		//		new KeyValuePair<string, DocumentFieldInfo>(DateOfExpiration, DateOfExpiration)
		//	});
	}

	public record struct DocumentFieldInfo(string Name, DocumentFieldType ExpectedFieldType)
	{
		public DocumentFieldType FieldType { get; set; }
		public float? Confidence { get; set; }
		public object? Content { get; set; }

		public static implicit operator string(DocumentFieldInfo record) => record.Name;
	}

	public readonly record struct DocReadReply()
	{
		public IdCardReadReply IdCardReadReply { get; } = new() {Success  = false, Details = new IdCardDetails() };

		public string RecognizedDocumentType
		{
			get => IdCardReadReply.Details.RecognizedType;
			set
			{
				if (!string.IsNullOrWhiteSpace(value) &&  IdCardReadReply.Details.RecognizedType != value)
				{
					IdCardReadReply.Details.RecognizedType = value;
					IdCardReadReply.Details.DocumentType = value switch
					{
						"idDocument.passport" => IdCardType.Passport,
						"idDocument.driverLicense" => IdCardType.DriverLicense,
						not null when value.Equals(Conf.CustomNationalIdDocumentModel) => IdCardType.NationalId,
						"idDocument.nationalIdentityCard" => IdCardType.NationalId,
						"idDocument.residencePermit" => IdCardType.NationalId,
						_ => IdCardType.Unknown
					};
				}
			}
		}

		public bool Success
		{
			get => IdCardReadReply.Success;
			set
			{
				if (IdCardReadReply.Success != value)
				{
					IdCardReadReply.Success = value;
				}
			}
		}
	}
}
