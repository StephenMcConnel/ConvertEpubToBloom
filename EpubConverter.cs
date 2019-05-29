// Copyright (c) 2019 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Text;

namespace ConvertEpubToBloom
{
	public class EpubConverter
	{
		private BookMetadata _metadata;
		private string _epubFolder;
		private string _bookFolder;
		private XmlDocument _bloomDoc;

		public EpubConverter(string epubFolder, string bookFolder, BookMetadata metadata, XmlDocument bloomDoc)
		{
			_metadata = metadata;
			_epubFolder = epubFolder;
			_bookFolder = bookFolder;
			_bloomDoc = bloomDoc;
		}

		public void ConvertBook()
		{
			foreach (var imageFile in _metadata.ImageFiles)
			{
				var destPath = Path.Combine(_bookFolder, Path.GetFileName(imageFile));
				File.Copy(imageFile, destPath);
			}

			SetDataDivTextValue("contentLanguage1", _metadata.LanguageCode);
			SetDataDivTextValue("smallCoverCredits", "");

			for (int pageNumber = 0; pageNumber < _metadata.PageFiles.Count; ++pageNumber)
			{
				ConvertPage(pageNumber, _metadata.PageFiles[pageNumber]);
			}
			Console.WriteLine("DEBUG: at least we didn't crash!");
		}

		private void ConvertPage(int pageNumber, string pageFilePath)
		{
			if (pageNumber == 0)
				ConvertCoverPage(pageFilePath);
			else if (!ConvertContentPage(pageFilePath, pageNumber))
				ConvertEndCreditsPage(pageFilePath);
		}

		private void ConvertCoverPage(string pageFilePath)
		{
			var pageDoc = new XmlDocument();
			pageDoc.Load(pageFilePath);
			var nsmgr = new XmlNamespaceManager(pageDoc.NameTable);
			nsmgr.AddNamespace("x", "http://www.w3.org/1999/xhtml");
			var body = pageDoc.SelectSingleNode("/x:html/x:body", nsmgr);
			bool imageSet = false;
			bool titleSet = false;
			bool authorEtcSet = false;
			for (int i = 0; i < body.ChildNodes.Count; ++i)
			{
				var child = body.ChildNodes[i];
				if (child.Name == "img")
				{
					var imageFile = (child as XmlElement).GetAttribute("src");
					// cover image always comes first
					if (!imageSet)
					{
						SetCoverImage(imageFile);
						imageSet = true;
					}
					else
					{
						AddInnerCoverImage(imageFile);
					}
				}
				if (child.Name == "p")
				{
					if (!titleSet)
					{
						var title = (child as XmlElement).InnerText.Trim();
						SetTitle(title);
						titleSet = true;
					}
					else
					{
						AddCoverContributor(child.OuterXml);
						authorEtcSet = true;
					}
				}
			}
			if (!titleSet)
				SetTitle(_metadata.Title);
			if (!authorEtcSet)
			{
				// TODO: make & < and > safe for XML.
				var bldr = new StringBuilder();
				if (_metadata.Authors.Count > 0)
				{
					bldr.Append("<p>");
					if (_metadata.Authors.Count == 1)
						bldr.AppendFormat("Author: {0}", _metadata.Authors[0]);
					else
						bldr.AppendFormat("Authors: {0}", String.Join(", ", _metadata.Authors));
					bldr.Append("</p>");
				}
				if (_metadata.Illustrators.Count > 0)
				{
					if (bldr.Length > 0)
						bldr.AppendLine();
					bldr.Append("<p>");
					if (_metadata.Illustrators.Count == 1)
						bldr.AppendFormat("Illustrator: {0}", _metadata.Illustrators[0]);
					else
						bldr.AppendFormat("Illustrators: {0}", String.Join(", ", _metadata.Illustrators));
					bldr.Append("</p>");
				}
				if (_metadata.OtherCreators.Count > 0)
				{
					if (bldr.Length > 0)
						bldr.AppendLine();
					bldr.Append("<p>");
					if (_metadata.OtherCreators.Count == 1)
						bldr.AppendFormat("Creator: {0}", _metadata.OtherCreators[0]);
					else
						bldr.AppendFormat("Creators: {0}", String.Join(", ", _metadata.OtherCreators));
					bldr.Append("</p>");
				}
				if (_metadata.OtherContributors.Count > 0)
				{
					if (bldr.Length > 0)
						bldr.AppendLine();
					bldr.Append("<p>");
					if (_metadata.OtherContributors.Count == 1)
						bldr.AppendFormat("Contributor: {0}", _metadata.OtherContributors[0]);
					else
						bldr.AppendFormat("Contributors: {0}", String.Join(", ", _metadata.OtherContributors));
					bldr.Append("</p>");
				}
				if (bldr.Length > 0)
				{
					AddCoverContributor(bldr.ToString());
				}
			}
		}

		private XmlElement GetOrCreateDataDivElement(string key)
		{
			var dataDiv = _bloomDoc.SelectSingleNode("/html/body/div[@id='bloomDataDiv']/div[@data-book='"+key+"']") as XmlElement;
			if (dataDiv == null)
			{
				dataDiv = _bloomDoc.CreateElement("div");
				dataDiv.SetAttribute("data-book", key);
				var dataBook = _bloomDoc.SelectSingleNode("/html/body/div[@id='bloomDataDiv']");
				Debug.Assert(dataBook != null);
				dataBook.AppendChild(dataDiv);
			}
			return dataDiv;
		}

		private void SetDataDivTextValue(string key, string value)
		{
			var dataDiv = GetOrCreateDataDivElement(key);
			dataDiv.InnerText = value;
		}

		private void SetDataDivParaValue(string key, string value)
		{
			var dataDiv = GetOrCreateDataDivElement(key);
			dataDiv.InnerXml = "<p>" + value + "</p>";
		}

		private void SetTitle(string title)
		{
			var titleNode =  _bloomDoc.SelectSingleNode("/html/head/title");
			titleNode.InnerText = title;
			SetDataDivParaValue("bookTitle", title);
			foreach (var node in _bloomDoc.SelectNodes("//div[contains(@class, 'bloom-editable') and @data-book='bookTitle' and @lang!='z']"))
			{
				var div = node as XmlElement;
				div.InnerXml = "<p>" + title + "</p>";
			}
		}

		private void AddCoverContributor(string paraXml)
		{
			var dataDiv = GetOrCreateDataDivElement("smallCoverCredits");
			var newXml = Regex.Replace(paraXml, " xmlns=[\"'][^\"']*[\"']", "", RegexOptions.CultureInvariant,Regex.InfiniteMatchTimeout);
			var credits = dataDiv.InnerXml + newXml;
			dataDiv.InnerXml = credits;
			foreach (var node in _bloomDoc.SelectNodes("//div[contains(@class, 'bloom-editable') and @data-book='smallCoverCredits' and @lang!='z']"))
			{
				var div = node as XmlElement;
				div.InnerXml = credits;
			}
		}

		private void SetCoverImage(string imageFile)
		{
			SetDataDivTextValue("coverImage", imageFile);
			var coverImg = _bloomDoc.SelectSingleNode("//div[@class='bloom-imageContainer']/img[@data-book='coverImage']") as XmlElement;
			if (coverImg != null)
				coverImg.SetAttribute("src", imageFile);
		}

		private void AddInnerCoverImage(string imageFile)
		{
			var innerFrontPage = _bloomDoc.SelectSingleNode("/html/body/div[@id='bloomDataDiv']/div[@data-xmatter-page='insideFrontCover']");
			var newImgPara = String.Format("<p><img src=\"{0}\" /></p>", imageFile);
			var content = innerFrontPage.InnerXml + newImgPara;
			innerFrontPage.InnerXml = content;

			var innerFrontPageDiv = _bloomDoc.SelectSingleNode("//div[@data-book='insideFontCover' and contains(@class,'bloom-editable')]");
			var newPara = _bloomDoc.CreateElement("p");
			newPara.InnerXml = String.Format("<img src=\"{0}\" />", imageFile);
			innerFrontPageDiv.AppendChild(newPara);
		}

		private bool ConvertContentPage(string pageFilePath, int pageNumber)
		{
			var pageDoc = new XmlDocument();
			pageDoc.Load(pageFilePath);
			var nsmgr = new XmlNamespaceManager(pageDoc.NameTable);
			nsmgr.AddNamespace("x", "http://www.w3.org/1999/xhtml");
			var body = pageDoc.SelectSingleNode("/x:html/x:body", nsmgr) as XmlElement;
			if (!IsContentPage(body, nsmgr))
				return false;
			var imageFile = "";
			var bldr = new StringBuilder();
			for (int i = 0; i < body.ChildNodes.Count; ++i)
			{
				var child = body.ChildNodes[i];
				if (child.Name == "img")
				{
					imageFile = (child as XmlElement).GetAttribute("src");
				}
				if (child.InnerText.Trim().Length > 0)
				{
					var outer = child.OuterXml;
					var xml = Regex.Replace(outer, " xmlns=[\"'][^\"']*[\"']", "", RegexOptions.CultureInvariant,Regex.InfiniteMatchTimeout);
					bldr.Append(xml);
				}
			}
			var newPage = new StringBuilder();
			newPage.AppendLine("        <div class=\"pageLabel\" data-i18n=\"TemplateBooks.PageLabel.Basic Text &amp; Picture\" lang=\"en\">");
			newPage.AppendLine("            Basic Text &amp; Picture");
			newPage.AppendLine("        </div>");
			newPage.AppendLine("        <div class=\"pageDescription\" lang=\"en\"></div>");
			newPage.AppendLine("        <div class=\"marginBox\">");
			newPage.AppendLine("            <div style=\"min-height: 42px;\" class=\"split-pane horizontal-percent\">");
			newPage.AppendLine("                <div class=\"split-pane-component position-top\" style=\"bottom: 50%\">");
			newPage.AppendLine("                    <div class=\"split-pane-component-inner\">");
			newPage.AppendFormat("                        <div class=\"bloom-imageContainer bloom-leadingElement\"><img src=\"{0}\" alt=\"\"></img></div>{1}", imageFile, Environment.NewLine);
			newPage.AppendLine("                    </div>");
			newPage.AppendLine("                </div>");
			newPage.AppendLine("                <div class=\"split-pane-divider horizontal-divider\" style=\"bottom: 50%\"></div>");
			newPage.AppendLine("                <div class=\"split-pane-component position-bottom\" style=\"height: 50%\">");
			newPage.AppendLine("                    <div class=\"split-pane-component-inner\">");
			newPage.AppendLine("                        <div class=\"bloom-translationGroup bloom-trailingElement\" data-default-languages=\"auto\">");
			newPage.AppendLine("                            <div aria-label=\"false\" role=\"textbox\" spellcheck=\"true\" tabindex=\"0\" style=\"min-height: 28px;\" data-languagetipcontent=\"English\" class=\"bloom-editable normal-style bloom-content1 bloom-contentNational1 bloom-visibility-code-on\" lang=\"en\" contenteditable=\"true\">");
			var content = bldr.ToString().Trim();
			if (content.StartsWith("<p>"))
				newPage.AppendLine(content);
			else
				newPage.AppendFormat("<p>{0}</p>{1}", content, Environment.NewLine);
			newPage.AppendLine("                            </div>");
			newPage.AppendLine("                            <div style=\"\" class=\"bloom-editable normal-style\" lang=\"z\" contenteditable=\"true\">");
			newPage.AppendLine("                                <p></p>");
			newPage.AppendLine("                            </div>");
			newPage.AppendLine("                        </div>");
			newPage.AppendLine("                    </div>");
			newPage.AppendLine("                </div>");
			newPage.AppendLine("            </div>");
			newPage.AppendLine("        </div>");
			newPage.AppendLine();
			var newPageXml = newPage.ToString();

			var newPageDiv = _bloomDoc.CreateElement("div");
			newPageDiv.SetAttribute("class", "bloom-page numberedPage customPage bloom-combinedPage A5Portrait side-right bloom-monolingual");
			newPageDiv.SetAttribute("id", Guid.NewGuid().ToString());
			newPageDiv.SetAttribute("data-pagelineage", "adcd48df-e9ab-4a07-afd4-6a24d0398382");
			newPageDiv.SetAttribute("data-page-number", pageNumber.ToString());
			newPageDiv.InnerXml = newPageXml;

			// Find the first endmatter page and insert the new page before it.
			var endMatter = _bloomDoc.SelectSingleNode("/html/body/div[@data-xmatter-page='insideBackCover']");
			var newBody = _bloomDoc.SelectSingleNode("/html/body");
			newBody.InsertBefore(newPageDiv, endMatter);
			return true;
		}

		private bool IsContentPage(XmlElement body, XmlNamespaceManager nsmgr)
		{
			var imageSeen = false;
			var otherSeen = false;
			var textSeen = false;
			for (int i = 0; i < body.ChildNodes.Count; ++i)
			{
				var child = body.ChildNodes[i];
				if (child.Name == "img")
				{
					if (imageSeen || otherSeen || textSeen)
						return false;
					imageSeen = true;
				}
				else if (child is XmlElement)
				{
					if (child.InnerText.Trim().Length > 0)
						textSeen = true;;
					otherSeen = true;
				}
				else if (child is XmlText)
				{
					if ((child as XmlText).Value.Trim().Length > 0)
						textSeen = true;
				}
			}
			return imageSeen;	// do we need text on a page?
		}


		private void ConvertEndCreditsPage(string pageFilePath)
		{
			Console.WriteLine("ConverEndCreditsPage(\"{0}\")", pageFilePath);
		}
	}
}
