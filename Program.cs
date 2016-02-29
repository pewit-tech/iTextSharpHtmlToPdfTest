using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.tool.xml;
using iTextSharp.tool.xml.css;
using iTextSharp.tool.xml.html;
using iTextSharp.tool.xml.parser;
using iTextSharp.tool.xml.pipeline.css;
using iTextSharp.tool.xml.pipeline.end;
using iTextSharp.tool.xml.pipeline.html;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace iTextSharpHtmlToPdfTest
{
    class Program
    {
        static void Main(string[] args)
        {
            // Create a byte array that will eventually hold our final PDF
            Byte[] bytes;

            // Boilerplate iTextSharp setup here

            // Create a stream that we can write to, in this case a MemoryStream
            using (var ms = new MemoryStream())
            {
                // Create an iTextSharp Document which is an abstraction of a PDF but **NOT** a PDF
                using (var doc = new Document())
                {
                    var tagProcessors = (DefaultTagProcessorFactory)Tags.GetHtmlTagProcessorFactory();
                    tagProcessors.RemoveProcessor(HTML.Tag.IMG); // remove the default processor
                    tagProcessors.AddProcessor(HTML.Tag.IMG, new CustomImageTagProcessor()); // use our new processor


                    // Create a writer that's bound to our PDF abstraction and our stream
                    using (var writer = PdfWriter.GetInstance(doc, ms))
                    {
                        // Open the document for writing
                        doc.Open();

                        string finalHtml = File.ReadAllText("file.html");

                        // Read your html by database or file here and store it into finalHtml e.g. a string
                        // XMLWorker also reads from a TextReader and not directly from a string
                        using (var srHtml = new StringReader(finalHtml))
                        {
                            CssFilesImpl cssFiles = new CssFilesImpl();
                            cssFiles.Add(XMLWorkerHelper.GetInstance().GetDefaultCSS());
                            var cssResolver = new StyleAttrCSSResolver(cssFiles);
                            cssResolver.AddCss(@"code { padding: 2px 4px; }", "utf-8", true);
                            var charset = Encoding.UTF8;

                            var hpc = new HtmlPipelineContext(new CssAppliersImpl(new XMLWorkerFontProvider()));
                            hpc.SetAcceptUnknown(true).AutoBookmark(true).SetTagFactory(tagProcessors); // inject the tagProcessors
                            var htmlPipeline = new HtmlPipeline(hpc, new PdfWriterPipeline(doc, writer));
                            var pipeline = new CssResolverPipeline(cssResolver, htmlPipeline);
                            var worker = new XMLWorker(pipeline, true);

                            // Parse the HTML
                            var xmlParser = new XMLParser(true, worker, charset);
                            xmlParser.Parse(srHtml);
                        }

                        doc.Close();
                    }
                }

                // After all of the PDF "stuff" above is done and closed but **before** we
                // close the MemoryStream, grab all of the active bytes from the stream
                bytes = ms.ToArray();
            }

            File.WriteAllBytes("file.pdf", bytes);
        }

        //this tag processor is required if we will use base64 embedded IMG SRCs
        public class CustomImageTagProcessor : iTextSharp.tool.xml.html.Image
        {
            public override IList<IElement> End(IWorkerContext ctx, Tag tag, IList<IElement> currentContent)
            {
                IDictionary<string, string> attributes = tag.Attributes;
                string src;
                if (!attributes.TryGetValue(HTML.Attribute.SRC, out src))
                    return new List<IElement>(1);

                if (string.IsNullOrEmpty(src))
                    return new List<IElement>(1);

                if (src.StartsWith("data:image/", StringComparison.InvariantCultureIgnoreCase))
                {
                    // data:[<MIME-type>][;charset=<encoding>][;base64],<data>
                    var base64Data = src.Substring(src.IndexOf(",") + 1);
                    var imagedata = Convert.FromBase64String(base64Data);
                    var image = iTextSharp.text.Image.GetInstance(imagedata);

                    var list = new List<IElement>();
                    var htmlPipelineContext = GetHtmlPipelineContext(ctx);
                    list.Add(GetCssAppliers().Apply(new Chunk((iTextSharp.text.Image)GetCssAppliers().Apply(image, tag, htmlPipelineContext), 0, 0, true), tag, htmlPipelineContext));
                    return list;
                }
                else
                {
                    return base.End(ctx, tag, currentContent);
                }
            }
        }
    }
}
