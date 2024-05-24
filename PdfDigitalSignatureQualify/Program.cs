using PdfDigitalSignatureQualify.Models;
using iText.Kernel.Pdf;
using iText.Signatures;
using Newtonsoft.Json;
using Org.BouncyCastle.X509;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PdfDigitalSignatureQualify.Helpers;
using Topshelf;

namespace PdfDigitalSignatureQualify
{
    class Program
    {
        static void Main(string[] args)
        {
            var exitCode = HostFactory.Run(app =>
            {
                app.Service<DigitalSign>(service =>
                {
                    service.ConstructUsing(digitalSign => new DigitalSign());
                    service.WhenStarted(digitalSign => digitalSign.Start().Wait());
                    service.WhenStopped(digitalSign => digitalSign.Stop());
                });

                app.RunAsLocalSystem();

                app.SetServiceName("DigitalSign");
                app.SetDisplayName("DigitalSign");
                app.SetDescription("Service that signs PDFs using DigitalSign.");

            });

            int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
            Environment.ExitCode = exitCodeValue;
        }
    }
}
