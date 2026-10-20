using System.Threading.Tasks;

namespace Contr.UI.Contracts.Services.Containers;

/// <summary>Runs a container from a local image, for surfaces outside the Containers module such as Images.</summary>
public interface IContainerRunner
{
    /// <summary>Shows the Run dialog; a container that starts opens on its page.</summary>
    /// <param name="image">The image to run, such as <c>nginx:1.29</c>; empty leaves the dialog's field blank.</param>
    Task RunAsync(string image);
}
