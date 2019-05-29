// Copyright (c) 2019 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ConvertEpubToBloom
{
	public class BookMetadata
	{
		public string Identifier;
		public string Title;
		public string LanguageCode;
		public string Description;
		public DateTime Modified;
		public List<string> Authors = new List<string>();
		public List<string> Illustrators = new List<string>();
		public List<string> OtherCreators = new List<string>();
		public List<string> OtherContributors = new List<string>();
		public List<string> PageFiles = new List<string>();
		public List<string> ImageFiles = new List<string>();

		public BookMetadata(string epubFolder)
		{
			var opfPath = GetOpfPath(epubFolder);
			if (String.IsNullOrEmpty(opfPath))
			{
				Console.WriteLine("Could not read rootfile information from META-INF/container.xml!?");
				return;
			}
			var opfDoc = new XmlDocument();
			opfDoc.Load(opfPath);
			var opfNsmgr = new  XmlNamespaceManager(opfDoc.NameTable);
			opfNsmgr.AddNamespace("o", "http://www.idpf.org/2007/opf");
			opfNsmgr.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");
			var identifierItem = opfDoc.SelectSingleNode("/o:package/o:metadata/dc:identifier", opfNsmgr);
			Identifier = identifierItem.InnerText;
			var titleItem = opfDoc.SelectSingleNode("/o:package/o:metadata/dc:title", opfNsmgr);
			Title = titleItem.InnerText;
			var langItem = opfDoc.SelectSingleNode("/o:package/o:metadata/dc:language", opfNsmgr);
			LanguageCode = langItem.InnerText;
			var modifiedItem = opfDoc.SelectSingleNode("/o:package/o:metadata/o:meta[@property='dcterms:modified']", opfNsmgr);
			Modified = DateTime.Parse(modifiedItem.InnerText);
			var descriptionItem = opfDoc.SelectSingleNode("/o:package/o:metadata/dc:description", opfNsmgr);
			Description = descriptionItem.InnerText;
			var creatorItems = opfDoc.SelectNodes("/o:package/o:metadata/dc:creator", opfNsmgr);
			foreach (var node in creatorItems)
			{
				var creator = node as XmlElement;
				var id = creator.GetAttribute("id");
				var refinementNode = opfDoc.SelectSingleNode("/o:package/o:metadata/o:meta[@refines='#"+id+"' and @property='role' and @scheme='marc:relators']", opfNsmgr);
				if (refinementNode == null || refinementNode.InnerText == "aut")
					Authors.Add(creator.InnerText);
				else
					OtherCreators.Add(creator.InnerText);
			}
			var contributorItems = opfDoc.SelectNodes("/o:package/o:metadata/dc:contributor", opfNsmgr);
			foreach (var node in contributorItems)
			{
				var contributor = node as XmlElement;
				var id = contributor.GetAttribute("id");
				var refinementNode = opfDoc.SelectSingleNode("/o:package/o:metadata/o:meta[@refines='#"+id+"' and @property='role' and @scheme='marc:relators']", opfNsmgr);
				if (refinementNode == null || refinementNode.InnerText == "ill")
					Illustrators.Add(contributor.InnerText);
				else
					OtherContributors.Add(contributor.InnerText);
			}
			var chapterItems = opfDoc.SelectNodes("/o:package/o:manifest/o:item[@media-type='application/xhtml+xml' and @id!='toc']", opfNsmgr);
			foreach (var node in chapterItems)
			{
				var chapter = node as XmlElement;
				var href = chapter.GetAttribute("href");
				PageFiles.Add(Path.Combine(epubFolder, "content", href));
			}
			var imageItems = opfDoc.SelectNodes("/o:package/o:manifest/o:item[starts-with(@media-type,'image/')]", opfNsmgr);
			foreach (var node in imageItems)
			{
				var image = node as XmlElement;
				var href = image.GetAttribute("href");
				ImageFiles.Add(Path.Combine(epubFolder, "content", href));
			}
		}

		public static string GetOpfPath(string epubFolder)
		{
			var metaInf = new XmlDocument();
			metaInf.Load(Path.Combine(epubFolder, "META-INF", "container.xml"));
			var nsmgr = new XmlNamespaceManager(metaInf.NameTable);
			nsmgr.AddNamespace("u", "urn:oasis:names:tc:opendocument:xmlns:container");
			var node = metaInf.SelectSingleNode("/u:container/u:rootfiles/u:rootfile", nsmgr) as XmlElement;
			if (node != null)
			{
				var relpath = node.GetAttribute("full-path");
				if (!String.IsNullOrEmpty(relpath))
					return Path.Combine(epubFolder, relpath);
			}
			return null;
		}
	}
}

