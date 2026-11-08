using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace ComplianceTracker.Infrastructure.Reports
{
    // US-132: puts a digital signature (the organisation's seal) in a PDF the tracker made, as an incremental update
    // (ISO 32000-1 12.8): the original bytes are kept unchanged, and a signature dictionary, a signature field and a new
    // cross-reference section are added after them. The signature is a detached CMS (PKCS#7) over the whole file except
    // the signature value itself, SHA-256, with the signing certificate and the signing time (/SubFilter
    // /adbe.pkcs7.detached), which Adobe Reader and other readers validate.
    // Only the tracker's own PDFs (written by PdfPig with a classic cross-reference table) are signed.
    public static partial class PdfSigner
    {
        // Room for the CMS signature, in bytes (written as twice as many hex digits)
        private const int SignatureSpace = 12288;
        private const string ByteRangePlaceholder = "/ByteRange [0 0000000000 0000000000 0000000000]";

        [GeneratedRegex(@"startxref\s+(\d+)\s+%%EOF\s*$")]
        private static partial Regex StartXref();

        [GeneratedRegex(@"trailer\s*<<(.*?)>>\s*startxref", RegexOptions.Singleline)]
        private static partial Regex Trailer();

        [GeneratedRegex(@"/Root\s+(\d+)\s+0\s+R")]
        private static partial Regex RootRef();

        [GeneratedRegex(@"/Size\s+(\d+)")]
        private static partial Regex SizeEntry();

        [GeneratedRegex(@"/ID\s*\[[^\]]*\]")]
        private static partial Regex IdEntry();

        [GeneratedRegex(@"/Info\s+\d+\s+0\s+R")]
        private static partial Regex InfoEntry();

        [GeneratedRegex(@"(?:^|\n)(\d+) 0 obj\s*\n?(<<.*?>>)\s*\n?endobj", RegexOptions.Singleline)]
        private static partial Regex Objects();

        [GeneratedRegex(@"/Type\s*/Page(?![s\w])")]
        private static partial Regex PageType();

        [GeneratedRegex(@"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]")]
        private static partial Regex ByteRangeEntry();

        public static byte[] Sign(byte[] pdf, X509Certificate2 certificate, string name, string reason, DateTime signedAtUtc)
        {
            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException("The certificate has no private key");
            }

            var text = Encoding.Latin1.GetString(pdf);
            var startXref = StartXref().Match(text);
            var trailer = Trailer().Matches(text).LastOrDefault();
            if (!startXref.Success || trailer is null || !text.Contains("\nxref", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Only a PDF with a classic cross-reference table can be signed");
            }

            var trailerText = trailer.Groups[1].Value;
            var root = int.Parse(RootRef().Match(trailerText).Groups[1].Value, CultureInfo.InvariantCulture);
            var size = int.Parse(SizeEntry().Match(trailerText).Groups[1].Value, CultureInfo.InvariantCulture);
            // After an incremental update the latest copy of an object counts
            var objects = Objects().Matches(text)
                .GroupBy(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                .ToDictionary(g => g.Key, g => g.Last().Groups[2].Value);
            var page = objects.Where(o => PageType().IsMatch(o.Value)).Select(o => o.Key).DefaultIfEmpty(-1).Min();
            if (!objects.TryGetValue(root, out var catalog) || page < 0 || catalog.Contains("/AcroForm", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The PDF cannot be signed: no catalog or page, or it has a form already");
            }

            var signatureNumber = size;
            var widgetNumber = size + 1;
            var hexLength = SignatureSpace * 2;

            // The new and changed objects, appended after the original file
            var update = new StringBuilder();
            var offsets = new SortedDictionary<int, long>();
            var start = (long)pdf.Length;
            update.Append('\n');

            void Add(int number, string body)
            {
                offsets[number] = start + Encoding.Latin1.GetByteCount(update.ToString());
                update.Append(number.ToString(CultureInfo.InvariantCulture)).Append(" 0 obj\n").Append(body).Append("\nendobj\n");
            }

            Add(root, Insert(catalog, $"/AcroForm <</Fields [{widgetNumber} 0 R] /SigFlags 3 >>"));
            var pageObject = objects[page];
            Add(page, pageObject.Contains("/Annots", StringComparison.Ordinal)
                ? Regex.Replace(pageObject, @"/Annots\s*\[", $"/Annots [{widgetNumber} 0 R ")
                : Insert(pageObject, $"/Annots [{widgetNumber} 0 R]"));
            Add(signatureNumber,
                $"<</Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached {ByteRangePlaceholder} " +
                $"/Contents <{new string('0', hexLength)}> /M ({Date(signedAtUtc)}) /Name ({Escape(name)}) /Reason ({Escape(reason)}) >>");
            Add(widgetNumber,
                $"<</Type /Annot /Subtype /Widget /FT /Sig /T (Seal) /V {signatureNumber} 0 R /Rect [0 0 0 0] /F 132 /P {page} 0 R >>");

            // A cross-reference section for the four objects, and a trailer pointing back at the original one
            var xrefOffset = start + Encoding.Latin1.GetByteCount(update.ToString());
            update.Append("xref\n");
            foreach (var group in Groups(offsets.Keys))
            {
                update.Append(CultureInfo.InvariantCulture, $"{group[0]} {group.Count}\n");
                foreach (var number in group)
                {
                    update.Append(offsets[number].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
                }
            }

            var id = IdEntry().Match(trailerText);
            var info = InfoEntry().Match(trailerText);
            update.Append(CultureInfo.InvariantCulture,
                $"trailer\n<</Size {widgetNumber + 1} /Root {root} 0 R /Prev {startXref.Groups[1].Value}{(info.Success ? " " + info.Value : "")}{(id.Success ? " " + id.Value : "")} >>\n");
            update.Append(CultureInfo.InvariantCulture, $"startxref\n{xrefOffset}\n%%EOF\n");

            var signed = pdf.Concat(Encoding.Latin1.GetBytes(update.ToString())).ToArray();

            // The byte range: everything but the hex value between < and >
            var signedText = Encoding.Latin1.GetString(signed);
            var contentsAt = signedText.IndexOf("/Contents <", (int)start, StringComparison.Ordinal) + "/Contents ".Length;
            var contentsEnd = contentsAt + hexLength + 2;
            var range = $"/ByteRange [0 {contentsAt.ToString("D10", CultureInfo.InvariantCulture)} {contentsEnd.ToString("D10", CultureInfo.InvariantCulture)} {(signed.Length - contentsEnd).ToString("D10", CultureInfo.InvariantCulture)}]";
            var rangeAt = signedText.IndexOf(ByteRangePlaceholder, (int)start, StringComparison.Ordinal);
            Encoding.Latin1.GetBytes(range).CopyTo(signed, rangeAt);

            // The CMS over both parts
            var data = signed.Take(contentsAt).Concat(signed.Skip(contentsEnd)).ToArray();
            var cms = new SignedCms(new ContentInfo(data), detached: true);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
            {
                DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"), // SHA-256
                IncludeOption = X509IncludeOption.EndCertOnly
            };
            signer.SignedAttributes.Add(new Pkcs9SigningTime(signedAtUtc));
            cms.ComputeSignature(signer);
            var encoded = cms.Encode();
            if (encoded.Length > SignatureSpace)
            {
                throw new InvalidOperationException("The signature is larger than the space for it");
            }

            Encoding.Latin1.GetBytes(Convert.ToHexString(encoded)).CopyTo(signed, contentsAt + 1);
            return signed;
        }

        // For checks and tests: whether the signature covers the whole file and is valid; the signer's certificate
        public static (bool Valid, bool CoversWholeFile, X509Certificate2? Signer) Verify(byte[] signed)
        {
            var text = Encoding.Latin1.GetString(signed);
            var match = ByteRangeEntry().Matches(text).LastOrDefault();
            if (match is null)
            {
                return (false, false, null);
            }

            var r = match.Groups.Values.Skip(1).Select(g => long.Parse(g.Value, CultureInfo.InvariantCulture)).ToArray();
            var covers = r[0] == 0 && r[2] + r[3] == signed.Length;
            var hex = text.Substring((int)r[1] + 1, (int)(r[2] - r[1] - 2)).TrimEnd('0');
            if (hex.Length % 2 == 1)
            {
                hex += "0";
            }

            try
            {
                var data = signed.Skip((int)r[0]).Take((int)r[1]).Concat(signed.Skip((int)r[2]).Take((int)r[3])).ToArray();
                var cms = new SignedCms(new ContentInfo(data), detached: true);
                cms.Decode(Convert.FromHexString(hex));
                cms.CheckSignature(verifySignatureOnly: true);
                return (true, covers, cms.SignerInfos[0].Certificate);
            }
            catch (CryptographicException)
            {
                return (false, covers, null);
            }
        }

        private static string Insert(string dictionary, string entry)
        {
            var end = dictionary.LastIndexOf(">>", StringComparison.Ordinal);
            return dictionary[..end] + entry + " " + dictionary[end..];
        }

        private static IEnumerable<List<int>> Groups(IEnumerable<int> numbers)
        {
            var group = new List<int>();
            foreach (var number in numbers)
            {
                if (group.Count > 0 && number != group[^1] + 1)
                {
                    yield return group;
                    group = [];
                }

                group.Add(number);
            }

            if (group.Count > 0)
            {
                yield return group;
            }
        }

        private static string Date(DateTime utc) => utc.ToString("'D:'yyyyMMddHHmmss'Z'", CultureInfo.InvariantCulture);

        // A PDF literal string: only text the standard font can show, with ( ) \ escaped
        private static string Escape(string text)
            => Application.Reports.PdfText.Clean(text).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)").Replace("\n", " ");
    }
}
