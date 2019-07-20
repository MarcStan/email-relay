using Newtonsoft.Json;

namespace EmailRelay.Logic.Models
{
    public class EmailAttachment
    {
        public string Id { get; set; }

        public string FileName { get; set; }

        public string Base64Data { get; set; }

        [JsonProperty("content-id")]
        public string ContentId { get; set; }

        [JsonProperty("type")]
        public string ContentType { get; set; }
    }
}
