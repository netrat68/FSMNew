using EnvironmentLib;

namespace FSM.OCRService;

public readonly record struct Conf
{
	public static string AzureFormRecognizerEndpoint { get; set; } = string.Empty;
	public static string AzureFormRecognizerKey { get; set; } = string.Empty;
	public static string AccessKey { get; set; } = string.Empty;
	public static string AccessSecret { get; set; } = string.Empty;
	public static string Address { get; set; } = string.Empty;
	public static string Bucket { get; set; } = string.Empty;
	public static int OcrPerSecondLimitation { get; set; }
	public static float OcrConfidenceLimitation { get; set; }

	/// <summary>
	/// Environment mode. May be implicitly converted to string for use like prefix.
	/// </summary>
	public static EnvMode? EnvironmentMode { get; set; }

	public static float OcrDocumentConfidenceLimitation { get; set; }
	public static string PrebuiltIdDocumentModel { get; set; } = string.Empty;
	public static string CustomNationalIdDocumentModel { get; set; } = string.Empty;
}
