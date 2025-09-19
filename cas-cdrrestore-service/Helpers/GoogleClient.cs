using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;

namespace cas_cdrrestore_service.Helpers
{
    public class GoogleClient
    {
        private readonly UrlSigner _signer;
        public GoogleClient(IConfiguration config)
        {
            var googleAuthFilePath = config.GetValue<string>("GoogleAuthFilePath")
                                     ?? throw new ArgumentNullException("GoogleAuthFilePath not configured");
            
            _signer = UrlSigner.FromCredentialFile(googleAuthFilePath);
        }
        public string SignUrl(string bucket, string objectName, TimeSpan validity)
        {
            return _signer.Sign(bucket, objectName, validity, HttpMethod.Get);
        }
    }
}
