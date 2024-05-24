using iText.Kernel.Pdf;
using iText.Signatures;
using Newtonsoft.Json;
using Org.BouncyCastle.X509;
using PdfDigitalSignatureQualify.Helpers;
using PdfDigitalSignatureQualify.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Data.SqlClient;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PdfDigitalSignatureQualify
{
    public class DigitalSign
    {
        private readonly string _token;
        private readonly string _inFolder;
        private readonly string _doneFolder;
        private readonly string _outFolder;
        private readonly int _degreeOfParallelism;
        private readonly bool _isToBackupOriginalFile;
        private readonly string _signatureSeed;
        private readonly string _signatureSecret;
        private readonly string _signatureHashAlgorithm;
        private readonly string _product;
        private readonly string _field1;
        private readonly string _endpoint1;

        private static readonly string rawConnectionString = Properties.db.Default.DOSTools;
        //private static readonly string pswd = Environment.GetEnvironmentVariable("ConnectionStrings__DOSToolsDBPassword");
        //private static readonly string connectionString = new Regex("#pswd#").Replace(rawConnectionString, pswd);
        private static readonly string connectionString = new Regex("#pswd#").Replace(rawConnectionString, "C0nt!systems1234");
        private static HttpClient _httpClient = HelperMethods.CreateHttpClient();

        public DigitalSign()
        {
            _token = ConfigurationManager.AppSettings["DigitalSignToken"];
            _inFolder = ConfigurationManager.AppSettings["InFolder"];
            _doneFolder = ConfigurationManager.AppSettings["DoneFolder"];
            _outFolder = ConfigurationManager.AppSettings["OutFolder"];
            _degreeOfParallelism = int.Parse(ConfigurationManager.AppSettings["DegreeOfParallelism"]);
            _isToBackupOriginalFile = bool.Parse(ConfigurationManager.AppSettings["IsToBackupOriginalFile"]);
            _signatureSeed = ConfigurationManager.AppSettings["SignatureSeed"];
            _signatureSecret = ConfigurationManager.AppSettings["SignatureSecret"];
            _signatureHashAlgorithm = ConfigurationManager.AppSettings["SignatureHashAlgorithm"];
            _product = ConfigurationManager.AppSettings["Product"];
            _field1 = ConfigurationManager.AppSettings["Field1"];
            _endpoint1 = ConfigurationManager.AppSettings["Endpoint1"];
        }

        internal async Task Start()
        {
            var certificate = await ObtainCertificate(_token);

            var filesName = Directory.EnumerateFiles(_inFolder, "*.pdf");

            Parallel.ForEach(filesName, new ParallelOptions() { MaxDegreeOfParallelism = _degreeOfParallelism }, async (fileName, _) =>
            {
                FileInfo fileInfo = new FileInfo(fileName);
                //ADD SIGNATURE WITH iText or Free Spire.PDF
                SignUsingiText(certificate, fileInfo, _token).Wait();

                if (_isToBackupOriginalFile)
                {
                    //MOVE FROM IN TO DONE
                    File.Move(fileName, $@"{_doneFolder}\{fileInfo.Name}");
                }

                await CallUsp_SignSET(fileName, _product, _field1);
            });
        }

        internal void Stop()
        {
            //Log.Write("Service " + _serviceName + " STOPED at " + DateTime.Now, LogLine.Log_Type.Action, _isDebug);
#if (DEBUG)
            Console.WriteLine("Service Stopped at " + DateTime.Now);
#endif
        }

        private async Task<Certificate> ObtainCertificate(string token)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                GetCertificate getCertificate = new GetCertificate() { totpID = _signatureSeed };
                StringContent content = new StringContent(JsonConvert.SerializeObject(getCertificate, Formatting.Indented), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_endpoint1, content);
                string apiResponse = await response.Content.ReadAsStringAsync();

                var test = JsonConvert.DeserializeObject<Certificate>(apiResponse);

                return test;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
                return null;
                throw e;
            }
        }

        private async Task SignUsingiText(Certificate inputCertificate, FileInfo inputFileInfo, string authenticationToken)
        {
            try
            {
                byte[] decoded = Convert.FromBase64String(inputCertificate.cert_64);
                var certificateParser = new X509CertificateParser();
                var certChain = new Org.BouncyCastle.X509.X509Certificate[] { certificateParser.ReadCertificate(decoded) };

                string pdfFileNameDest = Path.Combine(_outFolder, inputFileInfo.Name);

                using (iText.Kernel.Pdf.PdfReader pdfReader = new iText.Kernel.Pdf.PdfReader(inputFileInfo.FullName))
                {
                    pdfReader.SetUnethicalReading(true);
                    Console.WriteLine($"Processing file {inputFileInfo.Name}");
                    StampingProperties stampingProperties = new StampingProperties();
                    stampingProperties.UseAppendMode();

                    iText.Signatures.PdfSigner pdfSigner = new iText.Signatures.PdfSigner(pdfReader,
                        new FileStream(pdfFileNameDest, FileMode.OpenOrCreate), stampingProperties);

                    pdfSigner.GetSignatureAppearance().SetPageNumber(1).SetReason("Reason").SetLocation("Location");
                    pdfSigner.SetFieldName("SignatureFieldName");

                    iText.Signatures.IExternalSignature pks = new ServerSignature(inputFileInfo.Name, authenticationToken, inputCertificate.certAlias, _signatureSeed, _signatureSecret, _signatureHashAlgorithm);

                    try
                    {
                        pdfSigner.SignDetached(pks, certChain, null, null, null, 0, PdfSigner.CryptoStandard.CMS);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        throw e;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw e;
            }
        }

        private async Task CallUsp_SignSET(string pdfName, string product, string field1)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand("[dbo].[usp_SignSET]", connection))
                {
                    command.CommandType = System.Data.CommandType.StoredProcedure;

                    // Add the parameters to the stored procedure
                    command.Parameters.Add("@pdfName", System.Data.SqlDbType.NVarChar, 255).Value = pdfName;
                    command.Parameters.Add("@Product", System.Data.SqlDbType.NVarChar, 255).Value = product;
                    command.Parameters.Add("@Field1", System.Data.SqlDbType.NVarChar, 255).Value = field1;
                    command.Parameters.Add("@HaveError", System.Data.SqlDbType.Bit).Direction = System.Data.ParameterDirection.Output;
                    command.Parameters.Add("@ErrorDesc", System.Data.SqlDbType.VarChar, 2000).Direction = System.Data.ParameterDirection.Output;

                    // Execute the stored procedure
                    connection.Open();
                    await command.ExecuteNonQueryAsync();

                    // Get the output parameter values
                    bool haveError = (bool)command.Parameters["@HaveError"].Value;
                    string errorDesc = command.Parameters["@ErrorDesc"].Value.ToString();
                }
            }
        }
    }
}
