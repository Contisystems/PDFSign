using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfDigitalSignatureQualify.Models
{
    class Certificate
    {
        public string certAlias { get; set; }
        public string certFriendlyName { get; set; }
        public string cert_64 { get; set; }
        public string certExpDate { get; set; }
    }
}
