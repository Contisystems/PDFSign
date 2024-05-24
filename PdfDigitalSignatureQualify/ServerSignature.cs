using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using iText.Signatures;
using iText.Kernel.Exceptions;
using System.IO;
using PdfDigitalSignatureQualify.Helpers;
using OtpNet;
using System.Configuration;
using System.Net;

namespace PdfDigitalSignatureQualify
{
    class ServerSignature : iText.Signatures.IExternalSignature
    {
        #region Member Variables
        private string _sourcePlainPdf;
        private string _authenticationToken;
        private string _certificateAlias;
        private string _seed;
        private string _secret;
        private string _hashAlgorithmOID;
        private readonly string _endpoint2;
        private readonly string _endpoint3;
        private static HttpClient _httpClient = HelperMethods.CreateHttpClient();
        #endregion // Member Variables

        #region Ctors
        public ServerSignature(string sourcePlainPdf, string authenticationToken,
            string certificateAlias, string seed, string secret, string hashAlgorithmOID)
        {
            _sourcePlainPdf = sourcePlainPdf;
            _authenticationToken = authenticationToken;
            _certificateAlias = certificateAlias;
            _seed = seed;
            _secret = secret;
            _hashAlgorithmOID = hashAlgorithmOID;
            _endpoint2 = ConfigurationManager.AppSettings["Endpoint2"];
            _endpoint3 = ConfigurationManager.AppSettings["Endpoint3"];
        }
        #endregion // Ctors

        #region Overridables
        public String GetHashAlgorithm()
        {
            return DigestAlgorithms.SHA256;
        }

        public String GetEncryptionAlgorithm()
        {
            return "RSA";
        }

        public byte[] Sign(byte[] message)
        {
            try
            {
                string digest;
                using (SHA256Managed sha = new SHA256Managed())
                {
                    byte[] hash = sha.ComputeHash(message);

                    digest = Convert.ToBase64String(hash);
                }

                List<JObject> docsToSign = new List<JObject>();

                dynamic docToSignInfo = new JObject();
                docToSignInfo.docAlias = _sourcePlainPdf;
                docToSignInfo.hashAlg = _hashAlgorithmOID;
                docToSignInfo.sigType = 1;
                docToSignInfo.hashToSign_64 = digest;
                docsToSign.Add(docToSignInfo);
                dynamic jsonSigCompleteTOTPRequest = new JObject();
                jsonSigCompleteTOTPRequest.certAlias = _certificateAlias;
                jsonSigCompleteTOTPRequest["docsToSign"] = JToken.FromObject(docsToSign);
                jsonSigCompleteTOTPRequest.sigReqDescr = Guid.NewGuid().ToString();
                jsonSigCompleteTOTPRequest.totpID = _seed;

                var totp = new Totp(Base32Encoding.ToBytes(_secret));
                jsonSigCompleteTOTPRequest.totpValue = totp.ComputeTotp(DateTime.UtcNow);

                byte[] signedHash = null;
                    //create an HttpClient and pass a handler to it
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
                    //httpClient.DefaultRequestHeaders.Add("Keep-Alive", "timeout=6000");
                    //httpClient.Timeout = TimeSpan.FromSeconds(6000);


                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authenticationToken);

                        Console.WriteLine("A");
                        // ******WS - Polling 
                        HttpResponseMessage responseMessage = _httpClient.PostAsync(_endpoint2,
                            new StringContent(jsonSigCompleteTOTPRequest.ToString(), UnicodeEncoding.UTF8, "application/json")).Result;

                        Console.WriteLine("B");


                        if (responseMessage.IsSuccessStatusCode)
                        {
                            HttpContent responseContent = responseMessage.Content;
                            JObject responsePollingQSCDService = JObject.Parse(responseContent.ReadAsStringAsync().Result);
                            Console.Write("ID SigFinalize ..." + responsePollingQSCDService.GetValue("sigReqID").ToString() + "\n");
                            int i = 0;
                            do
                            {
                                // ******WS - Finalize 
                                HttpResponseMessage responseFinalize = _httpClient.PostAsync(_endpoint3,
                                new StringContent(responsePollingQSCDService.ToString(), UnicodeEncoding.UTF8, "application/json")).Result;

                                if (responseFinalize.IsSuccessStatusCode)
                                {
                                    HttpContent responseContentFinalize = responseFinalize.Content;
                                    JObject responseFinalizeQSCDService = JObject.Parse(responseContentFinalize.ReadAsStringAsync().Result);

                                    dynamic responseFinalizeHashSign = JArray.Parse(responseFinalizeQSCDService.GetValue("signedDocsInfo").ToString());
                                    foreach (JObject item in responseFinalizeHashSign)
                                    {
                                        signedHash = Convert.FromBase64String(item.GetValue("hashSig").ToString());
                                        break;
                                    }
                                    i = 5;
                                }
                                else
                                {
                                    HttpContent responseContentFinalize = responseFinalize.Content;
                                    JObject responseFinalizeQSCDService = JObject.Parse(responseContentFinalize.ReadAsStringAsync().Result);

                                    if (responseFinalizeQSCDService.GetValue("errorCode").ToString() == "028001") // Not Ready 
                                    {
                                        int secPause = Int32.Parse(responseFinalizeQSCDService.GetValue("retryAfter").ToString());

                                        Console.Write("In progress wait ..." + responseFinalizeQSCDService.GetValue("retryAfter").ToString() + " secounds");
                                        System.Threading.Thread.Sleep(secPause * 1000);
                                    }
                                    else
                                    {
                                        Console.WriteLine("HERE");
                                        Console.WriteLine(responseFinalize.Content.ReadAsStringAsync().Result);
                                        throw new PdfException(responseFinalize.Content.ReadAsStringAsync().Result);
                                    }
                                }
                                i++;
                                if (i > 5)
                                {
                                    Console.WriteLine("BREAAAAAAAAAAAAAAAK");
                                    break;
                                }
                            } while (i < 5);
                        }
                        else
                        {
                            Console.Write("********** ERROR ********** => ");
                            Console.Write(responseMessage.Content.ReadAsStringAsync().Result);
                            throw new PdfException(responseMessage.Content.ReadAsStringAsync().Result);
                        }
                return signedHash;
            }
            catch (IOException e)
            {
                Console.WriteLine("AQUI");
                Console.WriteLine(e.Message);
                return null;
                //throw e;
                throw new PdfException(e.Message);
            }
        }

        public string GetDigestAlgorithmName()
        {
            throw new NotImplementedException();
        }

        public string GetSignatureAlgorithmName()
        {
            throw new NotImplementedException();
        }
        #endregion // Overridables
    }
}
