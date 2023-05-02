# My Tutor Console

Experiment for creating a tutor assistant.

Most of the examples will require secrets and credentials, to access OpenAI, Azure OpenAI,
Bing and other resources. We suggest using .NET
[Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
to avoid the risk of leaking secrets into the repository, branches and pull requests.
You can also use environment variables if you prefer.

To set your secrets with Secret Manager:

```
dotnet user-secrets set "AZURE_OPENAI_SERVICE_ID" "..."
dotnet user-secrets set "AZURE_OPENAI_DEPLOYMENT_NAME" "..."
dotnet user-secrets set "AZURE_OPENAI_ENDPOINT" "https://... .openai.azure.com/"
dotnet user-secrets set "AZURE_OPENAI_KEY" "..."
```

To set your secrets with environment variables, use these names:

* AZURE_OPENAI_SERVICE_ID
* AZURE_OPENAI_DEPLOYMENT_NAME
* AZURE_OPENAI_ENDPOINT
* AZURE_OPENAI_KEY
