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
				.Single(str => str.EndsWith($"{data}.bin"));

			using(Stream stream = assembly.GetManifestResourceStream(resourceName))
			using(MemoryStream gzdata = new MemoryStream())
			{
				stream.CopyTo(gzdata);
				return GZToBytes(gzdata.ToArray());
			}
		}

		internal static byte[] GZToBytes(byte[] data)
		{
			using(var gzbl = new MemoryStream(data))
			using(var gzip = new GZipStream(gzbl, CompressionMode.Decompress))
			using(var stre = new MemoryStream())
			{
				gzip.CopyTo(stre);
				return stre.ToArray();
			}
		}
	}
}
