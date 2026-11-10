using System.IO.Compression;
using System.Net;
using System.Text;

namespace ComplianceTracker.Specs.Support
{
    // US-137: the pretend EU Publications Office. It answers a CELEX number with a zip of Formex 4 files, like CELLAR does,
    // so the real HttpEurLexSource and FormexParser are tested without a network. The files are written by hand in Formex's
    // structure; the wording is shortened and invented, only the numbering follows the acts.
    public static class FakeEurLex
    {
        public const string Cra = "32024R2847";
        public const string Gdpr = "32016R0679";

        public static HttpMessageHandler CreateHandler() => new Handler();

        private sealed class Handler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var celex = request.RequestUri!.Segments[^1];
                var files = celex switch
                {
                    Cra => CraFiles(),
                    Gdpr => GdprFiles(),
                    _ => null
                };

                if (files is null || !request.Headers.Accept.ToString().Contains("fmx4", StringComparison.Ordinal))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Zip(files)) });
            }
        }

        public static byte[] Zip(IReadOnlyDictionary<string, string> files)
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (name, xml) in files)
                {
                    using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                    writer.Write(xml);
                }
            }

            return stream.ToArray();
        }

        private static string Recitals(int count)
            => "<PREAMBLE><GR.CONSID>" + string.Concat(Enumerable.Range(1, count).Select(n =>
                $"<CONSID><NP><NO.P>({n})</NO.P><TXT>Recital {n} explains why the rules are needed.</TXT></NP></CONSID>")) + "</GR.CONSID></PREAMBLE>";

        private static string Points(params string[] letters)
            => "<LIST TYPE=\"alpha\">" + string.Concat(letters.Select(l => $"<ITEM><NP><NO.P>({l})</NO.P><TXT>point ({l}) of the obligation;</TXT></NP></ITEM>")) + "</LIST>";

        // The CRA's structure in short: recitals, definitions, the manufacturer's obligations with paragraphs and points,
        // a Member States' article, the final provisions, and Annex I with Part I (1) and (2)(a)–(m) and Part II (1)–(8)
        public static IReadOnlyDictionary<string, string> CraFiles() => new Dictionary<string, string>
        {
            ["L_202402847EN.000101.fmx.xml"] = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <ACT>
                  {Recitals(3)}
                  <ENACTING.TERMS>
                    <DIVISION><TITLE><TI><P>CHAPTER I</P></TI></TITLE>
                      <ARTICLE IDENTIFIER="001"><TI.ART>Article 1</TI.ART><STI.ART>Subject matter</STI.ART>
                        <ALINEA>This Regulation lays down rules for products with digital elements.</ALINEA></ARTICLE>
                      <ARTICLE IDENTIFIER="003"><TI.ART>Article 3</TI.ART><STI.ART>Definitions</STI.ART>
                        <ALINEA>For the purposes of this Regulation:{Points("1", "2")}</ALINEA></ARTICLE>
                    </DIVISION>
                    <ARTICLE IDENTIFIER="013"><TI.ART>Article 13</TI.ART><STI.ART>Obligations of manufacturers</STI.ART>
                      <PARAG IDENTIFIER="013.001"><NO.PARAG>1.</NO.PARAG><ALINEA>Manufacturers shall ensure that products meet the essential requirements.</ALINEA></PARAG>
                      <PARAG IDENTIFIER="013.002"><NO.PARAG>2.</NO.PARAG><ALINEA>Manufacturers shall assess the cybersecurity risks and shall:{Points("a", "b")}</ALINEA></PARAG>
                      <PARAG IDENTIFIER="013.003"><NO.PARAG>3.</NO.PARAG><ALINEA>(deleted)</ALINEA></PARAG>
                    </ARTICLE>
                    <ARTICLE IDENTIFIER="046"><TI.ART>Article 46</TI.ART><STI.ART>Market surveillance</STI.ART>
                      <PARAG IDENTIFIER="046.001"><NO.PARAG>1.</NO.PARAG><ALINEA>Member States shall designate market surveillance authorities.</ALINEA></PARAG>
                      <PARAG IDENTIFIER="046.002"><NO.PARAG>2.</NO.PARAG><ALINEA>Manufacturers shall cooperate with the authorities.</ALINEA></PARAG>
                    </ARTICLE>
                    <ARTICLE IDENTIFIER="071"><TI.ART>Article 71</TI.ART><STI.ART>Entry into force and application</STI.ART>
                      <PARAG IDENTIFIER="071.001"><NO.PARAG>1.</NO.PARAG><ALINEA>This Regulation shall enter into force on the twentieth day.</ALINEA></PARAG>
                    </ARTICLE>
                  </ENACTING.TERMS>
                </ACT>
                """,
            ["L_202402847EN.010101.fmx.xml"] = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <ANNEX>
                  <TITLE><TI><P>ANNEX I</P></TI><STI><P>ESSENTIAL CYBERSECURITY REQUIREMENTS</P></STI></TITLE>
                  <CONTENTS>
                    <GR.SEQ LEVEL="1"><TITLE><TI><P>Part I</P></TI><STI><P>Cybersecurity requirements relating to the properties of products</P></STI></TITLE>
                      <LIST TYPE="ARAB">
                        <ITEM><NP><NO.P>(1)</NO.P><TXT>Products shall be designed to ensure an appropriate level of cybersecurity.</TXT></NP></ITEM>
                        <ITEM><NP><NO.P>(2)</NO.P><TXT>On the basis of the risk assessment, products shall:</TXT>
                          <P>{Points("a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m")}</P></NP></ITEM>
                      </LIST>
                    </GR.SEQ>
                    <GR.SEQ LEVEL="1"><TITLE><TI><P>Part II</P></TI><STI><P>Vulnerability handling requirements</P></STI></TITLE>
                      <LIST TYPE="ARAB">
                        {string.Concat(Enumerable.Range(1, 8).Select(n => $"<ITEM><NP><NO.P>({n})</NO.P><TXT>Manufacturers shall handle vulnerabilities, rule {n}.</TXT></NP></ITEM>"))}
                      </LIST>
                    </GR.SEQ>
                  </CONTENTS>
                </ANNEX>
                """,
            ["L_202402847EN.doc.fmx.xml"] = "<?xml version=\"1.0\"?><DOC><BIB.DOC><NO.CELEX>32024R2847</NO.CELEX></BIB.DOC></DOC>"
        };

        // The GDPR's structure in short: recitals, definitions and the principles
        public static IReadOnlyDictionary<string, string> GdprFiles() => new Dictionary<string, string>
        {
            ["L_2016119EN.01000101.fmx.xml"] = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <ACT>
                  {Recitals(2)}
                  <ENACTING.TERMS>
                    <ARTICLE IDENTIFIER="004"><TI.ART>Article 4</TI.ART><STI.ART>Definitions</STI.ART><ALINEA>For the purposes of this Regulation:{Points("1")}</ALINEA></ARTICLE>
                    <ARTICLE IDENTIFIER="005"><TI.ART>Article 5</TI.ART><STI.ART>Principles relating to processing of personal data</STI.ART>
                      <PARAG IDENTIFIER="005.001"><NO.PARAG>1.</NO.PARAG><ALINEA>Personal data shall be:{Points("a", "b", "c")}</ALINEA></PARAG>
                      <PARAG IDENTIFIER="005.002"><NO.PARAG>2.</NO.PARAG><ALINEA>The controller shall be responsible for, and be able to demonstrate compliance with, paragraph 1.</ALINEA></PARAG>
                    </ARTICLE>
                  </ENACTING.TERMS>
                </ACT>
                """
        };
    }
}
