using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfDigitalSignatureQualify.Models
{
    class SigComplete
    {
        public string certAlias { get; set; }
        public string sigReqDescr { get; set; }
        public string totpID { get; set; }
        public string totpValue { get; set; }
        public List<DocsToSign> docsToSign { get; set; }
    }

    class DocsToSign
    {
        public string docAlias { get; set; }
        public string hashAlg { get; set; }
        public string hashToSign_64 { get; set; }
    }
}
