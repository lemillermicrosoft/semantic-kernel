

using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Planning;

public interface IKernelExtensions
{
    // TODO - is the plan the actual Object or is this of type ISKFunction[]?
    Task<SKContext> RunAsync(IPlan plan);

    Task<SKContext> RunAsync(
        IPlan plan,
        CancellationToken cancellationToken);

    Task<SKContext> RunAsync(
        string input,
        IPlan plan);

    Task<SKContext> RunAsync(
        string input,
        IPlan plan,
        CancellationToken cancellationToken);

    Task<SKContext> RunAsync(
        ContextVariables variables,
        IPlan plan);

    Task<SKContext> RunAsync(
        ContextVariables variables,
        IPlan plan,
        CancellationToken cancellationToken);
}
