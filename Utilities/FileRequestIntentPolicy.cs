using Azure.Core;
using Azure.Core.Pipeline;
using System;
using System.Threading.Tasks;

namespace AzureStorageManager.Utilities
{
    // Custom policy to add x-ms-file-request-intent header to file share requests
    internal class FileRequestIntentPolicy : HttpPipelinePolicy
    {
        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            message.Request.Headers.Add("x-ms-file-request-intent", "backup");
            ProcessNext(message, pipeline);
        }

        public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            message.Request.Headers.Add("x-ms-file-request-intent", "backup");
            return ProcessNextAsync(message, pipeline);
        }
    }
}
