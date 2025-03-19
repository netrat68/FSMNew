using Microsoft.Extensions.Hosting;

namespace EnvironmentLib
{
	public static class HostEnvironmentExtensions
	{
		public static bool IsTesting(this IHostEnvironment hostEnvironment)
		{
			return hostEnvironment.IsEnvironment("Testing");
		}
	}

	/// <summary>
	/// Environment mode. May be implicitly converted to string for use like prefix.
	/// </summary>
	/// <param name="Value"></param>
	public readonly record struct EnvMode(string Value, string PodNamePrefix)
	{
		public static implicit operator string(EnvMode record) => record.Value;
	}

	/// <summary>
	/// Environment modes.
	/// </summary>
	/// <param name="Value"></param>
	public readonly record struct EnvModes(string Value)
	{
		/// <summary>
		/// Development mode. May be implicitly converted to "DEVELOPMENT_" for use like prefix.
		/// </summary>
		public static readonly EnvMode Development = new ("DEVELOPMENT_", "development-");

		/// <summary>
		/// Testing mode. May be implicitly converted to "TESTING_" for use like prefix.
		/// </summary>
		public static readonly EnvMode Testing = new ("TESTING_", "testing-");

		/// <summary>
		/// Staging mode. May be implicitly converted to "STAGING_" for use like prefix.
		/// </summary>
		public static readonly EnvMode Staging = new ("STAGING_", "staging-");

		/// <summary>
		/// Production mode. May be implicitly converted to "PRODUCTION_" for use like prefix.
		/// </summary>
		public static readonly EnvMode Production = new ("PRODUCTION_", "production-");
	}
}
