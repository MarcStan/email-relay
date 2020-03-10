# Sendgrid based EmailRelay

Send & receive emails via an azure function. Processes the Sendgrid Inbound Parse webhook to make it work.

# Motivation

I recently moved from a regular app service to [storage account based static websites](https://marcstan.net/blog/2019/07/12/Static-websites-via-Azure-Storage-and-CDN/). In the process I broke my MX records and couldn't figure out quickly how to fix them. Since I [was aware of Inbound Parse](https://github.com/MarcStan/EmailBugTracker) I decided to throw together a small PoC, whether a "free" (as in freeloader) mail system via sendgrid is possible. After 1 hour the answer was yes; and this is the (somewhat) polished result.

# Features

* works for domains even without a mail package
* receive emails for a domain at your private email address (using Sendgrid Inbound Parse)
* send emails in the name of the domain by replying back to the domain from your private email address

See [Examples](#Examples) for more details.

:warning: When setting mx record of your root domain to sendgrid **all** emails will be relayed through sendgrid. If you have a regular mail client, **it will no longer receive emails!**

Additionally sendgrid free tier is limited to 100 mails per day (25000 mails per month if you signup via Azure) if this is not enough for you, consider a regular email service or the paid sendgrid plans.

You can also chose to only enable this system on a subdomain (e.g. foo.example.com) but then only emails of that subdomain will be received (e.g. bar@foo.example.com).

# Limitations

* CC/BCC are lost for relayed emails (but are visible in the blob storage if enabled)
* inline content is **currently not supported** (all media will be sent as regular attachments)
* Reply all/to multiple doesn't work, as each email goes through your domain, so only one recipient at a time is possible
* you can only use one email account as the sender/recipient for all emails (set via `EmailRelayTarget`)
* you can only use one domain per function (e.g. if you want to receive mails for example.com and mail.example.com you have to setup two functions)

# Known issues

* attachment names with [non ascii characters are wrongly encoded if sent via sendgrid](https://github.com/sendgrid/sendgrid-go/issues/362) (the content is always correctly encoded, though)

# Setup

You must first setup a Sendgrid account and connect your domain (make sure that Sendgrid is able to send emails on behalf of your domain).

You can follow [their documentation](https://sendgrid.com/docs/ui/account-and-settings/how-to-set-up-domain-authentication/) to setup domain authentication.

Deployment is fully automated via Github actions. Just [setup credentials](https://github.com/marketplace/actions/azure-login#configure-azure-credentials), adjust the variables at the start of the yaml file (resourcegroup name) and run the action.

App settings in Gitub action you may wish to adjust:

* `ArchiveContainerName` - if set all incoming emails will be stored in said container (attachments will also be extracted from the email), remove to disable

Additionally you must manually add these configuration values into the keyvault once it exists:

* `SendgridApiKey` - key with at least `Mail Send` permissions in your Sendgrid account
* `RelayTargetEmail` - All emails sent to the domain will be forwarded to this email
* `Domain` - (formats example.com and `@example.com` both work). If you have setup emails for a subdomain, then set the specific subdomain mail.example.com. Only emails matching this domain are processed
* `SendAsDomain` - set to `true` if you want to support sending mail as the domain (see [Sending mail](#Sending%20mail)). Defaults to false, which means only [Receiving mail](#Receiving%20mail) is possible
* `Prefix` - customize the prefix needed to send emails. Defaults to "Relay for" (see also [Sending mail](#Sending%20mail))

Finally connect [sendgrid inbound parse](https://sendgrid.com/docs/for-developers/parsing-email/inbound-email/) by setting your domain mx record to `mx.sendgrid.net` and then providing the Azure function url (with function code) to the Sendgrid Inbound webhook.

The url will be in format **\<myfunction>.azurewebsites.net/api/receive?code=\<code>**

# Testing

Once the azure function is hooked up, all you have to do is send an email to your domain.

The email should then be stored in the storage account or relayed to the target address (depending on your setup) within ~10 seconds.

# Examples

## Receiving mail

Since there is no email inbox at your domain when using sendgrid, all emails will be forwarded to the configured email.

Context:

* Your domain is example.com and you expect an email at contact@example.com (sendgrid will relay all aliases automatically)
* You have setup this function to relay emails to myprivatemail@me.com
* A user user@foobar.com sends an email to contact@example.com with subject `Inquiry` and arbitrary content and attachments.

Outcome:

You will receive an email at myprivateemail@me.com with subject: `Relay for user@foobar.com: Inquiry` and a sender of contact@example.com.

Based on the subject you will know who actually sent the email.

If you reply to the email (the reply will be sent to your own domain), the email is parsed again and based on the `Relay for user@foobar.com:` subject prefix a new email is sent to `user@foobar.com` with subject `Inquiry` from the origin contact@example.com.

**Important:** The subject prefix "Relay for: someemail@example.com" is used to determine whether the email needs to be forwarded to your relay target or whether it should be sent to an external email.

* The email in the "Relay for \<email>: " subject is used as the actual recipient
* The email of your domain will be used as the sender (your private email won't be visible anywhere)

The prefix is parsed lax (e.g. the Re: Re: Re: spam is recognized) and kept.

=> A subject like "Re: Fwd: Re: Re: Relay for user@foobar.com actual subject" is recognized and will result in a subject "Re: Fwd: Re: Re: actual subject" when sending the email to user@foobar.com.

**Note:** Only the email enteres as `RelayTargetEmail` will be able to send emails in the name of the domain. If another user sends an email with such a subject, the attempt is logged in application insights and the `RelayTargetEmail` will receive a copy of the email with a warning.

## Sending mail

To send emails in the name of the domain you must send a specially crafted email to the domain.

**Note:** appSetting `SendAsDomain` must also be set to true for this feature to work.

Context:

* Your domain is example.com and you want to send an email as contact@example.com
* You have setup this function to relay emails to myprivatemail@me.com
* `SendAsDomain` is set to true and `Prefix` is not set to a custom value (defaults to "Relay for")
* The intended recipient is user@foobar.com and the subject should be `Inquiry`

The subject pattern is:

> [RE: Fwd:...]%Prefix% %desiredRecipient%: %actualSubject%

The "Re: Fwd:" spam is automatically detected and forwarded to recipients as well.

From myprivateemail@me.com send an email to contact@example.com with subject `Relay for user@foobar.com: Inquiry` and any content/attachments.

Outcome:

user@foobar.com will receive an email `Inquiry` from email contact@example.com with whatever content/attachments you sent to the domain.

Note that this works because myprivateemail@me.com is configured as the owner and is the only one allowed to send emails in the name of the domain.

If you (or someone else) attempts to send an email in the above format to the domain (from another address) a warning will be sent to the registered owner (myprivateemail@me.com) and **no** email is sent to the requested recipient.

## Responding to emails

Similar to Sending mail.

Context:

* Your domain is example.com and someone sent an email to contact@example.com with subject `Inquiry`
* You have setup this function to relay emails to myprivatemail@me.com and received said email with subject `Relay for user@foobar.com: Inquiry`
* `SendAsDomain` must be set to true to allow responding in the name of the domain

You simply respond to the email (response will be sent to contact@example.com).

Note that some email programs will change the subject to `Re: Relay for user@foobar.com: Inquiry` (or any other known response prefix); this will be parsed correctly regardless.

Outcome:

Because you are sending as the configured owner (`RelayTargetEmail`), `SendAsDomain` is true and the subject is in the correct format, an email is sent from contact@example.com to user@foobar.com with either subject `Re: Inquiry` or `Inquiry` (depending on whether your email program added the `Re:` or not).

If the user respons to the email again, the `Re:` will be split yet again and you will receive a response email from contact@example.com with subject `Re: Relay for user@foobar.com: Inquiry`.
