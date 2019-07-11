using EmailRelay.Logic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
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
            var parser = new HttpFormDataParser();
            var result = parser.Deserialize(req.Form);
            try
            {
                var config = LoadConfig(context.FunctionAppDirectory, log);

                var container = config["ContainerName"];
                var target = config["RelayTargetEmail"];
                if (string.IsNullOrEmpty(container) && string.IsNullOrEmpty(target))
                    throw new NotSupportedException("Neither email target nor container name where set. Please set either ContainerName or RelayTargetEmail");

                if (!string.IsNullOrEmpty(container))
                {
                    var auditLogger = new BlobStoragePersister(config["AzureWebJobsStorage"], container);

                    var d = DateTimeOffset.UtcNow;
                    // one folder per day is fine for now
                    var id = $"{d.ToString("yyyy-MM")}/{d.ToString("dd")}/{d.ToString("HH-mm-ss")}_{Uri.EscapeDataString($"{result.From} - {result.Subject}")}";
                    await auditLogger.PersistAsync(id, dict =>
                    {
                        dict["from"] = result.From;
                        dict["to"] = result.To;
                        dict["subject"] = result.Subject;
                        dict["content"] = result.Content;
                    });
                }
                if (!string.IsNullOrEmpty(target))
                {
                    var client = new SendGridClient(config["SendgridApiKey"]);
                    var mail = MailHelper.CreateSingleEmail(new EmailAddress(result.From), new EmailAddress(target, $"via {result.To}"), result.Subject, null, result.Content);
                    await client.SendEmailAsync(mail, cancellationToken);
                }
            }
            catch (Exception e)
            {
                log.LogCritical(e, "Failed to process request!");
                return new BadRequestResult();
            }
            return new OkResult();
        }

        /// <summary>
        /// Helper that loads the config values from file, environment variables and keyvault.
        /// </summary>
        private static IConfiguration LoadConfig(string workingDirectory, ILogger log)
        {
            try
            {
                var builder = new ConfigurationBuilder()
                .SetBasePath(workingDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
                return builder.Build();
            }
            catch (Exception e)
            {
                log.LogCritical(e, $"Failed accessing the keyvault: '{e.Message}'. Possible reason: You are debugging locally (in which case you must add your user account to the keyvault access policies manually). Note that the infrastructure deployment will reset the keyvault policies to only allow the azure function MSI! More details on local fallback here: https://docs.microsoft.com/en-us/azure/key-vault/service-to-service-authentication#local-development-authentication");
                throw;
            }
        }
    }
}
