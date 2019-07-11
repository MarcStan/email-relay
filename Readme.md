# Sendgrid based EmailRelay

Uses Azure functions to process the webhook from Sendgrid Inbound Parse and to relay email.

[Go to release](https://dev.azure.com/marcstanlive/Opensource/_build/definition?definitionId=33) 

![HostMerger](https://dev.azure.com/marcstanlive/Opensource/_apis/build/status/33)

Uses Sendgrid [Inbound Parse Webhook](https://sendgrid.com/docs/for-developers/parsing-email/inbound-email/) to capture all incoming emails sent to a specific domain and then:

* forwards them to another email and/or
* stores them in a storage account

Also allows you to respond to said emails under the email of the domain.

# Motivation

I recently moved from a regular app service to [storage account based hosting](). In the process I broke my MX records and couldn't figure out quickly how to fix them. Since I [was aware of Inbound Parse]() I decided to throw together a small PoC, whether a "free" (as in freeloader) mail system via sendgrid is possible. After 1hour the answer was: yes and this is the (somewhat) polished result.

# Features

* receive emails from a domain that doesn't have a mail package (using Sendgrid Inbound Parse)
* send emails in the name of the domain by replying back to the domain

:warning: When setting mx record of your root domain to sendgrid **all** emails will be relayed through sendgrid. If you have a regular mail server, **it will no longer receive emails!**

Additionally sendgrid free tier is limited to 100 mails per day (25000 mails per month if you signup via Azure).

You can also chose to only enable this system on a subdomain (e.g. foo.example.com) but then only emails of that subdomain will be received (bar@foo.example.com).

# Examples

Setup:

* Your domain is example.com and you expect emails at contact@example.com
* You have setup this function to relay emails to myprivatemail@me.com
* A user user@foobar.com sends an email to contact@example.com with subject `Inquiry`.

Outcome:

You will receive an email at myprivateemail@me.com with subject: `Relay for user@foobar.com: Inquiry` and a sender of contact@example.com.

If you reply to the email the reply will be sent to your own domain, the email is parsed again and based on the `Relay for user@foobar.com ` subject prefix a new email is sent to `user@foobar.com` with subject `Inquiry` from the origin contact@example.com.

**Important:** The subject prefix "Relay for: someemail@example.com" is used to determine whether the email needs to be forwarded to your relay target or whether it should be sent to an external email.

* The email in the "Relay for: " subject is used as the target email.
* The email of your domain will be the sender

The prefix is parsed lax (e.g. the Re: Re: Re: spam is recognized) and kept.

A subject like "Re: Fwd: Re: Re: Relay for user@foobar.com actual subject" is recognized and will result in a subject "Re: Fwd: Re: Re: actual subject"

# Limitations

Reply all/to multiple doesn't work, as you always have to send by replying to your own domain email.

Sending a new email in the name of the domain is wonky: You have to send an email to the email address you want to send as, and the subject must start with: "Relay for: user@foobar.com " for it to send to user@example.com as the domain email.

# Setup

Deployment via the [azure-pipelines.yml](./azure-pipelines.yml) will automatically create the infrastructure and deploy the app.

Before you deploy it, be sure to ResourceGroupName variables (2x) and to customize the `appSettings`.

* `SendgridApiKey` - key with at least `Mail Send` permissions in your Sendgrid account

One (or both) of these appSettings must also be set:

* `ContainerName` - if set all emails will be stored in said container
* `RelayTargetEmail` - if set all externally incoming emails will be forwarded to this email
