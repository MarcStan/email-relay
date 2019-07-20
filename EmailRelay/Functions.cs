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
                    var auditLogger = new BlobStoragePersister(config["AzureWebJobsStorage"], container);

                    var d = DateTimeOffset.UtcNow;
                    // one folder per day is fine for now 
                    var id = $"{d.ToString("yyyy-MM")}/{d.ToString("dd")}/{d.ToString("HH-mm-ss")}_{email.From.Email} - {email.Subject}.json";
                    await auditLogger.PersistAsync(id, dict =>
                    {
                        dict["from"] = email.From.Email;
                        dict["to"] = string.Join(";", email.To.Select(_ => _.Email));
                        dict["cc"] = string.Join(";", email.Cc.Select(_ => _.Email));
                        dict["subject"] = email.Subject;
                        dict["content"] = email.Html ?? email.Text;
                        dict["email"] = JsonConvert.SerializeObject(email);
                    });
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
                    var relay = new RelayLogic(client, log);
                    await relay.RelayAsync(email, target, domain, cancellationToken);
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
