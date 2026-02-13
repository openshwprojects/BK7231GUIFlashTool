using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace BK7231Flasher
{
	public static class FLoaders
	{
		internal static byte[] GetBinaryFromAssembly(string data)
		{
			var assembly = Assembly.GetExecutingAssembly();
			string resourceName = assembly.GetManifestResourceNames()
				.Single(str => str.Contains($"{data}.bin"));
			using var stream = assembly.GetManifestResourceStream(resourceName);
			using var gzdata = new MemoryStream();
			stream.CopyTo(gzdata);
			return GZToBytes(gzdata.ToArray());
		}

		internal static string GetStringFromAssembly(string data, string ext = ".json")
		{
			var assembly = Assembly.GetExecutingAssembly();
			string resourceName = assembly.GetManifestResourceNames()
				.Single(str => str.Contains($"{data}{ext}"));
			using var stream = assembly.GetManifestResourceStream(resourceName);
			using var reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}

		internal static byte[] GZToBytes(byte[] data)
		{
			using var gzbl = new MemoryStream(data);
			using var gzip = new GZipStream(gzbl, CompressionMode.Decompress);
			using var stre = new MemoryStream();
			gzip.CopyTo(stre);
			return stre.ToArray();
		}
	}
}
