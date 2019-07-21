using EmailRelay.Logic;
using EmailRelay.Logic.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SendGrid;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmailRelay
{
    public static class Functions
    {
        [FunctionName("receive")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            Microsoft.Azure.WebJobs.ExecutionContext context,
            ILogger log,
            CancellationToken cancellationToken)
        {
            try
            {
                var config = LoadConfig(context.FunctionAppDirectory, log);

                var container = config["ArchiveContainerName"];
                var target = config["RelayTargetEmail"];
                if (string.IsNullOrEmpty(container) && string.IsNullOrEmpty(target))
                    throw new NotSupportedException("Neither email target nor container name where set. Please set either ArchiveContainerName or RelayTargetEmail");

                Email email;
                using (var stream = new MemoryStream())
                {
                    // body can only be read once
                    req.Body.CopyTo(stream);
                    stream.Position = 0;
                    var parser = new SendgridEmailParser();
                    email = parser.Parse(stream);
                }
                if (!string.IsNullOrEmpty(container))
                {
                    IPersister auditLogger = new BlobStoragePersister(config["AzureWebJobsStorage"], container);

                    var d = DateTimeOffset.UtcNow;
                    // one folder per day is fine for now 
                    var id = $"{d.ToString("yyyy-MM")}/{d.ToString("dd")}/{d.ToString("HH-mm-ss")}_{email.From.Email} - {email.Subject}";
                    await auditLogger.PersistAsync($"{id}.json", JsonConvert.SerializeObject(email));
                    // save all attachments in subfolder
                    await Task.WhenAll(email.Attachments.Select(a => auditLogger.PersistAsync($"{id} (Attachments)/{a.FileName}", Convert.FromBase64String(a.Base64Data))));
                }
                if (!string.IsNullOrEmpty(target))
                {
                    var domain = config["Domain"];
                    var key = config["SendgridApiKey"];
                    if (!string.IsNullOrEmpty(target) &&
                        string.IsNullOrEmpty(domain))
                        throw new NotSupportedException("Domain must be set as well when relay is used.");
                    if (!string.IsNullOrEmpty(target) &&
                        string.IsNullOrEmpty(key))
                        throw new NotSupportedException("SendgridApiKey must be set as well when relay is used.");

                    var client = new SendGridClient(key);
                    var subjectParser = new SubjectParser(config["Prefix"]);
                    var relay = new RelayLogic(client, subjectParser, log);
                    var sendAsDomain = "true".Equals(config["SendAsDomain"], StringComparison.OrdinalIgnoreCase);
                    await relay.RelayAsync(email, target, domain, sendAsDomain, cancellationToken);
                }

                return new OkResult();
            }
            catch (Exception e)
            {
                log.LogCritical(e, "Failed to process request!");
                return new BadRequestResult();
            }
        }

        /// <summary>
        /// Helper that loads the config values from file, environment variables and keyvault.
        /// </summary>
        private static IConfiguration LoadConfig(string workingDirectory, ILogger log)
        {
            var builder = new ConfigurationBuilder()
            .SetBasePath(workingDirectory)
            .AddJsonFile("local.settings.json", optional: true)
            .AddEnvironmentVariables();
            return builder.Build();
        }
    }
}
