// Copyright (c) 2019 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;

namespace ConvertEpubToBloom
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			var bookName = "Too Much Noise";
			var baseFolder = "/d/steve/MyTest/BL/BL-6981/";
			ExtractZippedFiles(Path.Combine(baseFolder, bookName)+".epub", Path.Combine(baseFolder, bookName, "epub"));
			var metadata = new BookMetadata(Path.Combine(baseFolder, bookName, "epub"));
			Console.WriteLine("Title = {0}", metadata.Title);
			Console.WriteLine("Author = {0}", String.Join(", ", metadata.Authors));
			Console.WriteLine("Modified = {0}", metadata.Modified.ToString());
			Console.WriteLine("Illustrator = {0}", String.Join(", ", metadata.Illustrators));
			Console.WriteLine("Page files = {0}", String.Join("\n", metadata.PageFiles));
			Console.WriteLine("Image files = {0}", String.Join("\n", metadata.ImageFiles));

			ExtractZippedFiles("/d/steve/MyTest/BL/BL-6981/BlankBloom.zip", Path.Combine(baseFolder, bookName, bookName));
			File.Move(Path.Combine(baseFolder, bookName, bookName, "Book.htm"), Path.Combine(baseFolder, bookName, bookName, bookName+".htm"));
			var bloomDoc = new XmlDocument();
			bloomDoc.PreserveWhitespace = true;
			bloomDoc.Load(Path.Combine(baseFolder, bookName, bookName, bookName + ".htm"));

			var epubConverter = new EpubConverter(Path.Combine(baseFolder, bookName, "epub"), Path.Combine(baseFolder, bookName, bookName), metadata, bloomDoc);
			epubConverter.ConvertBook();
			File.Move(Path.Combine(baseFolder, bookName, bookName, bookName+".htm"), Path.Combine(baseFolder, bookName, bookName, "bookhtml.bak"));
			bloomDoc.Save(Path.Combine(baseFolder,bookName, bookName, bookName+".htm"));
		}

		public static void ExtractZippedFiles(string epubFile, string folder)
		{
			Directory.CreateDirectory(folder);
			var extractPath = folder + Path.DirectorySeparatorChar;		// safer for unknown zip sources
			using (var zipToOpen = new FileStream(epubFile, FileMode.Open))
			{
				using (var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
				{
					foreach (var entry in archive.Entries)
					{
						// Gets the full path to ensure that relative segments are removed.
						string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
						// Ordinal match is safest, case-sensitive volumes can be mounted within volumes that
						// are case-insensitive.
						if (destinationPath.StartsWith(extractPath, StringComparison.Ordinal))
						{
							Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
							var length = (int)entry.Length;	// entry.Open() apparently clears this value, at least for Mono
							using (var reader = new BinaryReader(entry.Open()))
							{
								using (var writer = new BinaryWriter(new FileStream(destinationPath, FileMode.Create)))
								{
									var data = reader.ReadBytes(length);
									writer.Write(data);
									writer.Close();
								}
							}
						}
					}
				}
			}
		}
	}
}
