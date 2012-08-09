﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Novacode
{
    /// <summary>
    /// Represents a List in a document.
    /// </summary>
    public class List : InsertBeforeOrAfter
    {
        /// <summary>
        /// This is a list of paragraphs that will be added to the document
        /// when the list is inserted into the document.
        /// The paragraph needs a numPr defined to be in this items collection.
        /// </summary>
        public List<Paragraph> Items { get; private set; }
        /// <summary>
        /// The numId used to reference the list settings in the numbering.xml
        /// </summary>
        public int NumId { get; private set; }
        /// <summary>
        /// The ListItemType (bullet or numbered) of the list.
        /// </summary>
        public ListItemType? ListType { get; private set; }

        internal List(DocX document, XElement xml)
            : base(document, xml)
        {
            Items = new List<Paragraph>();
            ListType = null;
        }

        /// <summary>
        /// Adds an item to the list.
        /// </summary>
        /// <param name="paragraph"></param>
        /// <exception cref="InvalidOperationException">
        /// Throws an InvalidOperationException if the item cannot be added to the list.
        /// </exception>
        public void AddItem(Paragraph paragraph)
        {
            if (paragraph.IsListItem)
            {
                //var lvlNode = paragraph.Xml.Descendants().First(s => s.Name.LocalName == "ilvl");
                var numIdNode = paragraph.Xml.Descendants().First(s => s.Name.LocalName == "numId");
                var numId = Int32.Parse(numIdNode.Attribute(DocX.w + "val").Value);

                //Level = Int32.Parse(lvlNode.Attribute(DocX.w + "val").Value);
                if (CanAddListItem(paragraph))
                {
                    NumId = numId;
                    Items.Add(paragraph);
                }
                else
                    throw new InvalidOperationException("New list items can only be added to this list if they are have the same numId.");
            }
        }


        /// <summary>
        /// Determine if it is able to add the item to the list
        /// </summary>
        /// <param name="paragraph"></param>
        /// <returns>
        /// Return true if AddItem(...) will succeed with the given paragraph.
        /// </returns>
        public bool CanAddListItem(Paragraph paragraph)
        {
            if (paragraph.IsListItem)
            {
                var numIdNode = paragraph.Xml.Descendants().First(s => s.Name.LocalName == "numId");
                var numId = Int32.Parse(numIdNode.Attribute(DocX.w + "val").Value);

                if (NumId == 0 || (numId == NumId && numId > 0))
                {
                    return true;
                }
            }
            return false;
        }

        internal void CreateNewNumberingNumId(int level = 0, ListItemType listType = ListItemType.Numbered)
        {
            ValidateDocXNumberingPartExists();
            if (Document.numbering.Root == null)
            {
                throw new InvalidOperationException("Numbering section did not instantiate properly.");
            }

            ListType = listType;

            var numId = GetMaxNumId() + 1;
            var abstractNumId = GetMaxAbstractNumId() + 1;

            XDocument listTemplate;
            switch (listType)
            {
                case ListItemType.Bulleted:
                    listTemplate = HelperFunctions.DecompressXMLResource("Novacode.Resources.numbering.default_bullet_abstract.xml.gz");
                    break;
                case ListItemType.Numbered:
                    listTemplate = HelperFunctions.DecompressXMLResource("Novacode.Resources.numbering.default_decimal_abstract.xml.gz");
                    break;
                default:
                    throw new InvalidOperationException(string.Format("Unable to deal with ListItemType: {0}.", listType.ToString()));
            }
            var abstractNumTemplate = listTemplate.Descendants().Single(d => d.Name.LocalName == "abstractNum");
            abstractNumTemplate.SetAttributeValue(DocX.w + "abstractNumId", abstractNumId);


            Document.numbering.Root.Add(abstractNumTemplate);
            Document.numbering.Root.Add(
                new XElement(
                    XName.Get("num", DocX.w.NamespaceName),
                    new XAttribute(DocX.w + "numId", numId),
                    new XElement(XName.Get("abstractNumId", DocX.w.NamespaceName),
                                    new XAttribute(DocX.w + "val", abstractNumId))
                )
            );

            NumId = numId;
        }

        /// <summary>
        /// Method to determine the last numId for a list element. 
        /// Also useful for determining the next numId to use for inserting a new list element into the document.
        /// </summary>
        /// <returns>
        /// 0 if there are no elements in the list already.
        /// Increment the return for the next valid value of a new list element.
        /// </returns>
        private int GetMaxNumId()
        {
            const int defaultValue = 0;
            if (Document.numbering == null)
                return defaultValue;

            var numlist = Document.numbering.Descendants().Where(d => d.Name.LocalName == "num").ToList();
            if (numlist.Any())
                return numlist.Max(e => int.Parse(e.Value));
            return defaultValue;
        }

        /// <summary>
        /// Method to determine the last abstractNumId for a list element.
        /// Also useful for determining the next abstractNumId to use for inserting a new list element into the document.
        /// </summary>
        /// <returns>
        /// -1 if there are no elements in the list already.
        /// Increment the return for the next valid value of a new list element.
        /// </returns>
        private int GetMaxAbstractNumId()
        {
            const int defaultValue = -1;

            if (Document.numbering == null)
                return defaultValue;

            var numlist = Document.numbering.Descendants().Where(d => d.Name.LocalName == "abstractNum").ToList();
            if (numlist.Any())
                return numlist.Max(e => int.Parse(e.Value));
            return defaultValue;
        }

        /// <summary>
        /// Get the abstractNum definition for the given numId
        /// </summary>
        /// <param name="numId">The numId on the pPr element</param>
        /// <returns>XElement representing the requested abstractNum</returns>
        internal XElement GetAbstractNum(int numId)
        {
            var num = Document.numbering.Descendants().First(d => d.Name.LocalName == "num" && d.GetAttribute(DocX.w + "numId").Equals(numId.ToString()));
            var abstractNumId = num.Descendants().First(d => d.Name.LocalName == "abstractNumId");
            return Document.numbering.Descendants().First(d => d.Name.LocalName == "abstractNum" && d.GetAttribute("abstractNumId").Equals(abstractNumId.Value));
        }

        private void ValidateDocXNumberingPartExists()
        {
            var numberingUri = new Uri("/word/numbering.xml", UriKind.Relative);

            // If the internal document contains no /word/numbering.xml create one.
            if (!Document.package.PartExists(numberingUri))
                HelperFunctions.AddDefaultNumberingXml(Document.package);

            //XDocument numbering;
            using (TextReader tr = new StreamReader(Document.package.GetPart(numberingUri).GetStream()))
            {
                Document.numbering = XDocument.Load(tr);
            }
        }
    }
}
