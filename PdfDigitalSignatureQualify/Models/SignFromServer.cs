using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfDigitalSignatureQualify.Models
{
    class SignFromServer
    {
        public string docAlias { get; set; }
        public string docID { get; set; }
        public string hashSig { get; set; }
    }
}
