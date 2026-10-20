using Contr.SharedKernel;

namespace Contr.Modules.Images.Domain;

/// <summary>The errors the Images module returns before asking the engine.</summary>
public static class ImagesErrors
{
    public static readonly Error ReferenceRequired =
        new("Images.Reference.Required", "Enter an image to pull, such as nginx:latest.");

    public static Error ReferenceInvalid(string reference)
    {
        return new Error("Images.Reference.Invalid", $"'{reference}' is not a valid image reference.");
    }
}
